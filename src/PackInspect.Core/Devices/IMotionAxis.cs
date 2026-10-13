namespace PackInspect.Core.Devices;

/// <summary>One servo axis.</summary>
public interface IMotionAxis
{
    string Name { get; }

    Task<bool> IsHomedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs the homing routine and completes when the axis reports homed.
    /// The caller decides how long to wait and cancels the token to give up.
    /// </summary>
    Task HomeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Moves to an absolute position and completes when the axis is in position.
    /// As with homing, the caller applies the timeout by cancelling the token.
    /// </summary>
    Task MoveAbsoluteAsync(double positionMm, double speedMmPerSec, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
