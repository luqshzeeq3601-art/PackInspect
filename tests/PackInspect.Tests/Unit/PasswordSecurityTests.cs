using PackInspect.Data.Auth;

namespace PackInspect.Tests.Unit;

public class PasswordSecurityTests
{
    [Fact]
    public void Hashing_password_produces_salt_hash_and_default_iterations()
    {
        var (hash, salt, iterations) = PasswordSecurity.HashPassword("TestPass123!", iterations: 10_000);

        Assert.NotNull(hash);
        Assert.NotNull(salt);
        Assert.True(hash.Length >= 32);
        Assert.True(salt.Length >= 16);
        Assert.Equal(10_000, iterations);
    }

    [Fact]
    public void Matching_password_verifies_successfully()
    {
        var (hash, salt, iterations) = PasswordSecurity.HashPassword("SecretEngine#1", iterations: 10_000);

        bool isValid = PasswordSecurity.VerifyPassword("SecretEngine#1", hash, salt, iterations);

        Assert.True(isValid);
    }

    [Fact]
    public void Wrong_password_fails_verification()
    {
        var (hash, salt, iterations) = PasswordSecurity.HashPassword("SecretEngine#1", iterations: 10_000);

        bool isValid = PasswordSecurity.VerifyPassword("WrongPassword!", hash, salt, iterations);

        Assert.False(isValid);
    }

    [Fact]
    public void Empty_password_fails_verification()
    {
        var (hash, salt, iterations) = PasswordSecurity.HashPassword("ValidPassword123!", iterations: 10_000);

        Assert.False(PasswordSecurity.VerifyPassword("", hash, salt, iterations));
        Assert.False(PasswordSecurity.VerifyPassword("   ", hash, salt, iterations));
    }

    [Fact]
    public void Each_hash_uses_a_unique_cryptographic_salt()
    {
        var (_, salt1, _) = PasswordSecurity.HashPassword("IdenticalPassword123!", iterations: 10_000);
        var (_, salt2, _) = PasswordSecurity.HashPassword("IdenticalPassword123!", iterations: 10_000);

        Assert.False(salt1.SequenceEqual(salt2));
    }
}
