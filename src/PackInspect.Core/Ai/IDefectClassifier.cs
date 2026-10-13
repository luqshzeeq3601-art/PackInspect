using PackInspect.Core.Devices;

namespace PackInspect.Core.Ai;

/// <summary>
/// Interface for AI defect classification models.
/// </summary>
public interface IDefectClassifier
{
    Task<DefectClassification> ClassifyAsync(CapturedImage image, CancellationToken cancellationToken);
}
