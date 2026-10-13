using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NModbus;
using NModbus.Data;
using PackInspect.Simulator.Imaging;
using PackInspect.Simulator.Instrument;
using PackInspect.Simulator.Plc;
using PackInspect.Simulator.Units;

namespace PackInspect.Simulator;

/// <summary>
/// Runs the simulated hardware: a Modbus TCP server for the PLC and axis, a SCPI
/// server for the multimeter and a camera server, all sharing one station model.
/// Used by the console program and, in-process, by the integration tests.
/// </summary>
public sealed class SimulatorHost : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TcpListener _modbusListener;
    private readonly StationModel _station;
    private readonly PlcSimulator _plc;
    private readonly Task[] _tasks;

    private SimulatorHost(SimulatorOptions options)
    {
        _station = new StationModel(options);
        _station.UnitSorted += unit => UnitSorted?.Invoke(unit);

        // Loopback only: the simulator must never be reachable from the network.
        _modbusListener = new TcpListener(IPAddress.Loopback, options.ModbusPort);
        _modbusListener.Start();
        ModbusPort = ((IPEndPoint)_modbusListener.LocalEndpoint).Port;

        var factory = new ModbusFactory();
        var store = new DefaultSlaveDataStore();
        var network = factory.CreateSlaveNetwork(_modbusListener);
        network.AddSlave(factory.CreateSlave(ModbusMap.UnitId, store));

        _plc = new PlcSimulator(store, _station, Faults, options.HomingDuration);
        _plc.Scan(TimeSpan.Zero);

        var scpi = new ScpiServer(options.ScpiPort, _station, Faults);
        var camera = new CameraServer(options.CameraPort, _station);
        ScpiPort = scpi.Port;
        CameraPort = camera.Port;

        var token = _shutdown.Token;
        _tasks =
        [
            Task.Run(() => network.ListenAsync(token)),
            Task.Run(() => ScanLoopAsync(options.ScanInterval, token)),
            Task.Run(() => scpi.RunAsync(token)),
            Task.Run(() => camera.RunAsync(token)),
        ];
    }

    /// <summary>Raised when the app unloads a unit, with the truth about it and the bin the app chose.</summary>
    public event Action<SortedUnit>? UnitSorted;

    public int ModbusPort { get; }

    public int ScpiPort { get; }

    public int CameraPort { get; }

    public SimulatorFaults Faults { get; } = new();

    public string AxisStatus => _plc.DescribeAxis();

    /// <summary>Every unit unloaded so far.</summary>
    public IReadOnlyList<SortedUnit> SortedUnits => _station.SortedUnits;

    public static SimulatorHost Start(SimulatorOptions? options = null) => new(options ?? new SimulatorOptions());

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync().ConfigureAwait(false);
        _modbusListener.Stop();

        try
        {
            await Task.WhenAll(_tasks).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // Expected while shutting the listeners down.
        }

        _shutdown.Dispose();
    }

    private async Task ScanLoopAsync(TimeSpan scanInterval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(scanInterval);
        long last = Stopwatch.GetTimestamp();
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            long now = Stopwatch.GetTimestamp();
            _plc.Scan(Stopwatch.GetElapsedTime(last, now));
            last = now;
        }
    }
}
