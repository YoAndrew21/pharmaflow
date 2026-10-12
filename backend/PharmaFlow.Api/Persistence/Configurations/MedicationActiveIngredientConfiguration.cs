using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Persistence.Configurations;

public sealed class MedicationActiveIngredientConfiguration : IEntityTypeConfiguration<MedicationActiveIngredientEntity>
{
    public void Configure(EntityTypeBuilder<MedicationActiveIngredientEntity> builder)
    {
        builder.ToTable("medication_active_ingredients", table =>
            table.HasCheckConstraint("ck_medication_active_ingredients_position", "position >= 0"));
        builder.HasKey(row => new { row.MedicationId, row.Position }).HasName("pk_medication_active_ingredients");
        builder.Property(row => row.MedicationId).HasColumnName("medication_id").HasColumnType("text");
        builder.Property(row => row.Position).HasColumnName("position").HasColumnType("integer").ValueGeneratedNever();
        builder.Property(row => row.Ingredient).HasColumnName("ingredient").HasColumnType("text").IsRequired();
        builder.HasOne(row => row.Medication).WithMany(row => row.ActiveIngredients)
            .HasForeignKey(row => row.MedicationId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_medication_active_ingredients_medications");
    }
}
