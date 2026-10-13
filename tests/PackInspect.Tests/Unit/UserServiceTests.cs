using Microsoft.Extensions.DependencyInjection;
using PackInspect.Core.Audit;
using PackInspect.Core.Auth;
using PackInspect.Data;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

public sealed class UserServiceTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly TestTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _services;

    public UserServiceTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<TimeProvider>(_time)
            .AddPackInspectData(_folder.File("user_test.db"))
            .BuildServiceProvider();
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _services.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task Initialized_database_contains_seeded_roles()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var userService = _services.GetRequiredService<IUserService>();

        var users = await userService.GetAllUsersAsync(TestToken);

        Assert.Equal(3, users.Count);
        Assert.Contains(users, u => u.UserName == "admin" && u.Role == UserRole.Admin);
        Assert.Contains(users, u => u.UserName == "engineer" && u.Role == UserRole.Engineer);
        Assert.Contains(users, u => u.UserName == "operator" && u.Role == UserRole.Operator);
    }

    [Fact]
    public async Task Correct_credentials_authenticates_and_logs_audit_success()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var userService = _services.GetRequiredService<IUserService>();
        var auditStore = _services.GetRequiredService<IAuditStore>();

        var authResult = await userService.AuthenticateAsync("operator", "Operator123!", TestToken);

        Assert.True(authResult.IsSuccess);
        Assert.NotNull(authResult.User);
        Assert.Equal("operator", authResult.User.UserName);

        var auditEntries = await auditStore.GetRecentAsync(10, TestToken);
        Assert.Contains(auditEntries, e => e.Action == "LoginSuccess" && e.UserId == authResult.User.Id);
    }

    [Fact]
    public async Task Invalid_password_increments_failed_logins_and_audits_failure()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var userService = _services.GetRequiredService<IUserService>();
        var auditStore = _services.GetRequiredService<IAuditStore>();

        var authResult = await userService.AuthenticateAsync("operator", "WrongPassword!", TestToken);

        Assert.False(authResult.IsSuccess);
        Assert.Equal(AuthStatus.InvalidCredentials, authResult.Status);

        var users = await userService.GetAllUsersAsync(TestToken);
        var op = Assert.Single(users, u => u.UserName == "operator");
        Assert.Equal(1, op.FailedLogins);

        var auditEntries = await auditStore.GetRecentAsync(10, TestToken);
        Assert.Contains(auditEntries, e => e.Action == "LoginFailed" && e.UserId == op.Id);
    }

    [Fact]
    public async Task Five_consecutive_failed_logins_locks_the_account()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var userService = _services.GetRequiredService<IUserService>();

        for (int i = 0; i < 4; i++)
        {
            var result = await userService.AuthenticateAsync("operator", "WrongPassword!", TestToken);
            Assert.Equal(AuthStatus.InvalidCredentials, result.Status);
        }

        var fifthResult = await userService.AuthenticateAsync("operator", "WrongPassword!", TestToken);
        Assert.Equal(AuthStatus.LockedOut, fifthResult.Status);

        // Attempting with the correct password while still locked is rejected
        var lockedResult = await userService.AuthenticateAsync("operator", "Operator123!", TestToken);
        Assert.Equal(AuthStatus.LockedOut, lockedResult.Status);
    }

    [Fact]
    public async Task Expired_lockout_allows_login_with_correct_password()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var userService = _services.GetRequiredService<IUserService>();

        for (int i = 0; i < 5; i++)
        {
            await userService.AuthenticateAsync("operator", "WrongPassword!", TestToken);
        }

        // Advance simulated time past the 15-minute lockout window
        _time.Advance(TimeSpan.FromMinutes(16));

        var result = await userService.AuthenticateAsync("operator", "Operator123!", TestToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.User!.FailedLogins);
        Assert.Null(result.User.LockedUntilUtc);
    }

    [Fact]
    public async Task Admin_can_unlock_locked_user()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var userService = _services.GetRequiredService<IUserService>();

        var admin = (await userService.GetAllUsersAsync(TestToken)).Single(u => u.UserName == "admin");
        var op = (await userService.GetAllUsersAsync(TestToken)).Single(u => u.UserName == "operator");

        for (int i = 0; i < 5; i++)
        {
            await userService.AuthenticateAsync("operator", "WrongPassword!", TestToken);
        }

        await userService.UnlockUserAsync(op.Id, admin.Id, TestToken);

        var result = await userService.AuthenticateAsync("operator", "Operator123!", TestToken);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var userService = _services.GetRequiredService<IUserService>();

        var admin = (await userService.GetAllUsersAsync(TestToken)).Single(u => u.UserName == "admin");
        var op = (await userService.GetAllUsersAsync(TestToken)).Single(u => u.UserName == "operator");

        await userService.DeactivateUserAsync(op.Id, admin.Id, TestToken);

        var result = await userService.AuthenticateAsync("operator", "Operator123!", TestToken);
        Assert.Equal(AuthStatus.Deactivated, result.Status);
    }

    [Fact]
    public async Task Admin_can_create_new_user_with_hashed_password()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var userService = _services.GetRequiredService<IUserService>();
        var admin = (await userService.GetAllUsersAsync(TestToken)).Single(u => u.UserName == "admin");

        var created = await userService.CreateUserAsync("technician1", "Technician123!", UserRole.Operator, admin.Id, TestToken);

        Assert.Equal("technician1", created.UserName);
        Assert.Equal(UserRole.Operator, created.Role);

        var authResult = await userService.AuthenticateAsync("technician1", "Technician123!", TestToken);
        Assert.True(authResult.IsSuccess);
    }
}
