namespace PackInspect.Simulator.Plc;

/// <summary>
/// The simulated PLC's address map, as documented in docs/Data.md.
/// Zero-based protocol addresses.
/// </summary>
internal static class ModbusMap
{
    public const byte UnitId = 1;

    // Discrete inputs (PLC -> app). 1 = healthy.
    public const ushort EStopOk = 0;
    public const ushort DoorClosed = 1;
    public const ushort AirPressureOk = 2;
    public const ushort UnitPresent = 10;

    // Input registers (PLC -> app).
    public const ushort Heartbeat = 0;

    // Coils (app -> PLC). The PLC clears each command when it has acted on it.
    public const ushort FaultReset = 0;
    public const ushort LoadUnitCommand = 10;
    public const ushort UnloadUnitCommand = 11;
    public const ushort AxisHomeCommand = 100;
    public const ushort AxisMoveCommand = 101;
    public const ushort AxisStopCommand = 102;

    // Holding registers (app -> PLC).
    public const ushort SortBin = 10;
    public const ushort AxisTargetHigh = 100;
    public const ushort AxisTargetLow = 101;
    public const ushort AxisSpeed = 102;

    // Axis X status, discrete inputs (PLC -> app).
    public const ushort AxisHomed = 100;
    public const ushort AxisBusy = 101;
    public const ushort AxisInPosition = 102;
    public const ushort AxisFault = 103;

    // Axis X actual position in micrometres, 32-bit, high word first (PLC -> app).
    public const ushort AxisPositionHigh = 100;
    public const ushort AxisPositionLow = 101;

    public const ushort SortBinPass = 1;
}
