using PackInspect.Core.Domain;

namespace PackInspect.Core.Ai;

/// <summary>
/// Domain model for an AI review entry corresponding to docs/Data.md section 5.
/// </summary>
public sealed record AiReviewRecord(
    int Id,
    int UnitResultId,
    int LotId,
    string LotNumber,
    int Sequence,
    string? Barcode,
    string ImagePath,
    DefectType RuleDefectType,
    Verdict RuleVisionVerdict,
    DefectType AiLabel,
    double AiConfidence,
    string ModelVersion,
    string? OperatorVerdict,
    DefectType? OperatorLabel,
    int? ReviewedByUserId,
    string? ReviewedByUserName,
    DateTimeOffset? ReviewedAtUtc);
