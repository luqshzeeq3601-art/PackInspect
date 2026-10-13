namespace PackInspect.Core.Domain;

/// <summary>The stored record for one inspected and tested unit.</summary>
public sealed record UnitResult(
    int Sequence,
    Verdict VisionVerdict,
    DefectType DefectType,
    double MeasuredValue,
    Verdict ElectricalVerdict,
    Verdict FinalVerdict,
    string ImagePath,
    int CycleTimeMs,
    DateTimeOffset TimestampUtc,
    string? Barcode = null);
