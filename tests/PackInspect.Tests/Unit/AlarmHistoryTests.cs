using Microsoft.Extensions.DependencyInjection;
using PackInspect.Core.Alarms;
using PackInspect.Core.Machine;
using PackInspect.Data;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

public sealed class AlarmHistoryTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly ServiceProvider _services;

    public AlarmHistoryTests()
    {
        _services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddPackInspectData(_folder.File("alarm_history.db"))
            .BuildServiceProvider();
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _services.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task Raising_and_clearing_alarm_records_lifecycle_in_history_store()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var alarmStore = _services.GetRequiredService<IAlarmHistoryStore>();

        var raisedTime = DateTimeOffset.UtcNow;
        int id = await alarmStore.RecordRaisedAsync(AlarmCodes.DoorOpen, "Guard door opened while running", raisedTime, TestToken);
        Assert.True(id > 0);

        var recent = await alarmStore.GetRecentAsync(10, TestToken);
        var record = Assert.Single(recent, r => r.Code == AlarmCodes.DoorOpen);
        Assert.Null(record.ClearedAtUtc);

        var clearedTime = raisedTime.AddSeconds(5);
        await alarmStore.RecordClearedAsync(AlarmCodes.DoorOpen, clearedTime, TestToken);

        var updatedRecent = await alarmStore.GetRecentAsync(10, TestToken);
        var updatedRecord = Assert.Single(updatedRecent, r => r.Code == AlarmCodes.DoorOpen);
        Assert.NotNull(updatedRecord.ClearedAtUtc);
    }
}
