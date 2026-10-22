using System.Diagnostics;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Seeding;
using PharmaFlow.DatabaseRelease;
using Testcontainers.PostgreSql;
using Xunit;
using ReleaseProgram = PharmaFlow.DatabaseRelease.Program;

namespace PharmaFlow.Api.Tests;

public sealed class PostReleaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    internal string AdminSettings => container!.GetConnectionString();
    public async Task InitializeAsync()
    {
        try
        {
            container = new PostgreSqlBuilder("postgres:18").WithDatabase("post_release_bootstrap")
                .WithUsername("bootstrap").WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
                .WithLabel("pharmaflow.test-infrastructure", "disposable").Build();
            await container.StartAsync();
        }
        catch { await DisposeAsync(); throw new InvalidOperationException("Disposable post-release fixture failed; details suppressed."); }
    }
    public async Task DisposeAsync() { if (container is not null) { await container.DisposeAsync(); container = null; } }
}

// Each test gets its own database and owner/runtime roles inside one disposable PostgreSQL 18 container.
// No application configuration, User Secrets or persistent Compose service is consulted.
internal sealed class PostReleaseState : IAsyncDisposable
{
    internal readonly string Database = "audit_" + Guid.NewGuid().ToString("N");
    internal readonly string Owner = "owner_" + Guid.NewGuid().ToString("N");
    internal readonly string Runtime = "reader_" + Guid.NewGuid().ToString("N");
    internal readonly string Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly NpgsqlConnection admin;
    private readonly string adminSettings;
    internal string OwnerSettings { get; private set; } = "";
    private PostReleaseState(PostReleaseFixture fixture)
    {
        adminSettings = fixture.AdminSettings;
        admin = new NpgsqlConnection(adminSettings);
    }
    internal static async Task<PostReleaseState> Create(PostReleaseFixture fixture)
    {
        var state = new PostReleaseState(fixture);
        try
        {
            await state.admin.OpenAsync();
            await state.AdminSql($"CREATE ROLE {state.Owner} LOGIN CREATEROLE PASSWORD " + ScramVerifier.SqlLiteral(ScramVerifier.Create(state.Password)));
            await state.AdminSql($"CREATE ROLE {state.Runtime} LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS");
            await state.AdminSql($"CREATE DATABASE {state.Database} OWNER {state.Owner}");
            state.OwnerSettings = new NpgsqlConnectionStringBuilder(state.adminSettings)
                { Database = state.Database, Username = state.Owner, Password = state.Password, Pooling = false }.ConnectionString;
            await using var context = new PharmaFlowDbContext(new DbContextOptionsBuilder<PharmaFlowDbContext>().UseNpgsql(state.OwnerSettings).Options);
            await context.Database.MigrateAsync();
            await new CanonicalDatabaseSeeder(context).SeedAsync();
            await using var connection = new NpgsqlConnection(state.OwnerSettings);
            await connection.OpenAsync();
            await RuntimePermissions.ApplyAndVerifyAsync(connection, state.Runtime, default);
            return state;
        }
        catch { await state.DisposeAsync(); throw new InvalidOperationException("Disposable audit state setup failed; details suppressed."); }
    }
    internal Dictionary<string, string?> Settings()
    {
        var parsed = new NpgsqlConnectionStringBuilder(OwnerSettings);
        return new()
        {
            ["ConnectionStrings:PharmaFlow"] = new NpgsqlConnectionStringBuilder(OwnerSettings) { Password = "" }.ConnectionString,
            ["Database:Password"] = Password,
            ["DatabaseRelease:ExpectedHost"] = parsed.Host,
            ["DatabaseRelease:ExpectedDatabase"] = Database,
            ["DatabaseRelease:ExpectedReleaseRole"] = Owner,
            ["DatabaseRelease:RuntimeRole"] = Runtime,
            ["RuntimeRoleProvisioning:Password"] = "ignored-provisioning-sensitive-marker",
            ["RuntimeRoleProvisioning:PasswordMode"] = "ignored-mode-sensitive-marker"
        };
    }
    internal async Task Sql(string sql)
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminSettings) { Database = Database, Pooling = false }.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql.Replace("{runtime}", Runtime, StringComparison.Ordinal)
            .Replace("{owner}", Owner, StringComparison.Ordinal).Replace("{database}", Database, StringComparison.Ordinal), connection);
        await command.ExecuteNonQueryAsync();
    }
    internal async Task<object?> Scalar(string sql)
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminSettings) { Database = Database, Pooling = false }.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql.Replace("{runtime}", Runtime, StringComparison.Ordinal), connection);
        return await command.ExecuteScalarAsync();
    }
    private async Task AdminSql(string sql) { await using var command = new NpgsqlCommand(sql, admin); await command.ExecuteNonQueryAsync(); }
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (admin.State == System.Data.ConnectionState.Open)
            {
                await AdminSql($"DROP DATABASE IF EXISTS {Database} WITH (FORCE)");
                await AdminSql($"DROP ROLE IF EXISTS {Runtime}; DROP ROLE IF EXISTS {Owner}");
            }
        }
        finally { await admin.DisposeAsync(); }
    }
}

public sealed class PostReleaseVerificationTests(PostReleaseFixture fixture) : IClassFixture<PostReleaseFixture>
{
    private static readonly string[] ExpectedChecks = """
        configuration_valid connection_succeeded target_identity_matches transaction_repeatable_read transaction_read_only
        initial_migration_exactly_once no_unexpected_migrations expected_tables_present no_unexpected_public_tables schema_constraints_valid
        medications_count_exact ingredients_count_exact pharmacy_branches_count_exact opening_intervals_count_exact availability_count_exact
        canonical_values_match relationships_valid runtime_role_exists runtime_attributes_safe runtime_memberships_safe runtime_no_ownership
        runtime_connect_allowed runtime_public_usage_allowed runtime_expected_select_allowed runtime_no_unexpected_select
        runtime_no_insert runtime_no_update runtime_no_delete runtime_no_truncate runtime_no_references runtime_no_trigger runtime_no_maintain
        runtime_no_create runtime_no_temporary runtime_no_sequence_permissions runtime_no_migration_history_access runtime_no_grant_options
        runtime_default_privileges_safe verification_complete
        """.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public async Task Complete_state_is_safe_via_actual_dispatch_and_runtime_is_never_authenticated()
    {
        await using var state = await PostReleaseState.Create(fixture);
        var result = await Verify(state.Settings());
        Assert.True(result.Code == 0, string.Join(",", result.Checks.Where(c => c.Value == "NO").Select(c => c.Key)));
        Assert.All(VerifyDatabaseReleaseCommand.CheckNames, key => Assert.Equal("YES", result.Checks[key]));
        Assert.Equal("SAFE", result.Checks["database_release_verification"]);
        Assert.Equal(0L, await state.Scalar("SELECT count(*) FROM pg_stat_activity WHERE usename='{runtime}'"));
        Assert.Equal(0L, await state.Scalar($"SELECT count(*) FROM pg_stat_activity WHERE usename='{state.Owner}'"));
    }

    [Theory]
    [InlineData("DELETE FROM availability; DELETE FROM medications", "medications_count_exact")]
    [InlineData("DELETE FROM medication_active_ingredients WHERE ctid=(SELECT ctid FROM medication_active_ingredients LIMIT 1)", "ingredients_count_exact")]
    [InlineData("DELETE FROM availability; DELETE FROM pharmacy_branches", "pharmacy_branches_count_exact")]
    [InlineData("DELETE FROM opening_intervals WHERE ctid=(SELECT ctid FROM opening_intervals LIMIT 1)", "opening_intervals_count_exact")]
    [InlineData("DELETE FROM availability WHERE ctid=(SELECT ctid FROM availability LIMIT 1)", "availability_count_exact")]
    public async Task Each_incorrect_count_is_unsafe(string mutation, string check)
    {
        await using var state = await PostReleaseState.Create(fixture);
        await state.Sql(mutation);
        var result = await Unsafe(state, check);
        Assert.Equal("YES", result["verification_complete"]);
    }

    [Theory]
    [InlineData("UPDATE medications SET display_name=display_name||'_drift'", "medications_count_exact")]
    [InlineData("UPDATE medication_active_ingredients SET ingredient=ingredient||'_drift'", "ingredients_count_exact")]
    [InlineData("UPDATE pharmacy_branches SET time_zone='UTC'", "pharmacy_branches_count_exact")]
    [InlineData("UPDATE opening_intervals SET opens_at=opens_at+interval '1 minute'", "opening_intervals_count_exact")]
    [InlineData("UPDATE availability SET updated_at=updated_at+interval '1 second'", "availability_count_exact")]
    [InlineData("UPDATE availability SET price_minor_units=0,price_currency_code='USD' WHERE price_minor_units IS NULL", "availability_count_exact")]
    [InlineData("UPDATE availability SET price_minor_units=price_minor_units+1 WHERE status='unavailable' AND price_minor_units IS NOT NULL", "availability_count_exact")]
    public async Task Correct_counts_do_not_hide_changed_values(string mutation, string countCheck)
    {
        await using var state = await PostReleaseState.Create(fixture);
        await state.Sql(mutation);
        var result = await Unsafe(state, "canonical_values_match");
        Assert.Equal("YES", result[countCheck]);
        Assert.Equal("YES", result["verification_complete"]);
    }

    [Fact]
    public async Task Orphaned_rows_and_missing_role_are_unsafe()
    {
        await using var state = await PostReleaseState.Create(fixture);
        await state.Sql("ALTER TABLE medication_active_ingredients DROP CONSTRAINT fk_medication_active_ingredients_medications; UPDATE medication_active_ingredients SET medication_id='orphan_marker' WHERE ctid=(SELECT ctid FROM medication_active_ingredients LIMIT 1)");
        var result = await Unsafe(state, "relationships_valid");
        Assert.Equal("NO", result["canonical_values_match"]);
        Assert.Equal("YES", result["ingredients_count_exact"]);
        await state.Sql("DROP OWNED BY {runtime}; DROP ROLE {runtime}");
        result = await Unsafe(state, "runtime_role_exists");
        Assert.Equal("YES", result["verification_complete"]);
    }

    [Theory]
    [InlineData("DELETE FROM \"__EFMigrationsHistory\"", "initial_migration_exactly_once")]
    [InlineData("INSERT INTO \"__EFMigrationsHistory\" VALUES ('unexpected_marker','10.0.12')", "no_unexpected_migrations")]
    [InlineData("ALTER TABLE \"__EFMigrationsHistory\" DROP CONSTRAINT \"PK___EFMigrationsHistory\"; INSERT INTO \"__EFMigrationsHistory\" SELECT * FROM \"__EFMigrationsHistory\"", "initial_migration_exactly_once")]
    [InlineData("DROP TABLE availability", "expected_tables_present")]
    [InlineData("CREATE TABLE unexpected_marker(id int)", "no_unexpected_public_tables")]
    [InlineData("ALTER TABLE availability DROP CONSTRAINT pk_availability", "schema_constraints_valid")]
    [InlineData("ALTER TABLE availability DROP CONSTRAINT fk_availability_medications", "schema_constraints_valid")]
    [InlineData("ALTER TABLE availability DROP CONSTRAINT fk_availability_medications; ALTER TABLE availability ADD CONSTRAINT fk_availability_medications FOREIGN KEY(medication_id) REFERENCES medications(id) ON DELETE CASCADE", "schema_constraints_valid")]
    [InlineData("DROP INDEX ux_medications_slug", "schema_constraints_valid")]
    [InlineData("DROP INDEX ix_availability_branch_id; CREATE INDEX ix_availability_branch_id ON availability(medication_id)", "schema_constraints_valid")]
    [InlineData("ALTER TABLE availability DROP CONSTRAINT ck_availability_price_nonnegative", "schema_constraints_valid")]
    [InlineData("ALTER TABLE availability DROP CONSTRAINT ck_availability_price_nonnegative; ALTER TABLE availability ADD CONSTRAINT ck_availability_price_nonnegative CHECK(price_minor_units IS NULL OR price_minor_units>=1)", "schema_constraints_valid")]
    [InlineData("ALTER TABLE availability DROP CONSTRAINT ck_availability_price_nonnegative; ALTER TABLE availability ADD CONSTRAINT ck_availability_price_nonnegative CHECK(price_minor_units IS NULL OR price_minor_units>=0) NOT VALID", "schema_constraints_valid")]
    [InlineData("ALTER TABLE medications ADD COLUMN unexpected_marker text", "schema_constraints_valid")]
    [InlineData("ALTER TABLE \"__EFMigrationsHistory\" DROP CONSTRAINT \"PK___EFMigrationsHistory\"", "schema_constraints_valid")]
    [InlineData("ALTER TABLE availability DROP CONSTRAINT pk_availability; ALTER TABLE availability ADD CONSTRAINT pk_availability PRIMARY KEY(branch_id,medication_id)", "schema_constraints_valid")]
    [InlineData("DROP INDEX ux_medications_slug; CREATE UNIQUE INDEX ux_medications_slug ON medications(slug) WHERE prescription_required OR NOT prescription_required", "schema_constraints_valid")]
    [InlineData("DROP INDEX ux_medications_slug; CREATE UNIQUE INDEX ux_medications_slug ON medications(slug COLLATE \"C\")", "schema_constraints_valid")]
    [InlineData("DROP INDEX ux_medications_slug; CREATE UNIQUE INDEX ux_medications_slug ON medications(slug text_pattern_ops)", "schema_constraints_valid")]
    [InlineData("ALTER TABLE medications ENABLE ROW LEVEL SECURITY", "schema_constraints_valid")]
    public async Task Migration_and_physical_schema_drift_is_unsafe(string mutation, string check)
    {
        await using var state = await PostReleaseState.Create(fixture);
        await state.Sql(mutation);
        var result = await Unsafe(state, check);
        if (mutation != "DROP TABLE availability") Assert.Equal("YES", result["verification_complete"]);
    }

    [Theory]
    [InlineData("REVOKE SELECT ON medications FROM {runtime}", "runtime_expected_select_allowed")]
    [InlineData("REVOKE CONNECT ON DATABASE {database} FROM PUBLIC,{runtime}", "runtime_connect_allowed")]
    [InlineData("REVOKE USAGE ON SCHEMA public FROM PUBLIC,{runtime}", "runtime_public_usage_allowed")]
    [InlineData("GRANT INSERT ON medications TO {runtime}", "runtime_no_insert")]
    [InlineData("GRANT UPDATE ON medications TO {runtime}", "runtime_no_update")]
    [InlineData("GRANT DELETE ON medications TO {runtime}", "runtime_no_delete")]
    [InlineData("GRANT TRUNCATE ON medications TO {runtime}", "runtime_no_truncate")]
    [InlineData("GRANT REFERENCES ON medications TO {runtime}", "runtime_no_references")]
    [InlineData("GRANT TRIGGER ON medications TO {runtime}", "runtime_no_trigger")]
    [InlineData("GRANT MAINTAIN ON medications TO {runtime}", "runtime_no_maintain")]
    [InlineData("GRANT TEMPORARY ON DATABASE {database} TO {runtime}", "runtime_no_temporary")]
    [InlineData("GRANT CREATE ON DATABASE {database} TO {runtime}", "runtime_no_create")]
    [InlineData("GRANT CREATE ON SCHEMA public TO {runtime}", "runtime_no_create")]
    [InlineData("CREATE SEQUENCE unexpected_sequence; GRANT USAGE ON SEQUENCE unexpected_sequence TO {runtime}", "runtime_no_sequence_permissions")]
    [InlineData("GRANT SELECT ON \"__EFMigrationsHistory\" TO {runtime}", "runtime_no_migration_history_access")]
    [InlineData("GRANT SELECT(\"MigrationId\") ON \"__EFMigrationsHistory\" TO {runtime}", "runtime_no_migration_history_access")]
    [InlineData("GRANT UPDATE(display_name) ON medications TO {runtime}", "runtime_no_update")]
    [InlineData("GRANT INSERT(display_name) ON medications TO {runtime}", "runtime_no_insert")]
    [InlineData("GRANT REFERENCES(id) ON medications TO {runtime}", "runtime_no_references")]
    [InlineData("GRANT SELECT ON medications TO {runtime} WITH GRANT OPTION", "runtime_no_grant_options")]
    [InlineData("GRANT SELECT(display_name) ON medications TO {runtime} WITH GRANT OPTION", "runtime_no_grant_options")]
    [InlineData("GRANT CONNECT ON DATABASE {database} TO {runtime} WITH GRANT OPTION", "runtime_no_grant_options")]
    [InlineData("GRANT USAGE ON SCHEMA public TO {runtime} WITH GRANT OPTION", "runtime_no_grant_options")]
    [InlineData("GRANT UPDATE ON medications TO PUBLIC", "runtime_no_update")]
    [InlineData("CREATE TABLE unexpected_marker(id int); GRANT SELECT ON unexpected_marker TO {runtime}", "runtime_no_unexpected_select")]
    [InlineData("CREATE TABLE unexpected_marker(id int); GRANT SELECT(id) ON unexpected_marker TO {runtime}", "runtime_no_unexpected_select")]
    [InlineData("CREATE SCHEMA other; CREATE TABLE other.unexpected_marker(id int); GRANT SELECT ON other.unexpected_marker TO {runtime}", "runtime_no_unexpected_select")]
    [InlineData("ALTER DEFAULT PRIVILEGES FOR ROLE {owner} GRANT SELECT ON TABLES TO {runtime}", "runtime_default_privileges_safe")]
    [InlineData("ALTER DEFAULT PRIVILEGES FOR ROLE {owner} IN SCHEMA public GRANT SELECT ON TABLES TO PUBLIC", "runtime_default_privileges_safe")]
    [InlineData("ALTER DEFAULT PRIVILEGES FOR ROLE bootstrap GRANT SELECT ON TABLES TO {runtime}", "runtime_default_privileges_safe")]
    [InlineData("ALTER DEFAULT PRIVILEGES FOR ROLE bootstrap IN SCHEMA public GRANT USAGE ON SEQUENCES TO PUBLIC", "runtime_default_privileges_safe")]
    [InlineData("CREATE SCHEMA other; ALTER DEFAULT PRIVILEGES FOR ROLE bootstrap IN SCHEMA other GRANT SELECT ON TABLES TO {runtime}", "runtime_default_privileges_safe")]
    [InlineData("GRANT {owner} TO {runtime}", "runtime_memberships_safe")]
    [InlineData("ALTER TABLE medications OWNER TO {runtime}; GRANT SELECT ON medications TO {owner}", "runtime_no_ownership")]
    [InlineData("CREATE SCHEMA owned AUTHORIZATION {runtime}", "runtime_no_ownership")]
    [InlineData("CREATE SEQUENCE owned_sequence; ALTER SEQUENCE owned_sequence OWNER TO {runtime}", "runtime_no_ownership")]
    [InlineData("ALTER ROLE {runtime} NOLOGIN", "runtime_attributes_safe")]
    [InlineData("ALTER ROLE {runtime} INHERIT", "runtime_attributes_safe")]
    [InlineData("ALTER ROLE {runtime} SUPERUSER", "runtime_attributes_safe")]
    [InlineData("ALTER ROLE {runtime} CREATEDB", "runtime_attributes_safe")]
    [InlineData("ALTER ROLE {runtime} CREATEROLE", "runtime_attributes_safe")]
    [InlineData("ALTER ROLE {runtime} REPLICATION", "runtime_attributes_safe")]
    [InlineData("ALTER ROLE {runtime} BYPASSRLS", "runtime_attributes_safe")]
    public async Task Each_permission_violation_is_unsafe_without_repair(string mutation, string check)
    {
        await using var state = await PostReleaseState.Create(fixture);
        await state.Sql(mutation);
        var result = await Unsafe(state, check);
        Assert.Equal("YES", result["verification_complete"]);
    }

    [Fact]
    public async Task Identity_mismatch_and_query_errors_fail_closed()
    {
        await using var state = await PostReleaseState.Create(fixture);
        var settings = state.Settings();
        await state.Sql("ALTER DATABASE {database} OWNER TO bootstrap");
        Assert.Equal("NO", (await Verify(settings)).Checks["target_identity_matches"]);
        await state.Sql("ALTER DATABASE {database} OWNER TO {owner}; REVOKE SELECT ON pg_catalog.pg_constraint FROM PUBLIC");
        var result = await Verify(settings);
        Assert.Equal("YES", result.Checks["target_identity_matches"]);
        Assert.Equal("NO", result.Checks["verification_complete"]);
        Assert.Equal("UNSAFE", result.Checks["database_release_verification"]);
    }

    [Fact]
    public async Task Read_only_repeatable_read_rejects_writes_and_preserves_snapshot()
    {
        await using var state = await PostReleaseState.Create(fixture);
        var before = await Snapshot(state);
        Assert.Equal(0, (await Verify(state.Settings())).Code);
        Assert.True(before == await Snapshot(state), "Verification must not change logical data/catalog state or relevant xmin.");
        await using var connection = new NpgsqlConnection(state.OwnerSettings);
        await connection.OpenAsync();
        await using var transaction = await VerifyRuntimeRoleCommand.BeginReadOnlyAsync(connection, default);
        await using (var modes = new NpgsqlCommand("SELECT current_setting('transaction_read_only')='on' AND current_setting('transaction_isolation')='repeatable read'", connection, transaction))
            Assert.Equal(true, await modes.ExecuteScalarAsync());
        await using var write = new NpgsqlCommand("UPDATE medications SET display_name=display_name", connection, transaction);
        var error = await Assert.ThrowsAsync<PostgresException>(() => write.ExecuteNonQueryAsync());
        Assert.Equal("25006", error.SqlState);
        await transaction.RollbackAsync();
        Assert.True(before == await Snapshot(state), "Rejected write harness must leave no changes.");
        await state.Sql("GRANT UPDATE ON medications TO {runtime}");
        before = await Snapshot(state);
        Assert.Equal(1, (await Verify(state.Settings())).Code);
        Assert.True(before == await Snapshot(state), "Unsafe verification must not repair or change state.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var messages = new List<string>();
        Assert.Equal(1, await VerifyDatabaseReleaseCommand.ExecuteAsync(Config(state.Settings()), messages.Add, canceled.Token));
        Assert.Contains("database_release_verification: UNSAFE", messages);
        Assert.True(before == await Snapshot(state), "Cancellation must leave state unchanged.");
    }

    [Fact]
    public async Task Query_timeout_and_cancellation_are_unsafe_and_do_not_repair()
    {
        await using var state = await PostReleaseState.Create(fixture);
        await using var blocker = new NpgsqlConnection(state.OwnerSettings);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("LOCK TABLE medications IN ACCESS EXCLUSIVE MODE", blocker, transaction))
            await command.ExecuteNonQueryAsync();
        var settings = state.Settings();
        settings["ConnectionStrings:PharmaFlow"] = new NpgsqlConnectionStringBuilder(settings["ConnectionStrings:PharmaFlow"])
            { CommandTimeout = 1 }.ConnectionString;
        var result = await Verify(settings);
        Assert.Equal("YES", result.Checks["target_identity_matches"]);
        Assert.Equal("NO", result.Checks["verification_complete"]);
        Assert.Equal("UNSAFE", result.Checks["database_release_verification"]);
        using var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var messages = new List<string>();
        Assert.Equal(1, await VerifyDatabaseReleaseCommand.ExecuteAsync(Config(state.Settings()), messages.Add, canceled.Token));
        Validate(messages, state.Settings());
        Assert.Contains("verification_complete: NO", messages);
        await transaction.RollbackAsync();
        Assert.Equal(0, (await Verify(state.Settings())).Code);
    }

    [Fact]
    public async Task Own_data_source_suppresses_SQL_traces_and_actual_console_output_is_allowlisted()
    {
        await using var state = await PostReleaseState.Create(fixture);
        using var parent = new Activity("PharmaFlow post-release verification isolation").Start();
        var activities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Npgsql", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId) activities.Enqueue(activity); }
        };
        ActivitySource.AddActivityListener(listener);
        // Positive control: the listener really sees a normal Npgsql command before the audit.
        await state.Scalar("SELECT 1");
        Assert.NotEmpty(activities);
        activities.Clear();
        Assert.Equal(0, (await Verify(state.Settings())).Code);
        Assert.Empty(activities);
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(typeof(ReleaseCommand).Assembly.Location);
        start.ArgumentList.Add("--verify-database-release");
        // Child receives only synthetic test configuration; no local env file is consulted.
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("Database", StringComparison.OrdinalIgnoreCase)
            || k.StartsWith("ConnectionStrings", StringComparison.OrdinalIgnoreCase) || k.StartsWith("RuntimeRoleProvisioning", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        foreach (var setting in state.Settings()) start.Environment[setting.Key.Replace(":", "__", StringComparison.Ordinal)] = setting.Value;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        Validate((await stdout).Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).ToList(), state.Settings());
        Assert.True(string.IsNullOrEmpty(await stderr), "No diagnostics or secret values may reach stderr.");
    }

    private static async Task<string> Snapshot(PostReleaseState state)
    {
        var sections = new List<string>();
        foreach (var table in ReleaseReadContract.Tables.Append("\"__EFMigrationsHistory\""))
            sections.Add((string)(await state.Scalar($"SELECT coalesce(jsonb_agg(jsonb_build_array(xmin::text,to_jsonb(t)) ORDER BY to_jsonb(t)::text),'[]'::jsonb)::text FROM public.{table} t"))!);
        sections.Add((string)(await state.Scalar("SELECT jsonb_agg(to_jsonb(t) ORDER BY oid)::text FROM pg_roles t"))!);
        foreach (var table in new[] { "pg_auth_members", "pg_database", "pg_namespace", "pg_class", "pg_attribute", "pg_constraint", "pg_index", "pg_default_acl" })
            sections.Add((string)(await state.Scalar($"SELECT coalesce(jsonb_agg(jsonb_build_array(xmin::text,to_jsonb(t)) ORDER BY to_jsonb(t)::text),'[]'::jsonb)::text FROM pg_catalog.{table} t"))!);
        return string.Join('\n', sections);
    }

    private static async Task<Dictionary<string,string>> Unsafe(PostReleaseState state, string check)
    {
        var result = await Verify(state.Settings());
        Assert.Equal(1, result.Code);
        Assert.Equal("UNSAFE", result.Checks["database_release_verification"]);
        Assert.Equal("NO", result.Checks[check]);
        return result.Checks;
    }
    internal static IConfiguration Config(Dictionary<string,string?> settings) => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    internal static async Task<(int Code, Dictionary<string,string> Checks)> Verify(Dictionary<string,string?> settings)
    {
        var messages = new List<string>();
        var code = await ReleaseProgram.ExecuteAsync(["--verify-database-release"], Config(settings), messages.Add);
        Validate(messages, settings);
        return (code, messages.ToDictionary(line => line.Split(": ")[0], line => line.Split(": ")[1]));
    }
    internal static void Validate(List<string> messages, Dictionary<string,string?> settings)
    {
        Assert.Equal(40, messages.Count);
        Assert.Equal(ExpectedChecks, messages.SkipLast(1).Select(line => line.Split(": ")[0]));
        Assert.All(messages.SkipLast(1), line => Assert.Matches("^[a-z_]+: (YES|NO)$", line));
        Assert.Matches("^database_release_verification: (SAFE|UNSAFE)$", messages[^1]);
        var output = string.Join('\n', messages);
        foreach (var value in settings.Values.Where(v => !string.IsNullOrWhiteSpace(v)))
            Assert.False(output.Contains(value!, StringComparison.Ordinal), "Output must not contain configuration values.");
        Assert.DoesNotContain("SCRAM-SHA-256", output);
    }
}

public sealed class PostReleaseVerificationOfflineTests
{
    [Theory]
    [InlineData("query")]
    [InlineData("rollback")]
    [InlineData("transaction_disposal")]
    [InlineData("connection_disposal")]
    [InlineData("source_disposal")]
    public async Task Late_query_or_cleanup_failure_cannot_publish_safe_or_exception_details(string phase)
    {
        var messages = new List<string>();
        var checks = VerifyDatabaseReleaseCommand.CheckNames.ToDictionary(k => k, _ => true);
        checks["verification_complete"] = false;
        var disposed = new List<string>();
        Assert.Equal(1, await VerifyDatabaseReleaseCommand.RunAuditAsync(checks, Audit, messages.Add));
        Assert.Equal(new[] { "transaction_disposal", "connection_disposal", "source_disposal" }, disposed);
        PostReleaseVerificationTests.Validate(messages, new() { ["synthetic"] = "sensitive-cleanup-marker" });
        Assert.Contains("verification_complete: NO", messages);
        Assert.Equal("database_release_verification: UNSAFE", messages[^1]);

        async Task Audit()
        {
            await using var source = new FaultResource("source_disposal", phase, disposed);
            await using var connection = new FaultResource("connection_disposal", phase, disposed);
            await using var transaction = new FaultResource("transaction_disposal", phase, disposed);
            await Task.Yield();
            if (phase is "query" or "rollback") throw new InvalidOperationException("sensitive-cleanup-marker");
        }
    }

    [Fact]
    public async Task Reporting_failure_is_nonzero_without_exposing_exception_or_retrying()
    {
        var calls = 0;
        Assert.Equal(1, await VerifyDatabaseReleaseCommand.RunAuditAsync(
            VerifyDatabaseReleaseCommand.CheckNames.ToDictionary(k => k, _ => true), () => Task.CompletedTask,
            _ => { calls++; throw new InvalidOperationException("sensitive-output-marker"); }));
        Assert.Equal(1, calls);
    }

    private sealed class FaultResource(string name, string failurePhase, List<string> disposed) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            disposed.Add(name);
            return name == failurePhase ? ValueTask.FromException(new InvalidOperationException("sensitive-cleanup-marker")) : ValueTask.CompletedTask;
        }
    }

    [Theory]
    [InlineData("ConnectionStrings:PharmaFlow", "Host=secret-marker;Port=invalid-marker")]
    [InlineData("DatabaseRelease:ExpectedHost", "invalid-host-marker")]
    [InlineData("DatabaseRelease:ExpectedDatabase", "invalid-database-marker")]
    [InlineData("DatabaseRelease:ExpectedReleaseRole", "invalid-owner-marker")]
    [InlineData("DatabaseRelease:RuntimeRole", "pg_reserved_marker")]
    [InlineData("Database:Password", "")]
    [InlineData("DatabaseRelease:RuntimeRole", "owner_marker")]
    public async Task Invalid_configuration_is_unsafe_before_network(string key, string value)
    {
        var settings = Settings(); settings[key] = value;
        var result = await PostReleaseVerificationTests.Verify(settings);
        Assert.Equal(1, result.Code);
        Assert.Equal("NO", result.Checks["configuration_valid"]);
        Assert.Equal("NO", result.Checks["connection_succeeded"]);
    }
    [Fact]
    public async Task Connection_error_is_sanitized_and_unsafe()
    {
        var result = await PostReleaseVerificationTests.Verify(Settings());
        Assert.Equal(1, result.Code);
        Assert.Equal("YES", result.Checks["configuration_valid"]);
        Assert.Equal("NO", result.Checks["connection_succeeded"]);
        Assert.Equal("NO", result.Checks["verification_complete"]);
    }
    [Theory]
    [InlineData("a IS NULL OR b IS NULL AND c >= 0", "(a IS NULL OR b IS NULL) AND c >= 0")]
    [InlineData("a>=0", "a>=1")]
    public void Check_normalization_preserves_semantics(string first, string second) => Assert.NotEqual(CheckExpression.Normalize(first), CheckExpression.Normalize(second));

    private static Dictionary<string,string?> Settings() => new()
    {
        ["ConnectionStrings:PharmaFlow"] = "Host=127.0.0.1;Port=1;Database=offline_marker;Username=owner_marker;Timeout=1",
        ["Database:Password"] = "offline-sensitive-owner-marker",
        ["DatabaseRelease:ExpectedHost"] = "127.0.0.1",
        ["DatabaseRelease:ExpectedDatabase"] = "offline_marker",
        ["DatabaseRelease:ExpectedReleaseRole"] = "owner_marker",
        ["DatabaseRelease:RuntimeRole"] = "runtime_marker"
    };
}
