using Microsoft.Extensions.Configuration;
using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;

namespace PharmaFlow.DatabaseRelease;

public static partial class ProvisionRuntimeRoleCommand
{
    public static async Task<int> ExecuteAsync(IConfiguration configuration, Action<string> report, CancellationToken token = default)
    {
        NpgsqlDataSource? source = null;
        NpgsqlConnection? connection = null;
        NpgsqlTransaction? transaction = null;
        var exists = false;
        var outcome = await ProvisioningExecution.RunAsync(PrepareAsync,
            () => transaction!.CommitAsync(token),
            () => transaction?.DisposeAsync() ?? ValueTask.CompletedTask,
            DisposeConnectionAsync);
        return outcome.Publish(report, exists ? "Runtime role verified; unchanged." : "Runtime role provisioned and verified.").ExitCode;

        async Task PrepareAsync()
        {
            var settings = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("PharmaFlow"));
            var ownerPassword = configuration["Database:Password"];
            var role = configuration["DatabaseRelease:RuntimeRole"];
            var secret = configuration["RuntimeRoleProvisioning:Password"];
            var expectedOwner = configuration["DatabaseRelease:ExpectedReleaseRole"];
            if (string.IsNullOrWhiteSpace(ownerPassword) || string.IsNullOrWhiteSpace(role)
                || !RoleName().IsMatch(role) || role.StartsWith("pg_", StringComparison.Ordinal)
                || role == expectedOwner || string.IsNullOrWhiteSpace(expectedOwner)
                || string.IsNullOrWhiteSpace(configuration["DatabaseRelease:ExpectedHost"])
                || string.IsNullOrWhiteSpace(configuration["DatabaseRelease:ExpectedDatabase"])
                || settings.Host != configuration["DatabaseRelease:ExpectedHost"]
                || settings.Database != configuration["DatabaseRelease:ExpectedDatabase"] || settings.Username != expectedOwner)
                throw new ReleaseSafetyException("explicit-target-and-valid-distinct-role-required");
            if (secret is not null && !ScramVerifier.ValidPassword(secret))
                throw new ReleaseSafetyException("invalid-provisioning-password");
            var mode = ProvisioningPassword.ParseMode(configuration["RuntimeRoleProvisioning:PasswordMode"]);
            ProvisioningPassword.ConfigureConnection(settings, mode);
            settings.Password = ownerPassword;
            var sourceBuilder = new NpgsqlDataSourceBuilder(settings.ConnectionString) { Name = "PharmaFlow.RuntimeRoleProvisioning" };
            sourceBuilder.UseLoggerFactory(NullLoggerFactory.Instance).EnableParameterLogging(false);
            sourceBuilder.ConfigureTracing(tracing => tracing.ConfigureCommandFilter(_ => false)
                .ConfigureBatchFilter(_ => false).EnablePhysicalOpenTracing(false));
            source = sourceBuilder.Build();
            connection = source.CreateConnection();
            await connection.OpenAsync(token);
            await using (var identity = new NpgsqlCommand("""
                SELECT current_user = @owner AND current_database() = @database
                    AND pg_get_userbyid(d.datdba) = current_user AND (r.rolsuper OR r.rolcreaterole)
                FROM pg_database d JOIN pg_roles r ON r.rolname = current_user
                WHERE d.datname = current_database()
                """, connection))
            {
                identity.Parameters.AddWithValue("owner", expectedOwner);
                identity.Parameters.AddWithValue("database", settings.Database!);
                if (!Equals(await identity.ExecuteScalarAsync(token), true))
                    throw new ReleaseSafetyException("confirmed-owner-with-createrole-required");
            }
            transaction = await connection.BeginTransactionAsync(token);
            await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended('pharmaflow-runtime-role:' || @role, 0))", connection, transaction))
            {
                gate.Parameters.AddWithValue("role", role);
                await gate.ExecuteNonQueryAsync(token);
            }
            await using (var lookup = new NpgsqlCommand("SELECT EXISTS(SELECT FROM pg_roles WHERE rolname = @role)", connection, transaction))
            {
                lookup.Parameters.AddWithValue("role", role);
                exists = Equals(await lookup.ExecuteScalarAsync(token), true);
            }
            if (exists && secret is not null) throw new ReleaseSafetyException("existing-role-rejects-provisioning-password");
            if (!exists)
            {
                if (secret is null) throw new ReleaseSafetyException("missing-provisioning-password");
                if (mode == ProvisioningPasswordMode.NeonPlaintext)
                {
                    await using var encryption = new NpgsqlCommand("SET LOCAL password_encryption = 'scram-sha-256'", connection, transaction);
                    await encryption.ExecuteNonQueryAsync(token);
                }
                using var identifiers = new NpgsqlCommandBuilder();
                var sql = "CREATE ROLE " + identifiers.QuoteIdentifier(role)
                    + " LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT PASSWORD "
                    + ProvisioningPassword.Literal(secret, mode);
                await using var create = new NpgsqlCommand(sql, connection, transaction);
                await create.ExecuteNonQueryAsync(token);
            }
            var safety = await RuntimeRoleSafetyEvaluator.EvaluateAsync(connection, role, transaction, token);
            if (!safety.BasePreflightSafe)
                throw new ReleaseSafetyException("runtime-role-must-exist-without-ownership-elevated-flags-or-memberships");
            if (!safety.NoCreate)
                throw new ReleaseSafetyException("runtime-effective-create-privileges-forbidden");
            if (exists && !safety.ExistingDataSafe)
                throw new ReleaseSafetyException("existing-runtime-role-has-incompatible-data-privileges");
            if (!exists && !safety.Checks["runtime_no_application_data_privileges"])
                throw new ReleaseSafetyException("new-runtime-role-has-effective-data-privileges");
        }

        async ValueTask DisposeConnectionAsync()
        {
            Exception? primary = null;
            try { if (connection is not null) await connection.DisposeAsync(); }
            catch (Exception error) { primary = error; }
            try { if (source is not null) await source.DisposeAsync(); }
            catch (Exception error) { primary ??= error; }
            if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }

    [GeneratedRegex(@"\A[a-z][a-z0-9_]{0,62}\z", RegexOptions.CultureInvariant)]
    private static partial Regex RoleName();
}
