using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Persistence.Configurations;

public sealed class AvailabilityConfiguration : IEntityTypeConfiguration<AvailabilityEntity>
{
    public void Configure(EntityTypeBuilder<AvailabilityEntity> builder)
    {
        builder.ToTable("availability", table =>
        {
            table.HasCheckConstraint("ck_availability_status", "status IN ('available', 'low-stock', 'unavailable')");
            table.HasCheckConstraint("ck_availability_price_nonnegative", "price_minor_units IS NULL OR price_minor_units >= 0");
            table.HasCheckConstraint("ck_availability_price_consistency",
                "(price_minor_units IS NULL AND price_currency_code IS NULL) OR (price_minor_units IS NOT NULL AND price_currency_code IS NOT NULL)");
        });
        builder.HasKey(row => new { row.MedicationId, row.BranchId }).HasName("pk_availability");
        builder.Property(row => row.MedicationId).HasColumnName("medication_id").HasColumnType("text");
        builder.Property(row => row.BranchId).HasColumnName("branch_id").HasColumnType("text");
        builder.Property(row => row.Status).HasColumnName("status").HasColumnType("text").IsRequired();
        builder.Property(row => row.PriceMinorUnits).HasColumnName("price_minor_units").HasColumnType("bigint").IsRequired(false);
        builder.Property(row => row.PriceCurrencyCode).HasColumnName("price_currency_code").HasColumnType("text").IsRequired(false);
        builder.Property(row => row.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasOne(row => row.Medication).WithMany(row => row.Availability)
            .HasForeignKey(row => row.MedicationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_availability_medications");
        builder.HasOne(row => row.PharmacyBranch).WithMany(row => row.Availability)
            .HasForeignKey(row => row.BranchId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_availability_pharmacy_branches");
        builder.HasIndex(row => row.BranchId).HasDatabaseName("ix_availability_branch_id");
    }
}
