using System.Text.Json;
using PharmaFlow.Api.Models;

namespace PharmaFlow.Api.Services;

/// <summary>Reads branches from the canonical dataset embedded at build time; fixture changes require a rebuild.</summary>
public sealed class InMemoryPharmacyService : IPharmacyService
{
    private readonly IReadOnlyList<PharmacyBranch> branches;

    public InMemoryPharmacyService()
    {
        using var stream = typeof(InMemoryPharmacyService).Assembly
            .GetManifestResourceStream("PharmaFlow.Api.CanonicalCatalog.json")
            ?? throw new InvalidOperationException("The canonical catalog resource is missing.");
        using var dataset = JsonDocument.Parse(stream);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectNullableAnnotations = true
        };
        var canonical = dataset.RootElement.GetProperty("branches")
            .Deserialize<PharmacyBranch[]>(options)
            ?? throw new InvalidOperationException("The canonical branch array is missing.");

        // Preserve dataset order and protect every shared schedule list from mutation.
        branches = Array.AsReadOnly(canonical.Select(branch => branch with
        {
            OpeningHours = FreezeHours(branch.OpeningHours)
        }).ToArray());
    }

    public IReadOnlyList<PharmacyBranch> GetAll() => branches;

    public PharmacyBranch? GetBySlug(string slug) => branches.FirstOrDefault(
        branch => string.Equals(branch.Slug, slug, StringComparison.Ordinal));

    private static WeeklyOpeningHours FreezeHours(WeeklyOpeningHours hours) => hours with
    {
        Monday = Array.AsReadOnly(hours.Monday.ToArray()),
        Tuesday = Array.AsReadOnly(hours.Tuesday.ToArray()),
        Wednesday = Array.AsReadOnly(hours.Wednesday.ToArray()),
        Thursday = Array.AsReadOnly(hours.Thursday.ToArray()),
        Friday = Array.AsReadOnly(hours.Friday.ToArray()),
        Saturday = Array.AsReadOnly(hours.Saturday.ToArray()),
        Sunday = Array.AsReadOnly(hours.Sunday.ToArray())
    };
}
