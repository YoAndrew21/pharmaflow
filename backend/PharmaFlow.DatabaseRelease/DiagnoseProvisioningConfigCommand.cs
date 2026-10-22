using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.RegularExpressions;

namespace PharmaFlow.DatabaseRelease;

// Temporary local-only diagnostic. Never construct a connection or forward exception details.
internal static partial class DiagnoseProvisioningConfigCommand
{
    public static int Execute(IConfiguration configuration, Action<string> report)
    {
        var ownerPassword = configuration["Database:Password"];
        var role = configuration["DatabaseRelease:RuntimeRole"];
        var owner = configuration["DatabaseRelease:ExpectedReleaseRole"];
        var host = configuration["DatabaseRelease:ExpectedHost"];
        var database = configuration["DatabaseRelease:ExpectedDatabase"];
        var password = configuration["RuntimeRoleProvisioning:Password"];
        var connectionString = configuration.GetConnectionString("PharmaFlow");
        var present = !string.IsNullOrWhiteSpace(connectionString);
        NpgsqlConnectionStringBuilder? parsed = null;
        if (present)
        {
            try { parsed = new NpgsqlConnectionStringBuilder(connectionString); }
            catch (Exception) { /* Deliberately suppress all parser details and values. */ }
        }

        var modeValue = configuration["RuntimeRoleProvisioning:PasswordMode"];
        ProvisioningPasswordMode? mode = null;
        try { mode = ProvisioningPassword.ParseMode(modeValue); }
        catch (ReleaseSafetyException) { /* Only booleans may leave this diagnostic. */ }
        var neonConnectionValid = false;
        var neonGssDisabled = false;
        if (mode == ProvisioningPasswordMode.NeonPlaintext && parsed is not null)
        {
            var effective = new NpgsqlConnectionStringBuilder(parsed.ConnectionString);
            try
            {
                ProvisioningPassword.ConfigureConnection(effective, mode.Value);
                neonConnectionValid = true;
                neonGssDisabled = effective.GssEncryptionMode == GssEncryptionMode.Disable;
            }
            catch (ReleaseSafetyException) { /* Suppress configuration failure details. */ }
        }

        Write("owner_password_present_nonblank", !string.IsNullOrWhiteSpace(ownerPassword));
        Write("runtime_role_present_nonblank", !string.IsNullOrWhiteSpace(role));
        Write("runtime_role_pattern_valid", role is not null && RoleName().IsMatch(role));
        Write("runtime_role_reserved_prefix", role?.StartsWith("pg_", StringComparison.Ordinal) == true);
        Write("runtime_equals_owner", role == owner);
        Write("expected_owner_present_nonblank", !string.IsNullOrWhiteSpace(owner));
        Write("expected_host_present_nonblank", !string.IsNullOrWhiteSpace(host));
        Write("expected_database_present_nonblank", !string.IsNullOrWhiteSpace(database));
        Write("connection_string_present", present);
        Write("connection_string_parse_ok", parsed is not null);
        Write("parsed_host_matches_expected", parsed is not null && parsed.Host == host);
        Write("parsed_database_matches_expected", parsed is not null && parsed.Database == database);
        Write("parsed_username_matches_expected", parsed is not null && parsed.Username == owner);
        Write("provisioning_password_present", password is not null);
        Write("provisioning_password_policy_valid", password is not null && ScramVerifier.ValidPassword(password));
        Write("provisioning_password_mode_present", modeValue is not null);
        Write("provisioning_password_mode_valid", mode is not null);
        Write("provisioning_password_mode_neon_plaintext", mode == ProvisioningPasswordMode.NeonPlaintext);
        Write("neon_verify_full_valid", neonConnectionValid);
        Write("neon_gss_disabled_effective", neonGssDisabled);
        return 0;

        void Write(string name, bool result) => report(name + ": " + (result ? "YES" : "NO"));
    }

    // Mirrors the current provisioning policy without changing provisioning behavior.
    [GeneratedRegex(@"\A[a-z][a-z0-9_]{0,62}\z", RegexOptions.CultureInvariant)]
    private static partial Regex RoleName();
}
