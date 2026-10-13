namespace PackInspect.Core.Audit;

/// <summary>
/// Insert-only store for system audit trails.
/// No update or delete operations are provided or permitted.
/// </summary>
public interface IAuditStore
{
    Task RecordAsync(
        int userId,
        string action,
        string entity,
        int entityId,
        string field,
        string? oldValue,
        string? newValue,
        string? reason,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken);
}
