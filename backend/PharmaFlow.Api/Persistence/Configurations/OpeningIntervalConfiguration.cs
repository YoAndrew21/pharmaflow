using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Persistence.Configurations;

public sealed class OpeningIntervalConfiguration : IEntityTypeConfiguration<OpeningIntervalEntity>
{
    public void Configure(EntityTypeBuilder<OpeningIntervalEntity> builder)
    {
        builder.ToTable("opening_intervals", table =>
        {
            table.HasCheckConstraint("ck_opening_intervals_weekday", "day_of_week BETWEEN 1 AND 7");
            table.HasCheckConstraint("ck_opening_intervals_position", "position >= 0");
            table.HasCheckConstraint("ck_opening_intervals_time_order", "opens_at < closes_at");
        });
        builder.HasKey(row => new { row.PharmacyBranchId, row.DayOfWeek, row.Position }).HasName("pk_opening_intervals");
        builder.Property(row => row.PharmacyBranchId).HasColumnName("pharmacy_branch_id").HasColumnType("text");
        builder.Property(row => row.DayOfWeek).HasColumnName("day_of_week").HasColumnType("integer").ValueGeneratedNever();
        builder.Property(row => row.Position).HasColumnName("position").HasColumnType("integer").ValueGeneratedNever();
        builder.Property(row => row.OpensAt).HasColumnName("opens_at").HasColumnType("time without time zone").IsRequired();
        builder.Property(row => row.ClosesAt).HasColumnName("closes_at").HasColumnType("time without time zone").IsRequired();
        builder.HasOne(row => row.PharmacyBranch).WithMany(row => row.OpeningIntervals)
            .HasForeignKey(row => row.PharmacyBranchId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_opening_intervals_pharmacy_branches");
    }
}
