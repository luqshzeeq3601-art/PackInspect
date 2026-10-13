using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Devices.Vision;
using PackInspect.Simulator.Imaging;
using PackInspect.Simulator.Units;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

/// <summary>
/// The rule-based inspection against generated images. The generator is seeded,
/// so the same images are inspected on every run and the rates below are exact,
/// not statistical.
/// </summary>
public class VisionInspectorTests
{
    private const int ImagesPerClass = 300;
    private static readonly VisionSettings Settings = TestRecipes.Resistance100.Vision;
    private readonly OpenCvVisionInspector _inspector = new();

    [Fact]
    public void Good_units_are_never_rejected()
    {
        var results = InspectMany(UnitDefect.None);

        Assert.All(results, result => Assert.Equal(Verdict.Pass, result.Verdict));
    }

    [Theory]
    [InlineData(UnitDefect.CrackChip, DefectType.CrackChip)]
    [InlineData(UnitDefect.LeadDefect, DefectType.LeadDefect)]
    [InlineData(UnitDefect.MarkingDefect, DefectType.MarkingDefect)]
    public void Structural_defects_are_always_found_and_named(UnitDefect drawn, DefectType expected)
    {
        var results = InspectMany(drawn);

        Assert.All(results, result => Assert.Equal(expected, result.Defect));
    }

    [Theory]
    [InlineData(UnitDefect.Scratch, 0.85)]
    [InlineData(UnitDefect.Contamination, 0.78)]
    public void Most_surface_defects_are_rejected(UnitDefect drawn, double minimumRejectRate)
    {
        var results = InspectMany(drawn);

        // Known limit of the rule-based check: a small defect lying on the
        // marking can hide in it. This is the gap the AI inspection in phase 5 targets.
        double rejectRate = results.Count(result => result.Verdict == Verdict.Fail) / (double)results.Count;
        Assert.True(rejectRate >= minimumRejectRate, $"Only {rejectRate:P1} of {drawn} images were rejected.");
    }

    [Fact]
    public void Empty_carrier_is_reported_as_no_unit()
    {
        var empty = UnitImageGenerator.GenerateEmpty(seed: 1);

        var result = _inspector.Inspect(ToCaptured(empty), Settings);

        Assert.False(result.UnitFound);
    }

    [Fact]
    public void Lead_count_comes_from_the_recipe()
    {
        var good = ToCaptured(UnitImageGenerator.Generate(UnitDefect.None, seed: 1));

        var result = _inspector.Inspect(good, Settings with { ExpectedLeadCount = 20 });

        Assert.Equal(24, result.LeadCount);
        Assert.Equal(DefectType.LeadDefect, result.Defect);
    }

    [Fact]
    public void Marking_limit_comes_from_the_recipe()
    {
        var good = ToCaptured(UnitImageGenerator.Generate(UnitDefect.None, seed: 1));

        var result = _inspector.Inspect(good, Settings with { MinMarkingPixels = 5000 });

        Assert.Equal(DefectType.MarkingDefect, result.Defect);
    }

    [Fact]
    public void Image_whose_size_does_not_match_its_pixels_is_refused() =>
        Assert.Throws<ArgumentException>(() => _inspector.Inspect(new CapturedImage(320, 320, new byte[10]), Settings));

    [Fact]
    public void Same_seed_gives_the_same_image()
    {
        var first = UnitImageGenerator.Generate(UnitDefect.Scratch, seed: 42);
        var second = UnitImageGenerator.Generate(UnitDefect.Scratch, seed: 42);

        Assert.Equal(first.Pixels, second.Pixels);
    }

    private List<VisionResult> InspectMany(UnitDefect defect) =>
        [.. Enumerable.Range(0, ImagesPerClass).Select(seed => _inspector.Inspect(ToCaptured(UnitImageGenerator.Generate(defect, seed)), Settings))];

    private static CapturedImage ToCaptured(UnitImage image) => new(image.Width, image.Height, image.Pixels);
}
