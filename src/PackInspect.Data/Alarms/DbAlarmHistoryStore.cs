using Microsoft.EntityFrameworkCore;
using PackInspect.Core.Alarms;
using PackInspect.Data.Entities;

namespace PackInspect.Data.Alarms;

public sealed class DbAlarmHistoryStore(IDbContextFactory<PackInspectDbContext> contextFactory) : IAlarmHistoryStore
{
    public async Task<int> RecordRaisedAsync(string code, string message, DateTimeOffset raisedAtUtc, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var row = new AlarmHistoryEntity
        {
            Code = code,
            Message = message,
            RaisedAtUtc = raisedAtUtc.UtcDateTime,
            ClearedAtUtc = null,
        };

        db.AlarmHistory.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.Id;
    }

    public async Task RecordClearedAsync(string code, DateTimeOffset clearedAtUtc, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var activeAlarms = await db.AlarmHistory
            .Where(entry => entry.Code == code && entry.ClearedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var alarm in activeAlarms)
        {
            alarm.ClearedAtUtc = clearedAtUtc.UtcDateTime;
        }

        if (activeAlarms.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<AlarmHistoryRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var rows = await db.AlarmHistory
            .AsNoTracking()
            .OrderByDescending(entry => entry.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(row => new AlarmHistoryRecord(
            row.Id,
            row.Code,
            row.Message,
            new DateTimeOffset(row.RaisedAtUtc, TimeSpan.Zero),
            row.ClearedAtUtc.HasValue ? new DateTimeOffset(row.ClearedAtUtc.Value, TimeSpan.Zero) : null));
    }
}
