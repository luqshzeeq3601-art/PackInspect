using System.Security.Cryptography;

namespace PackInspect.Data.Auth;

/// <summary>
/// Cryptographic password hashing adhering to OWASP recommendations:
/// PBKDF2 with HMAC-SHA256, 16-byte random salt, 600,000 iterations default,
/// and constant-time comparison to prevent timing side-channels.
/// </summary>
public static class PasswordSecurity
{
    public const int DefaultIterations = 600_000;
    private const int SaltByteLength = 16;
    private const int HashByteLength = 32;

    public static (byte[] Hash, byte[] Salt, int Iterations) HashPassword(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltByteLength);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashByteLength);

        return (hash, salt, iterations);
    }

    public static bool VerifyPassword(string password, byte[] hash, byte[] salt, int iterations)
    {
        if (string.IsNullOrEmpty(password) || hash.Length == 0 || salt.Length == 0 || iterations <= 0)
        {
            return false;
        }

        byte[] computed = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            hash.Length);

        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }
}
