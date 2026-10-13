namespace PackInspect.Devices.Publishing;

/// <summary>
/// Configuration for forwarding inspected unit results to the FabTrace smart factory platform.
/// </summary>
public sealed class FabTracePublisherOptions
{
    /// <summary>
    /// Off by default: no factory server is part of this project, so results go
    /// to local files unless forwarding is switched on in configuration.
    /// </summary>
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "http://127.0.0.1:5080";

    /// <summary>
    /// Never stored in the repository (docs/Security.md). Supplied through the
    /// <c>FabTrace__ApiKey</c> environment variable when forwarding is enabled.
    /// </summary>
    public string ApiKey { get; set; } = "";

    public string ProcessStepCode { get; set; } = "VISION_INSPECT";

    public string OperatorBadge { get; set; } = "OP103";

    public string DefaultRecipeCode { get; set; } = "VIS-QFN32";

    public int DefaultRecipeVersion { get; set; } = 1;

    public string QueueDirectory { get; set; } = "";

    public int BatchSize { get; set; } = 50;

    public Dictionary<string, string> RecipeMap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["QFN-5x5-100R"] = "VIS-QFN32",
        ["QFN-5x5-100R-TIGHT"] = "VIS-QFN32",
        ["QFN-5x5-3V3"] = "VIS-QFN48",
    };
}
