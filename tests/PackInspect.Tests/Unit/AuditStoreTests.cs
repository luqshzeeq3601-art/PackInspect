using Microsoft.Extensions.DependencyInjection;
using PackInspect.Core.Audit;
using PackInspect.Data;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

public sealed class AuditStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly ServiceProvider _services;

    public AuditStoreTests()
    {
        _services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddPackInspectData(_folder.File("audit_test.db"))
            .BuildServiceProvider();
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _services.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task Audit_store_records_and_retrieves_entries_ordered_newest_first()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var auditStore = _services.GetRequiredService<IAuditStore>();

        await auditStore.RecordAsync(
            userId: 1,
            action: "ConfigChanged",
            entity: "System",
            entityId: 0,
            field: "LogLevel",
            oldValue: "Debug",
            newValue: "Info",
            reason: "Standard production policy",
            TestToken);

        var entries = await auditStore.GetRecentAsync(50, TestToken);
        var first = Assert.Single(entries, e => e.Action == "ConfigChanged");

        Assert.Equal("System", first.Entity);
        Assert.Equal("LogLevel", first.Field);
        Assert.Equal("Debug", first.OldValue);
        Assert.Equal("Info", first.NewValue);
        Assert.Equal("Standard production policy", first.Reason);
        Assert.Equal(TimeSpan.Zero, first.TimestampUtc.Offset);
    }

    [Fact]
    public void Audit_store_interface_provides_no_mutating_or_deleting_seams()
    {
        var methods = typeof(IAuditStore).GetMethods();

        Assert.DoesNotContain(methods, m => m.Name.StartsWith("Update", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Name.StartsWith("Remove", StringComparison.OrdinalIgnoreCase));
    }
}
