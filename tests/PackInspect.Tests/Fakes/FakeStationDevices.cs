using PackInspect.Core.Ai;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Core.Sequence;

namespace PackInspect.Tests.Fakes;

internal sealed class FakeCamera(StepLog log) : ICamera
{
    public Exception? Failure { get; set; }

    public Task<CapturedImage> CaptureAsync(CancellationToken cancellationToken)
    {
        log.Add("capture");
        return Failure is null
            ? Task.FromResult(new CapturedImage(2, 2, new byte[4]))
            : Task.FromException<CapturedImage>(Failure);
    }
}

/// <summary>Returns whatever vision result the test sets; a good unit by default.</summary>
internal sealed class FakeVisionInspector : IVisionInspector
{
    public VisionResult Result { get; set; } = new(true, Verdict.Pass, DefectType.None, 1.0, 24, 1500, 0);

    public VisionResult Inspect(CapturedImage image, VisionSettings settings) => Result;
}

internal sealed class FakeInstrument(StepLog log) : IInstrument
{
    public double Reading { get; set; } = 100.0;

    public Exception? Failure { get; set; }

    public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<string> IdentifyAsync(CancellationToken cancellationToken) => Task.FromResult("FAKE,DMM,0,0");

    public Task<double> MeasureAsync(MeasurementKind kind, CancellationToken cancellationToken)
    {
        log.Add($"measure {kind}");
        return Failure is null ? Task.FromResult(Reading) : Task.FromException<double>(Failure);
    }
}

internal sealed class FakeImageStore : IImageStore
{
    public Task<string> SaveAsync(int lotId, int sequence, CapturedImage image, CancellationToken cancellationToken) =>
        Task.FromResult($"lot-{lotId}/unit-{sequence}.png");

    public string GetFilePath(string storedPath) => storedPath;
}

/// <summary>Keeps lots and results in memory, with the same rules as the real store.</summary>
internal sealed class InMemoryResultStore : IResultStore
{
    private readonly Lock _gate = new();
    private readonly List<(int Id, string LotNumber, Recipe Recipe, int Quantity)> _lots = [];

    public List<UnitResult> Results { get; } = [];

    public List<int> EndedLots { get; } = [];

    public Task<int> StartLotAsync(string lotNumber, Recipe recipe, int quantity, DateTimeOffset startedAtUtc, int? operatorUserId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_lots.Exists(lot => lot.LotNumber == lotNumber))
            {
                throw new InvalidOperationException($"Lot '{lotNumber}' already exists.");
            }

            _lots.Add((_lots.Count + 1, lotNumber, recipe, quantity));
            return Task.FromResult(_lots.Count);
        }
    }

    public Task<int> StartLotAsync(string lotNumber, Recipe recipe, int quantity, DateTimeOffset startedAtUtc, CancellationToken cancellationToken) =>
        StartLotAsync(lotNumber, recipe, quantity, startedAtUtc, null, cancellationToken);

    public Task<int> SaveUnitResultAsync(int lotId, UnitResult result, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            Results.Add(result);
            return Task.FromResult(Results.Count);
        }
    }

    public Task EndLotAsync(int lotId, DateTimeOffset endedAtUtc, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            EndedLots.Add(lotId);
        }

        return Task.CompletedTask;
    }

    public Task<LotSummary?> GetLotSummaryAsync(int lotId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The in-memory store is not used for summaries.");
}

internal sealed class FakeDefectClassifier : IDefectClassifier
{
    public DefectClassification Classification { get; set; } = new(
        DefectType.Scratch,
        0.95,
        new Dictionary<DefectType, double> { [DefectType.Scratch] = 0.95 },
        "v1.0-fake");

    public Task<DefectClassification> ClassifyAsync(CapturedImage image, CancellationToken cancellationToken) =>
        Task.FromResult(Classification);
}

internal sealed class InMemoryAiReviewStore : IAiReviewStore
{
    public record SavedRecord(int UnitResultId, DefectType AiLabel, double AiConfidence, string ModelVersion);

    public List<SavedRecord> Records { get; } = [];

    public Task<int> SaveAiClassificationAsync(
        int unitResultId,
        DefectType aiLabel,
        double aiConfidence,
        string modelVersion,
        CancellationToken cancellationToken)
    {
        Records.Add(new(unitResultId, aiLabel, aiConfidence, modelVersion));
        return Task.FromResult(Records.Count);
    }

    public Task<IReadOnlyList<AiReviewRecord>> GetPendingReviewsAsync(int? lotId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AiReviewRecord>>([]);

    public Task<IReadOnlyList<AiReviewRecord>> GetReviewHistoryAsync(int? lotId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AiReviewRecord>>([]);

    public Task SubmitReviewAsync(
        int aiReviewId,
        string operatorVerdict,
        DefectType? operatorLabel,
        int reviewedByUserId,
        DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<AiReviewMetrics> GetMetricsAsync(int? lotId, CancellationToken cancellationToken) =>
        Task.FromResult(AiReviewMetrics.Empty);
}
