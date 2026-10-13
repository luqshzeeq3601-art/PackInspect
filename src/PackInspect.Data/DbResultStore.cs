using Microsoft.EntityFrameworkCore;
using PackInspect.Core.Domain;
using PackInspect.Data.Entities;

namespace PackInspect.Data;

public sealed class DbResultStore(IDbContextFactory<PackInspectDbContext> contextFactory) : IResultStore
{
    public async Task<int> StartLotAsync(
        string lotNumber,
        Recipe recipe,
        int quantity,
        DateTimeOffset startedAtUtc,
        int? operatorUserId,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (await db.Lots.AnyAsync(lot => lot.LotNumber == lotNumber, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Lot '{lotNumber}' already exists.");
        }

        var row = new LotEntity
        {
            LotNumber = lotNumber,
            RecipeId = recipe.Id,
            RecipeVersion = recipe.Version,
            Quantity = quantity,
            StartedAtUtc = startedAtUtc.UtcDateTime,
            OperatorUserId = operatorUserId,
        };
        db.Lots.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.Id;
    }

    public Task<int> StartLotAsync(string lotNumber, Recipe recipe, int quantity, DateTimeOffset startedAtUtc, CancellationToken cancellationToken) =>
        StartLotAsync(lotNumber, recipe, quantity, startedAtUtc, null, cancellationToken);

    public async Task<int> SaveUnitResultAsync(int lotId, UnitResult result, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = new UnitResultEntity
        {
            LotId = lotId,
            Sequence = result.Sequence,
            Barcode = result.Barcode,
            VisionVerdict = result.VisionVerdict,
            DefectType = result.DefectType,
            MeasuredValue = result.MeasuredValue,
            ElectricalVerdict = result.ElectricalVerdict,
            FinalVerdict = result.FinalVerdict,
            ImagePath = result.ImagePath,
            CycleTimeMs = result.CycleTimeMs,
            TimestampUtc = result.TimestampUtc.UtcDateTime,
        };
        db.UnitResults.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.Id;
    }

    public async Task EndLotAsync(int lotId, DateTimeOffset endedAtUtc, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.Lots.SingleAsync(lot => lot.Id == lotId, cancellationToken).ConfigureAwait(false);
        row.EndedAtUtc = endedAtUtc.UtcDateTime;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<LotSummary?> GetLotSummaryAsync(int lotId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // One SQL query: the counts are computed by the database, not by loading every row.
        return await db.Lots
            .AsNoTracking()
            .Where(lot => lot.Id == lotId)
            .Select(lot => new LotSummary(
                lot.LotNumber,
                lot.Recipe!.Name,
                lot.RecipeVersion,
                lot.Quantity,
                lot.Results.Count,
                lot.Results.Count(result => result.FinalVerdict == Verdict.Pass)))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
