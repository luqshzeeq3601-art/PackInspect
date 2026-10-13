using PackInspect.Core.Domain;

namespace PackInspect.Core.Ai;

/// <summary>
/// Persistence interface for AI review records, supporting human-in-the-loop inspection.
/// </summary>
public interface IAiReviewStore
{
    Task<int> SaveAiClassificationAsync(
        int unitResultId,
        DefectType aiLabel,
        double aiConfidence,
        string modelVersion,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiReviewRecord>> GetPendingReviewsAsync(int? lotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AiReviewRecord>> GetReviewHistoryAsync(int? lotId, CancellationToken cancellationToken);

    Task SubmitReviewAsync(
        int aiReviewId,
        string operatorVerdict,
        DefectType? operatorLabel,
        int reviewedByUserId,
        DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken);

    Task<AiReviewMetrics> GetMetricsAsync(int? lotId, CancellationToken cancellationToken);
}
