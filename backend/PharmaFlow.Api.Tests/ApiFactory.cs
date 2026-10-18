using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PharmaFlow.Api.Services;
using Xunit;

namespace PharmaFlow.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public JsonObject Canonical { get; } = JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "demo-data", "catalog.json")))!.AsObject();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // Existing offline contract tests must never fall back to developer credentials.
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PharmaFlow"] = "Host=127.0.0.1;Port=1;Database=pharmaflow_offline_tests;Timeout=1",
                ["Database:Password"] = null
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMedicationService>();
            services.RemoveAll<IPharmacyService>();
            services.RemoveAll<IAvailabilityService>();
            services.AddSingleton<IMedicationService, InMemoryMedicationService>();
            services.AddSingleton<IPharmacyService, InMemoryPharmacyService>();
            services.AddSingleton<IAvailabilityService, InMemoryAvailabilityService>();
        });
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        // TestServer uses no socket or certificate; HTTPS exercises the existing pipeline without redirects.
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    public static async Task<JsonArray> GetArrayAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return Assert.IsType<JsonArray>(JsonNode.Parse(await response.Content.ReadAsStringAsync()));
    }
}
