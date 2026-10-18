using Microsoft.EntityFrameworkCore;
using PharmaFlow.Api.Models;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Services;

public sealed class DatabaseMedicationService(PharmaFlowDbContext context, CanonicalCatalogOrder order) : IMedicationService
{
    private IQueryable<MedicationEntity> Query() => context.Medications.AsNoTracking()
        .Include(row => row.ActiveIngredients).AsSingleQuery();

    public async Task<IReadOnlyList<Medication>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Query().ToListAsync(cancellationToken);
        return rows.OrderBy(row => order.MedicationRank(row.Id)).ThenBy(row => row.Id, StringComparer.Ordinal).Select(Map).ToArray();
    }

    public async Task<Medication?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        // The ordinary predicate permits the existing slug index; C collation guarantees
        // exact equality even if the database's default collation is nondeterministic.
        var row = await Query().SingleOrDefaultAsync(row => row.Slug == slug && EF.Functions.Collate(row.Slug, "C") == slug, cancellationToken);
        return row is null ? null : Map(row);
    }

    private static Medication Map(MedicationEntity row) => new()
    {
        Id = row.Id, Slug = row.Slug, DisplayName = row.DisplayName, StrengthLabel = row.StrengthLabel,
        FormCode = row.FormCode, PresentationLabel = row.PresentationLabel, CategoryCode = row.CategoryCode,
        PrescriptionRequired = row.PrescriptionRequired,
        ActiveIngredients = row.ActiveIngredients.OrderBy(ingredient => ingredient.Position).Select(ingredient => ingredient.Ingredient).ToArray()
    };
}
