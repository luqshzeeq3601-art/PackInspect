namespace PackInspect.Core.Auth;

/// <summary>
/// Session state holding the currently authenticated user at the station.
/// </summary>
public interface IUserSession
{
    User? CurrentUser { get; }

    bool IsAuthenticated => CurrentUser is not null;

    bool IsInRole(UserRole role) => CurrentUser is not null && CurrentUser.Role == role;

    event EventHandler? UserChanged;

    void SetUser(User? user);
}
