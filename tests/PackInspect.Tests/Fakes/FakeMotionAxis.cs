using System.Globalization;
using PackInspect.Core.Devices;

namespace PackInspect.Tests.Fakes;

/// <summary>An axis that homes and moves instantly, or never, as the test chooses.</summary>
internal sealed class FakeMotionAxis(StepLog? log = null) : IMotionAxis
{
    private volatile bool _homed;
    private int _stopCount;

    /// <summary>While set, homing waits until it is cancelled.</summary>
    public volatile bool NeverFinishHoming;

    /// <summary>While set, a move waits until it is cancelled.</summary>
    public volatile bool NeverFinishMoves;

    public string Name => "X";

    public int StopCount => Volatile.Read(ref _stopCount);

    /// <summary>Simulates a drive that lost its position reference, for example after a power cycle.</summary>
    public void LoseReference() => _homed = false;

    public Task<bool> IsHomedAsync(CancellationToken cancellationToken) => Task.FromResult(_homed);

    public async Task HomeAsync(CancellationToken cancellationToken)
    {
        _homed = false;
        if (NeverFinishHoming)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        _homed = true;
    }

    public async Task MoveAbsoluteAsync(double positionMm, double speedMmPerSec, CancellationToken cancellationToken)
    {
        log?.Add(string.Create(CultureInfo.InvariantCulture, $"move {positionMm:0.#}"));
        if (NeverFinishMoves)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _stopCount);
        return Task.CompletedTask;
    }
}
