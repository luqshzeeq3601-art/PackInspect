using System.Net.Sockets;
using System.Text;

namespace PackInspect.Devices.Net;

/// <summary>
/// A TCP connection to a device driven by lines of ASCII text. It connects on
/// first use, applies one timeout to each whole exchange, and drops the
/// connection after any failure so the next exchange starts clean.
/// </summary>
internal sealed class TcpTextClient(string host, int port, TimeSpan timeout) : IDisposable
{
    private readonly SemaphoreSlim _exchangeLock = new(1, 1);
    private TcpClient? _client;

    public async Task<T> ExchangeAsync<T>(Func<NetworkStream, CancellationToken, Task<T>> exchange, CancellationToken cancellationToken)
    {
        await _exchangeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            try
            {
                if (_client is null)
                {
                    var client = new TcpClient { NoDelay = true };
                    try
                    {
                        await client.ConnectAsync(host, port, deadline.Token).ConfigureAwait(false);
                    }
                    catch
                    {
                        client.Dispose();
                        throw;
                    }

                    _client = client;
                }

                return await exchange(_client.GetStream(), deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Our own deadline fired, not the caller's token: report it as a timeout.
                Close();
                throw new TimeoutException($"No reply from {host}:{port} within {timeout.TotalSeconds:0.#} s.");
            }
            catch
            {
                Close();
                throw;
            }
        }
        finally
        {
            _exchangeLock.Release();
        }
    }

    public static Task WriteLineAsync(NetworkStream stream, string line, CancellationToken cancellationToken) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(line + "\n"), cancellationToken).AsTask();

    /// <summary>
    /// Reads up to the next line feed, one byte at a time so nothing after the
    /// line is consumed. A line longer than <paramref name="maxLength"/> is
    /// rejected rather than buffered without limit.
    /// </summary>
    public static async Task<string> ReadLineAsync(NetworkStream stream, int maxLength, CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        byte[] one = new byte[1];
        while (true)
        {
            await stream.ReadExactlyAsync(one, cancellationToken).ConfigureAwait(false);
            char c = (char)one[0];
            if (c == '\n')
            {
                return line.ToString().TrimEnd('\r');
            }

            if (line.Length >= maxLength)
            {
                throw new IOException($"The device sent a line longer than {maxLength} characters.");
            }

            line.Append(c);
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await _exchangeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Close();
        }
        finally
        {
            _exchangeLock.Release();
        }
    }

    public void Dispose()
    {
        Close();
        _exchangeLock.Dispose();
    }

    private void Close()
    {
        _client?.Dispose();
        _client = null;
    }
}
