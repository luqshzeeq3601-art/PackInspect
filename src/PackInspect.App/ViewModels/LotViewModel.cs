using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackInspect.Core.Auth;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Core.Machine;
using PackInspect.Core.Sequence;

namespace PackInspect.App.ViewModels;

/// <summary>Lot setup, the running lot summary and the latest unit results.</summary>
public partial class LotViewModel : ObservableObject
{
    private const int MaxRecentResults = 200;

    private readonly LotSession _session;
    private readonly IRecipeStore _recipeStore;
    private readonly MachineController _controller;
    private readonly IUserSession _userSession;
    private readonly SynchronizationContext _uiContext;
    private int _shownLotId;
    private int _shownSequence;

    public LotViewModel(LotSession session, IRecipeStore recipeStore, MachineController controller, IUserSession userSession)
    {
        _session = session;
        _recipeStore = recipeStore;
        _controller = controller;
        _userSession = userSession;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

        _session.Changed += OnChanged;
        _controller.StateChanged += OnChanged;
    }

    public ObservableCollection<Recipe> Recipes { get; } = [];

    /// <summary>Newest first.</summary>
    public ObservableCollection<UnitResult> RecentResults { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenLotCommand))]
    public partial Recipe? SelectedRecipe { get; set; }

    [ObservableProperty]
    public partial string LotNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Quantity { get; set; } = 20;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial ActiveLot? CurrentLot { get; private set; }

    [ObservableProperty]
    public partial UnitResult? LastResult { get; private set; }

    [ObservableProperty]
    public partial CapturedImage? LastImage { get; private set; }

    public async Task LoadRecipesAsync(CancellationToken cancellationToken)
    {
        var recipes = await _recipeStore.GetAllAsync(cancellationToken);
        Recipes.Clear();
        foreach (var recipe in recipes)
        {
            Recipes.Add(recipe);
        }

        SelectedRecipe = Recipes.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanOpenLot))]
    private async Task OpenLotAsync(CancellationToken cancellationToken)
    {
        string lotNumber = LotNumber.Trim();
        ErrorMessage = LotRules.Validate(lotNumber, Quantity);
        if (ErrorMessage is not null || SelectedRecipe is null)
        {
            return;
        }

        // A lot is traceable to the person who ran it, so nobody opens one anonymously.
        if (_userSession.CurrentUser is not { Id: var operatorUserId })
        {
            ErrorMessage = "Log in before opening a lot.";
            return;
        }

        try
        {
            await _session.OpenAsync(lotNumber, SelectedRecipe, Quantity, operatorUserId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // For example a lot number that was used before.
            ErrorMessage = ex.Message;
        }
    }

    private bool CanOpenLot() => SelectedRecipe is not null && CurrentLot is not { IsOpen: true };

    [RelayCommand(CanExecute = nameof(CanCloseLot))]
    private Task CloseLotAsync(CancellationToken cancellationToken) => _session.CloseAsync(cancellationToken);

    // A lot may only be closed by hand while no unit is in progress.
    private bool CanCloseLot() =>
        CurrentLot is { IsOpen: true } && _controller.State is not (MachineState.Running or MachineState.Paused);

    private void OnChanged(object? sender, EventArgs e) => _uiContext.Post(_ => Refresh(), null);

    private void Refresh()
    {
        var lot = _session.Current;
        var result = _session.LastResult;
        CurrentLot = lot;
        LastResult = result;
        LastImage = _session.LastImage;

        if (lot is not null && lot.Id != _shownLotId)
        {
            RecentResults.Clear();
            _shownLotId = lot.Id;
            _shownSequence = 0;
        }

        if (result is not null && result.Sequence > _shownSequence)
        {
            RecentResults.Insert(0, result);
            _shownSequence = result.Sequence;
            if (RecentResults.Count > MaxRecentResults)
            {
                RecentResults.RemoveAt(RecentResults.Count - 1);
            }
        }

        OpenLotCommand.NotifyCanExecuteChanged();
        CloseLotCommand.NotifyCanExecuteChanged();
    }
}
