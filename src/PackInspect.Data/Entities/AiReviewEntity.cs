using PackInspect.Core.Domain;

namespace PackInspect.Data.Entities;

/// <summary>
/// Row of the AiReviews table, storing AI classification and operator review results.
/// Schema documented in docs/Data.md section 5.
/// </summary>
public sealed class AiReviewEntity
{
    public int Id { get; set; }

    public int UnitResultId { get; set; }

    public UnitResultEntity? UnitResult { get; set; }

    public DefectType AiLabel { get; set; }

    public double AiConfidence { get; set; }

    public required string ModelVersion { get; set; }

    public string? OperatorVerdict { get; set; }

    public DefectType? OperatorLabel { get; set; }

    public int? ReviewedByUserId { get; set; }

    public UserEntity? ReviewedByUser { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }
}
