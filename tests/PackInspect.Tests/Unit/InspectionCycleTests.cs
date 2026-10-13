using PackInspect.Core.Ai;
using PackInspect.Core.Alarms;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Core.Sequence;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

/// <summary>The sequence logic against fake devices: which steps run, in which order, and what each failure becomes.</summary>
public class InspectionCycleTests
{
    private readonly StepLog _log = new();
    private readonly FakePlc _plc;
    private readonly FakeMotionAxis _axis;
    private readonly FakeCamera _camera;
    private readonly FakeVisionInspector _inspector = new();
    private readonly FakeInstrument _instrument;
    private readonly InMemoryResultStore _store = new();
    private readonly LotSession _lots;
    private readonly SequenceOptions _options = new();

    public InspectionCycleTests()
    {
        _plc = new FakePlc(_log);
        _axis = new FakeMotionAxis(_log);
        _camera = new FakeCamera(_log);
        _instrument = new FakeInstrument(_log);
        _lots = new LotSession(_store, TimeProvider.System);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Good_unit_runs_every_step_in_order_and_is_sorted_as_pass()
    {
        var cycle = await CreateCycleWithOpenLotAsync();

        await cycle.RunOnceAsync(TestToken);

        Assert.Equal(
            ["move 0", "load", "move 100", "capture", "move 200", "measure Resistance", "unload Pass"],
            _log.Steps);
        var result = Assert.Single(_store.Results);
        Assert.Equal(Verdict.Pass, result.VisionVerdict);
        Assert.Equal(Verdict.Pass, result.ElectricalVerdict);
        Assert.Equal(Verdict.Pass, result.FinalVerdict);
        Assert.Equal(100.0, result.MeasuredValue);
        Assert.Equal(1, result.Sequence);
    }

    [Fact]
    public async Task Visual_defect_rejects_the_unit_but_the_electrical_test_still_runs()
    {
        _inspector.Result = _inspector.Result with { Verdict = Verdict.Fail, Defect = DefectType.Scratch };
        var cycle = await CreateCycleWithOpenLotAsync();

        await cycle.RunOnceAsync(TestToken);

        Assert.Contains("measure Resistance", _log.Steps);
        Assert.Equal("unload Reject", _log.Steps[^1]);
        var result = Assert.Single(_store.Results);
        Assert.Equal(DefectType.Scratch, result.DefectType);
        Assert.Equal(Verdict.Pass, result.ElectricalVerdict);
        Assert.Equal(Verdict.Fail, result.FinalVerdict);
    }

    [Fact]
    public async Task Reading_outside_the_recipe_limits_rejects_the_unit()
    {
        _instrument.Reading = 112.4;
        var cycle = await CreateCycleWithOpenLotAsync();

        await cycle.RunOnceAsync(TestToken);

        Assert.Equal("unload Reject", _log.Steps[^1]);
        var result = Assert.Single(_store.Results);
        Assert.Equal(Verdict.Pass, result.VisionVerdict);
        Assert.Equal(Verdict.Fail, result.ElectricalVerdict);
        Assert.Equal(Verdict.Fail, result.FinalVerdict);
    }

    [Fact]
    public async Task Instrument_that_does_not_answer_becomes_an_instrument_alarm_and_nothing_is_recorded()
    {
        _instrument.Failure = new TimeoutException("no reply");
        var cycle = await CreateCycleWithOpenLotAsync();

        var fault = await Assert.ThrowsAsync<MachineFaultException>(() => cycle.RunOnceAsync(TestToken));

        Assert.Equal(AlarmCodes.InstrumentNoResponse, fault.AlarmCode);
        Assert.Empty(_store.Results);
        Assert.DoesNotContain(_log.Steps, step => step.StartsWith("unload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Camera_failure_becomes_a_camera_alarm()
    {
        _camera.Failure = new IOException("link down");
        var cycle = await CreateCycleWithOpenLotAsync();

        var fault = await Assert.ThrowsAsync<MachineFaultException>(() => cycle.RunOnceAsync(TestToken));

        Assert.Equal(AlarmCodes.CameraNoResponse, fault.AlarmCode);
    }

    [Fact]
    public async Task Empty_camera_image_becomes_a_unit_not_found_alarm()
    {
        _inspector.Result = VisionResult.NoUnit;
        var cycle = await CreateCycleWithOpenLotAsync();

        var fault = await Assert.ThrowsAsync<MachineFaultException>(() => cycle.RunOnceAsync(TestToken));

        Assert.Equal(AlarmCodes.UnitNotFound, fault.AlarmCode);
        Assert.DoesNotContain("measure Resistance", _log.Steps);
    }

    [Fact]
    public async Task Move_that_never_finishes_becomes_a_move_timeout_alarm()
    {
        _axis.NeverFinishMoves = true;
        _options.MoveTimeout = TimeSpan.FromMilliseconds(50);
        var cycle = await CreateCycleWithOpenLotAsync();

        var fault = await Assert.ThrowsAsync<MachineFaultException>(() => cycle.RunOnceAsync(TestToken));

        Assert.Equal(AlarmCodes.AxisMoveTimeout, fault.AlarmCode);
    }

    [Fact]
    public async Task Cancellation_during_a_move_is_not_reported_as_a_fault()
    {
        _axis.NeverFinishMoves = true;
        var cycle = await CreateCycleWithOpenLotAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestToken);

        var running = cycle.RunOnceAsync(stop.Token);
        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Empty(_store.Results);
    }

    [Fact]
    public async Task Cycle_has_no_more_work_once_the_lot_quantity_is_reached()
    {
        var cycle = await CreateCycleWithOpenLotAsync(quantity: 2);

        Assert.True(cycle.HasMoreWork);
        await cycle.RunOnceAsync(TestToken);
        Assert.True(cycle.HasMoreWork);
        await cycle.RunOnceAsync(TestToken);

        Assert.False(cycle.HasMoreWork);
        Assert.Equal([1, 2], _store.Results.Select(result => result.Sequence));
    }

    [Fact]
    public async Task Cycle_invokes_ai_classifier_and_saves_review_record_when_configured()
    {
        var fakeClassifier = new FakeDefectClassifier();
        var fakeAiStore = new InMemoryAiReviewStore();
        await _lots.OpenAsync("LOT-1", TestRecipes.Resistance100, 5, TestToken);
        var cycle = new InspectionCycle(
            _plc,
            _axis,
            _camera,
            _inspector,
            _instrument,
            new FakeImageStore(),
            _lots,
            _options,
            TimeProvider.System,
            defectClassifier: fakeClassifier,
            aiReviewStore: fakeAiStore);

        await cycle.RunOnceAsync(TestToken);

        var recorded = Assert.Single(fakeAiStore.Records);
        Assert.Equal(1, recorded.UnitResultId);
        Assert.Equal(DefectType.Scratch, recorded.AiLabel);
        Assert.Equal(0.95, recorded.AiConfidence);
        Assert.Equal("v1.0-fake", recorded.ModelVersion);
    }

    [Fact]
    public void Cycle_has_no_work_without_an_open_lot() =>
        Assert.False(CreateCycle().HasMoreWork);

    private InspectionCycle CreateCycle() =>
        new(_plc, _axis, _camera, _inspector, _instrument, new FakeImageStore(), _lots, _options, TimeProvider.System);

    private async Task<InspectionCycle> CreateCycleWithOpenLotAsync(int quantity = 5)
    {
        await _lots.OpenAsync("LOT-1", TestRecipes.Resistance100, quantity, TestToken);
        return CreateCycle();
    }
}
