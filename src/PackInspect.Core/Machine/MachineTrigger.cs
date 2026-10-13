namespace PackInspect.Core.Machine;

/// <summary>The events that can move the machine from one state to another.</summary>
public enum MachineTrigger
{
    Home,
    HomingDone,
    Start,
    Pause,
    Resume,
    Stop,
    AlarmRaised,
    Reset,
    EStopPressed,
}
