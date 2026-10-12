using System.Globalization;
using System.Text.Json;
using PharmaFlow.Api.Models;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Persistence.Seeding;

/// <summary>Validated, ordered mapping of the single canonical resource; no database access.</summary>
public sealed class CanonicalSeedData
{
    public IReadOnlyList<MedicationEntity> Medications { get; private init; } = [];
    public IReadOnlyList<MedicationActiveIngredientEntity> Ingredients { get; private init; } = [];
    public IReadOnlyList<PharmacyBranchEntity> Branches { get; private init; } = [];
    public IReadOnlyList<OpeningIntervalEntity> OpeningIntervals { get; private init; } = [];
    public IReadOnlyList<AvailabilityEntity> Availability { get; private init; } = [];

    public static CanonicalSeedData Load()
    {
        using var stream = typeof(CanonicalSeedData).Assembly.GetManifestResourceStream("PharmaFlow.Api.CanonicalCatalog.json")
            ?? throw new InvalidDataException("The canonical catalog resource is missing.");
        return Load(stream);
    }

    public static CanonicalSeedData Load(Stream stream)
    {
        try
        {
            using var document = JsonDocument.Parse(stream);
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { RespectNullableAnnotations = true };
            var medications = document.RootElement.GetProperty("medications").Deserialize<Medication[]>(options)
                ?? throw new InvalidDataException("Canonical medications are missing.");
            var branches = document.RootElement.GetProperty("branches").Deserialize<PharmacyBranch[]>(options)
                ?? throw new InvalidDataException("Canonical branches are missing.");
            var availability = document.RootElement.GetProperty("availability").Deserialize<Availability[]>(options)
                ?? throw new InvalidDataException("Canonical availability is missing.");
            if (medications.Any(row => row is null) || branches.Any(row => row is null) || availability.Any(row => row is null))
                throw new InvalidDataException("Canonical arrays cannot contain null records.");
            Unique(medications.Select(row => row.Id), "medication IDs");
            Unique(medications.Select(row => row.Slug), "medication slugs");
            Unique(branches.Select(row => row.Id), "branch IDs");
            Unique(branches.Select(row => row.Slug), "branch slugs");
            var medicationIds = medications.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
            var branchIds = branches.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
            var ingredients = new List<MedicationActiveIngredientEntity>();
            foreach (var row in medications)
            {
                Text(row.DisplayName, row.StrengthLabel, row.FormCode, row.PresentationLabel, row.CategoryCode);
                if (row.ActiveIngredients is null || row.ActiveIngredients.Count == 0) throw new InvalidDataException("Canonical ingredients cannot be empty.");
                for (var position = 0; position < row.ActiveIngredients.Count; position++)
                {
                    Text(row.ActiveIngredients[position]);
                    ingredients.Add(new() { MedicationId = row.Id, Position = position, Ingredient = row.ActiveIngredients[position] });
                }
            }
            var intervals = new List<OpeningIntervalEntity>();
            foreach (var branch in branches)
            {
                Text(branch.Name, branch.FictionalLocation, branch.TimeZone);
                var hours = branch.OpeningHours ?? throw new InvalidDataException("Canonical opening hours are missing.");
                IReadOnlyList<OpeningInterval>[] days = [hours.Monday, hours.Tuesday, hours.Wednesday, hours.Thursday,
                    hours.Friday, hours.Saturday, hours.Sunday];
                for (var day = 0; day < days.Length; day++)
                {
                    if (days[day] is null) throw new InvalidDataException("Canonical weekdays require interval arrays, including closed days.");
                    TimeOnly? previousClose = null;
                    for (var position = 0; position < days[day].Count; position++)
                    {
                        var interval = days[day][position] ?? throw new InvalidDataException("Canonical opening interval records cannot be null.");
                        if (!TimeOnly.TryParseExact(interval.OpensAt, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var opens) ||
                            !TimeOnly.TryParseExact(interval.ClosesAt, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var closes) ||
                            opens >= closes || (previousClose is { } last && opens < last))
                            throw new InvalidDataException("Canonical opening intervals must be ordered, non-overlapping HH:mm intervals within a day.");
                        intervals.Add(new() { PharmacyBranchId = branch.Id, DayOfWeek = day + 1, Position = position, OpensAt = opens, ClosesAt = closes });
                        previousClose = closes;
                    }
                }
            }
            var observations = new List<AvailabilityEntity>();
            var pairs = new HashSet<(string, string)>();
            foreach (var row in availability)
            {
                if (!medicationIds.Contains(row.MedicationId) || !branchIds.Contains(row.BranchId) || !pairs.Add((row.MedicationId, row.BranchId)))
                    throw new InvalidDataException("Canonical availability requires known references and unique medication/branch pairs.");
                if (row.Status is not ("available" or "low-stock" or "unavailable"))
                    throw new InvalidDataException("Canonical availability has an invalid status.");
                if (row.ReferencePrice is { } price &&
                    (price.MinorUnits < 0 || price.MinorUnits > 9_007_199_254_740_991 || price.CurrencyCode != "USD"))
                    throw new InvalidDataException("Canonical reference prices require nonnegative JavaScript-safe integer minor units and USD.");
                if (!DateTimeOffset.TryParseExact(row.UpdatedAt, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var updated) || updated.Offset != TimeSpan.Zero)
                    throw new InvalidDataException("Canonical timestamps must be UTC instants compatible with timestamptz.");
                observations.Add(new() { MedicationId = row.MedicationId, BranchId = row.BranchId, Status = row.Status,
                    PriceMinorUnits = row.ReferencePrice?.MinorUnits, PriceCurrencyCode = row.ReferencePrice?.CurrencyCode, UpdatedAt = updated });
            }
            return new()
            {
                Medications = medications.Select(row => new MedicationEntity { Id = row.Id, Slug = row.Slug, DisplayName = row.DisplayName,
                    StrengthLabel = row.StrengthLabel, FormCode = row.FormCode, PresentationLabel = row.PresentationLabel,
                    CategoryCode = row.CategoryCode, PrescriptionRequired = row.PrescriptionRequired }).ToArray(),
                Ingredients = ingredients, Branches = branches.Select(row => new PharmacyBranchEntity { Id = row.Id, Slug = row.Slug,
                    Name = row.Name, FictionalLocation = row.FictionalLocation, TimeZone = row.TimeZone }).ToArray(),
                OpeningIntervals = intervals, Availability = observations
            };
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException("Canonical JSON is malformed or has missing/invalid fields.", error);
        }
    }

    private static void Unique(IEnumerable<string> values, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            Text(value);
            if (!seen.Add(value)) throw new InvalidDataException($"Canonical {label} must be unique.");
        }
    }

    private static void Text(params string[] values)
    {
        if (values.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains('\0')))
            throw new InvalidDataException("Canonical text fields must be nonempty PostgreSQL-compatible text.");
    }
}
