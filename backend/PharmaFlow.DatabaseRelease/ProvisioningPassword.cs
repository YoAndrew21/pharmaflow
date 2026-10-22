using Npgsql;

namespace PharmaFlow.DatabaseRelease;

internal enum ProvisioningPasswordMode { ScramVerifier, NeonPlaintext }

internal static class ProvisioningPassword
{
    internal static ProvisioningPasswordMode ParseMode(string? value) => value switch
    {
        null or "ScramVerifier" => ProvisioningPasswordMode.ScramVerifier,
        "NeonPlaintext" => ProvisioningPasswordMode.NeonPlaintext,
        _ => throw new ReleaseSafetyException("invalid-provisioning-password-mode")
    };

    internal static void ConfigureConnection(NpgsqlConnectionStringBuilder settings, ProvisioningPasswordMode mode)
    {
        if (mode != ProvisioningPasswordMode.NeonPlaintext) return;
        if (settings.SslMode != SslMode.VerifyFull)
            throw new ReleaseSafetyException("neon-plaintext-requires-verify-full");
        settings.GssEncryptionMode = GssEncryptionMode.Disable;
    }

    internal static string Literal(string password, ProvisioningPasswordMode mode) => mode switch
    {
        ProvisioningPasswordMode.ScramVerifier => ScramVerifier.SqlLiteral(ScramVerifier.Create(password)),
        ProvisioningPasswordMode.NeonPlaintext => PlaintextLiteral(password),
        _ => throw new ReleaseSafetyException("invalid-provisioning-password-mode")
    };

    internal static string PlaintextLiteral(string password)
    {
        if (!ScramVerifier.ValidPassword(password)) throw new ReleaseSafetyException("invalid-provisioning-password");
        // Defense in depth: E-literal escaping remains correct even if the accepted alphabet
        // is broadened after review. Today both escape characters are rejected by policy.
        return "E'" + password.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal) + "'";
    }
}
