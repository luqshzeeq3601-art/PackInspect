namespace PackInspect.Devices.Modbus;

/// <summary>
/// The app's copy of the PLC address map in docs/Data.md. It is deliberately
/// not shared with the simulator: against a real PLC the two sides are written
/// separately, and the integration tests are what prove they agree.
/// Addresses are zero-based protocol addresses.
/// </summary>
internal static class PlcAddressMap
{
    // Discrete inputs, read as one block starting at EStopOk.
    public const ushort EStopOk = 0;
    public const ushort DoorClosed = 1;
    public const ushort AirPressureOk = 2;
    public const ushort InterlockCount = 3;
    public const ushort UnitPresent = 10;

    // Input registers.
    public const ushort Heartbeat = 0;

    // Coils (commands) and the holding register that goes with unloading.
    public const ushort FaultReset = 0;
    public const ushort LoadUnitCommand = 10;
    public const ushort UnloadUnitCommand = 11;
    public const ushort SortBin = 10;

    // Axis X commands.
    public const ushort AxisHomeCommand = 100;
    public const ushort AxisMoveCommand = 101;
    public const ushort AxisStopCommand = 102;

    // Axis X target: position in micrometres (32-bit, high word first), then speed in mm/s.
    public const ushort AxisTarget = 100;

    // Axis X status bits, read as one block starting at AxisHomed.
    public const ushort AxisHomed = 100;
    public const ushort AxisBusy = 101;
    public const ushort AxisInPosition = 102;
    public const ushort AxisFault = 103;
    public const ushort AxisStatusCount = 4;
}
