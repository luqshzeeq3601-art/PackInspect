using PackInspect.Core.Devices;

namespace PackInspect.Data.Entities;

/// <summary>Row of the Recipes table. Columns are documented in docs/Data.md.</summary>
public sealed class RecipeEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int Version { get; set; }

    public double CameraPositionMm { get; set; }

    public double TestPositionMm { get; set; }

    public double MinBodyRectangularity { get; set; }

    public int ExpectedLeadCount { get; set; }

    public int MinMarkingPixels { get; set; }

    public int MaxMarkingPixels { get; set; }

    public int MaxDefectAreaPx { get; set; }

    public MeasurementKind MeasurementKind { get; set; }

    public double LowLimit { get; set; }

    public double HighLimit { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int? UpdatedByUserId { get; set; }

    public UserEntity? UpdatedByUser { get; set; }
}
