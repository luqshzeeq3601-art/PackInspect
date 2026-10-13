using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PackInspect.Core.Domain;
using PackInspect.Data.Entities;

namespace PackInspect.Data;

public sealed class DbRecipeStore(IDbContextFactory<PackInspectDbContext> contextFactory, TimeProvider time) : IRecipeStore
{
    public async Task<IReadOnlyList<Recipe>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.Recipes.AsNoTracking().OrderBy(recipe => recipe.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(ToDomain);
    }

    public async Task<Recipe?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.Recipes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        return row is null ? null : ToDomain(row);
    }

    public async Task<Recipe> SaveWithSignOffAsync(Recipe recipe, int engineerUserId, string reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.Recipes.FindAsync([recipe.Id], cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Recipe #{recipe.Id} does not exist.");

        var now = time.GetUtcNow().UtcDateTime;

        // Record audit entries for every field that was changed
        AuditFieldChanges(db, entity, recipe, engineerUserId, reason, now);

        entity.Name = recipe.Name;
        entity.CameraPositionMm = recipe.CameraPositionMm;
        entity.TestPositionMm = recipe.TestPositionMm;
        entity.MinBodyRectangularity = recipe.Vision.MinBodyRectangularity;
        entity.ExpectedLeadCount = recipe.Vision.ExpectedLeadCount;
        entity.MinMarkingPixels = recipe.Vision.MinMarkingPixels;
        entity.MaxMarkingPixels = recipe.Vision.MaxMarkingPixels;
        entity.MaxDefectAreaPx = recipe.Vision.MaxDefectAreaPx;
        entity.MeasurementKind = recipe.MeasurementKind;
        entity.LowLimit = recipe.Limits.Low;
        entity.HighLimit = recipe.Limits.High;
        entity.Version++;
        entity.UpdatedAtUtc = now;
        entity.UpdatedByUserId = engineerUserId;

        // Single transaction: Recipe update and AuditEntry records commit together.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDomain(entity);
    }

    public async Task<Recipe> CreateWithSignOffAsync(Recipe recipe, int engineerUserId, string reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (await db.Recipes.AnyAsync(r => r.Name == recipe.Name, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"A recipe named '{recipe.Name}' already exists.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var entity = new RecipeEntity
        {
            Name = recipe.Name,
            Version = 1,
            CameraPositionMm = recipe.CameraPositionMm,
            TestPositionMm = recipe.TestPositionMm,
            MinBodyRectangularity = recipe.Vision.MinBodyRectangularity,
            ExpectedLeadCount = recipe.Vision.ExpectedLeadCount,
            MinMarkingPixels = recipe.Vision.MinMarkingPixels,
            MaxMarkingPixels = recipe.Vision.MaxMarkingPixels,
            MaxDefectAreaPx = recipe.Vision.MaxDefectAreaPx,
            MeasurementKind = recipe.MeasurementKind,
            LowLimit = recipe.Limits.Low,
            HighLimit = recipe.Limits.High,
            UpdatedAtUtc = now,
            UpdatedByUserId = engineerUserId,
        };

        db.Recipes.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        db.AuditEntries.Add(new AuditEntryEntity
        {
            UserId = engineerUserId,
            TimestampUtc = now,
            Action = "RecipeCreated",
            Entity = "Recipe",
            EntityId = entity.Id,
            Field = "All",
            NewValue = entity.Name,
            Reason = reason,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDomain(entity);
    }

    private static void AuditFieldChanges(
        PackInspectDbContext db,
        RecipeEntity oldEntity,
        Recipe newRecipe,
        int engineerUserId,
        string reason,
        DateTime now)
    {
        void Check<T>(string field, T oldVal, T newVal) where T : notnull
        {
            if (!EqualityComparer<T>.Default.Equals(oldVal, newVal))
            {
                db.AuditEntries.Add(new AuditEntryEntity
                {
                    UserId = engineerUserId,
                    TimestampUtc = now,
                    Action = "RecipeChanged",
                    Entity = "Recipe",
                    EntityId = oldEntity.Id,
                    Field = field,
                    OldValue = Convert.ToString(oldVal, CultureInfo.InvariantCulture),
                    NewValue = Convert.ToString(newVal, CultureInfo.InvariantCulture),
                    Reason = reason,
                });
            }
        }

        Check(nameof(Recipe.Name), oldEntity.Name, newRecipe.Name);
        Check(nameof(Recipe.CameraPositionMm), oldEntity.CameraPositionMm, newRecipe.CameraPositionMm);
        Check(nameof(Recipe.TestPositionMm), oldEntity.TestPositionMm, newRecipe.TestPositionMm);
        Check(nameof(Recipe.Vision.MinBodyRectangularity), oldEntity.MinBodyRectangularity, newRecipe.Vision.MinBodyRectangularity);
        Check(nameof(Recipe.Vision.ExpectedLeadCount), oldEntity.ExpectedLeadCount, newRecipe.Vision.ExpectedLeadCount);
        Check(nameof(Recipe.Vision.MinMarkingPixels), oldEntity.MinMarkingPixels, newRecipe.Vision.MinMarkingPixels);
        Check(nameof(Recipe.Vision.MaxMarkingPixels), oldEntity.MaxMarkingPixels, newRecipe.Vision.MaxMarkingPixels);
        Check(nameof(Recipe.Vision.MaxDefectAreaPx), oldEntity.MaxDefectAreaPx, newRecipe.Vision.MaxDefectAreaPx);
        Check(nameof(Recipe.MeasurementKind), oldEntity.MeasurementKind.ToString(), newRecipe.MeasurementKind.ToString());
        Check(nameof(RecipeEntity.LowLimit), oldEntity.LowLimit, newRecipe.Limits.Low);
        Check(nameof(RecipeEntity.HighLimit), oldEntity.HighLimit, newRecipe.Limits.High);
    }

    private static Recipe ToDomain(RecipeEntity row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Version = row.Version,
        CameraPositionMm = row.CameraPositionMm,
        TestPositionMm = row.TestPositionMm,
        Vision = new VisionSettings(
            row.MinBodyRectangularity,
            row.ExpectedLeadCount,
            row.MinMarkingPixels,
            row.MaxMarkingPixels,
            row.MaxDefectAreaPx),
        MeasurementKind = row.MeasurementKind,
        Limits = new ElectricalLimits(row.LowLimit, row.HighLimit),
        UpdatedByUserId = row.UpdatedByUserId,
        UpdatedAtUtc = new DateTimeOffset(row.UpdatedAtUtc, TimeSpan.Zero),
    };
}
