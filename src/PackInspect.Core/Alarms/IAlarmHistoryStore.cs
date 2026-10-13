namespace PackInspect.Core.Alarms;

public interface IAlarmHistoryStore
{
    Task<int> RecordRaisedAsync(string code, string message, DateTimeOffset raisedAtUtc, CancellationToken cancellationToken);

    Task RecordClearedAsync(string code, DateTimeOffset clearedAtUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<AlarmHistoryRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken);
}
