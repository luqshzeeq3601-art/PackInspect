using Stateless;

namespace PackInspect.Core.Machine;

/// <summary>
/// The machine's transition table and nothing else. It does no I/O and starts
/// no work, which is what makes every rule here testable in isolation.
/// Not thread-safe: <see cref="MachineController"/> fires every trigger under one lock.
/// </summary>
public sealed class MachineStateMachine
{
    private readonly StateMachine<MachineState, MachineTrigger> _machine = new(MachineState.Idle);

    /// <param name="noActiveAlarms">True when every alarm condition has gone.</param>
    /// <param name="axesHomed">True while the axis position can be trusted.</param>
    /// <param name="eStopReleased">True when the emergency stop button is released.</param>
    public MachineStateMachine(Func<bool> noActiveAlarms, Func<bool> axesHomed, Func<bool> eStopReleased)
    {
        _machine.Configure(MachineState.Idle)
            .Permit(MachineTrigger.Home, MachineState.Homing);

        _machine.Configure(MachineState.Homing)
            .Permit(MachineTrigger.HomingDone, MachineState.Ready);

        _machine.Configure(MachineState.Ready)
            .Permit(MachineTrigger.Start, MachineState.Running);

        _machine.Configure(MachineState.Running)
            .Permit(MachineTrigger.Pause, MachineState.Paused)
            .Permit(MachineTrigger.Stop, MachineState.Ready);

        _machine.Configure(MachineState.Paused)
            .Permit(MachineTrigger.Resume, MachineState.Running)
            .Permit(MachineTrigger.Stop, MachineState.Ready);

        // An alarm that interrupted homing leaves the position unknown, so the
        // machine may only return to Ready if the axes are still homed.
        _machine.Configure(MachineState.Alarm)
            .PermitIf(MachineTrigger.Reset, MachineState.Ready, () => noActiveAlarms() && axesHomed())
            .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => noActiveAlarms() && !axesHomed());

        // An emergency stop removes drive power on a real machine, so homing is
        // always required again afterwards.
        _machine.Configure(MachineState.EStop)
            .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => eStopReleased() && noActiveAlarms());

        foreach (var state in Enum.GetValues<MachineState>())
        {
            if (state != MachineState.EStop)
            {
                _machine.Configure(state).Permit(MachineTrigger.EStopPressed, MachineState.EStop);
            }

            if (state is not (MachineState.Alarm or MachineState.EStop))
            {
                _machine.Configure(state).Permit(MachineTrigger.AlarmRaised, MachineState.Alarm);
            }
        }

        _machine.OnTransitioned(transition => Transitioned?.Invoke(transition.Source, transition.Destination));
    }

    /// <summary>Raised during <see cref="Fire"/> with the old and the new state.</summary>
    public event Action<MachineState, MachineState>? Transitioned;

    public MachineState State => _machine.State;

    public bool CanFire(MachineTrigger trigger) => _machine.CanFire(trigger);

    /// <summary>
    /// Applies the trigger if the current state allows it. A trigger that is not
    /// allowed is ignored and returns false, so a late button press can never
    /// throw or corrupt the state.
    /// </summary>
    public bool Fire(MachineTrigger trigger)
    {
        if (!_machine.CanFire(trigger))
        {
            return false;
        }

        _machine.Fire(trigger);
        return true;
    }
}
