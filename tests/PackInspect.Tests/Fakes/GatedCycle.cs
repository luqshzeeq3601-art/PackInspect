using PackInspect.Core.Machine;

namespace PackInspect.Tests.Fakes;

/// <summary>A cycle that only finishes when the test releases it, so tests control the pace.</summary>
internal sealed class GatedCycle : IMachineCycle
{
    private readonly SemaphoreSlim _gate = new(0);
    private int _started;
    private int _remaining = int.MaxValue;
    private readonly TaskCompletionSource _firstStart = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Started => Volatile.Read(ref _started);

    /// <summary>
    /// Completes when the first cycle has begun. It stays completed, so a test
    /// that asks after the cycle already started does not wait for a second one.
    /// </summary>
    public Task WaitForFirstStartAsync() => _firstStart.Task;

    /// <summary>How many more cycles there are to run; unlimited by default.</summary>
    public int Remaining
    {
        get => Volatile.Read(ref _remaining);
        set => Volatile.Write(ref _remaining, value);
    }

    /// <summary>When set, the next cycle throws this instead of waiting.</summary>
    public Exception? Failure { get; set; }

    public bool HasMoreWork => Remaining > 0;

    /// <summary>Lets one cycle run to completion.</summary>
    public void ReleaseOne() => _gate.Release();

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _started);
        _firstStart.TrySetResult();
        if (Failure is { } failure)
        {
            throw failure;
        }

        await _gate.WaitAsync(cancellationToken);
        Interlocked.Decrement(ref _remaining);
    }
}
