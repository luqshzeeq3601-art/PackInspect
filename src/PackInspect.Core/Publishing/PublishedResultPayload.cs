using PackInspect.Core.Devices;
using PackInspect.Core.Domain;

namespace PackInspect.Core.Publishing;

/// <summary>
/// Schema for published unit inspection and test results, matching docs/Data.md section 6.
/// </summary>
public sealed record PublishedResultPayload(
    int SchemaVersion,
    string StationId,
    string LotNumber,
    PublishedRecipe Recipe,
    PublishedUnit Unit)
{
    public const int CurrentSchemaVersion = 1;

    public static PublishedResultPayload Create(
        string stationId,
        string lotNumber,
        Recipe recipe,
        UnitResult unit)
    {
        string unitString = recipe.MeasurementKind == MeasurementKind.Resistance ? "ohm" : "V";

        return new PublishedResultPayload(
            SchemaVersion: CurrentSchemaVersion,
            StationId: stationId,
            LotNumber: lotNumber,
            Recipe: new PublishedRecipe(recipe.Name, recipe.Version),
            Unit: new PublishedUnit(
                Sequence: unit.Sequence,
                Barcode: unit.Barcode,
                VisionVerdict: unit.VisionVerdict.ToString(),
                DefectType: unit.DefectType.ToString(),
                Measurement: new PublishedMeasurement(
                    Kind: recipe.MeasurementKind.ToString(),
                    Value: unit.MeasuredValue,
                    Unit: unitString,
                    LowLimit: recipe.Limits.Low,
                    HighLimit: recipe.Limits.High),
                ElectricalVerdict: unit.ElectricalVerdict.ToString(),
                FinalVerdict: unit.FinalVerdict.ToString(),
                CycleTimeMs: unit.CycleTimeMs,
                TimestampUtc: unit.TimestampUtc));
    }
}
