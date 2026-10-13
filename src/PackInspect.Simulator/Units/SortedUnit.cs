namespace PackInspect.Simulator.Units;

/// <summary>A unit the app has unloaded, with the bin it chose.</summary>
public sealed record SortedUnit(SimulatedUnit Unit, bool SortedAsPass)
{
    /// <summary>True when the app's decision matches the truth about the unit.</summary>
    public bool SortedCorrectly => SortedAsPass == Unit.IsGood;
}
