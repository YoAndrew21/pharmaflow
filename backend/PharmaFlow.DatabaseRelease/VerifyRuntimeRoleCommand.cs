using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PharmaFlow.DatabaseRelease;

internal static partial class VerifyRuntimeRoleCommand
{
    public static async Task<int> ExecuteAsync(IConfiguration configuration, Action<string> report, CancellationToken token = default)
    {
        var checks = new Dictionary<string, bool>
        {
            ["connection_succeeded"] = false,
            ["target_identity_matches"] = false,
            ["verification_complete"] = false
        };
        foreach (var name in RuntimeRoleSafetyEvaluator.CheckNames) checks[name] = false;
        var result = "UNSAFE";
        try
        {
            // Deliberately never read RuntimeRoleProvisioning:Password. It is not needed here.
            var settings = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("PharmaFlow"));
            var ownerPassword = configuration["Database:Password"];
            var role = configuration["DatabaseRelease:RuntimeRole"];
            var host = configuration["DatabaseRelease:ExpectedHost"];
            var database = configuration["DatabaseRelease:ExpectedDatabase"];
            var owner = configuration["DatabaseRelease:ExpectedReleaseRole"];
            if (string.IsNullOrWhiteSpace(ownerPassword) || string.IsNullOrWhiteSpace(role)
                || !RoleName().IsMatch(role) || role.StartsWith("pg_", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database)
                || string.IsNullOrWhiteSpace(owner) || role == owner
                || settings.Host != host || settings.Database != database || settings.Username != owner)
                throw new ReleaseSafetyException("invalid-verification-target");
            settings.Password = ownerPassword;
            await using (var connection = new NpgsqlConnection(settings.ConnectionString))
            {
                await connection.OpenAsync(token);
                checks["connection_succeeded"] = true;
                await using var transaction = await BeginReadOnlyAsync(connection, token);
                await using (var identity = new NpgsqlCommand("""
                    SELECT current_user=@owner AND current_database()=@database
                      AND pg_get_userbyid(d.datdba)=current_user AND (r.rolsuper OR r.rolcreaterole)
                    FROM pg_database d JOIN pg_roles r ON r.rolname=current_user
                    WHERE d.datname=current_database()
                    """, connection, transaction))
                {
                    identity.Parameters.AddWithValue("owner", owner);
                    identity.Parameters.AddWithValue("database", database);
                    if (!Equals(await identity.ExecuteScalarAsync(token), true))
                        throw new ReleaseSafetyException("verification-identity-mismatch");
                    checks["target_identity_matches"] = true;
                }
                var safety = await RuntimeRoleSafetyEvaluator.EvaluateAsync(connection, role, transaction, token);
                foreach (var check in safety.Checks) checks[check.Key] = check.Value;
                // No writes to commit. End the read-only transaction before reporting success.
                await transaction.RollbackAsync(token);
                result = !safety.Exists ? "NOT_FOUND" : safety.InitialSafe ? "SAFE" : "UNSAFE";
            }
            checks["verification_complete"] = true;
        }
        catch (Exception)
        {
            // No exception messages, SQL, identifiers, configuration or secrets reach output.
            checks["verification_complete"] = false;
            result = "UNSAFE";
        }
        foreach (var check in checks) report(check.Key + ": " + (check.Value ? "YES" : "NO"));
        report("runtime_role_verification: " + result);
        return result == "SAFE" ? 0 : result == "NOT_FOUND" ? 2 : 1;
    }

    internal static async Task<NpgsqlTransaction> BeginReadOnlyAsync(NpgsqlConnection connection, CancellationToken token)
    {
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        try
        {
            await using var mode = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction);
            await mode.ExecuteNonQueryAsync(token);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    [GeneratedRegex(@"\A[a-z][a-z0-9_]{0,62}\z", RegexOptions.CultureInvariant)]
    private static partial Regex RoleName();
}
