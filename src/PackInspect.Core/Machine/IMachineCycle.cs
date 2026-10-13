namespace PackInspect.Core.Machine;

/// <summary>The work done for one unit. The controller repeats it while Running.</summary>
public interface IMachineCycle
{
    /// <summary>False when there is nothing to run: no lot is open, or the lot is complete.</summary>
    bool HasMoreWork { get; }

    Task RunOnceAsync(CancellationToken cancellationToken);
}
