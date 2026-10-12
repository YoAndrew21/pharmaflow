using Microsoft.EntityFrameworkCore;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Persistence;

public sealed class PharmaFlowDbContext(DbContextOptions<PharmaFlowDbContext> options) : DbContext(options)
{
    public DbSet<MedicationEntity> Medications => Set<MedicationEntity>();
    public DbSet<MedicationActiveIngredientEntity> MedicationActiveIngredients => Set<MedicationActiveIngredientEntity>();
    public DbSet<PharmacyBranchEntity> PharmacyBranches => Set<PharmacyBranchEntity>();
    public DbSet<OpeningIntervalEntity> OpeningIntervals => Set<OpeningIntervalEntity>();
    public DbSet<AvailabilityEntity> Availability => Set<AvailabilityEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PharmaFlowDbContext).Assembly);
    }
}
