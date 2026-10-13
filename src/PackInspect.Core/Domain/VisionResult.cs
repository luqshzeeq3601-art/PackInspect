namespace PackInspect.Core.Domain;

/// <summary>The outcome of inspecting one image, with the measurements behind the verdict.</summary>
public sealed record VisionResult(
    bool UnitFound,
    Verdict Verdict,
    DefectType Defect,
    double BodyRectangularity,
    int LeadCount,
    int MarkingPixels,
    int DefectAreaPx)
{
    /// <summary>No package body was found in the image.</summary>
    public static VisionResult NoUnit { get; } = new(false, Verdict.Fail, DefectType.None, 0, 0, 0, 0);
}
