using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Infrastructure.Persistence.Configurations;

public class TaxPeriodConfiguration : IEntityTypeConfiguration<TaxPeriod>
{
    public void Configure(EntityTypeBuilder<TaxPeriod> builder)
    {
        builder.ToTable("TaxPeriods");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.BusinessId,
            x.PeriodType,
            x.Year,
            x.Month,
            x.Quarter
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.BusinessId,
            x.Status
        });

        builder.Property(x => x.PeriodType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.TaxCalculations)
            .WithOne(x => x.TaxPeriod)
            .HasForeignKey(x => x.TaxPeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.TaxDeclarations)
            .WithOne(x => x.TaxPeriod)
            .HasForeignKey(x => x.TaxPeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.TaxPayments)
            .WithOne(x => x.TaxPeriod)
            .HasForeignKey(x => x.TaxPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}