namespace PackInspect.Core.Devices;

/// <summary>
/// One snapshot of the PLC interlock inputs.
/// Every signal is wired fail-safe: <c>true</c> means healthy, so a broken wire
/// or a lost connection reads as a fault rather than as "all good".
/// </summary>
/// <param name="EStopOk">False while the emergency stop is pressed.</param>
/// <param name="DoorClosed">False while the guard door is open.</param>
/// <param name="AirPressureOk">False while the pneumatic supply is low.</param>
/// <param name="Heartbeat">Counter the PLC increments; a value that stops changing means the PLC has stalled.</param>
public sealed record PlcInputs(bool EStopOk, bool DoorClosed, bool AirPressureOk, ushort Heartbeat);
