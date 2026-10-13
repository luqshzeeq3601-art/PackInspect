namespace PackInspect.Core.Audit;

/// <summary>
/// Domain model for an immutable audit record.
/// Never contains passwords, salts or cryptographic hashes.
/// </summary>
public sealed record AuditEntry(
    int Id,
    DateTimeOffset TimestampUtc,
    int UserId,
    string UserName,
    string Action,
    string Entity,
    int EntityId,
    string Field,
    string? OldValue,
    string? NewValue,
    string? Reason);
