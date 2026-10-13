using NModbus;
using PackInspect.Simulator.Units;

namespace PackInspect.Simulator.Plc;

/// <summary>
/// Behaves like the PLC program of the station: on every scan it reads the
/// command coils, updates the axis and the handler, and writes the status the
/// app will read.
/// </summary>
internal sealed class PlcSimulator(ISlaveDataStore store, StationModel station, SimulatorFaults faults, TimeSpan homingDuration)
{
    private const double DefaultSpeedMmPerSec = 200;

    private ushort _heartbeat;
    private bool _homed;
    private bool _homing;
    private bool _moving;
    private bool _axisFault;
    private TimeSpan _homingRemaining;
    private double _positionMm;
    private double _targetMm;
    private double _speedMmPerSec = DefaultSpeedMmPerSec;

    /// <summary>One PLC scan. <paramref name="elapsed"/> is the time since the previous scan.</summary>
    public void Scan(TimeSpan elapsed)
    {
        ReadCommands();
        UpdateAxis(elapsed);
        WriteStatus();
    }

    public string DescribeAxis() =>
        _homing ? "homing" : !_homed ? "not homed" : $"{_positionMm:0.0} mm{(_moving ? " (moving)" : string.Empty)}";

    private void ReadCommands()
    {
        if (TakeCommand(ModbusMap.FaultReset))
        {
            _axisFault = false;
        }

        if (TakeCommand(ModbusMap.AxisStopCommand))
        {
            // A stopped homing run leaves the axis without a position reference.
            _homing = false;
            _moving = false;
        }

        if (TakeCommand(ModbusMap.AxisHomeCommand) && !faults.EStopPressed)
        {
            _homed = false;
            _moving = false;
            _homing = true;
            _homingRemaining = homingDuration;
        }

        if (TakeCommand(ModbusMap.AxisMoveCommand) && !faults.EStopPressed)
        {
            if (_homed)
            {
                ushort[] target = store.HoldingRegisters.ReadPoints(ModbusMap.AxisTargetHigh, 3);
                _targetMm = ((target[0] << 16) | target[1]) / 1000.0;
                _speedMmPerSec = target[2] > 0 ? target[2] : DefaultSpeedMmPerSec;
                _moving = true;
            }
            else
            {
                // A drive refuses to position without a reference.
                _axisFault = true;
            }
        }

        if (TakeCommand(ModbusMap.LoadUnitCommand))
        {
            station.LoadNextUnit();
        }

        if (TakeCommand(ModbusMap.UnloadUnitCommand))
        {
            ushort bin = store.HoldingRegisters.ReadPoints(ModbusMap.SortBin, 1)[0];
            station.UnloadUnit(sortedAsPass: bin == ModbusMap.SortBinPass);
        }
    }

    private void UpdateAxis(TimeSpan elapsed)
    {
        if (faults.EStopPressed)
        {
            // The emergency stop cuts drive power: motion stops and the reference is lost.
            _homing = false;
            _moving = false;
            _homed = false;
        }
        else if (!faults.AxisTimeout)
        {
            if (_homing)
            {
                _homingRemaining -= elapsed;
                if (_homingRemaining <= TimeSpan.Zero)
                {
                    _homing = false;
                    _homed = true;
                    _positionMm = 0;
                    _targetMm = 0;
                }
            }
            else if (_moving)
            {
                double step = _speedMmPerSec * elapsed.TotalSeconds;
                double remaining = _targetMm - _positionMm;
                if (Math.Abs(remaining) <= step)
                {
                    _positionMm = _targetMm;
                    _moving = false;
                }
                else
                {
                    _positionMm += Math.Sign(remaining) * step;
                }
            }
        }

        station.AxisPositionMm = _positionMm;
    }

    private void WriteStatus()
    {
        _heartbeat++;
        int positionUm = (int)Math.Round(_positionMm * 1000);

        store.InputRegisters.WritePoints(ModbusMap.Heartbeat, [_heartbeat]);
        store.InputRegisters.WritePoints(ModbusMap.AxisPositionHigh, [(ushort)(positionUm >> 16), (ushort)(positionUm & 0xFFFF)]);
        store.CoilInputs.WritePoints(ModbusMap.EStopOk, [!faults.EStopPressed, !faults.DoorOpen, !faults.AirPressureLow]);
        store.CoilInputs.WritePoints(ModbusMap.UnitPresent, [station.UnitPresent]);
        store.CoilInputs.WritePoints(
            ModbusMap.AxisHomed,
            [_homed, _homing || _moving, _homed && !_moving && _positionMm == _targetMm, _axisFault]);
    }

    // Reads a command coil and clears it, which is the "accepted" handshake the app waits for.
    private bool TakeCommand(ushort address)
    {
        bool requested = store.CoilDiscretes.ReadPoints(address, 1)[0];
        if (requested)
        {
            store.CoilDiscretes.WritePoints(address, [false]);
        }

        return requested;
    }
}
