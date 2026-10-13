using PackInspect.Core.Auth;

namespace PackInspect.Data.Entities;

/// <summary>
/// Row of the Users table. Columns documented in docs/Data.md.
/// Passwords hashed with PBKDF2; salts are per-user.
/// </summary>
public sealed class UserEntity
{
    public int Id { get; set; }

    public required string UserName { get; set; }

    public required byte[] PasswordHash { get; set; }

    public required byte[] PasswordSalt { get; set; }

    public int Iterations { get; set; }

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public int FailedLogins { get; set; }

    public DateTime? LockedUntilUtc { get; set; }
}
