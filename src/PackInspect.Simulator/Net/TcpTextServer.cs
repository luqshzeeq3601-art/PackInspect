using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PackInspect.Simulator.Net;

/// <summary>
/// A small TCP server for devices that are driven by lines of ASCII text
/// (the multimeter and the camera). Each client is served on its own task.
/// </summary>
internal abstract class TcpTextServer
{
    private const int MaxLineLength = 256;
    private readonly TcpListener _listener;

    protected TcpTextServer(int port)
    {
        // Loopback only: the simulator must never be reachable from the network.
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public int Port { get; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => ServeClientAsync(client, cancellationToken), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        finally
        {
            _listener.Stop();
        }
    }

    /// <summary>Handles one received line. Write the reply, if any, to <paramref name="stream"/>.</summary>
    protected abstract Task HandleLineAsync(string line, NetworkStream stream, CancellationToken cancellationToken);

    protected static Task WriteLineAsync(NetworkStream stream, string line, CancellationToken cancellationToken) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(line + "\n"), cancellationToken).AsTask();

    private async Task ServeClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                {
                    if (line.Length <= MaxLineLength)
                    {
                        await HandleLineAsync(line.Trim(), stream, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
            {
                // The client went away or the simulator is shutting down.
            }
        }
    }
}
