using PackInspect.Core.Domain;

namespace PackInspect.Data.Entities;

/// <summary>Row of the UnitResults table. Columns are documented in docs/Data.md.</summary>
public sealed class UnitResultEntity
{
    public int Id { get; set; }

    public int LotId { get; set; }

    public LotEntity? Lot { get; set; }

    public int Sequence { get; set; }

    public string? Barcode { get; set; }

    public Verdict VisionVerdict { get; set; }

    public DefectType DefectType { get; set; }

    public double MeasuredValue { get; set; }

    public Verdict ElectricalVerdict { get; set; }

    public Verdict FinalVerdict { get; set; }

    public required string ImagePath { get; set; }

    public int CycleTimeMs { get; set; }

    public DateTime TimestampUtc { get; set; }

    public AiReviewEntity? AiReview { get; set; }
}
