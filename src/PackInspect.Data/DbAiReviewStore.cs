using Microsoft.EntityFrameworkCore;
using PackInspect.Core.Ai;
using PackInspect.Core.Domain;
using PackInspect.Data.Entities;

namespace PackInspect.Data;

public sealed class DbAiReviewStore(IDbContextFactory<PackInspectDbContext> contextFactory) : IAiReviewStore
{
    public async Task<int> SaveAiClassificationAsync(
        int unitResultId,
        DefectType aiLabel,
        double aiConfidence,
        string modelVersion,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.AiReviews.FirstOrDefaultAsync(r => r.UnitResultId == unitResultId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            existing.AiLabel = aiLabel;
            existing.AiConfidence = aiConfidence;
            existing.ModelVersion = modelVersion;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing.Id;
        }

        var entry = new AiReviewEntity
        {
            UnitResultId = unitResultId,
            AiLabel = aiLabel,
            AiConfidence = aiConfidence,
            ModelVersion = modelVersion,
        };

        db.AiReviews.Add(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entry.Id;
    }

    public async Task<IReadOnlyList<AiReviewRecord>> GetPendingReviewsAsync(int? lotId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = db.AiReviews
            .AsNoTracking()
            .Include(r => r.UnitResult!)
            .ThenInclude(u => u.Lot)
            .Where(r => r.OperatorVerdict == null);

        if (lotId.HasValue)
        {
            query = query.Where(r => r.UnitResult!.LotId == lotId.Value);
        }

        var rows = await query
            .OrderBy(r => r.UnitResult!.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    public async Task<IReadOnlyList<AiReviewRecord>> GetReviewHistoryAsync(int? lotId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = db.AiReviews
            .AsNoTracking()
            .Include(r => r.UnitResult!)
            .ThenInclude(u => u.Lot)
            .Include(r => r.ReviewedByUser)
            .Where(r => r.OperatorVerdict != null);

        if (lotId.HasValue)
        {
            query = query.Where(r => r.UnitResult!.LotId == lotId.Value);
        }

        var rows = await query
            .OrderByDescending(r => r.ReviewedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    public async Task SubmitReviewAsync(
        int aiReviewId,
        string operatorVerdict,
        DefectType? operatorLabel,
        int reviewedByUserId,
        DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var review = await db.AiReviews
            .FirstOrDefaultAsync(r => r.Id == aiReviewId, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException($"AI review record {aiReviewId} not found.");

        review.OperatorVerdict = operatorVerdict;
        review.OperatorLabel = operatorLabel;
        review.ReviewedByUserId = reviewedByUserId;
        review.ReviewedAtUtc = reviewedAtUtc.UtcDateTime;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiReviewMetrics> GetMetricsAsync(int? lotId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var reviewQuery = db.AiReviews
            .AsNoTracking()
            .Include(r => r.UnitResult!)
            .ThenInclude(u => u.Lot)
            .Include(r => r.ReviewedByUser)
            .AsQueryable();

        var unitQuery = db.UnitResults.AsNoTracking().AsQueryable();

        if (lotId.HasValue)
        {
            reviewQuery = reviewQuery.Where(r => r.UnitResult!.LotId == lotId.Value);
            unitQuery = unitQuery.Where(u => u.LotId == lotId.Value);
        }

        var reviews = await reviewQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        int totalUnits = await unitQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var records = reviews.Select(MapToRecord);
        return AiReviewMetrics.Calculate(records, totalUnits);
    }

    private static AiReviewRecord MapToRecord(AiReviewEntity r) => new(
        Id: r.Id,
        UnitResultId: r.UnitResultId,
        LotId: r.UnitResult?.LotId ?? 0,
        LotNumber: r.UnitResult?.Lot?.LotNumber ?? string.Empty,
        Sequence: r.UnitResult?.Sequence ?? 0,
        Barcode: r.UnitResult?.Barcode,
        ImagePath: r.UnitResult?.ImagePath ?? string.Empty,
        RuleDefectType: r.UnitResult?.DefectType ?? DefectType.None,
        RuleVisionVerdict: r.UnitResult?.VisionVerdict ?? Verdict.Pass,
        AiLabel: r.AiLabel,
        AiConfidence: r.AiConfidence,
        ModelVersion: r.ModelVersion,
        OperatorVerdict: r.OperatorVerdict,
        OperatorLabel: r.OperatorLabel,
        ReviewedByUserId: r.ReviewedByUserId,
        ReviewedByUserName: r.ReviewedByUser?.UserName,
        ReviewedAtUtc: r.ReviewedAtUtc.HasValue ? new DateTimeOffset(r.ReviewedAtUtc.Value, TimeSpan.Zero) : null);
}
