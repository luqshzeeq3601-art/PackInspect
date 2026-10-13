namespace PackInspect.Simulator.Units;

/// <summary>One simulated package unit and the truth about it.</summary>
/// <param name="Number">Running number, starting at 1.</param>
/// <param name="Defect">The visual defect it carries, if any.</param>
/// <param name="ResistanceOhm">What the multimeter reads at the test position.</param>
/// <param name="VoltageV">What the multimeter reads in DC voltage mode.</param>
/// <param name="ElectricallyGood">Whether its electrical values are nominal.</param>
/// <param name="ImageSeed">Seed for its image, so every capture of this unit looks the same.</param>
public sealed record SimulatedUnit(
    int Number,
    UnitDefect Defect,
    double ResistanceOhm,
    double VoltageV,
    bool ElectricallyGood,
    int ImageSeed)
{
    public bool IsGood => Defect == UnitDefect.None && ElectricallyGood;
}
