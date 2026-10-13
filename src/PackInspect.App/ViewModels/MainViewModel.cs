using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackInspect.Core.Alarms;
using PackInspect.Core.Machine;
using PackInspect.Core.Sequence;

namespace PackInspect.App.ViewModels;

/// <summary>
/// Shows the machine and forwards button presses. It holds no machine rules:
/// whether a button is enabled is decided by asking the state machine.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly MachineController _controller;
    private readonly SynchronizationContext _uiContext;

    public MainViewModel(
        MachineController controller,
        LotSession lotSession,
        LotViewModel lot,
        AuthViewModel auth,
        RecipeEditorViewModel recipeEditor,
        AuditTrailViewModel auditTrail,
        UserManagementViewModel userManagement,
        ReviewViewModel review)
    {
        _controller = controller;
        Lot = lot;
        Auth = auth;
        RecipeEditor = recipeEditor;
        AuditTrail = auditTrail;
        UserManagement = userManagement;
        Review = review;

        // Captured on the UI thread at construction. Controller events arrive on
        // background threads and are posted back through it, so this class needs
        // no WPF dispatcher.
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

        _controller.StateChanged += OnControllerChanged;
        _controller.AlarmsChanged += OnControllerChanged;
        _controller.StatusChanged += OnControllerChanged;

        // Start is only possible with an open lot, so the buttons follow the lot too.
        lotSession.Changed += OnControllerChanged;
        Refresh();
    }

    [ObservableProperty]
    public partial MachineState State { get; private set; }

    [ObservableProperty]
    public partial bool IsPlcConnected { get; private set; }

    [ObservableProperty]
    public partial int CompletedCycles { get; private set; }

    /// <summary>Lot setup and results, shown on the right of the screen.</summary>
    public LotViewModel Lot { get; }

    public AuthViewModel Auth { get; }

    public RecipeEditorViewModel RecipeEditor { get; }

    public AuditTrailViewModel AuditTrail { get; }

    public UserManagementViewModel UserManagement { get; }

    public ReviewViewModel Review { get; }

    public ObservableCollection<Alarm> Alarms { get; } = [];

    [RelayCommand(CanExecute = nameof(CanHome))]
    private void Home() => _controller.Home();

    private bool CanHome() => _controller.CanFire(MachineTrigger.Home);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start() => _controller.Start();

    private bool CanStart() => _controller.CanFire(MachineTrigger.Start);

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause() => _controller.Pause();

    private bool CanPause() => _controller.CanFire(MachineTrigger.Pause);

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume() => _controller.Resume();

    private bool CanResume() => _controller.CanFire(MachineTrigger.Resume);

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _controller.Stop();

    private bool CanStop() => _controller.CanFire(MachineTrigger.Stop);

    [RelayCommand(CanExecute = nameof(CanReset))]
    private Task ResetAsync(CancellationToken cancellationToken) => _controller.ResetAsync(cancellationToken);

    // Reset stays enabled in both fault states so the operator can always try;
    // the controller refuses it while a fault condition is still present.
    private bool CanReset() => State is MachineState.Alarm or MachineState.EStop;

    private void OnControllerChanged(object? sender, EventArgs e) => _uiContext.Post(_ => Refresh(), null);

    private void Refresh()
    {
        State = _controller.State;
        IsPlcConnected = _controller.IsPlcConnected;
        CompletedCycles = _controller.CompletedCycles;

        var alarms = _controller.ActiveAlarms;
        if (!Alarms.SequenceEqual(alarms))
        {
            Alarms.Clear();
            foreach (var alarm in alarms)
            {
                Alarms.Add(alarm);
            }
        }

        HomeCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
    }
}
