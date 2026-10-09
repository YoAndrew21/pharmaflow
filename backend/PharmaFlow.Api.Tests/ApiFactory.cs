using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PharmaFlow.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public JsonObject Canonical { get; } = JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "demo-data", "catalog.json")))!.AsObject();

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

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
