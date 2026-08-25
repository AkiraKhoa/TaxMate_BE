using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Model.Persistence.Configurations;

public class UserTaxProfileConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Users_DeclaredRevenueBracket",
                "\"DeclaredRevenueBracket\" IS NULL OR \"DeclaredRevenueBracket\" IN ('AtOrBelow1B', 'Over1BTo3B', 'Over3BTo50B')");
            table.HasCheckConstraint(
                "CK_Users_PersonalIncomeTaxMethod",
                "\"PersonalIncomeTaxMethod\" IS NULL OR \"PersonalIncomeTaxMethod\" IN ('RevenueBased', 'IncomeBased')");
            table.HasCheckConstraint(
                "CK_Users_TaxMethodPair",
                "(\"PersonalIncomeTaxMethod\" IS NULL AND \"TaxMethodEffectiveYear\" IS NULL) OR " +
                "(\"PersonalIncomeTaxMethod\" IS NOT NULL AND \"TaxMethodEffectiveYear\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Users_CommencementPeriod",
                "\"CommencementPeriod\" IS NULL OR \"CommencementPeriod\" IN ('BeforeTaxYear', 'FirstHalfOfTaxYear', 'SecondHalfOfTaxYear')");
            table.HasCheckConstraint(
                "CK_Users_CommencementPair",
                "(\"CommencementPeriod\" IS NULL AND \"CommencementTaxYear\" IS NULL) OR " +
                "(\"CommencementPeriod\" IS NOT NULL AND \"CommencementTaxYear\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Users_TaxProfileCompatibility",
                "(\"DeclaredRevenueBracket\" IS NULL AND \"PersonalIncomeTaxMethod\" IS NULL AND " +
                "\"TaxMethodEffectiveYear\" IS NULL AND \"CommencementPeriod\" IS NULL AND " +
                "\"CommencementTaxYear\" IS NULL AND \"TaxProfileConfirmedAt\" IS NULL) OR " +
                "(\"DeclaredRevenueBracket\" = 'AtOrBelow1B' AND \"PersonalIncomeTaxMethod\" IS NULL) OR " +
                "(\"DeclaredRevenueBracket\" = 'Over1BTo3B' AND \"PersonalIncomeTaxMethod\" IN ('RevenueBased', 'IncomeBased') AND \"CommencementPeriod\" IS NULL) OR " +
                "(\"DeclaredRevenueBracket\" = 'Over3BTo50B' AND \"PersonalIncomeTaxMethod\" = 'IncomeBased' AND \"CommencementPeriod\" IS NULL)");
        });
    }
}

public class RevenueThresholdAlertConfiguration
    : IEntityTypeConfiguration<RevenueThresholdAlert>
{
    public void Configure(EntityTypeBuilder<RevenueThresholdAlert> builder)
    {
        builder.Property(x => x.ThresholdCode)
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => new { x.OwnerId, x.Year, x.ThresholdCode })
            .IsUnique();
        builder.HasIndex(x => new { x.OwnerId, x.Status });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_RevenueThresholdAlerts_Code",
                "\"ThresholdCode\" IN ('Crossed1B', 'Crossed3B', 'Crossed50B')");
            table.HasCheckConstraint(
                "CK_RevenueThresholdAlerts_Amount",
                "\"ThresholdAmount\" > 0");
            table.HasCheckConstraint(
                "CK_RevenueThresholdAlerts_Status",
                "\"Status\" IN ('PendingReview', 'Acknowledged', 'Resolved')");
            table.HasCheckConstraint(
                "CK_RevenueThresholdAlerts_Resolution",
                "(\"Status\" = 'Resolved' AND \"ResolvedAt\" IS NOT NULL) OR " +
                "(\"Status\" <> 'Resolved' AND \"ResolvedAt\" IS NULL)");
        });
    }
}

public class TaxPaymentConfiguration : IEntityTypeConfiguration<TaxPayment>
{
    public void Configure(EntityTypeBuilder<TaxPayment> builder)
    {
        builder.Property(x => x.TaxType)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => new { x.TaxType, x.Status, x.PaymentDate });

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TaxPayments_TaxType",
            "\"TaxType\" IN ('VAT', 'PIT', 'Unknown')"));
    }
}
