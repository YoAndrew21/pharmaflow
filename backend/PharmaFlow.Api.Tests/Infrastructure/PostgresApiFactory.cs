using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using PharmaFlow.Api.Persistence;
using System.Text.Json.Nodes;

namespace PharmaFlow.Api.Tests.Infrastructure;

/// <summary>Uses the unchanged Program registration with container-only configuration.</summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> configuration;
    private readonly ContainerDatabaseGuard guard;
    private readonly DbCommandInterceptor? observer;
    public JsonObject Canonical { get; } = JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "demo-data", "catalog.json")))!.AsObject();

    internal PostgresApiFactory(string containerConnectionString, DbCommandInterceptor? observer = null)
    {
        this.observer = observer;
        guard = new(containerConnectionString);
        var settings = new NpgsqlConnectionStringBuilder(containerConnectionString);
        var password = settings.Password;
        settings.Remove("Password");
        configuration = new()
        {
            ["ConnectionStrings:PharmaFlow"] = settings.ConnectionString,
            ["Database:Password"] = password
        };
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development retains the existing OpenAPI behavior; configuration sources are replaced.
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, sources) =>
        {
            sources.Sources.Clear();
            sources.AddInMemoryCollection(configuration);
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services => services.AddDbContext<PharmaFlowDbContext>(options =>
        {
            options.AddInterceptors(guard).EnableSensitiveDataLogging(false).EnableDetailedErrors(false);
            if (observer is not null) options.AddInterceptors(observer);
        }));
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });
}
