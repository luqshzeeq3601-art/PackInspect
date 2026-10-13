using PackInspect.Core.Devices;

namespace PackInspect.Devices.Modbus;

/// <summary>The PLC: interlocks and the unit handler, over Modbus TCP.</summary>
public sealed class ModbusPlc(ModbusConnection connection, ModbusOptions options) : IPlc
{
    public Task ConnectAsync(CancellationToken cancellationToken) => connection.ConnectAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken) => connection.DisconnectAsync(cancellationToken);

    public async Task<PlcInputs> ReadInputsAsync(CancellationToken cancellationToken)
    {
        // The three interlock bits come from one request so they describe the same instant.
        bool[] interlocks = await connection.ExecuteAsync(
            master => master.ReadInputsAsync(connection.UnitId, PlcAddressMap.EStopOk, PlcAddressMap.InterlockCount),
            cancellationToken).ConfigureAwait(false);

        ushort[] heartbeat = await connection.ExecuteAsync(
            master => master.ReadInputRegistersAsync(connection.UnitId, PlcAddressMap.Heartbeat, 1),
            cancellationToken).ConfigureAwait(false);

        if (interlocks.Length != PlcAddressMap.InterlockCount || heartbeat.Length != 1)
        {
            // A short reply must never be read as "all inputs off" or "all healthy".
            throw new IOException("The PLC returned an incomplete reply.");
        }

        return new PlcInputs(
            EStopOk: interlocks[PlcAddressMap.EStopOk],
            DoorClosed: interlocks[PlcAddressMap.DoorClosed],
            AirPressureOk: interlocks[PlcAddressMap.AirPressureOk],
            Heartbeat: heartbeat[0]);
    }

    public Task ResetFaultsAsync(CancellationToken cancellationToken) =>
        connection.ExecuteAsync(
            master => master.WriteSingleCoilAsync(connection.UnitId, PlcAddressMap.FaultReset, true),
            cancellationToken);

    public async Task LoadUnitAsync(CancellationToken cancellationToken)
    {
        await connection.SendCommandAsync(PlcAddressMap.LoadUnitCommand, cancellationToken).ConfigureAwait(false);

        // Wait for the PLC to confirm the unit is on the carrier before the sequence moves it.
        while (!await IsUnitPresentAsync(cancellationToken).ConfigureAwait(false))
        {
            await Task.Delay(options.StatusPollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task UnloadUnitAsync(SortBin bin, CancellationToken cancellationToken)
    {
        // The bin is written first, so it is already in place when the PLC sees the command.
        await connection.ExecuteAsync(
            master => master.WriteSingleRegisterAsync(connection.UnitId, PlcAddressMap.SortBin, (ushort)bin),
            cancellationToken).ConfigureAwait(false);
        await connection.SendCommandAsync(PlcAddressMap.UnloadUnitCommand, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsUnitPresentAsync(CancellationToken cancellationToken)
    {
        bool[] present = await connection.ExecuteAsync(
            master => master.ReadInputsAsync(connection.UnitId, PlcAddressMap.UnitPresent, 1),
            cancellationToken).ConfigureAwait(false);
        return present.Length == 1 ? present[0] : throw new IOException("The PLC returned an incomplete reply.");
    }
}
