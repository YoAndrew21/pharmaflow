using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Api.Models;
using PharmaFlow.Api.Persistence;

namespace PharmaFlow.Api.Services;

public sealed class DatabaseAvailabilityService(PharmaFlowDbContext context, CanonicalCatalogOrder order) : IAvailabilityService
{
    public async Task<IReadOnlyList<Availability>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // No parent loading, cross joins, inferred observations, or fixture fallback.
        var rows = await context.Availability.AsNoTracking().Select(row => new
        {
            row.MedicationId, row.BranchId, row.Status, row.PriceMinorUnits, row.PriceCurrencyCode, row.UpdatedAt
        }).ToListAsync(cancellationToken);
        return rows.OrderBy(row => order.AvailabilityRank(row.MedicationId, row.BranchId))
            .ThenBy(row => row.MedicationId, StringComparer.Ordinal).ThenBy(row => row.BranchId, StringComparer.Ordinal)
            .Select(row => new Availability
            {
                MedicationId = row.MedicationId, BranchId = row.BranchId, Status = row.Status,
                ReferencePrice = row.PriceMinorUnits is { } amount ? new ReferencePrice { MinorUnits = amount, CurrencyCode = row.PriceCurrencyCode! } : null,
                UpdatedAt = row.UpdatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            }).ToArray();
    }
}
