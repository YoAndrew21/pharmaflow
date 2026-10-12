using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Persistence.Configurations;

public sealed class PharmacyBranchConfiguration : IEntityTypeConfiguration<PharmacyBranchEntity>
{
    public void Configure(EntityTypeBuilder<PharmacyBranchEntity> builder)
    {
        builder.ToTable("pharmacy_branches");
        builder.HasKey(row => row.Id).HasName("pk_pharmacy_branches");
        builder.Property(row => row.Id).HasColumnName("id").HasColumnType("text").ValueGeneratedNever();
        builder.Property(row => row.Slug).HasColumnName("slug").HasColumnType("text").IsRequired();
        builder.HasIndex(row => row.Slug).IsUnique().HasDatabaseName("ux_pharmacy_branches_slug");
        builder.Property(row => row.Name).HasColumnName("name").HasColumnType("text").IsRequired();
        builder.Property(row => row.FictionalLocation).HasColumnName("fictional_location").HasColumnType("text").IsRequired();
        builder.Property(row => row.TimeZone).HasColumnName("time_zone").HasColumnType("text").IsRequired();
    }
}
