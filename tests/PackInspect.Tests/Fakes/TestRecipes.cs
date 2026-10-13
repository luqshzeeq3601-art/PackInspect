using PackInspect.Core.Devices;
using PackInspect.Core.Domain;

namespace PackInspect.Tests.Fakes;

internal static class TestRecipes
{
    /// <summary>The same thresholds and limits as the seeded QFN-5x5-100R recipe.</summary>
    public static Recipe Resistance100 { get; } = new()
    {
        Id = 1,
        Name = "TEST-100R",
        Version = 1,
        CameraPositionMm = 100,
        TestPositionMm = 200,
        Vision = new VisionSettings(
            MinBodyRectangularity: 0.965,
            ExpectedLeadCount: 24,
            MinMarkingPixels: 900,
            MaxMarkingPixels: 1800,
            MaxDefectAreaPx: 15),
        MeasurementKind = MeasurementKind.Resistance,
        Limits = new ElectricalLimits(95, 105),
    };
}
