using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PackInspect.Data;

/// <summary>Used only by "dotnet ef" when it creates a migration; the app never calls it.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PackInspectDbContext>
{
    public PackInspectDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PackInspectDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
