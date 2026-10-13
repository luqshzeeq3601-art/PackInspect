using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackInspect.Core.Auth;

namespace PackInspect.App.ViewModels;

/// <summary>
/// Admin console for creating users, deactivating accounts and unlocking locked accounts.
/// </summary>
public partial class UserManagementViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly IUserSession _session;

    public UserManagementViewModel(IUserService userService, IUserSession session)
    {
        _userService = userService;
        _session = session;

        _session.UserChanged += (_, _) => OnUserChanged();
        OnUserChanged();
    }

    public ObservableCollection<User> Users { get; } = [];

    [ObservableProperty]
    public partial bool CanManage { get; private set; }

    [ObservableProperty]
    public partial User? SelectedUser { get; set; }

    [ObservableProperty]
    public partial string NewUserName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial UserRole NewRole { get; set; } = UserRole.Operator;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        var list = await _userService.GetAllUsersAsync(cancellationToken);
        Users.Clear();
        foreach (var user in list)
        {
            Users.Add(user);
        }
    }

    [RelayCommand]
    private async Task CreateUserAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;

        var actor = _session.CurrentUser;
        if (actor is null || actor.Role != UserRole.Admin)
        {
            ErrorMessage = "Only an administrator may create users.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewUserName))
        {
            ErrorMessage = "Username is required.";
            return;
        }

        if (string.IsNullOrEmpty(NewPassword) || NewPassword.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters.";
            return;
        }

        try
        {
            var created = await _userService.CreateUserAsync(NewUserName.Trim(), NewPassword, NewRole, actor.Id, cancellationToken);
            StatusMessage = $"User '{created.UserName}' created with role {created.Role}.";
            NewUserName = string.Empty;
            NewPassword = string.Empty;
            await RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeactivateUserAsync(CancellationToken cancellationToken)
    {
        if (SelectedUser is null)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        var actor = _session.CurrentUser;
        if (actor is null || actor.Role != UserRole.Admin)
        {
            ErrorMessage = "Only an administrator may deactivate users.";
            return;
        }

        if (SelectedUser.Id == actor.Id)
        {
            ErrorMessage = "You cannot deactivate your own account.";
            return;
        }

        try
        {
            await _userService.DeactivateUserAsync(SelectedUser.Id, actor.Id, cancellationToken);
            StatusMessage = $"User '{SelectedUser.UserName}' has been deactivated.";
            await RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task UnlockUserAsync(CancellationToken cancellationToken)
    {
        if (SelectedUser is null)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        var actor = _session.CurrentUser;
        if (actor is null || actor.Role != UserRole.Admin)
        {
            ErrorMessage = "Only an administrator may unlock accounts.";
            return;
        }

        try
        {
            await _userService.UnlockUserAsync(SelectedUser.Id, actor.Id, cancellationToken);
            StatusMessage = $"User '{SelectedUser.UserName}' unlocked.";
            await RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void OnUserChanged()
    {
        CanManage = _session.CurrentUser?.Role == UserRole.Admin;
    }
}
