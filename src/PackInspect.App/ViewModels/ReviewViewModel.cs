using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackInspect.Core.Ai;
using PackInspect.Core.Domain;

namespace PackInspect.App.ViewModels;

/// <summary>
/// View model for the operator AI review screen, managing human-in-the-loop inspection
/// and tracking false calls and escapes.
/// </summary>
public partial class ReviewViewModel : ObservableObject
{
    private readonly IAiReviewStore _reviewStore;
    private readonly AuthViewModel _auth;
    private readonly IImageStore _images;
    private readonly SynchronizationContext _uiContext;

    public ReviewViewModel(IAiReviewStore reviewStore, AuthViewModel auth, IImageStore images)
    {
        _reviewStore = reviewStore;
        _auth = auth;
        _images = images;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
    }

    [ObservableProperty]
    public partial ObservableCollection<AiReviewRecord> PendingReviews { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    [NotifyCanExecuteChangedFor(nameof(OverrideCommand))]
    public partial AiReviewRecord? SelectedReview { get; set; }

    [ObservableProperty]
    public partial DefectType OverrideLabel { get; set; } = DefectType.None;

    [ObservableProperty]
    public partial AiReviewMetrics Metrics { get; set; } = AiReviewMetrics.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// The image file of the selected unit, so the operator judges what the camera
    /// saw. A path, not a WPF image: the view turns it into a picture.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedImageFile { get; set; }

    public bool HasSelectedReview => SelectedReview is not null;

    partial void OnSelectedReviewChanged(AiReviewRecord? value) =>
        SelectedImageFile = string.IsNullOrEmpty(value?.ImagePath) ? null : _images.GetFilePath(value.ImagePath);

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            var reviews = await _reviewStore.GetPendingReviewsAsync(lotId: null, CancellationToken.None);
            var metrics = await _reviewStore.GetMetricsAsync(lotId: null, CancellationToken.None);

            _uiContext.Post(_ =>
            {
                PendingReviews = new ObservableCollection<AiReviewRecord>(reviews);
                Metrics = metrics;
                if (SelectedReview is null || !PendingReviews.Any(r => r.Id == SelectedReview.Id))
                {
                    SelectedReview = PendingReviews.FirstOrDefault();
                }
            }, null);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load reviews: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedReview))]
    public async Task ConfirmAsync()
    {
        if (SelectedReview is null)
        {
            return;
        }

        // A review is signed by a person; it is never recorded under a default account.
        if (_auth.CurrentUser is not { Id: var userId })
        {
            ErrorMessage = "Log in before reviewing a unit.";
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            await _reviewStore.SubmitReviewAsync(
                SelectedReview.Id,
                operatorVerdict: "Confirmed",
                operatorLabel: null,
                reviewedByUserId: userId,
                reviewedAtUtc: DateTimeOffset.UtcNow,
                CancellationToken.None);

            StatusMessage = $"Unit #{SelectedReview.Sequence} confirmed as {SelectedReview.AiLabel}.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to confirm review: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedReview))]
    public async Task OverrideAsync()
    {
        if (SelectedReview is null)
        {
            return;
        }

        if (_auth.CurrentUser is not { Id: var userId })
        {
            ErrorMessage = "Log in before reviewing a unit.";
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            await _reviewStore.SubmitReviewAsync(
                SelectedReview.Id,
                operatorVerdict: "Overridden",
                operatorLabel: OverrideLabel,
                reviewedByUserId: userId,
                reviewedAtUtc: DateTimeOffset.UtcNow,
                CancellationToken.None);

            StatusMessage = $"Unit #{SelectedReview.Sequence} overridden to {OverrideLabel}.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to override review: {ex.Message}";
        }
    }
}
