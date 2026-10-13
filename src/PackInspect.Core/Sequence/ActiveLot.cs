using PackInspect.Core.Domain;

namespace PackInspect.Core.Sequence;

/// <summary>The lot on the machine and its running counts.</summary>
public sealed record ActiveLot(int Id, string LotNumber, Recipe Recipe, int Quantity, int Total, int Passed, bool IsOpen)
{
    public int Failed => Total - Passed;

    public double YieldPercent => Total == 0 ? 0 : 100.0 * Passed / Total;

    public bool HasUnitsRemaining => IsOpen && Total < Quantity;
}
