namespace PackInspect.Core.Auth;

/// <summary>
/// Domain model for station users. Passwords, hashes and salts are deliberately
/// excluded to prevent accidental exposure in memory or logs.
/// </summary>
public sealed record User(
    int Id,
    string UserName,
    UserRole Role,
    bool IsActive,
    int FailedLogins,
    DateTimeOffset? LockedUntilUtc);
