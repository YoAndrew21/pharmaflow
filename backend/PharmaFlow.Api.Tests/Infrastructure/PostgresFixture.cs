using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace PharmaFlow.Api.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Disposable PostgreSQL";
}

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Image = "postgres:18";
    public string DatabaseName { get; } = "pharmaflow_test_" + Guid.NewGuid().ToString("N");
    private readonly string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private PostgreSqlContainer? container;
    private PostgresApiFactory? factory;
    public PostgresApiFactory Factory => factory ?? throw new InvalidOperationException("The disposable database fixture is not initialized.");

    public async Task InitializeAsync()
    {
        var stage = "container startup";
        try
        {
            container = new PostgreSqlBuilder(Image)
                .WithDatabase(DatabaseName)
                .WithUsername("pharmaflow_test")
                .WithPassword(password)
                .WithPortBinding(5432, true)
                .WithLabel("pharmaflow.test-infrastructure", "disposable")
                .Build();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await container.StartAsync(timeout.Token);
            stage = "application configuration and migration";
            factory = new PostgresApiFactory(container.GetConnectionString());
            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
                AssertContainerTarget(context);
                await context.Database.MigrateAsync(timeout.Token);
            }
            stage = "canonical seed";
            await SeedCanonicalAsync(timeout.Token);
        }
        catch (Exception error)
        {
            await DisposeAsync();
            // Container diagnostics/exceptions can contain environment variables; do not echo them.
            var detail = error.Message.StartsWith("Refusing a connection", StringComparison.Ordinal)
                ? " " + error.Message : string.Empty;
            throw new InvalidOperationException($"Disposable PostgreSQL fixture failed during {stage} ({error.GetType().Name}).{detail} No developer database fallback is permitted. Origin: {error.TargetSite?.DeclaringType?.FullName}.{error.TargetSite?.Name}");
        }
    }

    public void AssertContainerTarget(PharmaFlowDbContext context)
    {
        if (container is null || container.State != DotNet.Testcontainers.Containers.TestcontainersStates.Running)
            throw new InvalidOperationException("The fixture-owned PostgreSQL container must be running.");
        new ContainerDatabaseGuard(container.GetConnectionString()).AssertTarget(context.Database.GetConnectionString());
    }

    public async Task SeedCanonicalAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
        AssertContainerTarget(context);
        await scope.ServiceProvider.GetRequiredService<CanonicalDatabaseSeeder>().SeedAsync(cancellationToken);
    }

    public PostgresApiFactory CreateFactory(DbCommandInterceptor? observer = null)
    {
        if (container is null || container.State != DotNet.Testcontainers.Containers.TestcontainersStates.Running)
            throw new InvalidOperationException("The fixture-owned PostgreSQL container must be running.");
        return new PostgresApiFactory(container.GetConnectionString(), observer);
    }

    public async Task ClearApplicationDataAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmaFlowDbContext>();
        AssertContainerTarget(context);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Availability.ExecuteDeleteAsync();
        await context.MedicationActiveIngredients.ExecuteDeleteAsync();
        await context.OpeningIntervals.ExecuteDeleteAsync();
        await context.Medications.ExecuteDeleteAsync();
        await context.PharmacyBranches.ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (factory is not null) { await factory.DisposeAsync(); factory = null; }
        }
        finally
        {
            if (container is not null) { await container.DisposeAsync(); container = null; }
        }
    }
}
