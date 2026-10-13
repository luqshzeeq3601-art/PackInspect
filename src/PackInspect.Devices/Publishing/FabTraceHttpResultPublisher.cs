using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PackInspect.Core.Publishing;

namespace PackInspect.Devices.Publishing;

/// <summary>
/// HTTP result publisher that forwards inspected unit results to FabTrace with a resilient
/// store-and-forward queue on local disk, ensuring machine cycle continuity even if network fails.
/// </summary>
public sealed class FabTraceHttpResultPublisher : IResultPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly FabTracePublisherOptions _options;
    private readonly ILogger<FabTraceHttpResultPublisher>? _logger;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly string _pendingDir;
    private readonly string _issuesDir;
    private readonly string _issuesLogFile;
    private bool _disposed;

    public FabTraceHttpResultPublisher(
        FabTracePublisherOptions options,
        HttpClient? httpClient = null,
        ILogger<FabTraceHttpResultPublisher>? logger = null)
    {
        _options = options;
        _logger = logger;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(5),
            };
            _ownsHttpClient = true;
        }

        var root = string.IsNullOrWhiteSpace(options.QueueDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "data", "store_and_forward")
            : options.QueueDirectory;

        _pendingDir = Path.Combine(root, "pending");
        _issuesDir = Path.Combine(root, "issues");
        _issuesLogFile = Path.Combine(_issuesDir, "issues.log");

        Directory.CreateDirectory(_pendingDir);
        Directory.CreateDirectory(_issuesDir);
    }

    public string PendingDirectory => _pendingDir;
    public string IssuesDirectory => _issuesDir;
    public string IssuesLogFile => _issuesLogFile;

    public int PendingCount => Directory.Exists(_pendingDir) ? Directory.GetFiles(_pendingDir, "*.json").Length : 0;
    public int IssueCount => Directory.Exists(_issuesDir) ? Directory.GetFiles(_issuesDir, "*.json").Length : 0;

    public async Task PublishAsync(PublishedResultPayload payload, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var resultId = Guid.NewGuid();
        var dto = MapToDto(resultId, payload);

        // 1. Persist to pending queue first
        var pendingPath = Path.Combine(_pendingDir, $"{resultId:N}.json");
        var tempPath = pendingPath + ".tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, dto, JsonOpts, cancellationToken).ConfigureAwait(false);
            }
            File.Move(tempPath, pendingPath, overwrite: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The unit is already in the database; a publishing problem must not stop the machine.
            _logger?.LogError(ex, "Failed to persist result {ResultId} to store-and-forward queue.", resultId);
            try
            {
                File.Delete(tempPath);
            }
            catch (IOException deleteError)
            {
                _logger?.LogWarning(deleteError, "Could not remove the partial queue file {TempPath}.", tempPath);
            }

            return;
        }

        // 2. Try to send what is queued. A transport error leaves it queued; a stop request still propagates.
        try
        {
            await FlushQueueAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Opportunistic flush failed for result {ResultId}; item retained in queue.", resultId);
        }
    }

    /// <summary>
    /// Flushes pending queued items to FabTrace.
    /// </summary>
    /// <returns>Number of successfully processed items (created + duplicate).</returns>
    public async Task<int> FlushQueueAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!await _flushLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            // Another flush is already active
            return 0;
        }

        try
        {
            var pendingFiles = Directory.GetFiles(_pendingDir, "*.json")
                .OrderBy(File.GetCreationTimeUtc)
                .Take(_options.BatchSize)
                .ToList();

            if (pendingFiles.Count == 0)
            {
                return 0;
            }

            var batch = new List<(string FilePath, FabTraceIngestDto Dto)>();
            foreach (var file in pendingFiles)
            {
                try
                {
                    await using var stream = File.OpenRead(file);
                    var dto = await JsonSerializer.DeserializeAsync<FabTraceIngestDto>(stream, JsonOpts, cancellationToken).ConfigureAwait(false);
                    if (dto is not null)
                    {
                        batch.Add((file, dto));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogWarning(ex, "Could not read queue item {File}; skipping.", file);
                }
            }

            if (batch.Count == 0)
            {
                return 0;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/ingest/inspection-results")
            {
                Content = JsonContent.Create(batch.Select(b => b.Dto).ToList(), options: JsonOpts),
            };
            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _options.ApiKey);
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException ||
                (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // HttpClient reports its own timeout as a cancellation. Only that one means
                // "unreachable"; a cancellation the caller asked for is left to propagate.
                _logger?.LogWarning(ex, "FabTrace ingestion endpoint unreachable. Pending items remain queued: {Count}.", batch.Count);
                return 0;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger?.LogWarning("FabTrace returned HTTP {StatusCode}: {Body}. Retaining {Count} items in queue.",
                        (int)response.StatusCode, body, batch.Count);
                    return 0;
                }

                var ingestResponse = await response.Content.ReadFromJsonAsync<FabTraceIngestResponse>(JsonOpts, cancellationToken).ConfigureAwait(false);
                if (ingestResponse?.Items is null)
                {
                    return 0;
                }

                var outcomeLookup = ingestResponse.Items.ToDictionary(i => i.Id);
                var resolved = 0;

                foreach (var (filePath, dto) in batch)
                {
                    if (!outcomeLookup.TryGetValue(dto.ResultId, out var outcome))
                    {
                        continue;
                    }

                    if (outcome.Status is "Created" or "Duplicate")
                    {
                        try
                        {
                            File.Delete(filePath);
                            resolved++;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to delete forwarded file {FilePath}", filePath);
                        }
                    }
                    else if (outcome.Status == "Rejected")
                    {
                        var issuePath = Path.Combine(_issuesDir, Path.GetFileName(filePath));
                        try
                        {
                            File.Move(filePath, issuePath, overwrite: true);
                            var logEntry = $"[{DateTime.UtcNow:O}] REJECTED ResultId={dto.ResultId} Serial={dto.SerialNumber} Error={outcome.Error}{Environment.NewLine}";
                            await File.AppendAllTextAsync(_issuesLogFile, logEntry, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger?.LogError(ex, "Failed to move rejected item {FilePath} to issues directory.", filePath);
                        }
                    }
                }

                return resolved;
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private FabTraceIngestDto MapToDto(Guid resultId, PublishedResultPayload payload)
    {
        var recipeName = payload.Recipe?.Name ?? "";
        var recipeCode = _options.RecipeMap.TryGetValue(recipeName, out var mapped)
            ? mapped
            : (!string.IsNullOrWhiteSpace(_options.DefaultRecipeCode) ? _options.DefaultRecipeCode : (!string.IsNullOrWhiteSpace(recipeName) ? recipeName : "DEFAULT"));

        var recipeVersion = (payload.Recipe?.Version ?? 0) > 0 ? payload.Recipe!.Version : _options.DefaultRecipeVersion;

        var verdict = string.Equals(payload.Unit.FinalVerdict, "Pass", StringComparison.OrdinalIgnoreCase)
            ? "Pass"
            : "Fail";

        var defectCode = string.Equals(payload.Unit.DefectType, "None", StringComparison.OrdinalIgnoreCase)
            ? null
            : payload.Unit.DefectType;

        var readings = new List<FabTraceTestReadingDto>();
        if (payload.Unit.Measurement is not null)
        {
            readings.Add(new FabTraceTestReadingDto
            {
                ParameterName = payload.Unit.Measurement.Kind,
                Value = payload.Unit.Measurement.Value,
                LowerLimit = payload.Unit.Measurement.LowLimit,
                UpperLimit = payload.Unit.Measurement.HighLimit,
                Unit = payload.Unit.Measurement.Unit,
            });
        }

        return new FabTraceIngestDto
        {
            ResultId = resultId,
            LotNumber = payload.LotNumber ?? "UNKNOWN",
            SerialNumber = payload.Unit.Barcode ?? resultId.ToString("N"),
            ProcessStepCode = _options.ProcessStepCode,
            RecipeCode = recipeCode,
            RecipeVersion = recipeVersion,
            OperatorBadge = _options.OperatorBadge,
            Verdict = verdict,
            DefectCode = defectCode,
            InspectedAt = payload.Unit.TimestampUtc,
            CycleTimeMs = payload.Unit.CycleTimeMs,
            Readings = readings,
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _flushLock.Dispose();
    }
}

internal sealed record FabTraceIngestDto
{
    public required Guid ResultId { get; init; }
    public required string LotNumber { get; init; }
    public required string SerialNumber { get; init; }
    public required string ProcessStepCode { get; init; }
    public required string RecipeCode { get; init; }
    public required int RecipeVersion { get; init; }
    public required string OperatorBadge { get; init; }
    public required string Verdict { get; init; }
    public string? DefectCode { get; init; }
    public required DateTimeOffset InspectedAt { get; init; }
    public required int CycleTimeMs { get; init; }
    public IReadOnlyList<FabTraceTestReadingDto> Readings { get; init; } = [];
}

internal sealed record FabTraceTestReadingDto
{
    public required string ParameterName { get; init; }
    public required double Value { get; init; }
    public double? LowerLimit { get; init; }
    public double? UpperLimit { get; init; }
    public string Unit { get; init; } = "";
}

internal sealed record FabTraceIngestResponse
{
    public IReadOnlyList<FabTraceIngestOutcomeItem> Items { get; init; } = [];
}

internal sealed record FabTraceIngestOutcomeItem(Guid Id, string Status, string? Error);
