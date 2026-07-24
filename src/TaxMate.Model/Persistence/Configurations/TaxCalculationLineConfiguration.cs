using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Infrastructure.Persistence.Configurations;

public class TaxCalculationLineConfiguration
    : IEntityTypeConfiguration<TaxCalculationLine>
{
    public void Configure(EntityTypeBuilder<TaxCalculationLine> builder)
    {
        builder.ToTable("TaxCalculationLines");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.TaxCalculationId,
            x.SectionCode,
            x.IndicatorCode,
            x.BusinessLocationId
        });

        builder.Property(x => x.SectionCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.IndicatorCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.BusinessActivityCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.BusinessActivityName)
            .HasMaxLength(255)
            .IsRequired();
    }
}