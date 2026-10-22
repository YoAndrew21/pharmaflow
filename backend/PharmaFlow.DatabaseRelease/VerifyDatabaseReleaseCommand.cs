using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PharmaFlow.Api.Persistence;

namespace PharmaFlow.DatabaseRelease;

internal static partial class VerifyDatabaseReleaseCommand
{
    internal static readonly string[] CheckNames =
    [
        "configuration_valid", "connection_succeeded", "target_identity_matches",
        "transaction_repeatable_read", "transaction_read_only", "initial_migration_exactly_once",
        "no_unexpected_migrations", "expected_tables_present", "no_unexpected_public_tables",
        "schema_constraints_valid", "medications_count_exact", "ingredients_count_exact",
        "pharmacy_branches_count_exact", "opening_intervals_count_exact", "availability_count_exact",
        "canonical_values_match", "relationships_valid", "runtime_role_exists", "runtime_attributes_safe",
        "runtime_memberships_safe", "runtime_no_ownership", "runtime_connect_allowed", "runtime_public_usage_allowed",
        "runtime_expected_select_allowed", "runtime_no_unexpected_select", "runtime_no_insert", "runtime_no_update",
        "runtime_no_delete", "runtime_no_truncate", "runtime_no_references", "runtime_no_trigger", "runtime_no_maintain",
        "runtime_no_create", "runtime_no_temporary", "runtime_no_sequence_permissions",
        "runtime_no_migration_history_access", "runtime_no_grant_options", "runtime_default_privileges_safe",
        "verification_complete"
    ];

    internal static async Task<int> ExecuteAsync(IConfiguration configuration, Action<string> report, CancellationToken token = default)
    {
        var checks = CheckNames.ToDictionary(name => name, _ => false);
        return await RunAuditAsync(checks, AuditAsync, report);

        async Task AuditAsync()
        {
            var settings = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("PharmaFlow"));
            var password = configuration["Database:Password"];
            var role = configuration["DatabaseRelease:RuntimeRole"];
            var host = configuration["DatabaseRelease:ExpectedHost"];
            var database = configuration["DatabaseRelease:ExpectedDatabase"];
            var owner = configuration["DatabaseRelease:ExpectedReleaseRole"];
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role)
                || !RoleName().IsMatch(role) || role.StartsWith("pg_", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database)
                || string.IsNullOrWhiteSpace(owner) || role == owner
                || settings.Host != host || settings.Database != database || settings.Username != owner)
                throw new ReleaseSafetyException("invalid-verification-target");
            settings.Password = password;
            // This action requires password authentication, never Kerberos. Avoid native GSS
            // fallback diagnostics outside our sanitized output; TLS settings remain untouched.
            settings.GssEncryptionMode = GssEncryptionMode.Disable;
            checks["configuration_valid"] = true;

            var builder = new NpgsqlDataSourceBuilder(settings.ConnectionString) { Name = "PharmaFlow.PostReleaseVerification" };
            builder.UseLoggerFactory(NullLoggerFactory.Instance).EnableParameterLogging(false);
            builder.ConfigureTracing(tracing => tracing.ConfigureCommandFilter(_ => false)
                .ConfigureBatchFilter(_ => false).EnablePhysicalOpenTracing(false));
            await using (var source = builder.Build())
            await using (var connection = source.CreateConnection())
            {
                await connection.OpenAsync(token);
                checks["connection_succeeded"] = true;
                await using var transaction = await VerifyRuntimeRoleCommand.BeginReadOnlyAsync(connection, token);
                await using (var modes = new NpgsqlCommand("SELECT current_setting('transaction_isolation')='repeatable read', current_setting('transaction_read_only')='on'", connection, transaction))
                await using (var reader = await modes.ExecuteReaderAsync(token))
                {
                    if (!await reader.ReadAsync(token)) throw new ReleaseSafetyException("missing-transaction-mode");
                    checks["transaction_repeatable_read"] = reader.GetBoolean(0);
                    checks["transaction_read_only"] = reader.GetBoolean(1);
                }
                if (!checks["transaction_repeatable_read"] || !checks["transaction_read_only"])
                    throw new ReleaseSafetyException("invalid-transaction-mode");
                await using (var identity = new NpgsqlCommand("""
                    SELECT current_user=@owner AND current_database()=@database
                      AND pg_get_userbyid(datdba)=current_user
                    FROM pg_database WHERE datname=current_database()
                    """, connection, transaction))
                {
                    identity.Parameters.AddWithValue("owner", owner);
                    identity.Parameters.AddWithValue("database", database);
                    checks["target_identity_matches"] = Equals(await identity.ExecuteScalarAsync(token), true);
                }
                if (!checks["target_identity_matches"]) throw new ReleaseSafetyException("identity-mismatch");
                await using var context = new PharmaFlowDbContext(new DbContextOptionsBuilder<PharmaFlowDbContext>()
                    .UseNpgsql(connection).UseLoggerFactory(NullLoggerFactory.Instance)
                    .EnableSensitiveDataLogging(false).Options);
                await PostReleaseSchema.VerifyAsync(connection, transaction, context, checks, token);
                await PostReleaseData.VerifyAsync(context, connection, transaction, checks, token);
                await PostReleasePermissions.VerifyAsync(connection, transaction, role, checks, token);
                await transaction.RollbackAsync(token);
            }
        }
    }

    // Small failure/output boundary also permits deterministic cleanup fault tests.
    // Audit returns only after every await-using (including source disposal) has finished.
    internal static async Task<int> RunAuditAsync(Dictionary<string, bool> checks, Func<Task> audit, Action<string> report)
    {
        try
        {
            await audit();
            checks["verification_complete"] = true;
        }
        catch (Exception)
        {
            // No exception details, SQLSTATE, identifiers, configuration or seed values reach output.
            checks["verification_complete"] = false;
        }
        var safe = checks.Values.All(value => value);
        try
        {
            foreach (var check in checks) report(check.Key + ": " + (check.Value ? "YES" : "NO"));
            report("database_release_verification: " + (safe ? "SAFE" : "UNSAFE"));
        }
        catch (Exception) { return 1; } // A broken output sink must not expose its exception or retry reporting.
        return safe ? 0 : 1;
    }

    [GeneratedRegex(@"\A[a-z][a-z0-9_]{0,62}\z", RegexOptions.CultureInvariant)]
    private static partial Regex RoleName();
}
