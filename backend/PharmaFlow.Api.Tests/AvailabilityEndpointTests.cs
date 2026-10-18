using System.Globalization;
using System.Text.Json.Nodes;
using Xunit;
using PharmaFlow.Api.Tests.Infrastructure;

namespace PharmaFlow.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class AvailabilityEndpointTests(PostgresFixture fixture)
{
    private PostgresApiFactory factory => fixture.Factory;

    [Fact]
    public async Task Observation_references_match_canonical_medications_and_branches()
    {
        using var client = factory.CreateApiClient();
        var rows = await ApiFactory.GetArrayAsync(client, "/api/availability");
        var medicationIds = factory.Canonical["medications"]!.AsArray().Select(row => Text(row!, "id")).ToHashSet();
        var branchIds = factory.Canonical["branches"]!.AsArray().Select(row => Text(row!, "id")).ToHashSet();
        Assert.All(rows, row =>
        {
            Assert.Contains(Text(row!, "medicationId"), medicationIds);
            Assert.Contains(Text(row!, "branchId"), branchIds);
        });
    }

    [Fact]
    public async Task Statuses_use_only_the_three_canonical_strings()
    {
        using var client = factory.CreateApiClient();
        var rows = await ApiFactory.GetArrayAsync(client, "/api/availability");
        Assert.All(rows, row => Assert.Contains(Text(row!, "status"), new[] { "available", "low-stock", "unavailable" }));
    }

    [Fact]
    public async Task Null_prices_and_unavailable_but_priced_observations_are_preserved()
    {
        using var client = factory.CreateApiClient();
        var rows = await ApiFactory.GetArrayAsync(client, "/api/availability");
        var expected = factory.Canonical["availability"]!.AsArray();
        Assert.Contains(expected, row => row!["referencePrice"] is null);
        Assert.Contains(expected, row => Text(row!, "status") == "unavailable" && row!["referencePrice"] is not null);
        Assert.Equal(expected.Count, rows.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.True(rows[index]!.AsObject().ContainsKey("referencePrice"), "Null prices must remain explicit JSON fields.");
            Assert.True(JsonNode.DeepEquals(expected[index]!["referencePrice"], rows[index]!["referencePrice"]));
            if (rows[index]!["referencePrice"] is { } price)
            {
                Assert.InRange(price["minorUnits"]!.GetValue<long>(), 0, 9_007_199_254_740_991);
                Assert.Equal("USD", Text(price, "currencyCode"));
            }
        }
        Assert.Contains(rows, row => Text(row!, "status") == "unavailable" && row!["referencePrice"] is not null);
    }

    [Fact]
    public async Task Timestamps_retain_exact_canonical_UTC_values()
    {
        using var client = factory.CreateApiClient();
        var rows = await ApiFactory.GetArrayAsync(client, "/api/availability");
        var expected = factory.Canonical["availability"]!.AsArray();
        Assert.Equal(expected.Select(row => Text(row!, "updatedAt")), rows.Select(row => Text(row!, "updatedAt")));
        Assert.All(rows, row =>
        {
            var timestamp = Text(row!, "updatedAt");
            Assert.EndsWith("Z", timestamp);
            Assert.True(DateTimeOffset.TryParseExact(timestamp, "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var instant));
            Assert.Equal(TimeSpan.Zero, instant.Offset);
        });
    }

    [Fact]
    public async Task Pairs_are_unique_and_unrecorded_combinations_remain_absent()
    {
        using var client = factory.CreateApiClient();
        var rows = await ApiFactory.GetArrayAsync(client, "/api/availability");
        var actualPairs = rows.Select(Pair).ToHashSet();
        var canonicalPairs = factory.Canonical["availability"]!.AsArray().Select(Pair).ToHashSet();
        Assert.Equal(rows.Count, actualPairs.Count);
        Assert.True(actualPairs.SetEquals(canonicalPairs));
        var missingPairs = (from medication in factory.Canonical["medications"]!.AsArray()
                            from branch in factory.Canonical["branches"]!.AsArray()
                            let pair = (Text(medication!, "id"), Text(branch!, "id"))
                            where !canonicalPairs.Contains(pair)
                            select pair).ToArray();
        Assert.NotEmpty(missingPairs);
        Assert.All(missingPairs, pair => Assert.DoesNotContain(pair, actualPairs));
    }

    private static string Text(JsonNode row, string property) => row[property]!.GetValue<string>();
    private static (string MedicationId, string BranchId) Pair(JsonNode? row) =>
        (Text(row!, "medicationId"), Text(row!, "branchId"));
}
