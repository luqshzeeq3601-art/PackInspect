using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackInspect.Core.Auth;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;

namespace PackInspect.App.ViewModels;

/// <summary>
/// Handles recipe modification and creation with enforced electronic sign-off:
/// requires Engineer or Admin role, password re-authentication, and a documented reason.
/// </summary>
public partial class RecipeEditorViewModel : ObservableObject
{
    private readonly IRecipeStore _recipeStore;
    private readonly IUserService _userService;
    private readonly IUserSession _session;

    public RecipeEditorViewModel(IRecipeStore recipeStore, IUserService userService, IUserSession session)
    {
        _recipeStore = recipeStore;
        _userService = userService;
        _session = session;

        _session.UserChanged += (_, _) => OnUserChanged();
        OnUserChanged();
    }

    public ObservableCollection<Recipe> Recipes { get; } = [];

    [ObservableProperty]
    public partial Recipe? SelectedRecipe { get; set; }

    [ObservableProperty]
    public partial bool CanEdit { get; private set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial bool IsCreatingNew { get; set; }

    [ObservableProperty]
    public partial bool IsSignOffDialogOpen { get; set; }

    // Editable recipe fields
    [ObservableProperty]
    public partial string RecipeName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double CameraPositionMm { get; set; } = 100.0;

    [ObservableProperty]
    public partial double TestPositionMm { get; set; } = 200.0;

    [ObservableProperty]
    public partial double MinBodyRectangularity { get; set; } = 0.965;

    [ObservableProperty]
    public partial int ExpectedLeadCount { get; set; } = 24;

    [ObservableProperty]
    public partial int MinMarkingPixels { get; set; } = 900;

    [ObservableProperty]
    public partial int MaxMarkingPixels { get; set; } = 1800;

    [ObservableProperty]
    public partial int MaxDefectAreaPx { get; set; } = 15;

    [ObservableProperty]
    public partial MeasurementKind MeasurementKind { get; set; } = MeasurementKind.Resistance;

    [ObservableProperty]
    public partial double LowLimit { get; set; } = 95.0;

    [ObservableProperty]
    public partial double HighLimit { get; set; } = 105.0;

    [ObservableProperty]
    public partial int Version { get; set; } = 1;

    // Sign-off credentials and audit justification
    [ObservableProperty]
    public partial string SignOffPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SignOffReason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public async Task LoadRecipesAsync(CancellationToken cancellationToken)
    {
        var list = await _recipeStore.GetAllAsync(cancellationToken);
        Recipes.Clear();
        foreach (var item in list)
        {
            Recipes.Add(item);
        }

        if (SelectedRecipe is null && Recipes.Count > 0)
        {
            SelectedRecipe = Recipes[0];
        }
        else if (SelectedRecipe is not null)
        {
            SelectedRecipe = Recipes.FirstOrDefault(r => r.Id == SelectedRecipe.Id) ?? Recipes.FirstOrDefault();
        }
    }

    partial void OnSelectedRecipeChanged(Recipe? value)
    {
        if (value is not null && !IsEditing && !IsCreatingNew)
        {
            LoadFieldsFromRecipe(value);
        }
    }

    [RelayCommand]
    private void StartEdit()
    {
        if (SelectedRecipe is null || !CanEdit)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        LoadFieldsFromRecipe(SelectedRecipe);
        IsEditing = true;
        IsCreatingNew = false;
    }

    [RelayCommand]
    private void StartNew()
    {
        if (!CanEdit)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        RecipeName = "NEW-RECIPE";
        CameraPositionMm = 100.0;
        TestPositionMm = 200.0;
        MinBodyRectangularity = 0.965;
        ExpectedLeadCount = 24;
        MinMarkingPixels = 900;
        MaxMarkingPixels = 1800;
        MaxDefectAreaPx = 15;
        MeasurementKind = MeasurementKind.Resistance;
        LowLimit = 90.0;
        HighLimit = 110.0;
        Version = 1;

        IsEditing = false;
        IsCreatingNew = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        IsCreatingNew = false;
        ErrorMessage = null;
        StatusMessage = null;
        if (SelectedRecipe is not null)
        {
            LoadFieldsFromRecipe(SelectedRecipe);
        }
    }

    [RelayCommand]
    private void RequestSignOff()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(RecipeName))
        {
            ErrorMessage = "Recipe name cannot be empty.";
            return;
        }

        if (LowLimit > HighLimit)
        {
            ErrorMessage = "Low limit cannot exceed high limit.";
            return;
        }

        SignOffPassword = string.Empty;
        SignOffReason = string.Empty;
        IsSignOffDialogOpen = true;
    }

    [RelayCommand]
    private void CancelSignOff()
    {
        IsSignOffDialogOpen = false;
        SignOffPassword = string.Empty;
        SignOffReason = string.Empty;
    }

    [RelayCommand]
    private async Task SubmitSignOffAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        var currentUser = _session.CurrentUser;
        if (currentUser is null || currentUser.Role is not (UserRole.Engineer or UserRole.Admin))
        {
            ErrorMessage = "You must be signed in as an Engineer or Admin to sign off recipe changes.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SignOffReason))
        {
            ErrorMessage = "A documented reason is mandatory for any recipe sign-off.";
            return;
        }

        bool isPasswordValid = await _userService.VerifyPasswordAsync(currentUser.Id, SignOffPassword, cancellationToken);
        if (!isPasswordValid)
        {
            ErrorMessage = "Sign-off password incorrect. Verification failed.";
            return;
        }

        try
        {
            Recipe updated;
            if (IsCreatingNew)
            {
                var newRecipe = new Recipe
                {
                    Name = RecipeName.Trim(),
                    Version = 1,
                    CameraPositionMm = CameraPositionMm,
                    TestPositionMm = TestPositionMm,
                    Vision = new VisionSettings(
                        MinBodyRectangularity,
                        ExpectedLeadCount,
                        MinMarkingPixels,
                        MaxMarkingPixels,
                        MaxDefectAreaPx),
                    MeasurementKind = MeasurementKind,
                    Limits = new ElectricalLimits(LowLimit, HighLimit),
                };

                updated = await _recipeStore.CreateWithSignOffAsync(newRecipe, currentUser.Id, SignOffReason.Trim(), cancellationToken);
                StatusMessage = $"Recipe '{updated.Name}' created successfully with electronic sign-off.";
            }
            else
            {
                var existingRecipe = new Recipe
                {
                    Id = SelectedRecipe!.Id,
                    Name = RecipeName.Trim(),
                    Version = SelectedRecipe.Version,
                    CameraPositionMm = CameraPositionMm,
                    TestPositionMm = TestPositionMm,
                    Vision = new VisionSettings(
                        MinBodyRectangularity,
                        ExpectedLeadCount,
                        MinMarkingPixels,
                        MaxMarkingPixels,
                        MaxDefectAreaPx),
                    MeasurementKind = MeasurementKind,
                    Limits = new ElectricalLimits(LowLimit, HighLimit),
                };

                updated = await _recipeStore.SaveWithSignOffAsync(existingRecipe, currentUser.Id, SignOffReason.Trim(), cancellationToken);
                StatusMessage = $"Recipe '{updated.Name}' updated to Version {updated.Version} with sign-off.";
            }

            IsSignOffDialogOpen = false;
            IsEditing = false;
            IsCreatingNew = false;
            SignOffPassword = string.Empty;
            SignOffReason = string.Empty;

            await LoadRecipesAsync(cancellationToken);
            SelectedRecipe = Recipes.FirstOrDefault(r => r.Id == updated.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void LoadFieldsFromRecipe(Recipe recipe)
    {
        RecipeName = recipe.Name;
        CameraPositionMm = recipe.CameraPositionMm;
        TestPositionMm = recipe.TestPositionMm;
        MinBodyRectangularity = recipe.Vision.MinBodyRectangularity;
        ExpectedLeadCount = recipe.Vision.ExpectedLeadCount;
        MinMarkingPixels = recipe.Vision.MinMarkingPixels;
        MaxMarkingPixels = recipe.Vision.MaxMarkingPixels;
        MaxDefectAreaPx = recipe.Vision.MaxDefectAreaPx;
        MeasurementKind = recipe.MeasurementKind;
        LowLimit = recipe.Limits.Low;
        HighLimit = recipe.Limits.High;
        Version = recipe.Version;
    }

    private void OnUserChanged()
    {
        CanEdit = _session.CurrentUser?.Role is UserRole.Engineer or UserRole.Admin;
        if (!CanEdit && (IsEditing || IsCreatingNew))
        {
            CancelEdit();
        }
    }
}
