namespace PackInspect.Core.Auth;

/// <summary>
/// Operational roles for station access control:
/// Operator: runs lots, reviews verdicts.
/// Engineer: edits recipes with sign-off.
/// Admin: manages users and inspects audit trails.
/// </summary>
public enum UserRole
{
    Operator,
    Engineer,
    Admin,
}
