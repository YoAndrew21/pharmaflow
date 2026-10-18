using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Api.Models;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Services;

public sealed class DatabasePharmacyService(PharmaFlowDbContext context, CanonicalCatalogOrder order) : IPharmacyService
{
    private IQueryable<PharmacyBranchEntity> Query() => context.PharmacyBranches.AsNoTracking()
        .Include(row => row.OpeningIntervals).AsSingleQuery();

    public async Task<IReadOnlyList<PharmacyBranch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Query().ToListAsync(cancellationToken);
        return rows.OrderBy(row => order.BranchRank(row.Id)).ThenBy(row => row.Id, StringComparer.Ordinal).Select(Map).ToArray();
    }

    public async Task<PharmacyBranch?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var row = await Query().SingleOrDefaultAsync(row => row.Slug == slug && EF.Functions.Collate(row.Slug, "C") == slug, cancellationToken);
        return row is null ? null : Map(row);
    }

    private static PharmacyBranch Map(PharmacyBranchEntity row)
    {
        OpeningInterval[] Day(int day) => row.OpeningIntervals.Where(interval => interval.DayOfWeek == day)
            .OrderBy(interval => interval.Position).Select(interval => new OpeningInterval
            {
                OpensAt = interval.OpensAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                ClosesAt = interval.ClosesAt.ToString("HH:mm", CultureInfo.InvariantCulture)
            }).ToArray();
        return new()
        {
            Id = row.Id, Slug = row.Slug, Name = row.Name, FictionalLocation = row.FictionalLocation, TimeZone = row.TimeZone,
            OpeningHours = new() { Monday = Day(1), Tuesday = Day(2), Wednesday = Day(3), Thursday = Day(4),
                Friday = Day(5), Saturday = Day(6), Sunday = Day(7) }
        };
    }
}
