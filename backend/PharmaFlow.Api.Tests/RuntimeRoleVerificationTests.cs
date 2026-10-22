using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PharmaFlow.DatabaseRelease;
using Testcontainers.PostgreSql;
using Xunit;
using ReleaseProgram = PharmaFlow.DatabaseRelease.Program;

namespace PharmaFlow.Api.Tests;

public sealed class RuntimeRoleVerificationFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    public NpgsqlConnectionStringBuilder Owner { get; private set; } = new();
    public string OwnerPassword { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public NpgsqlConnection Admin { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            container = new PostgreSqlBuilder("postgres:18")
                .WithDatabase("verification_test").WithUsername("bootstrap")
                .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
                .WithLabel("pharmaflow.test-infrastructure", "disposable").Build();
            await container.StartAsync();
            Admin = new NpgsqlConnection(container.GetConnectionString());
            await Admin.OpenAsync();
            await Sql("CREATE ROLE verification_owner LOGIN CREATEROLE PASSWORD " + ScramVerifier.SqlLiteral(ScramVerifier.Create(OwnerPassword)));
            await Sql("ALTER DATABASE verification_test OWNER TO verification_owner");
            await Sql("CREATE TABLE public.medications(id integer PRIMARY KEY, label text); INSERT INTO public.medications VALUES(1,'fictional'); CREATE SEQUENCE public.observation_sequence");
            await Sql("CREATE TABLE public.\"__EFMigrationsHistory\"(\"MigrationId\" text PRIMARY KEY, \"ProductVersion\" text); INSERT INTO public.\"__EFMigrationsHistory\" VALUES ('verification_sentinel','fixture')");
            Owner = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Username = "verification_owner", Password = OwnerPassword };
        }
        catch (Exception error)
        {
            await DisposeAsync();
            throw new InvalidOperationException("Disposable verifier fixture failed (" + error.GetType().Name + "); details suppressed.");
        }
    }

    public Dictionary<string, string?> Settings(string role) => new()
    {
        ["ConnectionStrings:PharmaFlow"] = new NpgsqlConnectionStringBuilder(Owner.ConnectionString) { Password = "" }.ConnectionString,
        ["Database:Password"] = OwnerPassword,
        ["DatabaseRelease:ExpectedHost"] = Owner.Host,
        ["DatabaseRelease:ExpectedDatabase"] = Owner.Database,
        ["DatabaseRelease:ExpectedReleaseRole"] = Owner.Username,
        ["DatabaseRelease:RuntimeRole"] = role
    };

    public async Task<string> CreateRole()
    {
        var role = "verify_" + Guid.NewGuid().ToString("N");
        await Sql("CREATE ROLE " + role + " LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS");
        return role;
    }

    public async Task Sql(string sql) { await using var command = new NpgsqlCommand(sql, Admin); await command.ExecuteNonQueryAsync(); }
    public async Task<object?> Scalar(string sql) { await using var command = new NpgsqlCommand(sql, Admin); return await command.ExecuteScalarAsync(); }
    public async Task DisposeAsync()
    {
        try { if (Admin is not null) await Admin.DisposeAsync(); }
        finally { if (container is not null) { await container.DisposeAsync(); container = null; } }
    }
}

public sealed class RuntimeRoleVerificationTests(RuntimeRoleVerificationFixture fixture) : IClassFixture<RuntimeRoleVerificationFixture>
{
    private static readonly string[] OutputChecks =
    [
        "connection_succeeded", "target_identity_matches", "verification_complete",
        "runtime_role_exists", "runtime_login", "runtime_noinherit", "runtime_no_superuser",
        "runtime_no_createdb", "runtime_no_createrole", "runtime_no_replication", "runtime_no_bypassrls",
        "runtime_no_direct_memberships", "runtime_no_database_ownership", "runtime_no_schema_ownership",
        "runtime_no_relation_ownership", "runtime_no_effective_database_create", "runtime_no_effective_schema_create",
        "runtime_no_incompatible_table_privileges", "runtime_no_incompatible_column_privileges",
        "runtime_no_incompatible_sequence_privileges", "runtime_no_application_data_privileges"
    ];

    [Fact]
    public async Task Shared_evaluator_runs_in_the_actual_read_only_transaction()
    {
        var role = await fixture.CreateRole();
        await using var connection = new NpgsqlConnection(fixture.Owner.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await VerifyRuntimeRoleCommand.BeginReadOnlyAsync(connection, default);
        var safety = await RuntimeRoleSafetyEvaluator.EvaluateAsync(connection, role, transaction, default);
        Assert.True(safety.InitialSafe);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task A_missing_role_is_not_found_only_after_confirmed_target()
    {
        var result = await Verify(fixture.Settings("missing_" + Guid.NewGuid().ToString("N")));
        Assert.Equal(2, result.Code);
        Assert.Equal("NOT_FOUND", result.Checks["runtime_role_verification"]);
        Assert.Equal("YES", result.Checks["verification_complete"]);
        Assert.Equal("YES", result.Checks["target_identity_matches"]);
        Assert.Equal("NO", result.Checks["runtime_role_exists"]);
    }

    [Fact]
    public async Task B_safe_role_is_safe_and_provisioning_password_is_completely_ignored()
    {
        var role = await fixture.CreateRole();
        var settings = fixture.Settings(role);
        var result = await Verify(settings);
        Assert.Equal(0, result.Code);
        Assert.Equal("SAFE", result.Checks["runtime_role_verification"]);
        Assert.All(OutputChecks, name => Assert.Equal("YES", result.Checks[name]));
        settings["RuntimeRoleProvisioning:Password"] = "ignored-sensitive-marker-invalid-policy";
        Assert.Equal(result.Checks, (await Verify(settings)).Checks);
        settings["RuntimeRoleProvisioning:Password"] = "";
        Assert.Equal(0, (await Verify(settings)).Code);
        Assert.Equal(0L, await fixture.Scalar("SELECT count(*) FROM pg_stat_activity WHERE usename='" + role + "'"));
    }

    [Theory]
    [InlineData("NOLOGIN", "runtime_login")]
    [InlineData("INHERIT", "runtime_noinherit")]
    [InlineData("SUPERUSER", "runtime_no_superuser")]
    [InlineData("CREATEDB", "runtime_no_createdb")]
    [InlineData("CREATEROLE", "runtime_no_createrole")]
    [InlineData("REPLICATION", "runtime_no_replication")]
    [InlineData("BYPASSRLS", "runtime_no_bypassrls")]
    public async Task C_D_E_each_attribute_is_reported_independently(string attribute, string check)
    {
        var role = await fixture.CreateRole();
        await fixture.Sql("ALTER ROLE " + role + " " + attribute);
        await Unsafe(role, check);
    }

    [Theory]
    [InlineData("membership", "runtime_no_direct_memberships")]
    [InlineData("database", "runtime_no_database_ownership")]
    [InlineData("schema", "runtime_no_schema_ownership")]
    [InlineData("relation", "runtime_no_relation_ownership")]
    [InlineData("database_create", "runtime_no_effective_database_create")]
    [InlineData("schema_create", "runtime_no_effective_schema_create")]
    [InlineData("table", "runtime_no_incompatible_table_privileges")]
    [InlineData("column", "runtime_no_incompatible_column_privileges")]
    [InlineData("sequence", "runtime_no_incompatible_sequence_privileges")]
    [InlineData("select", "runtime_no_application_data_privileges")]
    [InlineData("select_column", "runtime_no_application_data_privileges")]
    [InlineData("grant_option", "runtime_no_incompatible_table_privileges")]
    public async Task F_through_O_membership_ownership_and_effective_rights_are_unsafe(string category, string check)
    {
        var role = await fixture.CreateRole();
        var sql = category switch
        {
            "membership" => "GRANT verification_owner TO " + role,
            "database" => "CREATE DATABASE " + role + " OWNER " + role,
            "schema" => "CREATE SCHEMA " + role + " AUTHORIZATION " + role,
            "relation" => "CREATE TABLE public." + role + "(id int); ALTER TABLE public." + role + " OWNER TO " + role,
            "database_create" => "GRANT CREATE ON DATABASE verification_test TO " + role,
            "schema_create" => "GRANT CREATE ON SCHEMA public TO " + role,
            "table" => "GRANT INSERT ON public.medications TO " + role,
            "column" => "GRANT UPDATE(label) ON public.medications TO " + role,
            "sequence" => "GRANT USAGE ON SEQUENCE public.observation_sequence TO " + role,
            "select" => "GRANT SELECT ON public.medications TO " + role,
            "select_column" => "GRANT SELECT(label) ON public.medications TO " + role,
            "grant_option" => "GRANT SELECT ON public.medications TO " + role + " WITH GRANT OPTION",
            _ => throw new InvalidOperationException()
        };
        await fixture.Sql(sql);
        await Unsafe(role, check);
    }

    [Fact]
    public async Task PUBLIC_derived_create_and_data_rights_are_detected_without_repair()
    {
        var role = await fixture.CreateRole();
        try
        {
            await fixture.Sql("GRANT CREATE ON DATABASE verification_test TO PUBLIC; GRANT SELECT ON public.medications TO PUBLIC");
            var result = await Verify(fixture.Settings(role));
            Assert.Equal("UNSAFE", result.Checks["runtime_role_verification"]);
            Assert.Equal("NO", result.Checks["runtime_no_effective_database_create"]);
            Assert.Equal("NO", result.Checks["runtime_no_application_data_privileges"]);
            Assert.Equal(true, await fixture.Scalar("SELECT has_database_privilege('public','verification_test','CREATE')"));
        }
        finally { await fixture.Sql("REVOKE CREATE ON DATABASE verification_test FROM PUBLIC; REVOKE SELECT ON public.medications FROM PUBLIC"); }
    }

    [Theory]
    [InlineData("DatabaseRelease:ExpectedHost", "wrong-host-marker")]
    [InlineData("DatabaseRelease:ExpectedDatabase", "wrong-database-marker")]
    [InlineData("DatabaseRelease:ExpectedReleaseRole", "wrong-owner-marker")]
    [InlineData("DatabaseRelease:RuntimeRole", "pg_reserved")]
    public async Task P_invalid_explicit_target_never_becomes_not_found(string key, string value)
    {
        var settings = fixture.Settings("missing_reader"); settings[key] = value;
        var result = await Verify(settings);
        Assert.Equal("UNSAFE", result.Checks["runtime_role_verification"]);
        Assert.Equal("NO", result.Checks["connection_succeeded"]);
        Assert.Equal("NO", result.Checks["verification_complete"]);
    }

    [Fact]
    public async Task P_real_identity_or_owner_capability_mismatch_never_becomes_not_found()
    {
        var settings = fixture.Settings("missing_reader");
        await fixture.Sql("ALTER ROLE verification_owner NOCREATEROLE");
        try
        {
            var result = await Verify(settings);
            Assert.Equal("YES", result.Checks["connection_succeeded"]);
            Assert.Equal("NO", result.Checks["target_identity_matches"]);
            Assert.Equal("UNSAFE", result.Checks["runtime_role_verification"]);
        }
        finally { await fixture.Sql("ALTER ROLE verification_owner CREATEROLE"); }
    }

    [Fact]
    public async Task P_actual_nonowner_login_is_not_accepted_as_owner()
    {
        var impostor = "impostor_" + Guid.NewGuid().ToString("N");
        await fixture.Sql("CREATE ROLE " + impostor + " LOGIN CREATEROLE PASSWORD " + ScramVerifier.SqlLiteral(ScramVerifier.Create(fixture.OwnerPassword)));
        var settings = fixture.Settings("missing_reader");
        settings["ConnectionStrings:PharmaFlow"] = new NpgsqlConnectionStringBuilder(fixture.Owner.ConnectionString) { Username = impostor, Password = "" }.ConnectionString;
        settings["DatabaseRelease:ExpectedReleaseRole"] = impostor;
        var result = await Verify(settings);
        Assert.Equal("YES", result.Checks["connection_succeeded"]);
        Assert.Equal("NO", result.Checks["target_identity_matches"]);
        Assert.Equal("NO", result.Checks["verification_complete"]);
        Assert.Equal("UNSAFE", result.Checks["runtime_role_verification"]);
    }

    [Fact]
    public async Task SQL_failure_during_catalog_evaluation_is_incomplete_unsafe_never_not_found()
    {
        var role = await fixture.CreateRole();
        await fixture.Sql("REVOKE SELECT ON pg_catalog.pg_class FROM PUBLIC");
        try
        {
            var result = await Verify(fixture.Settings(role));
            Assert.Equal("YES", result.Checks["target_identity_matches"]);
            Assert.Equal("NO", result.Checks["verification_complete"]);
            Assert.Equal("UNSAFE", result.Checks["runtime_role_verification"]);
        }
        finally { await fixture.Sql("GRANT SELECT ON pg_catalog.pg_class TO PUBLIC"); }
    }

    [Fact]
    public async Task R_transaction_is_read_only_repeatable_read_and_rejects_writes()
    {
        await using var connection = new NpgsqlConnection(fixture.Owner.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await VerifyRuntimeRoleCommand.BeginReadOnlyAsync(connection, default);
        await using (var modes = new NpgsqlCommand("SELECT current_setting('transaction_read_only'),current_setting('transaction_isolation')", connection, transaction))
        await using (var reader = await modes.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync()); Assert.Equal("on", reader.GetString(0)); Assert.Equal("repeatable read", reader.GetString(1));
        }
        await using var write = new NpgsqlCommand("CREATE TABLE public.must_not_exist(id int)", connection, transaction);
        var error = await Assert.ThrowsAsync<PostgresException>(() => write.ExecuteNonQueryAsync());
        Assert.Equal("25006", error.SqlState);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task S_success_and_failure_leave_catalogs_ACLs_data_xmin_and_history_unchanged()
    {
        var role = await fixture.CreateRole();
        var before = await Snapshot();
        Assert.Equal(0, (await Verify(fixture.Settings(role))).Code);
        Assert.Equal(2, (await Verify(fixture.Settings("missing_snapshot_reader"))).Code);
        await using (var connection = new NpgsqlConnection(fixture.Owner.ConnectionString))
        {
            await connection.OpenAsync();
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            var messages = new List<string>();
            Assert.Equal(1, await VerifyRuntimeRoleCommand.ExecuteAsync(new ConfigurationBuilder().AddInMemoryCollection(fixture.Settings(role)).Build(), messages.Add, canceled.Token));
            Assert.Equal("runtime_role_verification: UNSAFE", messages[^1]);
        }
        Assert.True(before == await Snapshot(), "Verification must not change the catalog/data snapshot.");
        Assert.Equal("verification_sentinel", await fixture.Scalar("SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\""));
    }

    private async Task<string> Snapshot()
    {
        // pg_roles exposes masked passwords; never inspect pg_authid or stored verifiers.
        const string sql = """
            SELECT jsonb_build_object(
              'roles',(SELECT jsonb_agg(to_jsonb(r) ORDER BY oid) FROM pg_roles r),
              'members',(SELECT jsonb_agg(to_jsonb(m) ORDER BY oid) FROM pg_auth_members m),
              'databases',(SELECT jsonb_agg(to_jsonb(d) ORDER BY oid) FROM pg_database d),
              'schemas',(SELECT jsonb_agg(to_jsonb(n) ORDER BY oid) FROM pg_namespace n),
              'relations',(SELECT jsonb_agg(to_jsonb(c) ORDER BY oid) FROM pg_class c),
              'defaults',(SELECT jsonb_agg(to_jsonb(a) ORDER BY oid) FROM pg_default_acl a),
              'columns',(SELECT jsonb_agg(to_jsonb(a) ORDER BY attrelid,attnum) FROM pg_attribute a),
              'history',(SELECT jsonb_agg(jsonb_build_array(xmin::text,to_jsonb(h))) FROM public."__EFMigrationsHistory" h),
              'data',(SELECT jsonb_agg(jsonb_build_array(xmin::text,id,label) ORDER BY id) FROM public.medications)
            )::text
            """;
        return (string)(await fixture.Scalar(sql))!;
    }

    private async Task Unsafe(string role, string failedCheck)
    {
        var result = await Verify(fixture.Settings(role));
        Assert.Equal(1, result.Code); Assert.Equal("UNSAFE", result.Checks["runtime_role_verification"]);
        Assert.Equal("YES", result.Checks["verification_complete"]); Assert.Equal("NO", result.Checks[failedCheck]);
    }

    private static async Task<(int Code, Dictionary<string, string> Checks)> Verify(Dictionary<string, string?> settings)
    {
        var messages = new List<string>();
        var code = await ReleaseProgram.ExecuteAsync(["--verify-runtime-role"], new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), messages.Add);
        ValidateOutput(messages, settings);
        return (code, messages.ToDictionary(line => line.Split(": ")[0], line => line.Split(": ")[1]));
    }

    internal static void ValidateOutput(List<string> messages, Dictionary<string, string?> settings)
    {
        Assert.Equal(22, messages.Count);
        Assert.Equal(OutputChecks, messages.Take(21).Select(line => line.Split(": ")[0]).ToArray());
        Assert.All(messages.Take(21), line => Assert.Matches("^[a-z_]+: (YES|NO)$", line));
        Assert.Matches("^runtime_role_verification: (SAFE|UNSAFE|NOT_FOUND)$", messages[^1]);
        var output = string.Join('\n', messages);
        foreach (var value in settings.Values.Where(value => !string.IsNullOrWhiteSpace(value)))
            Assert.False(output.Contains(value!, StringComparison.Ordinal), "Verification output must not contain configuration values.");
        Assert.DoesNotContain("SCRAM-SHA-256", output); Assert.DoesNotContain("Exception", output);
    }
}

public sealed class RuntimeRoleVerificationOfflineTests
{
    [Theory]
    [InlineData("Host=127.0.0.1;Port=1;Database=offline_marker;Username=owner_marker;Timeout=1")]
    [InlineData("Host=secret-host-marker;Port=invalid-sensitive-marker")]
    public async Task Q_connection_and_parser_errors_are_sanitized_unsafe(string baseConnection)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:PharmaFlow"] = baseConnection,
            ["Database:Password"] = "offline-owner-sensitive-marker",
            ["DatabaseRelease:ExpectedHost"] = "127.0.0.1",
            ["DatabaseRelease:ExpectedDatabase"] = "offline_marker",
            ["DatabaseRelease:ExpectedReleaseRole"] = "owner_marker",
            ["DatabaseRelease:RuntimeRole"] = "runtime_marker",
            ["RuntimeRoleProvisioning:Password"] = "ignored-runtime-sensitive-marker"
        };
        var messages = new List<string>();
        Assert.Equal(1, await ReleaseProgram.ExecuteAsync(["--verify-runtime-role"], new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), messages.Add));
        RuntimeRoleVerificationTests.ValidateOutput(messages, settings);
        Assert.Equal("runtime_role_verification: UNSAFE", messages[^1]);
        Assert.Contains("verification_complete: NO", messages);
        Assert.DoesNotContain("runtime_role_verification: NOT_FOUND", messages);
    }
}
