using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Infrastructure.Persistence.Configurations;

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
            x.Version
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.TaxPeriodId,
            x.IsCurrent
        });

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