using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Services;
using PharmaFlow.Api.Tests.Infrastructure;
using Xunit;

namespace PharmaFlow.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class PostgresInfrastructureTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Actual_DI_context_connects_to_the_fixture_owned_PostgreSQL_18_database()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
        fixture.AssertContainerTarget(context);
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT current_database(), current_user, current_setting('server_version_num')::integer";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(fixture.DatabaseName, reader.GetString(0));
            Assert.NotEqual("pharmaflow_dev", reader.GetString(0));
            Assert.Equal("pharmaflow_test", reader.GetString(1));
            Assert.InRange(reader.GetInt32(2), 180000, 189999);
        }
        finally { await context.Database.CloseConnectionAsync(); }
    }

    [Fact]
    public async Task Real_InitialCreate_is_applied_exactly_once_without_pending_migrations()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
        fixture.AssertContainerTarget(context);
        Assert.Equal(new[] { "20261011043419_InitialCreate" }, await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Existing_canonical_seeder_populates_all_five_physical_tables_and_can_repeat()
    {
        await fixture.SeedCanonicalAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
        fixture.AssertContainerTarget(context);
        Assert.Equal(9, await context.Medications.CountAsync());
        Assert.Equal(10, await context.MedicationActiveIngredients.CountAsync());
        Assert.Equal(3, await context.PharmacyBranches.CountAsync());
        Assert.Equal(18, await context.OpeningIntervals.CountAsync());
        Assert.Equal(21, await context.Availability.CountAsync());
    }

    [Fact]
    public void Test_host_has_only_explicit_memory_configuration_and_production_database_services()
    {
        var configuration = fixture.Factory.Services.GetRequiredService<IConfiguration>();
        Assert.All(((IConfigurationRoot)configuration).Providers,
            provider => Assert.IsType<Microsoft.Extensions.Configuration.Memory.MemoryConfigurationProvider>(provider));
        Assert.False(string.IsNullOrEmpty(configuration["Database:Password"]));
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(configuration.GetConnectionString("PharmaFlow"));
        Assert.Equal(fixture.DatabaseName, settings.Database);
        Assert.True(string.IsNullOrEmpty(settings.Password));
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.IsType<DatabaseMedicationService>(scope.ServiceProvider.GetRequiredService<IMedicationService>());
        Assert.IsType<DatabasePharmacyService>(scope.ServiceProvider.GetRequiredService<IPharmacyService>());
        Assert.IsType<DatabaseAvailabilityService>(scope.ServiceProvider.GetRequiredService<IAvailabilityService>());
    }
}

public sealed class ContainerDatabaseGuardTests
{
    [Fact]
    public void Verified_connection_can_hide_its_password_but_cannot_change_its_destination()
    {
        var settings = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = 12345, Database = "pharmaflow_test_guard",
            Username = "guard_test", Password = Guid.NewGuid().ToString("N")
        };
        var guard = new ContainerDatabaseGuard(settings.ConnectionString);
        using var connection = new Npgsql.NpgsqlConnection(settings.ConnectionString);
        guard.ConnectionOpening(connection, null!, default);
        settings.Remove("Password");
        connection.ConnectionString = settings.ConnectionString;
        guard.ConnectionOpening(connection, null!, default);
        settings.Database = "pharmaflow_dev";
        connection.ConnectionString = settings.ConnectionString;
        Assert.Throws<InvalidOperationException>(() => guard.ConnectionOpening(connection, null!, default));
        // A fresh unverified connection cannot borrow the previously verified object's identity.
        settings.Database = "pharmaflow_test_guard";
        using var unverified = new Npgsql.NpgsqlConnection(settings.ConnectionString);
        Assert.Throws<InvalidOperationException>(() => guard.ConnectionOpening(unverified, null!, default));
    }

    [Fact]
    public void Offline_API_factory_also_removes_external_configuration_sources()
    {
        using var factory = new ApiFactory();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.All(((IConfigurationRoot)configuration).Providers,
            provider => Assert.IsType<Microsoft.Extensions.Configuration.Memory.MemoryConfigurationProvider>(provider));
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(configuration.GetConnectionString("PharmaFlow"));
        Assert.Equal("pharmaflow_offline_tests", settings.Database);
        Assert.Equal(1, settings.Port);
        Assert.Null(configuration["Database:Password"]);
    }

    [Theory]
    [InlineData("developer-database")]
    [InlineData("unknown-database")]
    [InlineData("different-host")]
    [InlineData("different-port")]
    [InlineData("different-user")]
    [InlineData("missing-password")]
    public void Target_guard_rejects_any_unproven_destination_before_opening(string change)
    {
        var expected = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = 12345, Database = "pharmaflow_test_guard",
            Username = "guard_test", Password = Guid.NewGuid().ToString("N")
        };
        var guard = new ContainerDatabaseGuard(expected.ConnectionString);
        var other = new Npgsql.NpgsqlConnectionStringBuilder(expected.ConnectionString);
        switch (change)
        {
            case "developer-database": other.Database = "pharmaflow_dev"; break;
            case "unknown-database": other.Database = "pharmaflow_test_other"; break;
            case "different-host": other.Host = "::1"; break;
            case "different-port": other.Port++; break;
            case "different-user": other.Username = "other"; break;
            case "missing-password": other.Remove("Password"); break;
        }
        Assert.Throws<InvalidOperationException>(() => guard.AssertTarget(other.ConnectionString));
    }

    [Theory]
    [InlineData("pharmaflow_dev")]
    [InlineData("PHARMAFLOW_DEV")]
    [InlineData("unknown")]
    public void Guard_cannot_be_initialized_with_a_developer_or_unidentified_database(string database)
    {
        var settings = new Npgsql.NpgsqlConnectionStringBuilder { Database = database };
        Assert.Throws<InvalidOperationException>(() => new ContainerDatabaseGuard(settings.ConnectionString));
    }
}
