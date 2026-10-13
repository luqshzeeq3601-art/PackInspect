namespace PackInspect.Simulator;

/// <summary>
/// Fault injection switches. Set from the console or from a test while the
/// simulator runs; the simulated devices read them as they work.
/// </summary>
public sealed class SimulatorFaults
{
    private volatile bool _doorOpen;
    private volatile bool _eStopPressed;
    private volatile bool _airPressureLow;
    private volatile bool _axisTimeout;
    private volatile bool _instrumentNotResponding;

    public bool DoorOpen
    {
        get => _doorOpen;
        set => _doorOpen = value;
    }

    public bool EStopPressed
    {
        get => _eStopPressed;
        set => _eStopPressed = value;
    }

    public bool AirPressureLow
    {
        get => _airPressureLow;
        set => _airPressureLow = value;
    }

    /// <summary>While set, a homing or positioning move starts but never completes.</summary>
    public bool AxisTimeout
    {
        get => _axisTimeout;
        set => _axisTimeout = value;
    }

    /// <summary>While set, the multimeter accepts commands but never replies.</summary>
    public bool InstrumentNotResponding
    {
        get => _instrumentNotResponding;
        set => _instrumentNotResponding = value;
    }
}
