using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Core.Publishing;
using PackInspect.Devices.Publishing;

namespace PackInspect.Tests.Unit;

public class FabTraceHttpResultPublisherTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly string _testQueueDir;

    public FabTraceHttpResultPublisherTests()
    {
        _testQueueDir = Path.Combine(Path.GetTempPath(), "fabtrace_test_queue_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testQueueDir))
        {
            try { Directory.Delete(_testQueueDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private static PublishedResultPayload CreateSamplePayload(string barcode = "U00001", string lotNumber = "LOT-001")
    {
        var recipe = new Recipe
        {
            Id = 1,
            Name = "QFN-5x5-100R",
            Version = 1,
            CameraPositionMm = 50.0,
            TestPositionMm = 120.0,
            Vision = new VisionSettings(0.965, 24, 900, 1800, 15),
            MeasurementKind = MeasurementKind.Resistance,
            Limits = new ElectricalLimits(95.0, 105.0),
        };

        var unitResult = new UnitResult(
            Sequence: 1,
            VisionVerdict: Verdict.Pass,
            DefectType: DefectType.None,
            MeasuredValue: 100.2,
            ElectricalVerdict: Verdict.Pass,
            FinalVerdict: Verdict.Pass,
            ImagePath: "path/to/img.png",
            CycleTimeMs: 1500,
            TimestampUtc: DateTimeOffset.UtcNow,
            Barcode: barcode);

        return PublishedResultPayload.Create("PACKINSPECT-SIM-01", lotNumber, recipe, unitResult);
    }

    [Fact]
    public async Task Successful_publish_forwards_payload_with_api_key_and_cleans_pending_queue()
    {
        string? capturedApiKey = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            capturedApiKey = request.Headers.TryGetValues("X-Api-Key", out var vals) ? vals.FirstOrDefault() : null;
            capturedBody = await request.Content!.ReadAsStringAsync(ct);

            // Extract ResultIds from posted JSON array
            using var doc = JsonDocument.Parse(capturedBody);
            var id = doc.RootElement[0].GetProperty("resultId").GetGuid();

            var resp = new { items = new[] { new { id, status = "Created", error = (string?)null } } };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080/") };
        var options = new FabTracePublisherOptions
        {
            BaseUrl = "http://localhost:5080",
            ApiKey = "dev-key-VI-01",
            QueueDirectory = _testQueueDir,
        };

        using var publisher = new FabTraceHttpResultPublisher(options, client);
        var payload = CreateSamplePayload();

        await publisher.PublishAsync(payload, Ct);

        Assert.Equal("dev-key-VI-01", capturedApiKey);
        Assert.NotNull(capturedBody);
        Assert.Contains("VIS-QFN32", capturedBody); // recipe mapping applied
        Assert.Contains("U00001", capturedBody);
        Assert.Equal(0, publisher.PendingCount);
        Assert.Equal(0, publisher.IssueCount);
    }

    [Fact]
    public async Task Offline_server_retains_item_in_pending_queue_without_throwing()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Connection refused by localhost:5080"));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080/") };
        var options = new FabTracePublisherOptions
        {
            BaseUrl = "http://localhost:5080",
            QueueDirectory = _testQueueDir,
        };

        using var publisher = new FabTraceHttpResultPublisher(options, client);
        var payload = CreateSamplePayload();

        // Must not throw an exception to the inspection cycle caller
        var exception = await Record.ExceptionAsync(() => publisher.PublishAsync(payload, Ct));
        Assert.Null(exception);

        Assert.Equal(1, publisher.PendingCount);
        Assert.Equal(0, publisher.IssueCount);
    }

    [Fact]
    public async Task Subsequent_flush_succeeds_when_server_recovers()
    {
        var isOnline = false;

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            if (!isOnline)
            {
                throw new HttpRequestException("Server offline");
            }

            var body = await request.Content!.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement[0].GetProperty("resultId").GetGuid();

            var resp = new { items = new[] { new { id, status = "Created", error = (string?)null } } };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080/") };
        var options = new FabTracePublisherOptions
        {
            BaseUrl = "http://localhost:5080",
            QueueDirectory = _testQueueDir,
        };

        using var publisher = new FabTraceHttpResultPublisher(options, client);
        var payload = CreateSamplePayload();

        // Step 1: while offline
        await publisher.PublishAsync(payload, Ct);
        Assert.Equal(1, publisher.PendingCount);

        // Step 2: server comes online
        isOnline = true;
        var flushed = await publisher.FlushQueueAsync(Ct);

        Assert.Equal(1, flushed);
        Assert.Equal(0, publisher.PendingCount);
    }

    [Fact]
    public async Task Rejected_item_is_moved_to_issues_folder_and_logged()
    {
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement[0].GetProperty("resultId").GetGuid();

            var resp = new { items = new[] { new { id, status = "Rejected", error = "Unknown process step" } } };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080/") };
        var options = new FabTracePublisherOptions
        {
            BaseUrl = "http://localhost:5080",
            QueueDirectory = _testQueueDir,
        };

        using var publisher = new FabTraceHttpResultPublisher(options, client);
        var payload = CreateSamplePayload();

        await publisher.PublishAsync(payload, Ct);

        Assert.Equal(0, publisher.PendingCount);
        Assert.Equal(1, publisher.IssueCount);
        Assert.True(File.Exists(publisher.IssuesLogFile));

        var logContent = await File.ReadAllTextAsync(publisher.IssuesLogFile, Ct);
        Assert.Contains("REJECTED", logContent);
        Assert.Contains("Unknown process step", logContent);
    }

    [Fact]
    public async Task Duplicate_item_is_cleaned_up_from_pending_queue()
    {
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement[0].GetProperty("resultId").GetGuid();

            var resp = new { items = new[] { new { id, status = "Duplicate", error = (string?)null } } };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resp), System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080/") };
        var options = new FabTracePublisherOptions
        {
            BaseUrl = "http://localhost:5080",
            QueueDirectory = _testQueueDir,
        };

        using var publisher = new FabTraceHttpResultPublisher(options, client);
        var payload = CreateSamplePayload();

        await publisher.PublishAsync(payload, Ct);

        Assert.Equal(0, publisher.PendingCount);
        Assert.Equal(0, publisher.IssueCount);
    }

    private sealed class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
