namespace PackInspect.Data.Entities;

/// <summary>
/// Row of the AuditEntries table. Insert-only; columns documented in docs/Data.md.
/// </summary>
public sealed class AuditEntryEntity
{
    public int Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public int UserId { get; set; }

    public UserEntity? User { get; set; }

    public required string Action { get; set; }

    public required string Entity { get; set; }

    public int EntityId { get; set; }

    public required string Field { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Reason { get; set; }
}
