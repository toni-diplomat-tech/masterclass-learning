using System.Security.Cryptography;
using System.Text;

namespace MasterclassQuiz.Api.HostAuth;

/// <summary>
/// Hashes and checks the single Host's password. The hash is a self-describing string,
/// <c>pbkdf2-sha256.&lt;iterations&gt;.&lt;salt&gt;.&lt;key&gt;</c> (base64 parts, no <c>$</c> so it needs
/// no escaping in a <c>.env</c> file), set in the <c>HOST_PASSWORD_HASH</c> environment variable.
/// </summary>
public static class HostPassword
{
    private const string Algorithm = "pbkdf2-sha256";
    private const int Iterations = 600_000; // OWASP's recommendation for PBKDF2-HMAC-SHA256.
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Derive(password, salt, Iterations, KeyBytes);
        return $"{Algorithm}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    /// <summary>False for a wrong password and for a missing or malformed hash.</summary>
    public static bool Verify(string password, string? hash)
    {
        var parts = hash?.Split('.');
        if (parts is not [Algorithm, var iterationsText, var saltText, var keyText]
            || !int.TryParse(iterationsText, out var iterations) || iterations < 1)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(saltText);
            expected = Convert.FromBase64String(keyText);
        }
        catch (FormatException)
        {
            return false;
        }
        if (salt.Length == 0 || expected.Length == 0) return false;

        var actual = Derive(password, salt, iterations, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, length);
}
