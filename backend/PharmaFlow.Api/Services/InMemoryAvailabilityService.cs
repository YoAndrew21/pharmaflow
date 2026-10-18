using System.Globalization;
using System.Text.Json;
using PharmaFlow.Api.Models;

namespace PharmaFlow.Api.Services;

/// <summary>Loads only recorded observations from the embedded canonical source in dataset order.</summary>
public sealed class InMemoryAvailabilityService : IAvailabilityService
{
    private readonly IReadOnlyList<Availability> observations;

    public InMemoryAvailabilityService()
    {
        using var stream = typeof(InMemoryAvailabilityService).Assembly
            .GetManifestResourceStream("PharmaFlow.Api.CanonicalCatalog.json")
            ?? throw new InvalidOperationException("The canonical catalog resource is missing.");
        using var dataset = JsonDocument.Parse(stream);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectNullableAnnotations = true
        };
        var canonical = dataset.RootElement.GetProperty("availability")
            .Deserialize<Availability[]>(options)
            ?? throw new InvalidOperationException("The canonical availability array is missing.");

        var medicationIds = dataset.RootElement.GetProperty("medications").EnumerateArray().Select(row => row.GetProperty("id").GetString())
            .ToHashSet(StringComparer.Ordinal);
        var branchIds = dataset.RootElement.GetProperty("branches").EnumerateArray().Select(row => row.GetProperty("id").GetString())
            .ToHashSet(StringComparer.Ordinal);
        var pairs = new HashSet<(string MedicationId, string BranchId)>();

        foreach (var observation in canonical)
        {
            if (!medicationIds.Contains(observation.MedicationId))
                throw new InvalidOperationException($"Canonical availability references unknown medicationId '{observation.MedicationId}'.");
            if (!branchIds.Contains(observation.BranchId))
                throw new InvalidOperationException($"Canonical availability references unknown branchId '{observation.BranchId}'.");
            if (!pairs.Add((observation.MedicationId, observation.BranchId)))
                throw new InvalidOperationException($"Duplicate canonical availability pair '{observation.MedicationId}/{observation.BranchId}'.");
            if (observation.Status is not ("available" or "low-stock" or "unavailable"))
                throw new InvalidOperationException($"Invalid canonical availability status '{observation.Status}'.");
            if (observation.ReferencePrice is { } price &&
                (price.MinorUnits < 0 || price.MinorUnits > 9_007_199_254_740_991 || price.CurrencyCode != "USD"))
                throw new InvalidOperationException("Canonical reference prices must use nonnegative JavaScript-safe integer minor units and USD.");
            if (!DateTimeOffset.TryParseExact(observation.UpdatedAt, "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
                throw new InvalidOperationException($"Invalid canonical UTC observation timestamp '{observation.UpdatedAt}'.");
        }

        // Records and nested price records have init-only properties; no missing pairs are generated.
        observations = Array.AsReadOnly(canonical);
    }

    public IReadOnlyList<Availability> GetAll() => observations;

    public Task<IReadOnlyList<Availability>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetAll());
    }
}
