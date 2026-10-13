using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PackInspect.Core.Ai;
using PackInspect.Core.Alarms;
using PackInspect.Core.Audit;
using PackInspect.Core.Auth;
using PackInspect.Core.Domain;
using PackInspect.Data.Alarms;
using PackInspect.Data.Audit;
using PackInspect.Data.Auth;

namespace PackInspect.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers the SQLite database and the stores that Core uses through its interfaces.</summary>
    public static IServiceCollection AddPackInspectData(this IServiceCollection services, string databasePath)
    {
        // A factory rather than one shared context: each operation gets its own
        // short-lived context, which is safe to use from background threads.
        services.AddDbContextFactory<PackInspectDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
        services.AddSingleton<IRecipeStore, DbRecipeStore>();
        services.AddSingleton<IResultStore, DbResultStore>();
        services.AddSingleton<IUserService, DbUserService>();
        services.AddSingleton<IAuditStore, DbAuditStore>();
        services.AddSingleton<IAlarmHistoryStore, DbAlarmHistoryStore>();
        services.AddSingleton<IAiReviewStore, DbAiReviewStore>();
        services.AddSingleton<DatabaseInitializer>();
        return services;
    }
}
