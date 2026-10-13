namespace PackInspect.Core.Domain;

/// <summary>Per-recipe thresholds for the rule-based vision inspection.</summary>
/// <param name="MinBodyRectangularity">Body area divided by the area of its bounding box; a chipped corner lowers it.</param>
/// <param name="ExpectedLeadCount">Number of leads that must be visible around the body.</param>
/// <param name="MinMarkingPixels">Fewest bright pixels the marking area must contain.</param>
/// <param name="MaxMarkingPixels">Most bright pixels the marking area may contain; more means something covers the marking.</param>
/// <param name="MaxDefectAreaPx">Largest bright area allowed on the body outside the marking.</param>
public sealed record VisionSettings(
    double MinBodyRectangularity,
    int ExpectedLeadCount,
    int MinMarkingPixels,
    int MaxMarkingPixels,
    int MaxDefectAreaPx);
