using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Common;
using TaxMate.Model.Entities;

namespace TaxMate.Model.Persistence.Configurations;

public class TaxPeriodConfiguration : IEntityTypeConfiguration<TaxPeriod>
{
    public void Configure(EntityTypeBuilder<TaxPeriod> builder)
    {
        builder.ToTable("TaxPeriods");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.BusinessId, x.Year, x.Month })
            .IsUnique()
            .HasFilter($"\"PeriodType\" = '{TaxPeriodTypes.Monthly}'");

        builder.HasIndex(x => new { x.BusinessId, x.Year, x.Quarter })
            .IsUnique()
            .HasFilter($"\"PeriodType\" = '{TaxPeriodTypes.Quarterly}'");

        builder.HasIndex(x => new { x.BusinessId, x.Year })
            .IsUnique()
            .HasFilter($"\"PeriodType\" = '{TaxPeriodTypes.Yearly}'");

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

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TaxPeriods_PeriodShape",
            "(\"PeriodType\" = 'Monthly' AND \"Month\" BETWEEN 1 AND 12 AND \"Quarter\" IS NULL) OR " +
            "(\"PeriodType\" = 'Quarterly' AND \"Month\" IS NULL AND \"Quarter\" BETWEEN 1 AND 4) OR " +
            "(\"PeriodType\" = 'Yearly' AND \"Month\" IS NULL AND \"Quarter\" IS NULL)"));

        builder.HasOne(x => x.Business)
            .WithMany(x => x.TaxPeriods)
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

        builder.HasOne(x => x.EvidenceReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.EvidenceReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TaxPeriods_EvidenceReviewPair",
            "(\"EvidenceReviewedAt\" IS NULL AND \"EvidenceReviewedByUserId\" IS NULL) OR " +
            "(\"EvidenceReviewedAt\" IS NOT NULL AND \"EvidenceReviewedByUserId\" IS NOT NULL)"));
    }
}
