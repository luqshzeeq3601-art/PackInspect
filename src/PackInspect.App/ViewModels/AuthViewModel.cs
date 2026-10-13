using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackInspect.Core.Auth;

namespace PackInspect.App.ViewModels;

/// <summary>
/// Manages user login, session state and role privileges for the station UI.
/// Passwords are not retained in memory longer than the login attempt.
/// </summary>
public partial class AuthViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly IUserSession _session;
    private readonly SynchronizationContext _uiContext;

    public AuthViewModel(IUserService userService, IUserSession session)
    {
        _userService = userService;
        _session = session;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _session.UserChanged += (_, _) => _uiContext.Post(_ => RefreshSession(), null);
        RefreshSession();
    }

    [ObservableProperty]
    public partial User? CurrentUser { get; private set; }

    [ObservableProperty]
    public partial bool IsAuthenticated { get; private set; }

    [ObservableProperty]
    public partial bool IsOperator { get; private set; }

    [ObservableProperty]
    public partial bool IsEngineer { get; private set; }

    [ObservableProperty]
    public partial bool IsAdmin { get; private set; }

    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    // The station starts at the login dialog: nobody is signed in automatically.
    [ObservableProperty]
    public partial bool IsLoginDialogOpen { get; set; } = true;

    [RelayCommand]
    private void OpenLoginDialog()
    {
        ErrorMessage = null;
        IsLoginDialogOpen = true;
    }

    [RelayCommand]
    private void CloseLoginDialog()
    {
        IsLoginDialogOpen = false;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        var result = await _userService.AuthenticateAsync(UserName, Password, cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage ?? "Login failed.";
            return;
        }

        _session.SetUser(result.User);
        Password = string.Empty;
        IsLoginDialogOpen = false;
    }

    [RelayCommand]
    private void Logout()
    {
        _session.SetUser(null);
    }

    private void RefreshSession()
    {
        CurrentUser = _session.CurrentUser;
        IsAuthenticated = _session.IsAuthenticated;
        IsOperator = _session.CurrentUser?.Role == UserRole.Operator;
        IsEngineer = _session.CurrentUser?.Role is UserRole.Engineer or UserRole.Admin;
        IsAdmin = _session.CurrentUser?.Role == UserRole.Admin;
    }
}
