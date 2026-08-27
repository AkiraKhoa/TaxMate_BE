using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Model.Persistence.Configurations;

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
            x.RecommendedFormCode,
            x.Version
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.TaxPeriodId,
            x.RecommendedFormCode
        })
            .IsUnique()
            .HasFilter("\"IsCurrent\" = TRUE");

        builder.Property(x => x.TaxMethod)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CalculationDataJson)
            .HasColumnType("jsonb");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TaxCalculations_TaxMethod",
            "\"TaxMethod\" IN ('RevenueBased', 'IncomeBased', 'NotApplicable')"));

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.TaxCalculation)
            .HasForeignKey(x => x.TaxCalculationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
