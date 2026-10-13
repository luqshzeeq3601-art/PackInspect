using Microsoft.EntityFrameworkCore;
using PackInspect.Data.Entities;

namespace PackInspect.Data;

public sealed class PackInspectDbContext(DbContextOptions<PackInspectDbContext> options) : DbContext(options)
{
    public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();

    public DbSet<LotEntity> Lots => Set<LotEntity>();

    public DbSet<UnitResultEntity> UnitResults => Set<UnitResultEntity>();

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();

    public DbSet<AlarmHistoryEntity> AlarmHistory => Set<AlarmHistoryEntity>();

    public DbSet<AiReviewEntity> AiReviews => Set<AiReviewEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Enums are stored as text so the database can be read without the code.
        modelBuilder.Entity<UserEntity>(user =>
        {
            user.HasIndex(u => u.UserName).IsUnique();
            user.Property(u => u.UserName).HasMaxLength(64);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<AuditEntryEntity>(audit =>
        {
            audit.Property(a => a.Action).HasMaxLength(64);
            audit.Property(a => a.Entity).HasMaxLength(64);
            audit.Property(a => a.Field).HasMaxLength(64);
            audit.Property(a => a.OldValue).HasMaxLength(256);
            audit.Property(a => a.NewValue).HasMaxLength(256);
            audit.Property(a => a.Reason).HasMaxLength(512);

            // Users are deactivated, never deleted; audit entries keep their actor.
            audit.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AlarmHistoryEntity>(alarm =>
        {
            alarm.Property(a => a.Code).HasMaxLength(32);
            alarm.Property(a => a.Message).HasMaxLength(256);
        });

        modelBuilder.Entity<RecipeEntity>(recipe =>
        {
            recipe.HasIndex(r => r.Name).IsUnique();
            recipe.Property(r => r.Name).HasMaxLength(64);
            recipe.Property(r => r.MeasurementKind).HasConversion<string>().HasMaxLength(16);
            recipe.HasOne(r => r.UpdatedByUser).WithMany().HasForeignKey(r => r.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LotEntity>(lot =>
        {
            lot.HasIndex(l => l.LotNumber).IsUnique();
            lot.Property(l => l.LotNumber).HasMaxLength(32);

            // A recipe that lots were run with must not be deleted from under them.
            lot.HasOne(l => l.Recipe).WithMany().HasForeignKey(l => l.RecipeId).OnDelete(DeleteBehavior.Restrict);
            lot.HasOne(l => l.OperatorUser).WithMany().HasForeignKey(l => l.OperatorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UnitResultEntity>(result =>
        {
            result.HasIndex(r => new { r.LotId, r.Sequence }).IsUnique();
            result.HasOne(r => r.Lot).WithMany(l => l.Results).HasForeignKey(r => r.LotId);
            result.Property(r => r.Barcode).HasMaxLength(64);
            result.Property(r => r.ImagePath).HasMaxLength(260);
            result.Property(r => r.VisionVerdict).HasConversion<string>().HasMaxLength(8);
            result.Property(r => r.ElectricalVerdict).HasConversion<string>().HasMaxLength(8);
            result.Property(r => r.FinalVerdict).HasConversion<string>().HasMaxLength(8);
            result.Property(r => r.DefectType).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<AiReviewEntity>(review =>
        {
            review.HasIndex(r => r.UnitResultId).IsUnique();
            review.HasOne(r => r.UnitResult).WithOne(u => u.AiReview).HasForeignKey<AiReviewEntity>(r => r.UnitResultId).OnDelete(DeleteBehavior.Cascade);
            review.HasOne(r => r.ReviewedByUser).WithMany().HasForeignKey(r => r.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
            review.Property(r => r.AiLabel).HasConversion<string>().HasMaxLength(32);
            review.Property(r => r.OperatorLabel).HasConversion<string>().HasMaxLength(32);
            review.Property(r => r.OperatorVerdict).HasMaxLength(32);
            review.Property(r => r.ModelVersion).HasMaxLength(64);
        });
    }
}
