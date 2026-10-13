using PackInspect.Core.Ai;
using PackInspect.Core.Domain;

namespace PackInspect.Tests.Unit;

public class AiReviewMetricsCalculationTests
{
    [Fact]
    public void Empty_reviews_returns_zero_metrics()
    {
        var metrics = AiReviewMetrics.Calculate([], totalInspectedUnits: 0);

        Assert.Equal(0, metrics.TotalReviewed);
        Assert.Equal(0.0, metrics.FalseCallRate);
        Assert.Equal(0.0, metrics.EscapeRate);
        Assert.Equal(0.0, metrics.OverrideRate);
    }

    [Fact]
    public void Metrics_accurately_computes_false_call_and_escape_and_override_rates()
    {
        var reviews = new List<AiReviewRecord>
        {
            // Unit 1: Rule-based called Scratch, AI called None, operator Confirmed AI (so rule made a false call, overkill)
            new(
                Id: 1, UnitResultId: 101, LotId: 1, LotNumber: "LOT-1", Sequence: 1, Barcode: "U001",
                ImagePath: "img1.png", RuleDefectType: DefectType.Scratch, RuleVisionVerdict: Verdict.Fail,
                AiLabel: DefectType.None, AiConfidence: 0.98, ModelVersion: "v1",
                OperatorVerdict: "Confirmed", OperatorLabel: null, ReviewedByUserId: 1, ReviewedByUserName: "operator",
                ReviewedAtUtc: DateTimeOffset.UtcNow),

            // Unit 2: AI called Scratch, operator Overrode to None (AI false call, overkill)
            new(
                Id: 2, UnitResultId: 102, LotId: 1, LotNumber: "LOT-1", Sequence: 2, Barcode: "U002",
                ImagePath: "img2.png", RuleDefectType: DefectType.Scratch, RuleVisionVerdict: Verdict.Fail,
                AiLabel: DefectType.Scratch, AiConfidence: 0.85, ModelVersion: "v1",
                OperatorVerdict: "Overridden", OperatorLabel: DefectType.None, ReviewedByUserId: 1, ReviewedByUserName: "operator",
                ReviewedAtUtc: DateTimeOffset.UtcNow),

            // Unit 3: AI called None (Good), but operator Overrode to CrackChip (escape / underkill)
            new(
                Id: 3, UnitResultId: 103, LotId: 1, LotNumber: "LOT-1", Sequence: 3, Barcode: "U003",
                ImagePath: "img3.png", RuleDefectType: DefectType.None, RuleVisionVerdict: Verdict.Pass,
                AiLabel: DefectType.None, AiConfidence: 0.95, ModelVersion: "v1",
                OperatorVerdict: "Overridden", OperatorLabel: DefectType.CrackChip, ReviewedByUserId: 1, ReviewedByUserName: "operator",
                ReviewedAtUtc: DateTimeOffset.UtcNow),

            // Unit 4: AI called LeadDefect, operator Confirmed LeadDefect (true positive defect)
            new(
                Id: 4, UnitResultId: 104, LotId: 1, LotNumber: "LOT-1", Sequence: 4, Barcode: "U004",
                ImagePath: "img4.png", RuleDefectType: DefectType.LeadDefect, RuleVisionVerdict: Verdict.Fail,
                AiLabel: DefectType.LeadDefect, AiConfidence: 0.99, ModelVersion: "v1",
                OperatorVerdict: "Confirmed", OperatorLabel: null, ReviewedByUserId: 1, ReviewedByUserName: "operator",
                ReviewedAtUtc: DateTimeOffset.UtcNow),

            // Unit 5: Pending review (should be excluded from rate calculation)
            new(
                Id: 5, UnitResultId: 105, LotId: 1, LotNumber: "LOT-1", Sequence: 5, Barcode: "U005",
                ImagePath: "img5.png", RuleDefectType: DefectType.None, RuleVisionVerdict: Verdict.Pass,
                AiLabel: DefectType.None, AiConfidence: 0.99, ModelVersion: "v1",
                OperatorVerdict: null, OperatorLabel: null, ReviewedByUserId: null, ReviewedByUserName: null,
                ReviewedAtUtc: null),
        };

        // 4 units reviewed out of 10 inspected
        var metrics = AiReviewMetrics.Calculate(reviews, totalInspectedUnits: 10);

        Assert.Equal(4, metrics.TotalReviewed);
        Assert.Equal(2, metrics.ConfirmedCount);
        Assert.Equal(2, metrics.OverriddenCount);
        Assert.Equal(0.5, metrics.OverrideRate); // 2 overridden / 4 reviewed = 50%

        Assert.Equal(2, metrics.FalseCallCount);  // Units 1 and 2
        Assert.Equal(0.2, metrics.FalseCallRate); // 2 / 10 = 20%

        Assert.Equal(1, metrics.EscapeCount);     // Unit 3
        Assert.Equal(0.1, metrics.EscapeRate);    // 1 / 10 = 10%
    }
}
