namespace PackInspect.Core.Alarms;

/// <summary>
/// Thrown by the sequence when a step fails in a way the operator must deal
/// with. The controller turns it into the alarm named by <see cref="AlarmCode"/>.
/// </summary>
public sealed class MachineFaultException(string alarmCode, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string AlarmCode { get; } = alarmCode;
}
