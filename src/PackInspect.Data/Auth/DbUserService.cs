using Microsoft.EntityFrameworkCore;
using PackInspect.Core.Auth;
using PackInspect.Data.Entities;

namespace PackInspect.Data.Auth;

/// <summary>
/// User management and authentication against SQLite via EF Core.
/// Enforces lockout policies, constant-time password verification,
/// and comprehensive audit logging for all authentication and user modifications.
/// </summary>
public sealed class DbUserService(IDbContextFactory<PackInspectDbContext> contextFactory, TimeProvider time) : IUserService
{
    private const int MaxFailedLoginsBeforeLockout = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AuthResult> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return new AuthResult(AuthStatus.InvalidCredentials, null, "Username and password are required.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string normalizedName = userName.Trim();
        var user = await db.Users
            .SingleOrDefaultAsync(u => u.UserName.ToLower() == normalizedName.ToLower(), cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return new AuthResult(AuthStatus.InvalidCredentials, null, "Invalid username or password.");
        }

        if (!user.IsActive)
        {
            return new AuthResult(AuthStatus.Deactivated, null, "This user account has been deactivated. Contact an administrator.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (user.LockedUntilUtc.HasValue)
        {
            if (user.LockedUntilUtc.Value > now)
            {
                var remaining = user.LockedUntilUtc.Value - now;
                return new AuthResult(
                    AuthStatus.LockedOut,
                    null,
                    $"Account is locked due to repeated failed logins. Try again in {Math.Ceiling(remaining.TotalMinutes)} minute(s).");
            }

            // Lockout has elapsed: reset counter
            user.LockedUntilUtc = null;
            user.FailedLogins = 0;
        }

        bool isValid = PasswordSecurity.VerifyPassword(password, user.PasswordHash, user.PasswordSalt, user.Iterations);
        if (!isValid)
        {
            user.FailedLogins++;
            bool newlyLocked = false;
            if (user.FailedLogins >= MaxFailedLoginsBeforeLockout)
            {
                user.LockedUntilUtc = now.Add(LockoutDuration);
                newlyLocked = true;
            }

            db.AuditEntries.Add(new AuditEntryEntity
            {
                UserId = user.Id,
                TimestampUtc = now,
                Action = "LoginFailed",
                Entity = "User",
                EntityId = user.Id,
                Field = "Password",
                Reason = newlyLocked ? "Exceeded maximum failed attempts; account locked" : $"Failed attempt {user.FailedLogins}/{MaxFailedLoginsBeforeLockout}",
            });

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (newlyLocked)
            {
                return new AuthResult(
                    AuthStatus.LockedOut,
                    null,
                    $"Account is now locked for {LockoutDuration.TotalMinutes:0} minutes after {MaxFailedLoginsBeforeLockout} failed attempts.");
            }

            int attemptsLeft = MaxFailedLoginsBeforeLockout - user.FailedLogins;
            return new AuthResult(
                AuthStatus.InvalidCredentials,
                null,
                $"Invalid username or password. {attemptsLeft} attempt(s) remaining before lockout.");
        }

        // Authentication succeeded: clear failed logins
        if (user.FailedLogins > 0 || user.LockedUntilUtc.HasValue)
        {
            user.FailedLogins = 0;
            user.LockedUntilUtc = null;
        }

        db.AuditEntries.Add(new AuditEntryEntity
        {
            UserId = user.Id,
            TimestampUtc = now,
            Action = "LoginSuccess",
            Entity = "User",
            EntityId = user.Id,
            Field = "Session",
            Reason = "Successful credentials verification",
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new AuthResult(AuthStatus.Success, ToDomain(user));
    }

    public async Task<bool> VerifyPasswordAsync(int userId, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FindAsync([userId], cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return false;
        }

        return PasswordSecurity.VerifyPassword(password, user.PasswordHash, user.PasswordSalt, user.Iterations);
    }

    public async Task<User> CreateUserAsync(
        string userName,
        string password,
        UserRole role,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        if (password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));
        }

        string trimmedName = userName.Trim();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (await db.Users.AnyAsync(u => u.UserName.ToLower() == trimmedName.ToLower(), cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"User '{trimmedName}' already exists.");
        }

        var (hash, salt, iterations) = PasswordSecurity.HashPassword(password);
        var userEntity = new UserEntity
        {
            UserName = trimmedName,
            PasswordHash = hash,
            PasswordSalt = salt,
            Iterations = iterations,
            Role = role,
            IsActive = true,
            FailedLogins = 0,
            LockedUntilUtc = null,
        };

        db.Users.Add(userEntity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        db.AuditEntries.Add(new AuditEntryEntity
        {
            UserId = actorUserId,
            TimestampUtc = time.GetUtcNow().UtcDateTime,
            Action = "UserCreated",
            Entity = "User",
            EntityId = userEntity.Id,
            Field = "Role",
            NewValue = role.ToString(),
            Reason = $"Created account for {trimmedName}",
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDomain(userEntity);
    }

    public async Task DeactivateUserAsync(int userId, int actorUserId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FindAsync([userId], cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User #{userId} was not found.");

        if (!user.IsActive)
        {
            return;
        }

        user.IsActive = false;
        db.AuditEntries.Add(new AuditEntryEntity
        {
            UserId = actorUserId,
            TimestampUtc = time.GetUtcNow().UtcDateTime,
            Action = "UserDeactivated",
            Entity = "User",
            EntityId = user.Id,
            Field = "IsActive",
            OldValue = "true",
            NewValue = "false",
            Reason = $"Deactivated user {user.UserName}",
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UnlockUserAsync(int userId, int actorUserId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FindAsync([userId], cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User #{userId} was not found.");

        user.FailedLogins = 0;
        user.LockedUntilUtc = null;

        db.AuditEntries.Add(new AuditEntryEntity
        {
            UserId = actorUserId,
            TimestampUtc = time.GetUtcNow().UtcDateTime,
            Action = "UserUnlocked",
            Entity = "User",
            EntityId = user.Id,
            Field = "LockedUntilUtc",
            OldValue = "locked",
            NewValue = "unlocked",
            Reason = $"Unlocked user {user.UserName} by administrator",
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<User>> GetAllUsersAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(ToDomain);
    }

    private static User ToDomain(UserEntity entity) => new(
        entity.Id,
        entity.UserName,
        entity.Role,
        entity.IsActive,
        entity.FailedLogins,
        entity.LockedUntilUtc.HasValue ? new DateTimeOffset(entity.LockedUntilUtc.Value, TimeSpan.Zero) : null);
}
