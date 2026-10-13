namespace PackInspect.Core.Auth;

/// <summary>
/// In-memory session tracking the operator or engineer logged in at the HMI.
/// Thread-safe via a dedicated lock.
/// </summary>
public sealed class CurrentUserSession : IUserSession
{
    private readonly Lock _gate = new();
    private User? _currentUser;

    public event EventHandler? UserChanged;

    public User? CurrentUser
    {
        get
        {
            lock (_gate)
            {
                return _currentUser;
            }
        }
    }

    public void SetUser(User? user)
    {
        lock (_gate)
        {
            _currentUser = user;
        }

        UserChanged?.Invoke(this, EventArgs.Empty);
    }
}
