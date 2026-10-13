using System.IO;
using System.Text.Json;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Core.Publishing;
using PackInspect.Devices.Publishing;

namespace PackInspect.Tests.Unit;

public class ResultPublisherTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static readonly VisionSettings DefaultVision = new(
        MinBodyRectangularity: 0.965,
        ExpectedLeadCount: 24,
        MinMarkingPixels: 900,
        MaxMarkingPixels: 1800,
        MaxDefectAreaPx: 15);

    [Fact]
    public void Payload_factory_creates_exact_schema_matching_data_spec()
    {
        var recipe = new Recipe
        {
            Id = 1,
            Name = "QFN-5x5",
            Version = 3,
            CameraPositionMm = 50.0,
            TestPositionMm = 120.0,
            Vision = DefaultVision,
            MeasurementKind = MeasurementKind.Resistance,
            Limits = new ElectricalLimits(95.0, 105.0),
        };

        var timestamp = DateTimeOffset.Parse("2026-10-13T08:15:30Z");
        var unitResult = new UnitResult(
            Sequence: 17,
            VisionVerdict: Verdict.Pass,
            DefectType: DefectType.None,
            MeasuredValue: 99.87,
            ElectricalVerdict: Verdict.Pass,
            FinalVerdict: Verdict.Pass,
            ImagePath: "data/images/lot-1/unit-17.png",
            CycleTimeMs: 2140,
            TimestampUtc: timestamp,
            Barcode: "U000017");

        var payload = PublishedResultPayload.Create(
            stationId: "PACKINSPECT-SIM-01",
            lotNumber: "LOT-0001",
            recipe: recipe,
            unit: unitResult);

        Assert.Equal(1, payload.SchemaVersion);
        Assert.Equal("PACKINSPECT-SIM-01", payload.StationId);
        Assert.Equal("LOT-0001", payload.LotNumber);
        Assert.Equal("QFN-5x5", payload.Recipe.Name);
        Assert.Equal(3, payload.Recipe.Version);

        Assert.Equal(17, payload.Unit.Sequence);
        Assert.Equal("U000017", payload.Unit.Barcode);
        Assert.Equal("Pass", payload.Unit.VisionVerdict);
        Assert.Equal("None", payload.Unit.DefectType);
        Assert.Equal("Pass", payload.Unit.ElectricalVerdict);
        Assert.Equal("Pass", payload.Unit.FinalVerdict);
        Assert.Equal(2140, payload.Unit.CycleTimeMs);
        Assert.Equal(timestamp, payload.Unit.TimestampUtc);

        Assert.Equal("Resistance", payload.Unit.Measurement.Kind);
        Assert.Equal(99.87, payload.Unit.Measurement.Value);
        Assert.Equal("ohm", payload.Unit.Measurement.Unit);
        Assert.Equal(95.0, payload.Unit.Measurement.LowLimit);
        Assert.Equal(105.0, payload.Unit.Measurement.HighLimit);
    }

    [Fact]
    public void Payload_maps_dc_voltage_units_correctly()
    {
        var recipe = new Recipe
        {
            Id = 2,
            Name = "QFN-5x5-3V3",
            Version = 1,
            CameraPositionMm = 50.0,
            TestPositionMm = 120.0,
            Vision = DefaultVision,
            MeasurementKind = MeasurementKind.DcVoltage,
            Limits = new ElectricalLimits(3.2, 3.4),
        };

        var timestamp = DateTimeOffset.Parse("2026-10-13T08:00:00Z");
        var unitResult = new UnitResult(
            Sequence: 1,
            VisionVerdict: Verdict.Pass,
            DefectType: DefectType.None,
            MeasuredValue: 3.305,
            ElectricalVerdict: Verdict.Pass,
            FinalVerdict: Verdict.Pass,
            ImagePath: "data/images/lot-1/unit-1.png",
            CycleTimeMs: 1900,
            TimestampUtc: timestamp,
            Barcode: "U000001");

        var payload = PublishedResultPayload.Create(
            stationId: "PACKINSPECT-SIM-01",
            lotNumber: "LOT-0001",
            recipe: recipe,
            unit: unitResult);

        Assert.Equal("DcVoltage", payload.Unit.Measurement.Kind);
        Assert.Equal("V", payload.Unit.Measurement.Unit);
        Assert.Equal(3.305, payload.Unit.Measurement.Value);
    }

    [Fact]
    public async Task FileResultPublisher_writes_json_to_disk_with_camel_case_schema()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"packinspect-pub-test-{Guid.NewGuid():N}");
        try
        {
            var publisher = new FileResultPublisher(tempDir);

            var recipe = new Recipe
            {
                Id = 1,
                Name = "QFN-5x5",
                Version = 3,
                CameraPositionMm = 50.0,
                TestPositionMm = 120.0,
                Vision = DefaultVision,
                MeasurementKind = MeasurementKind.Resistance,
                Limits = new ElectricalLimits(95.0, 105.0),
            };

            var timestamp = DateTimeOffset.Parse("2026-10-13T08:15:30Z");
            var unitResult = new UnitResult(
                Sequence: 17,
                VisionVerdict: Verdict.Pass,
                DefectType: DefectType.None,
                MeasuredValue: 99.87,
                ElectricalVerdict: Verdict.Pass,
                FinalVerdict: Verdict.Pass,
                ImagePath: "data/images/lot-1/unit-17.png",
                CycleTimeMs: 2140,
                TimestampUtc: timestamp,
                Barcode: "U000017");

            var payload = PublishedResultPayload.Create("PACKINSPECT-SIM-01", "LOT-0001", recipe, unitResult);

            await publisher.PublishAsync(payload, TestToken);

            string expectedFilePath = Path.Combine(tempDir, "LOT-0001", "unit-000017.json");
            Assert.True(File.Exists(expectedFilePath));

            string jsonContent = await File.ReadAllTextAsync(expectedFilePath, TestToken);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("PACKINSPECT-SIM-01", root.GetProperty("stationId").GetString());
            Assert.Equal("LOT-0001", root.GetProperty("lotNumber").GetString());

            var recipeElem = root.GetProperty("recipe");
            Assert.Equal("QFN-5x5", recipeElem.GetProperty("name").GetString());
            Assert.Equal(3, recipeElem.GetProperty("version").GetInt32());

            var unitElem = root.GetProperty("unit");
            Assert.Equal(17, unitElem.GetProperty("sequence").GetInt32());
            Assert.Equal("U000017", unitElem.GetProperty("barcode").GetString());
            Assert.Equal("Pass", unitElem.GetProperty("visionVerdict").GetString());
            Assert.Equal("None", unitElem.GetProperty("defectType").GetString());
            Assert.Equal("Pass", unitElem.GetProperty("electricalVerdict").GetString());
            Assert.Equal("Pass", unitElem.GetProperty("finalVerdict").GetString());
            Assert.Equal(2140, unitElem.GetProperty("cycleTimeMs").GetInt32());
            Assert.Equal(timestamp, unitElem.GetProperty("timestampUtc").GetDateTimeOffset());

            var measurementElem = unitElem.GetProperty("measurement");
            Assert.Equal("Resistance", measurementElem.GetProperty("kind").GetString());
            Assert.Equal(99.87, measurementElem.GetProperty("value").GetDouble(), precision: 2);
            Assert.Equal("ohm", measurementElem.GetProperty("unit").GetString());
            Assert.Equal(95.0, measurementElem.GetProperty("lowLimit").GetDouble(), precision: 1);
            Assert.Equal(105.0, measurementElem.GetProperty("highLimit").GetDouble(), precision: 1);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
