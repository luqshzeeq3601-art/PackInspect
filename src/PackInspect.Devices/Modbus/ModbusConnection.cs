using System.Net.Sockets;
using NModbus;

namespace PackInspect.Devices.Modbus;

/// <summary>
/// The one TCP connection to the PLC, shared by the PLC and axis drivers.
/// Modbus TCP is strictly request then reply, so requests are queued one at a time.
/// </summary>
public sealed class ModbusConnection(ModbusOptions options) : IDisposable
{
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private TcpClient? _client;
    private IModbusMaster? _master;

    public byte UnitId => options.UnitId;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Close();

            var client = new TcpClient { NoDelay = true };
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(options.RequestTimeout);
                await client.ConnectAsync(options.Host, options.Port, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Our own timeout fired, not the caller's token: report it as what it is.
                client.Dispose();
                throw new TimeoutException($"No PLC answered at {options.Host}:{options.Port}.");
            }
            catch
            {
                client.Dispose();
                throw;
            }

            var master = new ModbusFactory().CreateMaster(client);
            var timeoutMs = (int)options.RequestTimeout.TotalMilliseconds;
            master.Transport.ReadTimeout = timeoutMs;
            master.Transport.WriteTimeout = timeoutMs;
            // A failed request is reported at once; the machine decides what to do, not the library.
            master.Transport.Retries = 0;

            _client = client;
            _master = master;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Close();
        }
        finally
        {
            _requestLock.Release();
        }
    }

    /// <summary>
    /// Sends one request and waits for its reply. Cancellation is honoured while
    /// waiting for the connection; a request already on the wire is allowed to
    /// finish (at most <see cref="ModbusOptions.RequestTimeout"/>) so the next
    /// request never reads a stale reply.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<IModbusMaster, Task<T>> request, CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var master = _master ?? throw new IOException("Not connected to the PLC.");
            try
            {
                return await request(master).WaitAsync(options.RequestTimeout, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // After a failed or timed-out request the stream may hold half a
                // reply, so the connection is dropped and re-opened by the caller.
                Close();
                throw;
            }
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public Task ExecuteAsync(Func<IModbusMaster, Task> request, CancellationToken cancellationToken) =>
        ExecuteAsync(
            async master =>
            {
                await request(master).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    /// <summary>
    /// Sets a command bit and waits until the PLC clears it. The cleared bit is
    /// the PLC saying "accepted", so status read afterwards belongs to this
    /// command and not to an earlier one.
    /// </summary>
    public async Task SendCommandAsync(ushort coilAddress, CancellationToken cancellationToken)
    {
        await ExecuteAsync(master => master.WriteSingleCoilAsync(UnitId, coilAddress, true), cancellationToken).ConfigureAwait(false);

        while (true)
        {
            bool[] coil = await ExecuteAsync(master => master.ReadCoilsAsync(UnitId, coilAddress, 1), cancellationToken).ConfigureAwait(false);
            if (coil.Length != 1)
            {
                throw new IOException("The PLC returned an incomplete reply.");
            }

            if (!coil[0])
            {
                return;
            }

            await Task.Delay(options.StatusPollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        Close();
        _requestLock.Dispose();
    }

    private void Close()
    {
        _master?.Dispose();
        _client?.Dispose();
        _master = null;
        _client = null;
    }
}
