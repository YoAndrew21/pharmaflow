using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaFlow.Api.Persistence.Entities;

namespace PharmaFlow.Api.Persistence.Configurations;

public sealed class MedicationConfiguration : IEntityTypeConfiguration<MedicationEntity>
{
    public void Configure(EntityTypeBuilder<MedicationEntity> builder)
    {
        builder.ToTable("medications");
        builder.HasKey(row => row.Id).HasName("pk_medications");
        builder.Property(row => row.Id).HasColumnName("id").HasColumnType("text").ValueGeneratedNever();
        builder.Property(row => row.Slug).HasColumnName("slug").HasColumnType("text").IsRequired();
        builder.HasIndex(row => row.Slug).IsUnique().HasDatabaseName("ux_medications_slug");
        builder.Property(row => row.DisplayName).HasColumnName("display_name").HasColumnType("text").IsRequired();
        builder.Property(row => row.StrengthLabel).HasColumnName("strength_label").HasColumnType("text").IsRequired();
        builder.Property(row => row.FormCode).HasColumnName("form_code").HasColumnType("text").IsRequired();
        builder.Property(row => row.PresentationLabel).HasColumnName("presentation_label").HasColumnType("text").IsRequired();
        builder.Property(row => row.CategoryCode).HasColumnName("category_code").HasColumnType("text").IsRequired();
        builder.Property(row => row.PrescriptionRequired).HasColumnName("prescription_required").HasColumnType("boolean").IsRequired();
    }
}
