using Microsoft.Extensions.DependencyInjection;
using PackInspect.Core.Ai;
using PackInspect.Core.Domain;
using PackInspect.Data;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

public sealed class AiReviewStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly ServiceProvider _services;

    public AiReviewStoreTests()
    {
        _services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddPackInspectData(_folder.File("test.db"))
            .BuildServiceProvider();
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _services.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task AiReviewStore_saves_classifications_and_submits_human_verdicts()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var resultStore = _services.GetRequiredService<IResultStore>();
        var reviewStore = _services.GetRequiredService<IAiReviewStore>();

        int lotId = await resultStore.StartLotAsync("LOT-AI-01", TestRecipes.Resistance100, 10, DateTimeOffset.UtcNow, TestToken);
        var unitResult = new UnitResult(
            Sequence: 1,
            VisionVerdict: Verdict.Fail,
            DefectType: DefectType.Scratch,
            MeasuredValue: 100.0,
            ElectricalVerdict: Verdict.Pass,
            FinalVerdict: Verdict.Fail,
            ImagePath: "lot-1/unit-1.png",
            CycleTimeMs: 2000,
            TimestampUtc: DateTimeOffset.UtcNow,
            Barcode: "U000001");

        int unitResultId = await resultStore.SaveUnitResultAsync(lotId, unitResult, TestToken);

        // 1. Save AI classification
        int reviewId = await reviewStore.SaveAiClassificationAsync(
            unitResultId,
            aiLabel: DefectType.None,
            aiConfidence: 0.96,
            modelVersion: "DINOv2-ViT-S-v1",
            TestToken);

        Assert.True(reviewId > 0);

        // 2. Query pending reviews
        var pending = await reviewStore.GetPendingReviewsAsync(lotId, TestToken);
        var pendingItem = Assert.Single(pending);
        Assert.Equal(reviewId, pendingItem.Id);
        Assert.Equal(DefectType.None, pendingItem.AiLabel);
        Assert.Equal(DefectType.Scratch, pendingItem.RuleDefectType);
        Assert.Null(pendingItem.OperatorVerdict);

        // 3. Submit operator confirmation (rule-based false call confirmed good)
        await reviewStore.SubmitReviewAsync(
            reviewId,
            operatorVerdict: "Confirmed",
            operatorLabel: null,
            reviewedByUserId: 1, // seeded admin
            reviewedAtUtc: DateTimeOffset.UtcNow,
            TestToken);

        // 4. Pending should now be empty, and review history should contain the entry
        var pendingAfter = await reviewStore.GetPendingReviewsAsync(lotId, TestToken);
        Assert.Empty(pendingAfter);

        var history = await reviewStore.GetReviewHistoryAsync(lotId, TestToken);
        var historyItem = Assert.Single(history);
        Assert.Equal("Confirmed", historyItem.OperatorVerdict);
        Assert.Equal(1, historyItem.ReviewedByUserId);

        // 5. Verify computed metrics
        var metrics = await reviewStore.GetMetricsAsync(lotId, TestToken);
        Assert.Equal(1, metrics.TotalReviewed);
        Assert.Equal(1, metrics.ConfirmedCount);
        Assert.Equal(0, metrics.OverriddenCount);
        Assert.Equal(1, metrics.FalseCallCount); // Rule called Scratch, AI called None, confirmed None
        Assert.Equal(1.0, metrics.FalseCallRate);
    }
}
