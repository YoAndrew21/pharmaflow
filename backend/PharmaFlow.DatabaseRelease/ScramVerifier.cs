using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PharmaFlow.DatabaseRelease;

public static partial class ScramVerifier
{
    public static bool ValidPassword(string password) => password.Length >= 48
        && password.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    public static string Create(string password)
    {
        if (!ValidPassword(password)) throw new ReleaseSafetyException("invalid-provisioning-password");
        return Derive(password, RandomNumberGenerator.GetBytes(16), 16384);
    }

    internal static string Derive(string password, byte[] salt, int iterations)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var salted = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA256, 32);
        var client = HMACSHA256.HashData(salted, "Client Key"u8);
        var stored = SHA256.HashData(client);
        var server = HMACSHA256.HashData(salted, "Server Key"u8);
        try
        {
            return $"SCRAM-SHA-256${iterations}:{Convert.ToBase64String(salt)}${Convert.ToBase64String(stored)}:{Convert.ToBase64String(server)}";
        }
        finally { CryptographicOperations.ZeroMemory(passwordBytes); CryptographicOperations.ZeroMemory(stored); CryptographicOperations.ZeroMemory(salted); CryptographicOperations.ZeroMemory(client); CryptographicOperations.ZeroMemory(server); }
    }

    public static string SqlLiteral(string verifier)
    {
        if (!Format().IsMatch(verifier)) throw new ReleaseSafetyException("invalid-scram-verifier");
        return "'" + verifier.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    [GeneratedRegex(@"\ASCRAM-SHA-256\$16384:[A-Za-z0-9+/]{22}==\$[A-Za-z0-9+/]{43}=:[A-Za-z0-9+/]{43}=\z", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
