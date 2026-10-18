using System.Collections.Frozen;
using System.Text.Json;

namespace PharmaFlow.Api.Services;

/// <summary>Identity/rank metadata only. Never supplies records or response field values.</summary>
public sealed class CanonicalCatalogOrder
{
    private readonly FrozenDictionary<string, int> medications;
    private readonly FrozenDictionary<string, int> branches;
    private readonly FrozenDictionary<(string, string), int> availability;

    public CanonicalCatalogOrder()
    {
        using var stream = typeof(CanonicalCatalogOrder).Assembly.GetManifestResourceStream("PharmaFlow.Api.CanonicalCatalog.json")
            ?? throw new InvalidOperationException("Canonical ordering metadata is missing.");
        using var document = JsonDocument.Parse(stream);
        medications = document.RootElement.GetProperty("medications").EnumerateArray()
            .Select((row, rank) => (Id: row.GetProperty("id").GetString()!, Rank: rank))
            .ToFrozenDictionary(row => row.Id, row => row.Rank, StringComparer.Ordinal);
        branches = document.RootElement.GetProperty("branches").EnumerateArray()
            .Select((row, rank) => (Id: row.GetProperty("id").GetString()!, Rank: rank))
            .ToFrozenDictionary(row => row.Id, row => row.Rank, StringComparer.Ordinal);
        availability = document.RootElement.GetProperty("availability").EnumerateArray()
            .Select((row, rank) => (Key: (row.GetProperty("medicationId").GetString()!, row.GetProperty("branchId").GetString()!), Rank: rank))
            .ToFrozenDictionary(row => row.Key, row => row.Rank);
    }

    // Unknown database keys follow canonical keys; callers use ordinal IDs as tie-breakers.
    public int MedicationRank(string id) => medications.GetValueOrDefault(id, int.MaxValue);
    public int BranchRank(string id) => branches.GetValueOrDefault(id, int.MaxValue);
    public int AvailabilityRank(string medicationId, string branchId) => availability.GetValueOrDefault((medicationId, branchId), int.MaxValue);
}
