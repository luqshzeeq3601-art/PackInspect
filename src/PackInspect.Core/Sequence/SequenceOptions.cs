namespace PackInspect.Core.Sequence;

/// <summary>Machine settings for the cycle that do not depend on the product. Read from appsettings.json.</summary>
public sealed class SequenceOptions
{
    /// <summary>Where a new unit is placed on the carrier. This is also the home position.</summary>
    public double LoadPositionMm { get; set; }

    public double AxisSpeedMmPerSec { get; set; } = 400;

    /// <summary>How long one move may take before it is reported as a fault.</summary>
    public TimeSpan MoveTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
