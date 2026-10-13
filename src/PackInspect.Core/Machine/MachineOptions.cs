namespace PackInspect.Core.Machine;

/// <summary>Timing settings for <see cref="MachineController"/>, read from appsettings.json.</summary>
public sealed class MachineOptions
{
    /// <summary>How often the PLC interlocks are read.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>How long homing may take before it is reported as a fault.</summary>
    public TimeSpan HomingTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How long the PLC heartbeat may stay unchanged before the PLC is considered stalled.</summary>
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(2);
}
