using System.Text.Json;
using PharmaFlow.Api.Models;

namespace PharmaFlow.Api.Services;

/// <summary>Reads the canonical dataset embedded at build time; fixture changes require a rebuild.</summary>
public sealed class InMemoryMedicationService : IMedicationService
{
    private readonly IReadOnlyList<Medication> medications;

    public InMemoryMedicationService()
    {
        using var stream = typeof(InMemoryMedicationService).Assembly
            .GetManifestResourceStream("PharmaFlow.Api.CanonicalCatalog.json")
            ?? throw new InvalidOperationException("The canonical catalog resource is missing.");
        using var dataset = JsonDocument.Parse(stream);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectNullableAnnotations = true
        };
        var canonical = dataset.RootElement.GetProperty("medications")
            .Deserialize<Medication[]>(options)
            ?? throw new InvalidOperationException("The canonical medication array is missing.");

        // Protect the shared singleton's collection and ingredient arrays from mutation.
        medications = Array.AsReadOnly(canonical.Select(medication => medication with
        {
            ActiveIngredients = Array.AsReadOnly(medication.ActiveIngredients.ToArray())
        }).ToArray());
    }

    public IReadOnlyList<Medication> GetAll() => medications;

    public Task<IReadOnlyList<Medication>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetAll());
    }

    public Task<Medication?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetBySlug(slug));
    }

    public Medication? GetBySlug(string slug) => medications.FirstOrDefault(
        medication => string.Equals(medication.Slug, slug, StringComparison.Ordinal));
}
