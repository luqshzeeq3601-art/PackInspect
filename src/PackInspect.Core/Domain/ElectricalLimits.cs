namespace PackInspect.Core.Domain;

/// <summary>The pass window for the electrical test, in the unit of the measurement (ohms or volts).</summary>
public sealed record ElectricalLimits(double Low, double High)
{
    /// <summary>
    /// Pass when Low &lt;= value &lt;= High. Both limits are inclusive, and a reading
    /// that is not a real number (NaN or infinity) always fails.
    /// </summary>
    public Verdict Evaluate(double value) =>
        double.IsFinite(value) && value >= Low && value <= High ? Verdict.Pass : Verdict.Fail;
}
