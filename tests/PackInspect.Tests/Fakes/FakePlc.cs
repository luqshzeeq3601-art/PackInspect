using PackInspect.Core.Devices;

namespace PackInspect.Tests.Fakes;

/// <summary>A PLC whose inputs the test sets directly. Healthy by default.</summary>
internal sealed class FakePlc(StepLog? log = null) : IPlc
{
    private ushort _heartbeat;

    public volatile bool EStopOk = true;
    public volatile bool DoorClosed = true;
    public volatile bool AirPressureOk = true;

    /// <summary>While set, every read throws, as a dropped connection would.</summary>
    public volatile bool FailReads;

    /// <summary>While set, the heartbeat stops counting, as a stalled PLC would.</summary>
    public volatile bool FreezeHeartbeat;

    public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<PlcInputs> ReadInputsAsync(CancellationToken cancellationToken)
    {
        if (FailReads)
        {
            throw new IOException("Simulated PLC read failure.");
        }

        if (!FreezeHeartbeat)
        {
            _heartbeat++;
        }

        return Task.FromResult(new PlcInputs(EStopOk, DoorClosed, AirPressureOk, _heartbeat));
    }

    public Task ResetFaultsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task LoadUnitAsync(CancellationToken cancellationToken)
    {
        log?.Add("load");
        return Task.CompletedTask;
    }

    public Task UnloadUnitAsync(SortBin bin, CancellationToken cancellationToken)
    {
        log?.Add($"unload {bin}");
        return Task.CompletedTask;
    }
}
