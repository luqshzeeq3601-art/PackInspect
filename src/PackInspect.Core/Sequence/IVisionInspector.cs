using PackInspect.Core.Devices;
using PackInspect.Core.Domain;

namespace PackInspect.Core.Sequence;

/// <summary>Decides whether the unit in an image is visually good. Implemented with OpenCV in PackInspect.Devices.</summary>
public interface IVisionInspector
{
    VisionResult Inspect(CapturedImage image, VisionSettings settings);
}
