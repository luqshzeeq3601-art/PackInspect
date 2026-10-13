namespace PackInspect.Core.Machine;

/// <summary>The top-level states of the station. Transitions are defined in phase 1.</summary>
public enum MachineState
{
    /// <summary>Powered but axis position is not trusted; homing is required.</summary>
    Idle,
    Homing,
    /// <summary>Homed and waiting for Start.</summary>
    Ready,
    Running,
    Paused,
    /// <summary>Stopped on a fault that the operator must clear and reset.</summary>
    Alarm,
    /// <summary>Emergency stop pressed; homing is required again after reset.</summary>
    EStop,
}
