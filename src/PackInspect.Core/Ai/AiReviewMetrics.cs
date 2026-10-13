namespace PackInspect.Core.Ai;

/// <summary>
/// Operational AI and vision quality metrics, as specified in docs/PRD.md section 7.
/// </summary>
public sealed record AiReviewMetrics(
    int TotalReviewed,
    int ConfirmedCount,
    int OverriddenCount,
    int FalseCallCount,
    int EscapeCount,
    double OverrideRate,
    double FalseCallRate,
    double EscapeRate)
{
    public static AiReviewMetrics Empty => new(
        TotalReviewed: 0,
        ConfirmedCount: 0,
        OverriddenCount: 0,
        FalseCallCount: 0,
        EscapeCount: 0,
        OverrideRate: 0.0,
        FalseCallRate: 0.0,
        EscapeRate: 0.0);

    public static AiReviewMetrics Calculate(IEnumerable<AiReviewRecord> reviews, int totalInspectedUnits)
    {
        var reviewedList = reviews.Where(r => r.OperatorVerdict is not null).ToList();
        if (reviewedList.Count == 0)
        {
            return Empty;
        }

        int totalReviewed = reviewedList.Count;
        int confirmed = reviewedList.Count(r => string.Equals(r.OperatorVerdict, "Confirmed", StringComparison.OrdinalIgnoreCase));
        int overridden = reviewedList.Count(r => string.Equals(r.OperatorVerdict, "Overridden", StringComparison.OrdinalIgnoreCase));

        // Overkill / False call: Unit was flagged defective by rule/AI, but operator overrode it as good (None)
        int falseCalls = reviewedList.Count(r =>
            (r.AiLabel != Domain.DefectType.None || r.RuleDefectType != Domain.DefectType.None) &&
            ((string.Equals(r.OperatorVerdict, "Overridden", StringComparison.OrdinalIgnoreCase) && r.OperatorLabel == Domain.DefectType.None) ||
             (string.Equals(r.OperatorVerdict, "Confirmed", StringComparison.OrdinalIgnoreCase) && r.AiLabel == Domain.DefectType.None && r.RuleDefectType != Domain.DefectType.None)));

        // Escape / Underkill: AI classified unit as Good (None), but operator corrected it to a defect
        int escapes = reviewedList.Count(r =>
            r.AiLabel == Domain.DefectType.None &&
            string.Equals(r.OperatorVerdict, "Overridden", StringComparison.OrdinalIgnoreCase) &&
            r.OperatorLabel.HasValue && r.OperatorLabel.Value != Domain.DefectType.None);

        double overrideRate = totalReviewed > 0 ? (double)overridden / totalReviewed : 0.0;
        int inspectionBase = Math.Max(totalInspectedUnits, totalReviewed);
        double falseCallRate = inspectionBase > 0 ? (double)falseCalls / inspectionBase : 0.0;
        double escapeRate = inspectionBase > 0 ? (double)escapes / inspectionBase : 0.0;

        return new AiReviewMetrics(
            TotalReviewed: totalReviewed,
            ConfirmedCount: confirmed,
            OverriddenCount: overridden,
            FalseCallCount: falseCalls,
            EscapeCount: escapes,
            OverrideRate: Math.Round(overrideRate, 4),
            FalseCallRate: Math.Round(falseCallRate, 4),
            EscapeRate: Math.Round(escapeRate, 4));
    }
}
