using Microsoft.Extensions.Logging;
using PackInspect.Core.Alarms;
using PackInspect.Core.Devices;

namespace PackInspect.Core.Machine;

/// <summary>
/// Runs the machine around the state machine: watches the PLC interlocks,
/// raises and clears alarms, and starts and cancels the homing and cycle work.
/// Events are raised on background threads; the UI marshals them itself.
/// </summary>
public sealed class MachineController : IAsyncDisposable
{
    private readonly IPlc _plc;
    private readonly IMotionAxis _axis;
    private readonly IMachineCycle _cycle;
    private readonly MachineOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<MachineController> _logger;
    private readonly IAlarmHistoryStore? _alarmHistoryStore;
    private readonly MachineStateMachine _stateMachine;

    // Stateless is not thread-safe and triggers arrive from the UI thread, the
    // monitor loop and the activities, so every Fire goes through this lock.
    private readonly Lock _gate = new();
    private readonly List<Alarm> _alarms = [];
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();

    private Task _monitorTask = Task.CompletedTask;
    private Task _activityTask = Task.CompletedTask;
    private CancellationTokenSource? _activityCts;
    private volatile TaskCompletionSource? _pauseGate;
    private volatile PlcInputs? _lastInputs;
    private volatile bool _isPlcConnected;
    private volatile bool _axesHomed;
    private volatile bool _heartbeatStale;
    private bool _commsFailureLogged;
    private ushort? _lastHeartbeat;
    private long _lastHeartbeatChange;
    private int _completedCycles;

    public MachineController(
        IPlc plc,
        IMotionAxis axis,
        IMachineCycle cycle,
        MachineOptions options,
        TimeProvider time,
        ILogger<MachineController> logger,
        IAlarmHistoryStore? alarmHistoryStore = null)
    {
        _plc = plc;
        _axis = axis;
        _cycle = cycle;
        _options = options;
        _time = time;
        _logger = logger;
        _alarmHistoryStore = alarmHistoryStore;

        _stateMachine = new MachineStateMachine(
            noActiveAlarms: () => _alarms.Count == 0,
            axesHomed: () => _axesHomed,
            eStopReleased: () => _lastInputs is { EStopOk: true });
        _stateMachine.Transitioned += OnTransitioned;
    }

    public event EventHandler? StateChanged;

    public event EventHandler? AlarmsChanged;

    /// <summary>Raised when the PLC connection or the cycle counter changes.</summary>
    public event EventHandler? StatusChanged;

    public MachineState State
    {
        get
        {
            lock (_gate)
            {
                return _stateMachine.State;
            }
        }
    }

    public IReadOnlyList<Alarm> ActiveAlarms
    {
        get
        {
            lock (_gate)
            {
                return [.. _alarms];
            }
        }
    }

    public bool IsPlcConnected => _isPlcConnected;

    public int CompletedCycles => Volatile.Read(ref _completedCycles);

    /// <summary>True if the trigger would be accepted now. The UI uses this to enable buttons.</summary>
    public bool CanFire(MachineTrigger trigger)
    {
        if (trigger == MachineTrigger.Start && !_cycle.HasMoreWork)
        {
            return false;
        }

        lock (_gate)
        {
            return _stateMachine.CanFire(trigger);
        }
    }

    /// <summary>Starts the background loop that reads the PLC interlocks.</summary>
    public void StartMonitoring() => _monitorTask = Task.Run(() => MonitorAsync(_shutdown.Token));

    public bool Home() => Fire(MachineTrigger.Home);

    /// <summary>Starts running units. Refused when there is nothing to run, for example no lot is open.</summary>
    public bool Start() => _cycle.HasMoreWork && Fire(MachineTrigger.Start);

    /// <summary>Cycle stop: the unit in progress is finished before the machine pauses.</summary>
    public bool Pause() => Fire(MachineTrigger.Pause);

    public bool Resume() => Fire(MachineTrigger.Resume);

    public bool Stop() => Fire(MachineTrigger.Stop);

    /// <summary>
    /// Operator acknowledgement. Alarms whose cause has gone are cleared; if any
    /// cause is still present the machine stays where it is.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        if (State is not (MachineState.Alarm or MachineState.EStop))
        {
            return;
        }

        try
        {
            await _plc.ResetFaultsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "The PLC did not accept the fault reset");
        }

        // Read the interlocks now rather than trusting the last poll, so a door
        // closed a moment ago is seen as closed.
        await PollOnceAsync(cancellationToken).ConfigureAwait(false);

        // Ask the axis whether it still has its reference instead of assuming:
        // a PLC that was power-cycled during the fault has lost it.
        try
        {
            _axesHomed = await _axis.IsHomedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not read the homed status of axis {Axis}", _axis.Name);
            _axesHomed = false;
        }

        List<Alarm> clearedAlarms;
        lock (_gate)
        {
            clearedAlarms = _alarms.FindAll(alarm => !IsConditionPresent(alarm.Code));
            _alarms.RemoveAll(alarm => !IsConditionPresent(alarm.Code));
        }

        if (clearedAlarms.Count > 0)
        {
            AlarmsChanged?.Invoke(this, EventArgs.Empty);
            foreach (var cleared in clearedAlarms)
            {
                _ = _alarmHistoryStore?.RecordClearedAsync(cleared.Code, _time.GetUtcNow(), cancellationToken);
            }
        }

        Fire(MachineTrigger.Reset);
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync().ConfigureAwait(false);
        lock (_gate)
        {
            CancelActivity();
        }

        // Both tasks handle their own cancellation and faults, so they complete normally.
        await _monitorTask.ConfigureAwait(false);
        await _activityTask.ConfigureAwait(false);

        try
        {
            await _plc.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disconnecting from the PLC failed");
        }

        _shutdown.Dispose();
        _pollLock.Dispose();
    }

    private bool Fire(MachineTrigger trigger)
    {
        bool fired;
        lock (_gate)
        {
            fired = _stateMachine.Fire(trigger);
        }

        if (fired)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        return fired;
    }

    // Runs inside Fire, under the lock, so activities start and stop in exactly
    // the order the transitions happened.
    private void OnTransitioned(MachineState from, MachineState to)
    {
        _logger.LogInformation("Machine state {From} -> {To}", from, to);

        if (to is MachineState.Homing or MachineState.EStop)
        {
            _axesHomed = false;
        }
        else if (from == MachineState.Homing && to == MachineState.Ready)
        {
            _axesHomed = true;
        }

        switch (to)
        {
            case MachineState.Homing:
                StartActivity(HomeAxisAsync, AlarmCodes.AxisFault);
                break;

            case MachineState.Running when from == MachineState.Ready:
                StartActivity(RunCyclesAsync, AlarmCodes.CycleFault);
                break;

            case MachineState.Running:
                _pauseGate?.TrySetResult();
                _pauseGate = null;
                break;

            case MachineState.Paused:
                _pauseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                break;

            default:
                CancelActivity();
                _pauseGate = null;
                if (to is MachineState.Alarm or MachineState.EStop)
                {
                    _ = StopAxisAsync();
                }

                break;
        }
    }

    private void StartActivity(Func<CancellationToken, Task> work, string faultCode)
    {
        CancelActivity();
        var cts = new CancellationTokenSource();
        _activityCts = cts;
        _activityTask = Task.Run(() => RunActivityAsync(work, faultCode, cts.Token));
    }

    private void CancelActivity()
    {
        _activityCts?.Cancel();
        _activityCts = null;
    }

    private async Task RunActivityAsync(Func<CancellationToken, Task> work, string faultCode, CancellationToken cancellationToken)
    {
        try
        {
            await work(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Asked to stop by a state change. Whatever the interrupted step threw
            // on its way out is a consequence of stopping, not a fault.
        }
        catch (MachineFaultException fault)
        {
            _logger.LogError(fault, "Machine fault {Code}", fault.AlarmCode);
            RaiseAlarm(fault.AlarmCode, fault.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Machine activity failed");
            RaiseAlarm(faultCode, ex.Message);
        }
    }

    private async Task HomeAxisAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.HomingTimeout);
        try
        {
            await _axis.HomeAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RaiseAlarm(AlarmCodes.AxisHomeTimeout, $"Axis {_axis.Name} did not finish homing within {_options.HomingTimeout.TotalSeconds:0.#} s.");
            return;
        }

        Fire(MachineTrigger.HomingDone);
    }

    private async Task RunCyclesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_cycle.HasMoreWork)
            {
                // Lot complete: the machine stops itself and waits for the next lot.
                Fire(MachineTrigger.Stop);
                return;
            }

            if (_pauseGate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            await _cycle.RunOnceAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _completedCycles);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task StopAxisAsync()
    {
        try
        {
            await _axis.StopAsync(_shutdown.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stopping axis {Axis} failed", _axis.Name);
        }
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        try
        {
            do
            {
                try
                {
                    await PollOnceAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // The monitor is the machine's view of its interlocks; it must keep running.
                    _logger.LogError(ex, "Interlock monitor iteration failed");
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        await _pollLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isPlcConnected)
            {
                await _plc.ConnectAsync(cancellationToken).ConfigureAwait(false);
                _lastHeartbeat = null;
            }

            var inputs = await _plc.ReadInputsAsync(cancellationToken).ConfigureAwait(false);
            TrackHeartbeat(inputs.Heartbeat);
            _lastInputs = inputs;
            _commsFailureLogged = false;
            SetPlcConnected(true);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (!_commsFailureLogged)
            {
                _logger.LogWarning(ex, "PLC communication failed");
                _commsFailureLogged = true;
            }

            // Without communication the interlock state is unknown, which is treated as unsafe.
            _lastInputs = null;
            SetPlcConnected(false);
        }
        finally
        {
            _pollLock.Release();
        }

        EvaluateInterlocks();
    }

    private void TrackHeartbeat(ushort heartbeat)
    {
        if (_lastHeartbeat != heartbeat)
        {
            _lastHeartbeat = heartbeat;
            _lastHeartbeatChange = _time.GetTimestamp();
            _heartbeatStale = false;
        }
        else
        {
            _heartbeatStale = _time.GetElapsedTime(_lastHeartbeatChange) > _options.HeartbeatTimeout;
        }
    }

    private void EvaluateInterlocks()
    {
        var inputs = _lastInputs;
        if (inputs is null)
        {
            RaiseAlarm(AlarmCodes.PlcCommsLost, "No communication with the PLC.");
            return;
        }

        if (_heartbeatStale)
        {
            RaiseAlarm(AlarmCodes.PlcCommsLost, "The PLC heartbeat has stopped.");
        }

        if (!inputs.DoorClosed)
        {
            RaiseAlarm(AlarmCodes.DoorOpen, "The guard door is open.");
        }

        if (!inputs.AirPressureOk)
        {
            RaiseAlarm(AlarmCodes.AirPressureLow, "Air pressure is low.");
        }

        if (!inputs.EStopOk)
        {
            Fire(MachineTrigger.EStopPressed);
        }
    }

    private bool IsConditionPresent(string alarmCode) => alarmCode switch
    {
        AlarmCodes.DoorOpen => _lastInputs is not { DoorClosed: true },
        AlarmCodes.AirPressureLow => _lastInputs is not { AirPressureOk: true },
        AlarmCodes.PlcCommsLost => _lastInputs is null || _heartbeatStale,
        _ => false,
    };

    private void RaiseAlarm(string code, string message)
    {
        bool added;
        lock (_gate)
        {
            added = _alarms.TrueForAll(alarm => alarm.Code != code);
            if (added)
            {
                _alarms.Add(new Alarm(code, message, _time.GetLocalNow()));
            }
        }

        if (added)
        {
            _logger.LogWarning("Alarm raised: {Code} {Message}", code, message);
            AlarmsChanged?.Invoke(this, EventArgs.Empty);
            _ = _alarmHistoryStore?.RecordRaisedAsync(code, message, _time.GetUtcNow(), CancellationToken.None);
        }

        // Fired even for an alarm that is already listed, so a condition that is
        // still present after a reset puts the machine back into Alarm.
        Fire(MachineTrigger.AlarmRaised);
    }

    private void SetPlcConnected(bool connected)
    {
        if (_isPlcConnected == connected)
        {
            return;
        }

        _isPlcConnected = connected;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
