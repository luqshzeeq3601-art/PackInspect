using Microsoft.Extensions.DependencyInjection;
using PackInspect.Core.Alarms;
using PackInspect.Core.Domain;
using PackInspect.Core.Machine;
using PackInspect.Core.Sequence;
using PackInspect.Data;
using PackInspect.Devices.Camera;
using PackInspect.Devices.Modbus;
using PackInspect.Devices.Scpi;
using PackInspect.Devices.Vision;
using PackInspect.Simulator;
using PackInspect.Simulator.Units;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Integration;

/// <summary>
/// The whole station against the simulator hosted in-process on free ports:
/// real Modbus, SCPI and camera drivers, real OpenCV inspection, real SQLite.
/// These prove the drivers and the simulator agree on every protocol.
/// </summary>
[Trait("Category", "Integration")]
public class SimulatorStationTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lot_of_good_units_runs_to_completion_and_every_result_is_stored()
    {
        await using var station = await Station.StartAsync(new SimulatorOptions { DefectRate = 0, ElectricalFailRate = 0 });
        var controller = station.Controller;
        int lotId = await station.OpenLotAsync("LOT-GOOD", quantity: 5);

        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);
        Assert.True(controller.Start());
        await station.WaitForLotCompleteAsync();

        var summary = await station.Results.GetLotSummaryAsync(lotId, TestToken);
        Assert.NotNull(summary);
        Assert.Equal(5, summary.Total);
        Assert.Equal(5, summary.Passed);
        Assert.Equal(100.0, summary.YieldPercent);

        Assert.Equal(5, station.Simulator.SortedUnits.Count);
        Assert.All(station.Simulator.SortedUnits, sorted => Assert.True(sorted.SortedAsPass));
        Assert.Equal(5, Directory.GetFiles(station.ImageFolder, "*.png", SearchOption.AllDirectories).Length);
        Assert.Empty(controller.ActiveAlarms);
    }

    [Fact]
    public async Task Mixed_lot_is_sorted_according_to_the_truth_about_each_unit()
    {
        await using var station = await Station.StartAsync(new SimulatorOptions { DefectRate = 0.4, ElectricalFailRate = 0.3, Seed = 2026 });
        var controller = station.Controller;
        int lotId = await station.OpenLotAsync("LOT-MIXED", quantity: 15);

        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);
        controller.Start();
        await station.WaitForLotCompleteAsync();

        var sorted = station.Simulator.SortedUnits;
        Assert.Equal(15, sorted.Count);
        Assert.Contains(sorted, unit => !unit.Unit.IsGood);

        // Electrical failures and structural defects are always caught; good units are never rejected.
        // (Scratches and contamination are left out: the rule-based check is known to miss a few.)
        Assert.All(sorted.Where(unit => !unit.Unit.ElectricallyGood), unit => Assert.False(unit.SortedAsPass));
        Assert.All(
            sorted.Where(unit => unit.Unit.Defect is UnitDefect.CrackChip or UnitDefect.LeadDefect or UnitDefect.MarkingDefect),
            unit => Assert.False(unit.SortedAsPass));
        Assert.All(sorted.Where(unit => unit.Unit.IsGood), unit => Assert.True(unit.SortedAsPass));

        // What was stored matches what was physically sorted.
        var summary = await station.Results.GetLotSummaryAsync(lotId, TestToken);
        Assert.Equal(15, summary!.Total);
        Assert.Equal(sorted.Count(unit => unit.SortedAsPass), summary.Passed);
    }

    [Fact]
    public async Task Lot_of_one_hundred_mixed_units_runs_unattended_and_every_result_is_stored()
    {
        const int Quantity = 100;
        await using var station = await Station.StartAsync(new SimulatorOptions { DefectRate = 0.2, ElectricalFailRate = 0.1, Seed = 100 });
        var controller = station.Controller;
        int lotId = await station.OpenLotAsync("LOT-100", Quantity);

        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);
        Assert.True(controller.Start());

        // A hundred units take longer than the default wait, which is sized for a handful.
        await station.WaitForLotCompleteAsync(TimeSpan.FromMinutes(2));

        var sorted = station.Simulator.SortedUnits;
        Assert.Equal(Quantity, sorted.Count);

        var summary = await station.Results.GetLotSummaryAsync(lotId, TestToken);
        Assert.NotNull(summary);
        Assert.Equal(Quantity, summary.Total);
        Assert.Equal(sorted.Count(unit => unit.SortedAsPass), summary.Passed);

        Assert.Equal(Quantity, Directory.GetFiles(station.ImageFolder, "*.png", SearchOption.AllDirectories).Length);
        Assert.Empty(controller.ActiveAlarms);
    }

    [Fact]
    public async Task Injected_instrument_silence_raises_alarm_and_the_lot_finishes_after_recovery()
    {
        await using var station = await Station.StartAsync(new SimulatorOptions { DefectRate = 0, ElectricalFailRate = 0 });
        var controller = station.Controller;
        int lotId = await station.OpenLotAsync("LOT-DMM", quantity: 3);
        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);

        station.Simulator.Faults.InstrumentNotResponding = true;
        controller.Start();
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);
        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.InstrumentNoResponse);

        station.Simulator.Faults.InstrumentNotResponding = false;
        await ResetUntilAsync(controller, MachineState.Ready);
        controller.Start();
        await station.WaitForLotCompleteAsync();

        // The interrupted unit was not recorded; three complete units were.
        var summary = await station.Results.GetLotSummaryAsync(lotId, TestToken);
        Assert.Equal(3, summary!.Total);
        Assert.Equal(3, summary.Passed);
    }

    [Fact]
    public async Task Injected_axis_timeout_during_a_move_raises_alarm_and_the_lot_finishes_after_recovery()
    {
        await using var station = await Station.StartAsync(
            new SimulatorOptions { DefectRate = 0, ElectricalFailRate = 0 },
            moveTimeout: TimeSpan.FromMilliseconds(400));
        var controller = station.Controller;
        await station.OpenLotAsync("LOT-AXIS", quantity: 2);
        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);

        station.Simulator.Faults.AxisTimeout = true;
        controller.Start();
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);
        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.AxisMoveTimeout);

        station.Simulator.Faults.AxisTimeout = false;
        await ResetUntilAsync(controller, MachineState.Ready);
        controller.Start();
        await station.WaitForLotCompleteAsync();

        Assert.Equal(2, station.Simulator.SortedUnits.Count);
    }

    [Fact]
    public async Task Injected_door_open_during_a_lot_raises_alarm_and_the_lot_finishes_after_recovery()
    {
        await using var station = await Station.StartAsync(new SimulatorOptions { DefectRate = 0, ElectricalFailRate = 0 });
        var controller = station.Controller;
        await station.OpenLotAsync("LOT-DOOR", quantity: 4);
        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);
        controller.Start();
        await MachineWait.UntilAsync(controller, () => controller.CompletedCycles >= 1, "first unit completed");

        station.Simulator.Faults.DoorOpen = true;
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);
        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.DoorOpen);

        await controller.ResetAsync(TestToken);
        Assert.Equal(MachineState.Alarm, controller.State);

        station.Simulator.Faults.DoorOpen = false;
        await ResetUntilAsync(controller, MachineState.Ready);
        controller.Start();
        await station.WaitForLotCompleteAsync();

        Assert.Equal(4, station.Lots.Current!.Total);
    }

    [Fact]
    public async Task Injected_axis_timeout_during_homing_raises_alarm_and_homing_works_again_afterwards()
    {
        await using var station = await Station.StartAsync(homingTimeout: TimeSpan.FromMilliseconds(400));
        var controller = station.Controller;

        station.Simulator.Faults.AxisTimeout = true;
        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Alarm);
        Assert.Contains(controller.ActiveAlarms, alarm => alarm.Code == AlarmCodes.AxisHomeTimeout);

        station.Simulator.Faults.AxisTimeout = false;
        await controller.ResetAsync(TestToken);
        Assert.Equal(MachineState.Idle, controller.State);

        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);
    }

    [Fact]
    public async Task Injected_emergency_stop_requires_homing_again()
    {
        await using var station = await Station.StartAsync();
        var controller = station.Controller;
        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);

        station.Simulator.Faults.EStopPressed = true;
        await MachineWait.ForStateAsync(controller, MachineState.EStop);

        station.Simulator.Faults.EStopPressed = false;
        await ResetUntilAsync(controller, MachineState.Idle);

        controller.Home();
        await MachineWait.ForStateAsync(controller, MachineState.Ready);
    }

    // The simulated PLC applies a cleared fault on its next scan, so a reset sent
    // in the same instant can still see the old input. An operator would simply
    // press Reset again; so does the test, each time the PLC status is re-read.
    private static async Task ResetUntilAsync(MachineController controller, MachineState expected)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));

        while (controller.State != expected)
        {
            await controller.ResetAsync(deadline.Token);
            await Task.Yield();
        }
    }

    private sealed class Station : IAsyncDisposable
    {
        private readonly TempFolder _folder;
        private readonly ServiceProvider _data;
        private readonly ModbusConnection _connection;
        private readonly ScpiInstrument _instrument;
        private readonly TcpCamera _camera;
        private readonly IReadOnlyList<Recipe> _recipes;

        private Station(
            TempFolder folder,
            ServiceProvider data,
            SimulatorHost simulator,
            ModbusConnection connection,
            ScpiInstrument instrument,
            TcpCamera camera,
            LotSession lots,
            MachineController controller,
            IReadOnlyList<Recipe> recipes)
        {
            _folder = folder;
            _data = data;
            Simulator = simulator;
            _connection = connection;
            _instrument = instrument;
            _camera = camera;
            Lots = lots;
            Controller = controller;
            _recipes = recipes;
        }

        public SimulatorHost Simulator { get; }

        public MachineController Controller { get; }

        public LotSession Lots { get; }

        public IResultStore Results => _data.GetRequiredService<IResultStore>();

        public string ImageFolder => _folder.File("images");

        public static async Task<Station> StartAsync(
            SimulatorOptions? simulatorOptions = null,
            TimeSpan? homingTimeout = null,
            TimeSpan? moveTimeout = null)
        {
            // Fast and on free ports, so tests are quick and can run side by side.
            var simulator = SimulatorHost.Start((simulatorOptions ?? new SimulatorOptions()) with
            {
                ModbusPort = 0,
                ScpiPort = 0,
                CameraPort = 0,
                HomingDuration = TimeSpan.FromMilliseconds(50),
                ScanInterval = TimeSpan.FromMilliseconds(5),
            });

            var folder = new TempFolder();
            var data = new ServiceCollection()
                .AddSingleton(TimeProvider.System)
                .AddPackInspectData(folder.File("station.db"))
                .BuildServiceProvider();
            await data.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
            var recipes = await data.GetRequiredService<IRecipeStore>().GetAllAsync(TestToken);

            // Communication timeouts are generous here. On a busy CI runner a loopback
            // reply can take over a second, and that is the runner being slow, not a
            // PLC fault. Fault behaviour is tested by injecting faults, not by load.
            var modbusOptions = new ModbusOptions
            {
                Port = simulator.ModbusPort,
                StatusPollInterval = TimeSpan.FromMilliseconds(5),
                RequestTimeout = TimeSpan.FromSeconds(5),
            };
            var connection = new ModbusConnection(modbusOptions);
            var plc = new ModbusPlc(connection, modbusOptions);
            var axis = new ModbusMotionAxis(connection, modbusOptions);
            var instrument = new ScpiInstrument(new ScpiOptions { Port = simulator.ScpiPort, Timeout = TimeSpan.FromMilliseconds(500) });
            var camera = new TcpCamera(new CameraOptions { Port = simulator.CameraPort });

            var lots = new LotSession(data.GetRequiredService<IResultStore>(), TimeProvider.System);
            var cycle = new InspectionCycle(
                plc,
                axis,
                camera,
                new OpenCvVisionInspector(),
                instrument,
                new PngImageStore(new ImageStoreOptions { RootDirectory = folder.File("images") }),
                lots,
                new SequenceOptions { AxisSpeedMmPerSec = 20_000, MoveTimeout = moveTimeout ?? TimeSpan.FromSeconds(10) },
                TimeProvider.System);

            var controller = new MachineController(
                plc,
                axis,
                cycle,
                new MachineOptions
                {
                    PollInterval = TimeSpan.FromMilliseconds(20),
                    HeartbeatTimeout = TimeSpan.FromSeconds(10),
                    HomingTimeout = homingTimeout ?? TimeSpan.FromSeconds(10),
                },
                TimeProvider.System,
                new TestOutputLogger<MachineController>());
            controller.StartMonitoring();

            // Commands sent before the first successful poll would fail with "not connected".
            await MachineWait.UntilAsync(controller, () => controller.IsPlcConnected, "PLC connected");
            return new Station(folder, data, simulator, connection, instrument, camera, lots, controller, recipes);
        }

        public async Task<int> OpenLotAsync(string lotNumber, int quantity)
        {
            var recipe = _recipes.Single(recipe => recipe.Name == "QFN-5x5-100R");
            await Lots.OpenAsync(lotNumber, recipe, quantity, TestToken);
            return Lots.Current!.Id;
        }

        public Task WaitForLotCompleteAsync(TimeSpan? timeout = null) =>
            MachineWait.UntilAsync(
                Controller,
                () => Lots.Current is { IsOpen: false } && Controller.State == MachineState.Ready,
                "the lot to complete and the machine to return to Ready",
                timeout);

        public async ValueTask DisposeAsync()
        {
            await Controller.DisposeAsync();
            _connection.Dispose();
            _instrument.Dispose();
            _camera.Dispose();
            await Simulator.DisposeAsync();
            await _data.DisposeAsync();
            _folder.Dispose();
        }
    }
}
