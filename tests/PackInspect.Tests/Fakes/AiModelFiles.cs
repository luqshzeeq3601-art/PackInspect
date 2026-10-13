namespace PackInspect.Tests.Fakes;

/// <summary>
/// Finds the model files in the repository's <c>models</c> folder. They are not
/// committed, so a test that needs one is skipped on a clone that has none.
/// </summary>
internal static class AiModelFiles
{
    public const string HeadFileName = "defect_classifier_head.zip";

    private const string BackboneFileName = "dinov2_small_quantized.onnx";

    /// <summary>The path of the DINOv2 backbone; skips the calling test when the file is absent.</summary>
    public static string BackboneOrSkip()
    {
        // From the test output folder (bin/Debug/net10.0) up to the repository root.
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../models", BackboneFileName));
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine("models", BackboneFileName));
        }

        if (!File.Exists(path))
        {
            Assert.Skip($"{BackboneFileName} is not in the models folder; see the README for the download step.");
        }

        return path;
    }
}
