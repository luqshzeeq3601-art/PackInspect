using PackInspect.Core.Domain;

namespace PackInspect.Core.Ai;

/// <summary>
/// Result produced by an AI defect classifier model.
/// </summary>
public sealed record DefectClassification(
    DefectType Label,
    double Confidence,
    IReadOnlyDictionary<DefectType, double> Probabilities,
    string ModelVersion);
