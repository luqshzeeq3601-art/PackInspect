using Microsoft.EntityFrameworkCore;
using PackInspect.Core.Auth;
using PackInspect.Core.Devices;
using PackInspect.Data.Auth;
using PackInspect.Data.Entities;

namespace PackInspect.Data;

/// <summary>
/// Brings the database up to the current schema and seeds baseline users and recipes.
/// Default development accounts:
///   admin / Admin123! (Admin role)
///   engineer / Engineer123! (Engineer role)
///   operator / Operator123! (Operator role)
/// </summary>
public sealed class DatabaseInitializer(IDbContextFactory<PackInspectDbContext> contextFactory, TimeProvider time)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        var now = time.GetUtcNow().UtcDateTime;

        int adminId = 0;
        if (!await db.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            var admin = CreateUser("admin", "Admin123!", UserRole.Admin);
            var engineer = CreateUser("engineer", "Engineer123!", UserRole.Engineer);
            var op = CreateUser("operator", "Operator123!", UserRole.Operator);

            db.Users.AddRange(admin, engineer, op);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            adminId = admin.Id;

            db.AuditEntries.AddRange(
                new AuditEntryEntity
                {
                    UserId = admin.Id,
                    TimestampUtc = now,
                    Action = "UserSeeded",
                    Entity = "User",
                    EntityId = admin.Id,
                    Field = "All",
                    NewValue = "admin (Admin)",
                    Reason = "System initialization",
                },
                new AuditEntryEntity
                {
                    UserId = admin.Id,
                    TimestampUtc = now,
                    Action = "UserSeeded",
                    Entity = "User",
                    EntityId = engineer.Id,
                    Field = "All",
                    NewValue = "engineer (Engineer)",
                    Reason = "System initialization",
                },
                new AuditEntryEntity
                {
                    UserId = admin.Id,
                    TimestampUtc = now,
                    Action = "UserSeeded",
                    Entity = "User",
                    EntityId = op.Id,
                    Field = "All",
                    NewValue = "operator (Operator)",
                    Reason = "System initialization",
                });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin, cancellationToken).ConfigureAwait(false);
            adminId = admin?.Id ?? 1;
        }

        if (!await db.Recipes.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            db.Recipes.AddRange(
                CreateRecipe("QFN-5x5-100R", MeasurementKind.Resistance, lowLimit: 95, highLimit: 105, adminId, now),
                CreateRecipe("QFN-5x5-100R-TIGHT", MeasurementKind.Resistance, lowLimit: 99.5, highLimit: 100.5, adminId, now),
                CreateRecipe("QFN-5x5-3V3", MeasurementKind.DcVoltage, lowLimit: 3.2, highLimit: 3.4, adminId, now));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static UserEntity CreateUser(string userName, string password, UserRole role)
    {
        var (hash, salt, iterations) = PasswordSecurity.HashPassword(password);
        return new UserEntity
        {
            UserName = userName,
            PasswordHash = hash,
            PasswordSalt = salt,
            Iterations = iterations,
            Role = role,
            IsActive = true,
            FailedLogins = 0,
            LockedUntilUtc = null,
        };
    }

    private static RecipeEntity CreateRecipe(string name, MeasurementKind kind, double lowLimit, double highLimit, int adminUserId, DateTime now) => new()
    {
        Name = name,
        Version = 1,
        CameraPositionMm = 100,
        TestPositionMm = 200,
        MinBodyRectangularity = 0.965,
        ExpectedLeadCount = 24,
        MinMarkingPixels = 900,
        MaxMarkingPixels = 1800,
        MaxDefectAreaPx = 15,
        MeasurementKind = kind,
        LowLimit = lowLimit,
        HighLimit = highLimit,
        UpdatedAtUtc = now,
        UpdatedByUserId = adminUserId,
    };
}
