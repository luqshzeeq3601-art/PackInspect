namespace PackInspect.Core.Auth;

public interface IUserService
{
    Task<AuthResult> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken);

    Task<bool> VerifyPasswordAsync(int userId, string password, CancellationToken cancellationToken);

    Task<User> CreateUserAsync(string userName, string password, UserRole role, int actorUserId, CancellationToken cancellationToken);

    Task DeactivateUserAsync(int userId, int actorUserId, CancellationToken cancellationToken);

    Task UnlockUserAsync(int userId, int actorUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> GetAllUsersAsync(CancellationToken cancellationToken);
}
