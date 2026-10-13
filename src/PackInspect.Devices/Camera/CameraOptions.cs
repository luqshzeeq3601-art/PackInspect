namespace PackInspect.Devices.Camera;

/// <summary>Connection settings for the camera, read from appsettings.json.</summary>
public sealed class CameraOptions
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5030;

    /// <summary>Longest time to wait for one image.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);
}
