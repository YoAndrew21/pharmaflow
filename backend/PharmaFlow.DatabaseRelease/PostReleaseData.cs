using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Seeding;

namespace PharmaFlow.DatabaseRelease;

internal static class PostReleaseData
{
    internal static async Task VerifyAsync(PharmaFlowDbContext context, NpgsqlConnection connection,
        NpgsqlTransaction transaction, Dictionary<string, bool> checks, CancellationToken token)
    {
        var canonical = CanonicalSeedData.Load();
        // Direct commands on the private data source avoid EF command diagnostic events.
        // EF is used only for trusted scalar/column metadata, never for database initialization.
        var medications = await Read<PharmaFlow.Api.Persistence.Entities.MedicationEntity>();
        var ingredients = await Read<PharmaFlow.Api.Persistence.Entities.MedicationActiveIngredientEntity>();
        var branches = await Read<PharmaFlow.Api.Persistence.Entities.PharmacyBranchEntity>();
        var intervals = await Read<PharmaFlow.Api.Persistence.Entities.OpeningIntervalEntity>();
        var availability = await Read<PharmaFlow.Api.Persistence.Entities.AvailabilityEntity>();
        checks["medications_count_exact"] = medications.Count == 9 && medications.Count == canonical.Medications.Count;
        checks["ingredients_count_exact"] = ingredients.Count == 10 && ingredients.Count == canonical.Ingredients.Count;
        checks["pharmacy_branches_count_exact"] = branches.Count == 3 && branches.Count == canonical.Branches.Count;
        checks["opening_intervals_count_exact"] = intervals.Count == 18 && intervals.Count == canonical.OpeningIntervals.Count;
        checks["availability_count_exact"] = availability.Count == 21 && availability.Count == canonical.Availability.Count;
        checks["canonical_values_match"] = Equal(medications, canonical.Medications) && Equal(ingredients, canonical.Ingredients)
            && Equal(branches, canonical.Branches) && Equal(intervals, canonical.OpeningIntervals) && Equal(availability, canonical.Availability);
        var medicationIds = medications.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var branchIds = branches.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        checks["relationships_valid"] = ingredients.All(row => medicationIds.Contains(row.MedicationId))
            && intervals.All(row => branchIds.Contains(row.PharmacyBranchId))
            && availability.All(row => medicationIds.Contains(row.MedicationId) && branchIds.Contains(row.BranchId))
            && availability.Select(row => (row.MedicationId, row.BranchId)).Distinct().Count() == availability.Count;

        bool Equal<T>(IEnumerable<T> actual, IEnumerable<T> expected) where T : class
        {
            // Scalar model properties only: navigation collections are not part of row identity/value comparison.
            var properties = context.Model.FindEntityType(typeof(T))!.GetProperties().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
            string Row(T row) => JsonSerializer.Serialize(properties.Select(p =>
            {
                var value = p.PropertyInfo!.GetValue(row);
                return value is DateTimeOffset instant ? instant.ToUniversalTime() : value;
            }).ToArray());
            return actual.Select(Row).Order(StringComparer.Ordinal).SequenceEqual(expected.Select(Row).Order(StringComparer.Ordinal));
        }

        async Task<List<T>> Read<T>() where T : class
        {
            var entity = context.Model.FindEntityType(typeof(T))!;
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var properties = entity.GetProperties().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
            using var identifiers = new NpgsqlCommandBuilder();
            var sql = "SELECT " + string.Join(',', properties.Select(p => identifiers.QuoteIdentifier(p.GetColumnName(table)!)))
                + " FROM public." + identifiers.QuoteIdentifier(table.Name);
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(token);
            var rows = new List<T>();
            while (await reader.ReadAsync(token))
            {
                var row = Activator.CreateInstance<T>();
                for (var i = 0; i < properties.Length; i++)
                {
                    object? value = reader.IsDBNull(i) ? null
                        : properties[i].ClrType == typeof(DateTimeOffset) ? reader.GetFieldValue<DateTimeOffset>(i)
                        : properties[i].ClrType == typeof(TimeOnly) ? reader.GetFieldValue<TimeOnly>(i) : reader.GetValue(i);
                    properties[i].PropertyInfo!.SetValue(row, value);
                }
                rows.Add(row);
            }
            return rows;
        }
    }
}
