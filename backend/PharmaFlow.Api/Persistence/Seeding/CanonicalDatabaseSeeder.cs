using System.Data;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Persistence.Seeding;

/// <summary>Explicit demo synchronization. Never migrates, creates a schema, or deletes unrelated rows.</summary>
public sealed class CanonicalDatabaseSeeder(PharmaFlowDbContext context)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Complete validation before opening a connection or starting any writes.
        var data = CanonicalSeedData.Load();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await context.Medications.LoadAsync(cancellationToken);
        await context.PharmacyBranches.LoadAsync(cancellationToken);
        await context.MedicationActiveIngredients.LoadAsync(cancellationToken);
        await context.OpeningIntervals.LoadAsync(cancellationToken);
        await context.Availability.LoadAsync(cancellationToken);
        ApplyToTrackedEntities(context, data);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    internal static void ApplyToTrackedEntities(PharmaFlowDbContext context, CanonicalSeedData data)
    {
        // Fail instead of deleting extra child rows or stealing another entity's unique slug.
        CheckSlugs(data.Medications.Select(row => (row.Id, row.Slug)), context.Medications.Local.Select(row => (row.Id, row.Slug)));
        CheckSlugs(data.Branches.Select(row => (row.Id, row.Slug)), context.PharmacyBranches.Local.Select(row => (row.Id, row.Slug)));
        var medicationIds = data.Medications.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var branchIds = data.Branches.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var ingredientKeys = data.Ingredients.Select(row => (row.MedicationId, row.Position)).ToHashSet();
        var intervalKeys = data.OpeningIntervals.Select(row => (row.PharmacyBranchId, row.DayOfWeek, row.Position)).ToHashSet();
        if (context.MedicationActiveIngredients.Local.Any(row => medicationIds.Contains(row.MedicationId) && !ingredientKeys.Contains((row.MedicationId, row.Position))) ||
            context.OpeningIntervals.Local.Any(row => branchIds.Contains(row.PharmacyBranchId) && !intervalKeys.Contains((row.PharmacyBranchId, row.DayOfWeek, row.Position))))
            throw new InvalidOperationException("Extra child rows belong to canonical demo parents; review them explicitly before seeding. No rows were deleted.");

        Upsert(context, data.Medications, row => row.Id);
        Upsert(context, data.Branches, row => row.Id);
        Upsert(context, data.Ingredients, row => (row.MedicationId, row.Position));
        Upsert(context, data.OpeningIntervals, row => (row.PharmacyBranchId, row.DayOfWeek, row.Position));
        Upsert(context, data.Availability, row => (row.MedicationId, row.BranchId));
    }

    private static void CheckSlugs(IEnumerable<(string Id, string Slug)> desired, IEnumerable<(string Id, string Slug)> existing)
    {
        var owners = existing.ToDictionary(row => row.Slug, row => row.Id, StringComparer.Ordinal);
        foreach (var row in desired)
            if (owners.TryGetValue(row.Slug, out var owner) && owner != row.Id)
                throw new InvalidOperationException("A canonical slug is owned by a different database entity. No rows were overwritten.");
    }

    private static void Upsert<TEntity, TKey>(PharmaFlowDbContext context, IEnumerable<TEntity> desired, Func<TEntity, TKey> key)
        where TEntity : class where TKey : notnull
    {
        var existing = context.Set<TEntity>().Local.ToDictionary(key);
        foreach (var row in desired)
        {
            if (existing.TryGetValue(key(row), out var tracked)) context.Entry(tracked).CurrentValues.SetValues(row);
            else context.Set<TEntity>().Add(row);
        }
    }
}
