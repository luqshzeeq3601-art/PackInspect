namespace PackInspect.Core.Publishing;

public sealed record PublishedUnit(
    int Sequence,
    string? Barcode,
    string VisionVerdict,
    string DefectType,
    PublishedMeasurement Measurement,
    string ElectricalVerdict,
    string FinalVerdict,
    int CycleTimeMs,
    DateTimeOffset TimestampUtc);
