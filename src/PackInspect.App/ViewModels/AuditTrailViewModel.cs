using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackInspect.Core.Audit;

namespace PackInspect.App.ViewModels;

/// <summary>
/// Displays immutable audit entries for station operations (logins, recipe changes, user updates).
/// </summary>
public partial class AuditTrailViewModel(IAuditStore auditStore) : ObservableObject
{
    private const int MaxEntries = 100;

    public ObservableCollection<AuditEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            var list = await auditStore.GetRecentAsync(MaxEntries, cancellationToken);
            Entries.Clear();
            foreach (var item in list)
            {
                Entries.Add(item);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
