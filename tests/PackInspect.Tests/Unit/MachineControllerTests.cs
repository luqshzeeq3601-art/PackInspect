using Microsoft.Extensions.Logging.Abstractions;
using PackInspect.Core.Alarms;
using PackInspect.Core.Machine;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

/// <summary>The machine logic against fake devices: no sockets, no simulator.</summary>
public class MachineControllerTests
{
    private readonly FakePlc _plc = new();
    private readonly FakeMotionAxis _axis = new();
    private readonly GatedCycle _cycle = new();

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Home_moves_through_homing_to_ready()
    {
        await using var controller = CreateController();

        Assert.True(controller.Home());

        await MachineWait.ForStateAsync(controller, MachineState.Ready);
    }

    [Fact]
    public async Task Start_runs_cycles_until_stop()
    {
        await using var controller = await CreateReadyControllerAsync();

        controller.Start();
        _cycle.ReleaseOne();
        _cycle.ReleaseOne();
        await MachineWait.UntilAsync(controller, () => controller.CompletedCycles == 2, "two completed cycles");
        Assert.Equal(MachineState.Running, controller.State);

        controller.Stop();

        Assert.Equal(MachineState.Ready, controller.State);
    }

    [Fact]
    public async Task Pause_lets_the_current_cycle_finish_and_resume_continues()
    {
        await using var controller = await CreateReadyControllerAsync();
        controller.Start();
        await _cycle.WaitForFirstStartAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        controller.Pause();
        _cycle.ReleaseOne();
        await MachineWait.UntilAsync(controller, () => controller.CompletedCycles == 1, "first cycle completed");

        Assert.Equal(MachineState.Paused, controller.State);
        Assert.Equal(1, _cycle.Started);

        controller.Resume();
        _cycle.ReleaseOne();
        await MachineWait.UntilAsync(controller, () => controller.CompletedCycles == 2, "second cycle completed");
        Assert.Equal(MachineState.Running, controller.State);
    }

    [Fact]
    public async Task Homing_timeout_raises_alarm_stops_the_axis_and_resets_to_idle()
    {
        _axis.NeverFinishHoming = true;
        await using var controller = CreateController(homingTimeout: TimeSpan.FromMilliseconds(50));

        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);

        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.AxisHomeTimeout);
        Assert.True(_axis.StopCount > 0);

        await controller.ResetAsync(TestToken);

        // Homing did not finish, so the position is unknown and homing is required again.
        Assert.Equal(MachineState.Idle, controller.State);
        Assert.Empty(controller.ActiveAlarms);
    }

    [Fact]
    public async Task Door_open_while_running_raises_alarm()
    {
        await using var controller = await CreateReadyControllerAsync();
        controller.Start();

        _plc.DoorClosed = false;
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);

        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.DoorOpen);
        Assert.True(_axis.StopCount > 0);
    }

    [Fact]
    public async Task Reset_is_refused_while_the_door_is_still_open()
    {
        await using var controller = await CreateReadyControllerAsync();
        _plc.DoorClosed = false;
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);

        await controller.ResetAsync(TestToken);

        Assert.Equal(MachineState.Alarm, controller.State);
        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.DoorOpen);
    }

    [Fact]
    public async Task Reset_after_the_door_is_closed_returns_to_ready_because_axes_are_still_homed()
    {
        await using var controller = await CreateReadyControllerAsync();
        _plc.DoorClosed = false;
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);

        _plc.DoorClosed = true;
        await controller.ResetAsync(TestToken);

        Assert.Equal(MachineState.Ready, controller.State);
        Assert.Empty(controller.ActiveAlarms);
    }

    [Fact]
    public async Task Reset_returns_to_idle_when_the_axis_lost_its_reference_during_the_alarm()
    {
        await using var controller = await CreateReadyControllerAsync();
        _plc.DoorClosed = false;
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);

        _axis.LoseReference();
        _plc.DoorClosed = true;
        await controller.ResetAsync(TestToken);

        Assert.Equal(MachineState.Idle, controller.State);
    }

    [Fact]
    public async Task Alarm_before_homing_resets_to_idle()
    {
        await using var controller = CreateController();
        _plc.AirPressureOk = false;
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);
        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.AirPressureLow);

        _plc.AirPressureOk = true;
        await controller.ResetAsync(TestToken);

        Assert.Equal(MachineState.Idle, controller.State);
    }

    [Fact]
    public async Task EStop_stops_the_machine_and_requires_homing_again_after_reset()
    {
        await using var controller = await CreateReadyControllerAsync();
        controller.Start();

        _plc.EStopOk = false;
        await MachineWait.ForStateAsync(controller, MachineState.EStop);

        await controller.ResetAsync(TestToken);
        Assert.Equal(MachineState.EStop, controller.State);

        _plc.EStopOk = true;
        await controller.ResetAsync(TestToken);
        Assert.Equal(MachineState.Idle, controller.State);
    }

    [Fact]
    public async Task Lost_plc_communication_raises_alarm_and_clears_when_it_returns()
    {
        await using var controller = CreateController();
        await MachineWait.UntilAsync(controller, () => controller.IsPlcConnected, "PLC connected");

        _plc.FailReads = true;
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);

        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.PlcCommsLost);
        Assert.False(controller.IsPlcConnected);

        _plc.FailReads = false;
        await controller.ResetAsync(TestToken);

        Assert.Equal(MachineState.Idle, controller.State);
        Assert.True(controller.IsPlcConnected);
    }

    [Fact]
    public async Task Stalled_plc_heartbeat_raises_comms_alarm()
    {
        await using var controller = CreateController(heartbeatTimeout: TimeSpan.FromMilliseconds(50));

        _plc.FreezeHeartbeat = true;
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);

        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.PlcCommsLost);
    }

    [Fact]
    public async Task Machine_stops_itself_when_there_is_no_more_work()
    {
        _cycle.Remaining = 2;
        await using var controller = await CreateReadyControllerAsync();

        controller.Start();
        _cycle.ReleaseOne();
        _cycle.ReleaseOne();

        await MachineWait.UntilAsync(
            controller,
            () => controller.CompletedCycles == 2 && controller.State == MachineState.Ready,
            "two cycles and back to Ready");
        Assert.Equal(2, _cycle.Started);
    }

    [Fact]
    public async Task Start_is_refused_when_there_is_nothing_to_run()
    {
        _cycle.Remaining = 0;
        await using var controller = await CreateReadyControllerAsync();

        Assert.False(controller.CanFire(MachineTrigger.Start));
        Assert.False(controller.Start());
        Assert.Equal(MachineState.Ready, controller.State);
    }

    [Fact]
    public async Task Fault_thrown_by_the_cycle_becomes_the_alarm_it_names()
    {
        _cycle.Failure = new MachineFaultException(AlarmCodes.InstrumentNoResponse, "The instrument did not answer.");
        await using var controller = await CreateReadyControllerAsync();

        controller.Start();
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);

        var alarm = Assert.Single(controller.ActiveAlarms);
        Assert.Equal(AlarmCodes.InstrumentNoResponse, alarm.Code);
        Assert.Equal("The instrument did not answer.", alarm.Message);
    }

    private MachineController CreateController(TimeSpan? homingTimeout = null, TimeSpan? heartbeatTimeout = null)
    {
        var options = new MachineOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(10),
            HomingTimeout = homingTimeout ?? TimeSpan.FromSeconds(10),
            HeartbeatTimeout = heartbeatTimeout ?? TimeSpan.FromSeconds(10),
        };

        var controller = new MachineController(
            _plc, _axis, _cycle, options, TimeProvider.System, NullLogger<MachineController>.Instance);
        controller.StartMonitoring();
        return controller;
    }

    private async Task<MachineController> CreateReadyControllerAsync()
    {
        var controller = CreateController();
        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);
        return controller;
    }
}
