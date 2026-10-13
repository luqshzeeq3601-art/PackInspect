using PackInspect.Core.Machine;

namespace PackInspect.Tests.Unit;

public class MachineStateMachineTests
{
    private bool _noActiveAlarms = true;
    private bool _axesHomed;
    private bool _eStopReleased = true;

    [Theory]
    [InlineData(MachineState.Idle, MachineTrigger.Home, MachineState.Homing)]
    [InlineData(MachineState.Homing, MachineTrigger.HomingDone, MachineState.Ready)]
    [InlineData(MachineState.Ready, MachineTrigger.Start, MachineState.Running)]
    [InlineData(MachineState.Running, MachineTrigger.Pause, MachineState.Paused)]
    [InlineData(MachineState.Running, MachineTrigger.Stop, MachineState.Ready)]
    [InlineData(MachineState.Paused, MachineTrigger.Resume, MachineState.Running)]
    [InlineData(MachineState.Paused, MachineTrigger.Stop, MachineState.Ready)]
    [InlineData(MachineState.Idle, MachineTrigger.AlarmRaised, MachineState.Alarm)]
    [InlineData(MachineState.Homing, MachineTrigger.AlarmRaised, MachineState.Alarm)]
    [InlineData(MachineState.Ready, MachineTrigger.AlarmRaised, MachineState.Alarm)]
    [InlineData(MachineState.Running, MachineTrigger.AlarmRaised, MachineState.Alarm)]
    [InlineData(MachineState.Paused, MachineTrigger.AlarmRaised, MachineState.Alarm)]
    [InlineData(MachineState.Idle, MachineTrigger.EStopPressed, MachineState.EStop)]
    [InlineData(MachineState.Homing, MachineTrigger.EStopPressed, MachineState.EStop)]
    [InlineData(MachineState.Ready, MachineTrigger.EStopPressed, MachineState.EStop)]
    [InlineData(MachineState.Running, MachineTrigger.EStopPressed, MachineState.EStop)]
    [InlineData(MachineState.Paused, MachineTrigger.EStopPressed, MachineState.EStop)]
    [InlineData(MachineState.Alarm, MachineTrigger.EStopPressed, MachineState.EStop)]
    public void Allowed_trigger_moves_to_the_expected_state(MachineState from, MachineTrigger trigger, MachineState expected)
    {
        var machine = CreateIn(from);

        bool fired = machine.Fire(trigger);

        Assert.True(fired);
        Assert.Equal(expected, machine.State);
    }

    [Theory]
    [InlineData(MachineState.Idle, MachineTrigger.Start)]
    [InlineData(MachineState.Idle, MachineTrigger.Reset)]
    [InlineData(MachineState.Idle, MachineTrigger.HomingDone)]
    [InlineData(MachineState.Homing, MachineTrigger.Start)]
    [InlineData(MachineState.Homing, MachineTrigger.Home)]
    [InlineData(MachineState.Ready, MachineTrigger.Home)]
    [InlineData(MachineState.Ready, MachineTrigger.Pause)]
    [InlineData(MachineState.Running, MachineTrigger.Start)]
    [InlineData(MachineState.Running, MachineTrigger.Home)]
    [InlineData(MachineState.Running, MachineTrigger.Reset)]
    [InlineData(MachineState.Paused, MachineTrigger.Pause)]
    [InlineData(MachineState.Paused, MachineTrigger.Start)]
    [InlineData(MachineState.Alarm, MachineTrigger.Start)]
    [InlineData(MachineState.Alarm, MachineTrigger.Home)]
    [InlineData(MachineState.Alarm, MachineTrigger.AlarmRaised)]
    [InlineData(MachineState.EStop, MachineTrigger.Start)]
    [InlineData(MachineState.EStop, MachineTrigger.Home)]
    [InlineData(MachineState.EStop, MachineTrigger.AlarmRaised)]
    [InlineData(MachineState.EStop, MachineTrigger.EStopPressed)]
    public void Trigger_that_is_not_allowed_is_ignored(MachineState from, MachineTrigger trigger)
    {
        var machine = CreateIn(from);

        bool fired = machine.Fire(trigger);

        Assert.False(fired);
        Assert.False(machine.CanFire(trigger));
        Assert.Equal(from, machine.State);
    }

    [Fact]
    public void Reset_from_alarm_is_refused_while_an_alarm_is_still_active()
    {
        var machine = CreateIn(MachineState.Alarm);
        _noActiveAlarms = false;

        Assert.False(machine.Fire(MachineTrigger.Reset));
        Assert.Equal(MachineState.Alarm, machine.State);
    }

    [Fact]
    public void Reset_from_alarm_returns_to_ready_when_axes_are_still_homed()
    {
        var machine = CreateIn(MachineState.Alarm);
        _axesHomed = true;

        Assert.True(machine.Fire(MachineTrigger.Reset));
        Assert.Equal(MachineState.Ready, machine.State);
    }

    [Fact]
    public void Reset_from_alarm_returns_to_idle_when_axes_are_not_homed()
    {
        var machine = CreateIn(MachineState.Alarm);
        _axesHomed = false;

        Assert.True(machine.Fire(MachineTrigger.Reset));
        Assert.Equal(MachineState.Idle, machine.State);
    }

    [Fact]
    public void Reset_from_estop_is_refused_while_the_button_is_still_pressed()
    {
        var machine = CreateIn(MachineState.EStop);
        _eStopReleased = false;

        Assert.False(machine.Fire(MachineTrigger.Reset));
        Assert.Equal(MachineState.EStop, machine.State);
    }

    [Fact]
    public void Reset_from_estop_always_returns_to_idle_so_homing_is_repeated()
    {
        var machine = CreateIn(MachineState.EStop);
        _axesHomed = true;

        Assert.True(machine.Fire(MachineTrigger.Reset));
        Assert.Equal(MachineState.Idle, machine.State);
    }

    [Fact]
    public void Transition_is_reported_with_old_and_new_state()
    {
        var machine = CreateIn(MachineState.Idle);
        (MachineState From, MachineState To)? reported = null;
        machine.Transitioned += (from, to) => reported = (from, to);

        machine.Fire(MachineTrigger.Home);

        Assert.Equal((MachineState.Idle, MachineState.Homing), reported);
    }

    private MachineStateMachine CreateIn(MachineState state)
    {
        var machine = new MachineStateMachine(() => _noActiveAlarms, () => _axesHomed, () => _eStopReleased);

        MachineTrigger[] path = state switch
        {
            MachineState.Idle => [],
            MachineState.Homing => [MachineTrigger.Home],
            MachineState.Ready => [MachineTrigger.Home, MachineTrigger.HomingDone],
            MachineState.Running => [MachineTrigger.Home, MachineTrigger.HomingDone, MachineTrigger.Start],
            MachineState.Paused => [MachineTrigger.Home, MachineTrigger.HomingDone, MachineTrigger.Start, MachineTrigger.Pause],
            MachineState.Alarm => [MachineTrigger.AlarmRaised],
            MachineState.EStop => [MachineTrigger.EStopPressed],
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

        foreach (var trigger in path)
        {
            Assert.True(machine.Fire(trigger));
        }

        Assert.Equal(state, machine.State);
        return machine;
    }
}
