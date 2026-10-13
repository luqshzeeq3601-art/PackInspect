namespace PackInspect.Core.Auth;

public enum AuthStatus
{
    Success,
    InvalidCredentials,
    LockedOut,
    Deactivated,
}

public sealed record AuthResult(AuthStatus Status, User? User = null, string? ErrorMessage = null)
{
    public bool IsSuccess => Status == AuthStatus.Success;
}
