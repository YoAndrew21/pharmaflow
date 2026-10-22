using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PharmaFlow.DatabaseRelease;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace PharmaFlow.Api.Tests;

public sealed class RuntimeRoleProvisioningTests
{
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUV!")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUV ")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVé")]
    public void Invalid_passwords_are_rejected(string password) => Assert.False(ScramVerifier.ValidPassword(password));

    [Fact]
    public void Scram_matches_RFC7677_vector_and_strict_literal_rejects_injection()
    {
        // Public RFC 7677 test vector; not a database credential.
        var actual = ScramVerifier.Derive("pencil", Convert.FromBase64String("W22ZaJ0SNY7soEsUEjb6gQ=="), 4096);
        var authMessage = "n=user,r=rOprNGfwEbeRWgbNEkqO,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096,c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0";
        var serverKey = Convert.FromBase64String(actual.Split(':')[2]);
        Assert.Equal("6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=", Convert.ToBase64String(HMACSHA256.HashData(serverKey, System.Text.Encoding.UTF8.GetBytes(authMessage))));
        Assert.ThrowsAny<Exception>(() => ScramVerifier.SqlLiteral("' ; SELECT 1; --"));
        var generated = ScramVerifier.Create(new string('A', 48));
        Assert.StartsWith("'SCRAM-SHA-256$16384:", ScramVerifier.SqlLiteral(generated));
        Assert.NotEqual(generated, ScramVerifier.Create(new string('A', 48)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-test-password")]
    public async Task Release_rejects_provisioning_secret_without_connecting(string secret)
    {
        var messages = new List<string>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["RuntimeRoleProvisioning:Password"] = secret }).Build();
        Assert.Equal(1, await ReleaseCommand.ExecuteAsync(["--release-database"], configuration, messages.Add));
        Assert.Contains("release-rejects-provisioning-password", Assert.Single(messages));
    }

    [Fact]
    public async Task Disposable_postgres_proves_bootstrap_contracts_and_runtime_permissions()
    {
        var bootstrapPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var container = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("provisioning_test").WithUsername("bootstrap").WithPassword(bootstrapPassword)
            .WithLabel("pharmaflow.test-infrastructure", "disposable").Build();
        await container.StartAsync();
        await using var admin = new NpgsqlConnection(container.GetConnectionString());
        await admin.OpenAsync();
        var ownerPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await Sql(admin, "CREATE ROLE release_owner LOGIN CREATEROLE PASSWORD " + ScramVerifier.SqlLiteral(ScramVerifier.Create(ownerPassword)));
        await Sql(admin, "ALTER DATABASE provisioning_test OWNER TO release_owner");
        var settings = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Username = "release_owner", Password = ownerPassword };
        await using var owner = new NpgsqlConnection(settings.ConnectionString);
        await owner.OpenAsync();
        var messages = new List<string>();
        var runtimePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        IConfiguration Config(string role, string? password = null) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PharmaFlow"] = new NpgsqlConnectionStringBuilder(settings.ConnectionString) { Password = "" }.ConnectionString,
            ["Database:Password"] = ownerPassword,
            ["DatabaseRelease:ExpectedHost"] = settings.Host,
            ["DatabaseRelease:ExpectedDatabase"] = settings.Database,
            ["DatabaseRelease:ExpectedReleaseRole"] = settings.Username,
            ["DatabaseRelease:RuntimeRole"] = role,
            ["RuntimeRoleProvisioning:Password"] = password
        }).Build();
        Task<int> Provision(string role, string? password = null) => ProvisionRuntimeRoleCommand.ExecuteAsync(Config(role, password), message => { lock (messages) messages.Add(message); });
        Assert.Equal(1, await Provision("runtime_missing"));
        foreach (var invalid in new[] { "", "short", new string('a', 48) + "'", new string('a', 48) + "é" })
            Assert.Equal(1, await Provision("runtime_missing", invalid));
        foreach (var invalid in new[] { "pg_reserved", "a; SELECT 1", new string('a', 64), "release_owner" })
            Assert.Equal(1, await Provision(invalid, runtimePassword));
        Assert.False(await Exists(owner, "runtime_missing"));
        await Sql(admin, "ALTER ROLE release_owner NOCREATEROLE");
        Assert.Equal(1, await Provision("incapable_owner_reader", runtimePassword));
        Assert.False(await Exists(owner, "incapable_owner_reader"));
        await Sql(admin, "ALTER ROLE release_owner CREATEROLE");
        Assert.Equal(0, await Provision("runtime_reader", runtimePassword));
        Assert.Equal(0, await Provision("runtime_reader"));
        Assert.Equal(1, await Provision("runtime_reader", Convert.ToHexString(RandomNumberGenerator.GetBytes(32))));
        var runtimeSettings = new NpgsqlConnectionStringBuilder(settings.ConnectionString) { Username = "runtime_reader", Password = runtimePassword };
        await using var runtime = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await runtime.OpenAsync();
        Assert.Equal("runtime_reader", await Scalar(runtime, "SELECT current_user"));
        Assert.Equal(0L, await Scalar(owner, "SELECT count(*) FROM pg_tables WHERE schemaname='public'"));
        await RuntimePermissions.CheckRoleAsync(owner, "runtime_reader", default);
        Assert.Equal(true, await Scalar(owner, "SELECT NOT rolinherit FROM pg_roles WHERE rolname='runtime_reader'"));
        foreach (var sql in new[] { "CREATE DATABASE forbidden", "CREATE SCHEMA forbidden", "CREATE TABLE public.forbidden(id int)" })
            await Denied(runtime, sql);
        // Each dangerous attribute is tested independently. Administrative mutation is confined to this container.
        foreach (var flag in new[] { "SUPERUSER", "CREATEDB", "CREATEROLE", "REPLICATION", "BYPASSRLS" })
        {
            await Sql(admin, "ALTER ROLE runtime_reader " + flag);
            Assert.Equal(1, await Provision("runtime_reader"));
            await Sql(admin, "ALTER ROLE runtime_reader NO" + flag);
        }
        await Sql(admin, "CREATE ROLE unsafe_group");
        await Sql(admin, "GRANT unsafe_group TO runtime_reader");
        Assert.Equal(1, await Provision("runtime_reader"));
        await Sql(admin, "REVOKE unsafe_group FROM runtime_reader");
        await Sql(admin, "CREATE SCHEMA owned AUTHORIZATION runtime_reader");
        Assert.Equal(1, await Provision("runtime_reader"));
        await Sql(admin, "DROP SCHEMA owned");
        await Sql(admin, "CREATE TABLE public.owned(id int); ALTER TABLE public.owned OWNER TO runtime_reader");
        Assert.Equal(1, await Provision("runtime_reader"));
        await Sql(admin, "DROP TABLE public.owned");
        await Sql(admin, "CREATE DATABASE owned_database OWNER runtime_reader");
        Assert.Equal(1, await Provision("runtime_reader"));
        await Sql(admin, "DROP DATABASE owned_database");
        await Sql(admin, "ALTER ROLE runtime_reader NOLOGIN");
        Assert.Equal(1, await Provision("runtime_reader"));
        await Sql(admin, "ALTER ROLE runtime_reader LOGIN");
        await Sql(owner, "CREATE TABLE public.extra(id int); GRANT INSERT ON public.extra TO runtime_reader");
        Assert.Equal(1, await Provision("runtime_reader"));
        await Sql(owner, "REVOKE INSERT ON public.extra FROM runtime_reader; GRANT SELECT ON public.extra TO PUBLIC");
        Assert.Equal(1, await Provision("new_public_reader", runtimePassword));
        Assert.False(await Exists(owner, "new_public_reader"));
        await Sql(owner, "DROP TABLE public.extra");
        await Sql(owner, "GRANT CREATE ON SCHEMA public TO PUBLIC");
        Assert.Equal(1, await Provision("new_schema_reader", runtimePassword));
        Assert.False(await Exists(owner, "new_schema_reader"));
        await Sql(owner, "REVOKE CREATE ON SCHEMA public FROM PUBLIC");
        // Post-CREATE failure must roll back role creation; PUBLIC is not repaired silently.
        await Sql(owner, "GRANT CREATE ON DATABASE provisioning_test TO PUBLIC");
        Assert.Equal(1, await Provision("rolled_back_reader", runtimePassword));
        Assert.False(await Exists(owner, "rolled_back_reader"));
        Assert.Equal(true, await Scalar(owner, "SELECT has_database_privilege('public','provisioning_test','CREATE')"));
        await Sql(owner, "REVOKE CREATE ON DATABASE provisioning_test FROM PUBLIC");
        var concurrent = await Task.WhenAll(Provision("concurrent_reader", runtimePassword), Provision("concurrent_reader", runtimePassword));
        Assert.Equal(new[] { 0, 1 }, concurrent.Order().ToArray());
        Assert.Equal(0, await Provision("concurrent_reader"));
        // Provisioning produced no application schema/data. Use the real migration and canonical seed next.
        await using var context = new PharmaFlowDbContext(new DbContextOptionsBuilder<PharmaFlowDbContext>().UseNpgsql(owner).Options);
        await context.Database.MigrateAsync();
        await new CanonicalDatabaseSeeder(context).SeedAsync();
        await RuntimePermissions.ApplyAndVerifyAsync(owner, "runtime_reader", default);
        var tables = new[] { "medications", "medication_active_ingredients", "pharmacy_branches", "opening_intervals", "availability" };
        var counts = new long[] { 9, 10, 3, 18, 21 };
        for (var i = 0; i < tables.Length; i++) Assert.Equal(counts[i], await Scalar(runtime, "SELECT count(*) FROM public." + tables[i]));
        Assert.Equal(0, await Provision("runtime_reader"));
        var before = await Scalar(owner, "SELECT string_agg(xmin::text, ',' ORDER BY id) FROM medications");
        await context.Database.MigrateAsync();
        await new CanonicalDatabaseSeeder(context).SeedAsync();
        await RuntimePermissions.ApplyAndVerifyAsync(owner, "runtime_reader", default);
        Assert.Equal(before, await Scalar(owner, "SELECT string_agg(xmin::text, ',' ORDER BY id) FROM medications"));
        foreach (var sql in new[] { "CREATE TABLE public.forbidden(id int)", "ALTER TABLE medications ADD COLUMN forbidden int", "DROP TABLE medications", "INSERT INTO medications(id) VALUES ('forbidden')", "UPDATE medications SET display_name=display_name", "DELETE FROM medications" })
            await Denied(runtime, sql);
        Assert.DoesNotContain(messages, m => m.Contains(runtimePassword, StringComparison.Ordinal) || m.Contains(ownerPassword, StringComparison.Ordinal) || m.Contains("SCRAM-SHA-256", StringComparison.Ordinal));
    }

    private static async Task Sql(NpgsqlConnection connection, string sql) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
    private static async Task<object?> Scalar(NpgsqlConnection connection, string sql) { await using var command = new NpgsqlCommand(sql, connection); return await command.ExecuteScalarAsync(); }
    private static async Task<bool> Exists(NpgsqlConnection connection, string role)
    { await using var command = new NpgsqlCommand("SELECT EXISTS(SELECT FROM pg_roles WHERE rolname=@role)", connection); command.Parameters.AddWithValue("role", role); return Equals(await command.ExecuteScalarAsync(), true); }
    private static async Task Denied(NpgsqlConnection connection, string sql)
    { var error = await Assert.ThrowsAsync<PostgresException>(() => Sql(connection, sql)); Assert.Equal("42501", error.SqlState); }
}
