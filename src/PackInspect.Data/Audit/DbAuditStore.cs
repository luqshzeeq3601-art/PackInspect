using Microsoft.EntityFrameworkCore;
using PackInspect.Core.Audit;
using PackInspect.Data.Entities;

namespace PackInspect.Data.Audit;

/// <summary>
/// Insert-only implementation of the audit store.
/// No update or delete operations exist. Timestamps are stored in UTC.
/// </summary>
public sealed class DbAuditStore(IDbContextFactory<PackInspectDbContext> contextFactory, TimeProvider time) : IAuditStore
{
    public async Task RecordAsync(
        int userId,
        string action,
        string entity,
        int entityId,
        string field,
        string? oldValue,
        string? newValue,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        db.AuditEntries.Add(new AuditEntryEntity
        {
            UserId = userId,
            TimestampUtc = time.GetUtcNow().UtcDateTime,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var rows = await db.AuditEntries
            .AsNoTracking()
            .Include(entry => entry.User)
            .OrderByDescending(entry => entry.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(row => new AuditEntry(
            row.Id,
            new DateTimeOffset(row.TimestampUtc, TimeSpan.Zero),
            row.UserId,
            row.User?.UserName ?? $"User #{row.UserId}",
            row.Action,
            row.Entity,
            row.EntityId,
            row.Field,
            row.OldValue,
            row.NewValue,
            row.Reason));
    }
}
