using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PharmaFlow.DatabaseRelease;
using Testcontainers.PostgreSql;
using Xunit;

namespace PharmaFlow.Api.Tests;

public sealed class NeonProvisioningFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private readonly string certificate = Path.Combine(Path.GetTempPath(), "pharmaflow-provisioning-" + Guid.NewGuid().ToString("N") + ".crt");
    public string OwnerPassword { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public NpgsqlConnection Admin { get; private set; } = null!;
    public NpgsqlConnectionStringBuilder Owner { get; private set; } = new();

    public async Task InitializeAsync()
    {
        try
        {
            container = new PostgreSqlBuilder("postgres:18")
                .WithDatabase("neon_mode_test").WithUsername("bootstrap")
                .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
                .WithLabel("pharmaflow.test-infrastructure", "disposable").Build();
            await container.StartAsync();
            var generated = await container.ExecAsync(["sh", "-c", "openssl req -x509 -newkey rsa:2048 -nodes -keyout /tmp/pharmaflow.key -out /tmp/pharmaflow.crt -days 1 -subj /CN=localhost -addext 'subjectAltName=DNS:localhost,IP:127.0.0.1' >/dev/null 2>&1 && chown postgres:postgres /tmp/pharmaflow.key /tmp/pharmaflow.crt && chmod 600 /tmp/pharmaflow.key"]);
            if (generated.ExitCode != 0) throw new InvalidOperationException("Disposable TLS certificate generation failed.");
            var publicCertificate = await container.ExecAsync(["cat", "/tmp/pharmaflow.crt"]);
            if (publicCertificate.ExitCode != 0) throw new InvalidOperationException("Disposable TLS certificate export failed.");
            await File.WriteAllTextAsync(certificate, publicCertificate.Stdout);
            Admin = new NpgsqlConnection(container.GetConnectionString()); await Admin.OpenAsync();
            await Sql("ALTER SYSTEM SET ssl_cert_file='/tmp/pharmaflow.crt'");
            await Sql("ALTER SYSTEM SET ssl_key_file='/tmp/pharmaflow.key'");
            await Sql("ALTER SYSTEM SET ssl='on'");
            await Scalar("SELECT pg_reload_conf()");
            await Sql("CREATE ROLE mode_owner LOGIN CREATEROLE PASSWORD " + ScramVerifier.SqlLiteral(ScramVerifier.Create(OwnerPassword)));
            await Sql("ALTER DATABASE neon_mode_test OWNER TO mode_owner");
            Owner = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
            { Username = "mode_owner", Password = OwnerPassword, SslMode = SslMode.VerifyFull, RootCertificate = certificate, GssEncryptionMode = GssEncryptionMode.Disable };
            // Verify the real connection validates the certificate before exercising provisioning.
            await using var probe = new NpgsqlConnection(Owner.ConnectionString); await probe.OpenAsync();
        }
        catch (Exception error)
        {
            await DisposeAsync();
            throw new InvalidOperationException("Disposable Neon-mode fixture failed (" + error.GetType().Name + "); details suppressed.");
        }
    }
    public Dictionary<string, string?> Settings(string role, string? password, string? mode) => new()
    {
        ["ConnectionStrings:PharmaFlow"] = new NpgsqlConnectionStringBuilder(Owner.ConnectionString) { Password = "" }.ConnectionString,
        ["Database:Password"] = OwnerPassword,
        ["DatabaseRelease:ExpectedHost"] = Owner.Host,
        ["DatabaseRelease:ExpectedDatabase"] = Owner.Database,
        ["DatabaseRelease:ExpectedReleaseRole"] = Owner.Username,
        ["DatabaseRelease:RuntimeRole"] = role,
        ["RuntimeRoleProvisioning:Password"] = password,
        ["RuntimeRoleProvisioning:PasswordMode"] = mode
    };
    public async Task Sql(string sql) { await using var command = new NpgsqlCommand(sql, Admin); await command.ExecuteNonQueryAsync(); }
    public async Task<object?> Scalar(string sql) { await using var command = new NpgsqlCommand(sql, Admin); return await command.ExecuteScalarAsync(); }
    public async Task DisposeAsync()
    {
        try { if (Admin is not null) await Admin.DisposeAsync(); }
        finally
        {
            try { if (container is not null) { await container.DisposeAsync(); container = null; } }
            finally { if (File.Exists(certificate)) File.Delete(certificate); }
        }
    }
}

public sealed class NeonProvisioningTests(NeonProvisioningFixture fixture) : IClassFixture<NeonProvisioningFixture>
{
    [Theory]
    [InlineData(null)]
    [InlineData("ScramVerifier")]
    [InlineData("NeonPlaintext")]
    public async Task Both_modes_preserve_A_B_C_D_E_rollback_lock_and_authentication(string? mode)
    {
        var role = "mode_" + Guid.NewGuid().ToString("N");
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var messages = new ConcurrentQueue<string>();
        async Task<int> Provision(string target, string? secret)
        {
            var settings = fixture.Settings(target, secret, mode);
            return await ProvisionRuntimeRoleCommand.ExecuteAsync(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), messages.Enqueue);
        }
        Assert.Equal(1, await Provision(role, null)); // D
        Assert.Equal(1, await Provision(role, ""));
        Assert.Equal(1, await Provision(role, "invalid"));
        Assert.Equal(0, await Provision(role, password)); // A
        Assert.Equal(0, await Provision(role, null)); // B
        Assert.Equal(1, await Provision(role, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))); // E
        await using (var runtime = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(fixture.Owner.ConnectionString) { Username = role, Password = password }.ConnectionString))
        {
            await runtime.OpenAsync();
            await using var identity = new NpgsqlCommand("SELECT current_user = @role AND (SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid())", runtime);
            identity.Parameters.AddWithValue("role", role); Assert.Equal(true, await identity.ExecuteScalarAsync());
        }
        // Only a boolean crosses the test boundary: never select or print the verifier itself.
        await using (var storage = new NpgsqlCommand("SELECT rolpassword LIKE 'SCRAM-SHA-256$%' FROM pg_authid WHERE rolname=@role", fixture.Admin))
        { storage.Parameters.AddWithValue("role", role); Assert.Equal(true, await storage.ExecuteScalarAsync()); }
        await using (var owner = new NpgsqlConnection(fixture.Owner.ConnectionString))
        {
            await owner.OpenAsync();
            Assert.True((await RuntimeRoleSafetyEvaluator.EvaluateAsync(owner, role, null, default)).InitialSafe);
        }
        await fixture.Sql("ALTER ROLE " + role + " CREATEDB");
        Assert.Equal(1, await Provision(role, null)); // C, no repair
        Assert.Equal(true, await fixture.Scalar("SELECT rolcreatedb FROM pg_roles WHERE rolname='" + role + "'"));
        await fixture.Sql("ALTER ROLE " + role + " NOCREATEDB");
        var rolledBack = "rollback_" + Guid.NewGuid().ToString("N");
        await fixture.Sql("GRANT CREATE ON DATABASE neon_mode_test TO PUBLIC");
        try
        {
            Assert.Equal(1, await Provision(rolledBack, password));
            Assert.Equal(false, await fixture.Scalar("SELECT EXISTS(SELECT FROM pg_roles WHERE rolname='" + rolledBack + "')"));
        }
        finally { await fixture.Sql("REVOKE CREATE ON DATABASE neon_mode_test FROM PUBLIC"); }
        var concurrent = "concurrent_" + Guid.NewGuid().ToString("N");
        var outcomes = await Task.WhenAll(Provision(concurrent, password), Provision(concurrent, password));
        Assert.Equal(new[] { 0, 1 }, outcomes.Order().ToArray());
        Assert.Equal(0L, await fixture.Scalar("SELECT count(*) FROM pg_tables WHERE schemaname='public'"));
        Assert.All(messages, message => { Assert.False(message.Contains(password, StringComparison.Ordinal)); Assert.False(message.Contains(fixture.OwnerPassword, StringComparison.Ordinal)); Assert.DoesNotContain("SCRAM-SHA-256", message); });
    }

    [Fact]
    public async Task Neon_uses_a_real_advisory_transaction_lock()
    {
        var role = "locked_" + Guid.NewGuid().ToString("N");
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var owner = new NpgsqlConnection(fixture.Owner.ConnectionString); await owner.OpenAsync();
        await using var transaction = await owner.BeginTransactionAsync();
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended('pharmaflow-runtime-role:' || @role,0))", owner, transaction))
        { gate.Parameters.AddWithValue("role", role); await gate.ExecuteNonQueryAsync(); }
        var settings = fixture.Settings(role, password, "NeonPlaintext");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var output = new List<string>();
        Assert.Equal(1, await ProvisionRuntimeRoleCommand.ExecuteAsync(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), output.Add, timeout.Token));
        Assert.Equal(false, await fixture.Scalar("SELECT EXISTS(SELECT FROM pg_roles WHERE rolname='" + role + "')"));
        Assert.DoesNotContain(output, line => line.Contains("commit-outcome-unknown", StringComparison.Ordinal));
        await transaction.RollbackAsync();
    }

    [Theory]
    [InlineData("NeonPlaintext", SslMode.Require)]
    [InlineData("invalid-mode", SslMode.VerifyFull)]
    [InlineData("", SslMode.VerifyFull)]
    public async Task Invalid_mode_or_TLS_fails_before_connecting(string mode, SslMode ssl)
    {
        var settings = fixture.Settings("missing_reader", new string('x', 48), mode);
        settings["ConnectionStrings:PharmaFlow"] = new NpgsqlConnectionStringBuilder(fixture.Owner.ConnectionString) { Password = "", Port = 1, SslMode = ssl }.ConnectionString;
        var output = new List<string>();
        Assert.Equal(1, await ProvisionRuntimeRoleCommand.ExecuteAsync(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), output.Add));
        Assert.Contains(mode == "NeonPlaintext" ? "neon-plaintext-requires-verify-full" : "invalid-provisioning-password-mode", output.Single());
    }

    [Fact]
    public async Task TLS_errors_do_not_trigger_any_password_mode_fallback()
    {
        var settings = fixture.Settings("must_not_exist", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), "NeonPlaintext");
        settings["ConnectionStrings:PharmaFlow"] = new NpgsqlConnectionStringBuilder(fixture.Owner.ConnectionString) { Password = "", RootCertificate = "missing-synthetic-certificate.crt" }.ConnectionString;
        var output = new List<string>();
        Assert.Equal(1, await ProvisionRuntimeRoleCommand.ExecuteAsync(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), output.Add));
        Assert.Equal(false, await fixture.Scalar("SELECT EXISTS(SELECT FROM pg_roles WHERE rolname='must_not_exist')"));
        Assert.Single(output); Assert.DoesNotContain("commit-outcome-unknown", output[0]);
    }

    [Fact]
    public async Task Synthetic_password_never_reaches_report_output_or_Npgsql_tracing()
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var role = "trace_" + Guid.NewGuid().ToString("N");
        using var parent = new Activity("PharmaFlow provisioning trace isolation").Start();
        var activities = new ConcurrentQueue<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Npgsql", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId) activities.Enqueue(activity.DisplayName + string.Join(";", activity.TagObjects.Select(tag => tag.Key + "=" + tag.Value))); }
        };
        ActivitySource.AddActivityListener(listener);
        // Positive control: this listener sees ordinary Npgsql commands in this async trace.
        await using (var control = new NpgsqlConnection(fixture.Owner.ConnectionString))
        {
            await control.OpenAsync();
            await using var query = new NpgsqlCommand("SELECT 1", control);
            await query.ExecuteScalarAsync();
        }
        Assert.NotEmpty(activities);
        activities.Clear();
        using var stdout = new StringWriter();
        // Capture only this command's report sink; do not replace process-global Console streams
        // while unrelated xUnit classes run in parallel. All command SQL tracing is disabled.
        var settings = fixture.Settings(role, password, "NeonPlaintext");
        Assert.Equal(0, await ProvisionRuntimeRoleCommand.ExecuteAsync(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), stdout.WriteLine));
        Assert.False(stdout.ToString().Contains(password, StringComparison.Ordinal));
        Assert.DoesNotContain(activities, line => line.Contains(password, StringComparison.Ordinal));
        Assert.DoesNotContain(activities, line => line.Contains("CREATE ROLE", StringComparison.Ordinal));
        Assert.DoesNotContain(activities, line => line.Contains("SCRAM-SHA-256", StringComparison.Ordinal));
        Assert.Empty(activities);
    }
    [Fact]
    public async Task Real_child_process_stdout_stderr_and_sanitized_errors_never_expose_secrets()
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var role = "process_" + Guid.NewGuid().ToString("N");
        var settings = fixture.Settings(role, password, "NeonPlaintext");
        async Task<int> Run()
        {
            var start = new ProcessStartInfo("dotnet")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add(typeof(ProvisionRuntimeRoleCommand).Assembly.Location);
            start.ArgumentList.Add("--provision-runtime-role");
            foreach (var item in settings) if (item.Value is not null) start.Environment[item.Key.Replace(":", "__", StringComparison.Ordinal)] = item.Value;
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var captured = (await stdout) + (await stderr);
            Assert.False(captured.Contains(password, StringComparison.Ordinal), "Child output must not disclose runtime password.");
            Assert.False(captured.Contains(fixture.OwnerPassword, StringComparison.Ordinal), "Child output must not disclose owner password.");
            Assert.DoesNotContain("SCRAM-SHA-256", captured);
            Assert.DoesNotContain("CREATE ROLE", captured);
            return process.ExitCode;
        }
        Assert.Equal(0, await Run());
        Assert.Equal(1, await Run()); // Existing role plus password fails without exposing values.
    }
}
