namespace PackInspect.Data.Entities;

/// <summary>
/// Row of the AlarmHistory table. Columns documented in docs/Data.md.
/// </summary>
public sealed class AlarmHistoryEntity
{
    public int Id { get; set; }

    public required string Code { get; set; }

    public required string Message { get; set; }

    public DateTime RaisedAtUtc { get; set; }

    public DateTime? ClearedAtUtc { get; set; }
}
