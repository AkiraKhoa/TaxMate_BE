using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Model.Persistence.Configurations;

public class TaxDeclarationConfiguration
    : IEntityTypeConfiguration<TaxDeclaration>
{
    public void Configure(EntityTypeBuilder<TaxDeclaration> builder)
    {
        builder.ToTable("TaxDeclarations");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.DeclarationCode)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.TaxPeriodId,
            x.FormCode,
            x.Version
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.TaxPeriodId,
            x.FormCode
        })
            .IsUnique()
            .HasFilter("\"IsCurrent\" = TRUE");

        builder.Property(x => x.FormDataJson)
            .HasColumnType("jsonb");

        builder.HasOne(x => x.TaxCalculation)
            .WithMany(x => x.TaxDeclarations)
            .HasForeignKey(x => x.TaxCalculationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.TaxDeclaration)
            .HasForeignKey(x => x.TaxDeclarationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Obligations)
            .WithOne(x => x.TaxDeclaration)
            .HasForeignKey(x => x.TaxDeclarationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
