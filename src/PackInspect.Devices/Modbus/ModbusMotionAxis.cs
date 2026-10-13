using PackInspect.Core.Devices;

namespace PackInspect.Devices.Modbus;

/// <summary>Axis X, commanded through the PLC over Modbus TCP.</summary>
public sealed class ModbusMotionAxis(ModbusConnection connection, ModbusOptions options) : IMotionAxis
{
    // The PLC takes the target as a signed 32-bit number of micrometres.
    private const double MaxPositionMm = int.MaxValue / 1000.0;

    public string Name => "X";

    public async Task<bool> IsHomedAsync(CancellationToken cancellationToken)
    {
        bool[] status = await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
        return StatusBit(status, PlcAddressMap.AxisHomed);
    }

    public async Task HomeAsync(CancellationToken cancellationToken)
    {
        await connection.SendCommandAsync(PlcAddressMap.AxisHomeCommand, cancellationToken).ConfigureAwait(false);
        await WaitUntilAsync(PlcAddressMap.AxisHomed, "homing", cancellationToken).ConfigureAwait(false);
    }

    public async Task MoveAbsoluteAsync(double positionMm, double speedMmPerSec, CancellationToken cancellationToken)
    {
        if (!double.IsFinite(positionMm) || Math.Abs(positionMm) > MaxPositionMm)
        {
            throw new ArgumentOutOfRangeException(nameof(positionMm), positionMm, "Target position is outside the range the PLC accepts.");
        }

        int targetUm = (int)Math.Round(positionMm * 1000);
        ushort speed = (ushort)Math.Clamp(Math.Round(speedMmPerSec), 1, ushort.MaxValue);
        ushort[] target = [(ushort)(targetUm >> 16), (ushort)(targetUm & 0xFFFF), speed];

        // Target and speed go in one write, before the command, so the PLC never
        // starts a move with half of the old target.
        await connection.ExecuteAsync(
            master => master.WriteMultipleRegistersAsync(connection.UnitId, PlcAddressMap.AxisTarget, target),
            cancellationToken).ConfigureAwait(false);
        await connection.SendCommandAsync(PlcAddressMap.AxisMoveCommand, cancellationToken).ConfigureAwait(false);
        await WaitUntilAsync(PlcAddressMap.AxisInPosition, "moving", cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        connection.ExecuteAsync(
            master => master.WriteSingleCoilAsync(connection.UnitId, PlcAddressMap.AxisStopCommand, true),
            cancellationToken);

    // Polls the status bits until the wanted one is set and the axis is no longer busy.
    private async Task WaitUntilAsync(ushort doneBit, string activity, CancellationToken cancellationToken)
    {
        while (true)
        {
            bool[] status = await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
            if (StatusBit(status, PlcAddressMap.AxisFault))
            {
                throw new InvalidOperationException($"Axis {Name} reported a drive fault while {activity}.");
            }

            if (StatusBit(status, doneBit) && !StatusBit(status, PlcAddressMap.AxisBusy))
            {
                return;
            }

            await Task.Delay(options.StatusPollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    // The status block starts at AxisHomed, so a bit's index is its address minus that start.
    private static bool StatusBit(bool[] status, ushort address) => status[address - PlcAddressMap.AxisHomed];

    private async Task<bool[]> ReadStatusAsync(CancellationToken cancellationToken)
    {
        bool[] status = await connection.ExecuteAsync(
            master => master.ReadInputsAsync(connection.UnitId, PlcAddressMap.AxisHomed, PlcAddressMap.AxisStatusCount),
            cancellationToken).ConfigureAwait(false);
        return status.Length == PlcAddressMap.AxisStatusCount
            ? status
            : throw new IOException("The PLC returned an incomplete reply.");
    }
}
