namespace PackInspect.Core.Domain;

public sealed record LotSummary(
    string LotNumber,
    string RecipeName,
    int RecipeVersion,
    int Quantity,
    int Total,
    int Passed)
{
    public int Failed => Total - Passed;

    /// <summary>Passed units as a percentage of tested units; 0 before the first unit.</summary>
    public double YieldPercent => Total == 0 ? 0 : 100.0 * Passed / Total;
}
