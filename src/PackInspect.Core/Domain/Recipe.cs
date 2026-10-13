using PackInspect.Core.Devices;

namespace PackInspect.Core.Domain;

/// <summary>Everything that differs from one product to the next.</summary>
public sealed record Recipe
{
    public int Id { get; init; }

    public required string Name { get; init; }

    public int Version { get; init; } = 1;

    public required double CameraPositionMm { get; init; }

    public required double TestPositionMm { get; init; }

    public required VisionSettings Vision { get; init; }

    public required MeasurementKind MeasurementKind { get; init; }

    public required ElectricalLimits Limits { get; init; }

    public int? UpdatedByUserId { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}
