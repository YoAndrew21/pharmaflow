using System.Net;
using System.Text.Json.Nodes;
using Xunit;
using PharmaFlow.Api.Tests.Infrastructure;

namespace PharmaFlow.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ApiRegressionTests(PostgresFixture fixture)
{
    private PostgresApiFactory factory => fixture.Factory;

    [Theory]
    [InlineData("/api/medications", "POST")]
    [InlineData("/api/medications", "PUT")]
    [InlineData("/api/medications", "PATCH")]
    [InlineData("/api/medications", "DELETE")]
    [InlineData("/api/pharmacies", "POST")]
    [InlineData("/api/pharmacies", "PUT")]
    [InlineData("/api/pharmacies", "PATCH")]
    [InlineData("/api/pharmacies", "DELETE")]
    [InlineData("/api/availability", "POST")]
    [InlineData("/api/availability", "PUT")]
    [InlineData("/api/availability", "PATCH")]
    [InlineData("/api/availability", "DELETE")]
    public async Task Collection_writes_are_rejected(string path, string method)
    {
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("GET", response.Content.Headers.Allow);
    }

    [Fact]
    public async Task WeatherForecast_is_absent()
    {
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync("/WeatherForecast");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenAPI_describes_only_the_five_read_routes_and_existing_404s()
    {
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var paths = document!["paths"]!.AsObject();
        Assert.Equal(new[] { "/api/availability", "/api/medications", "/api/medications/{slug}", "/api/pharmacies", "/api/pharmacies/{slug}" },
            paths.Select(path => path.Key).Order().ToArray());
        Assert.All(paths, path => Assert.Equal(new[] { "get" }, path.Value!.AsObject().Select(operation => operation.Key)));
        Assert.NotNull(paths["/api/medications/{slug}"]!["get"]!["responses"]!["404"]);
        Assert.NotNull(paths["/api/pharmacies/{slug}"]!["get"]!["responses"]!["404"]);
    }
}
