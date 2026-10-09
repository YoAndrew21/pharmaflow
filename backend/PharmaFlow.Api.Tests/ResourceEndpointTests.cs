using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace PharmaFlow.Api.Tests;

public sealed class ResourceEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("medications", "medications", 9)]
    [InlineData("pharmacies", "branches", 3)]
    [InlineData("availability", "availability", 21)]
    public async Task Collections_return_exact_canonical_records_in_order(string resource, string source, int count)
    {
        using var client = factory.CreateApiClient();
        var actual = await ApiFactory.GetArrayAsync(client, $"/api/{resource}");
        Assert.Equal(count, actual.Count);
        // DeepEquals compares array order, all fields and explicit nulls, independent of JSON property order.
        Assert.True(JsonNode.DeepEquals(factory.Canonical[source], actual),
            $"The {resource} HTTP response differs from its ordered canonical source.");
    }

    [Theory]
    [InlineData("medications", "medications", "paracetamol-500-tablets", "med-par-500")]
    [InlineData("pharmacies", "branches", "aster-commons", "aster-commons")]
    public async Task Known_details_return_canonical_identity_and_content(string resource, string source, string slug, string id)
    {
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync($"/api/{resource}/{slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var actual = Assert.IsType<JsonObject>(JsonNode.Parse(await response.Content.ReadAsStringAsync()));
        Assert.Equal(id, actual["id"]!.GetValue<string>());
        Assert.Equal(slug, actual["slug"]!.GetValue<string>());
        var expected = factory.Canonical[source]!.AsArray().Single(row => row!["slug"]!.GetValue<string>() == slug);
        Assert.True(JsonNode.DeepEquals(expected, actual));
    }

    [Theory]
    [InlineData("/api/medications/unknown-presentation")]
    [InlineData("/api/pharmacies/unknown-branch")]
    public async Task Unknown_details_return_404_problem_responses(string path)
    {
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, problem!["status"]!.GetValue<int>());
    }
}
