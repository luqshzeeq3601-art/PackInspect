namespace PackInspect.Core.Publishing;

public sealed record PublishedMeasurement(
    string Kind,
    double Value,
    string Unit,
    double LowLimit,
    double HighLimit);
