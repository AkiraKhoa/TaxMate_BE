using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Infrastructure.Persistence.Configurations;

public class TaxCalculationConfiguration
    : IEntityTypeConfiguration<TaxCalculation>
{
    public void Configure(EntityTypeBuilder<TaxCalculation> builder)
    {
        builder.ToTable("TaxCalculations");

        builder.HasKey(x => x.Id);

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

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.TaxCalculation)
            .HasForeignKey(x => x.TaxCalculationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}