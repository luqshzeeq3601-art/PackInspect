namespace PackInspect.Simulator;

/// <summary>Settings for one simulator instance. A port of 0 lets the system pick a free one (used by tests).</summary>
public sealed record SimulatorOptions
{
    public int ModbusPort { get; init; } = 5020;

    public int ScpiPort { get; init; } = 5025;

    public int CameraPort { get; init; } = 5030;

    /// <summary>How long a simulated homing move takes.</summary>
    public TimeSpan HomingDuration { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>The PLC scan time.</summary>
    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromMilliseconds(20);

    /// <summary>Seed for the units and their images; the same seed gives the same lot. Null picks a random one.</summary>
    public int? Seed { get; init; }

    /// <summary>Fraction of units that carry a visual defect.</summary>
    public double DefectRate { get; init; } = 0.30;

    /// <summary>Fraction of units whose electrical value is out of limits.</summary>
    public double ElectricalFailRate { get; init; } = 0.08;
}
