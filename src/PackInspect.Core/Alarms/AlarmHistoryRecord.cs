namespace PackInspect.Core.Alarms;

public sealed record AlarmHistoryRecord(
    int Id,
    string Code,
    string Message,
    DateTimeOffset RaisedAtUtc,
    DateTimeOffset? ClearedAtUtc);
