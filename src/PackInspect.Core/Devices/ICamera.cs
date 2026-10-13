namespace PackInspect.Core.Devices;

/// <summary>
/// The inspection camera. First-cut contract; implemented in phase 2.
/// </summary>
public interface ICamera
{
    /// <summary>Triggers one exposure and returns the image.</summary>
    Task<CapturedImage> CaptureAsync(CancellationToken cancellationToken);
}
