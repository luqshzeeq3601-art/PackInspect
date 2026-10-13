namespace PackInspect.Core.Alarms;

/// <summary>Alarm codes. The list and its meaning are documented in docs/Data.md.</summary>
public static class AlarmCodes
{
    public const string DoorOpen = "DOOR_OPEN";
    public const string AirPressureLow = "AIR_PRESSURE_LOW";
    public const string PlcCommsLost = "PLC_COMMS_LOST";
    public const string AxisHomeTimeout = "AXIS_HOME_TIMEOUT";
    public const string AxisMoveTimeout = "AXIS_MOVE_TIMEOUT";
    public const string AxisFault = "AXIS_FAULT";
    public const string HandlerFault = "HANDLER_FAULT";
    public const string CameraNoResponse = "CAMERA_NO_RESPONSE";
    public const string UnitNotFound = "UNIT_NOT_FOUND";
    public const string InstrumentNoResponse = "INSTRUMENT_NO_RESPONSE";
    public const string BarcodeReaderFault = "BARCODE_READER_FAULT";
    public const string ResultNotSaved = "RESULT_NOT_SAVED";
    public const string CycleFault = "CYCLE_FAULT";
}
