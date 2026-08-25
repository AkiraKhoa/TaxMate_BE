using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Model.Persistence.Configurations;

public class PaymentAccountConfiguration
    : IEntityTypeConfiguration<PaymentAccount>
{
    public void Configure(EntityTypeBuilder<PaymentAccount> builder)
    {
        builder.Property(x => x.AccountType)
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.InitialBalance)
            .HasPrecision(20, 2);

        builder.HasIndex(x => new { x.BusinessId, x.AccountType, x.IsActive });
        builder.HasIndex(x => x.BusinessId, "IX_PaymentAccounts_OneCashAccount")
            .IsUnique()
            .HasFilter("\"AccountType\" = 'Cash'");
        builder.HasIndex(x => x.BusinessId, "IX_PaymentAccounts_OneActiveDefaultBank")
            .IsUnique()
            .HasFilter("\"AccountType\" = 'Bank' AND \"IsDefault\" = TRUE AND \"IsActive\" = TRUE");
        builder.HasIndex(x => x.SePayBankAccountXid)
            .IsUnique()
            .HasFilter("\"SePayBankAccountXid\" IS NOT NULL");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_PaymentAccounts_AccountType",
                "\"AccountType\" IN ('Cash', 'Bank')");
            table.HasCheckConstraint(
                "CK_PaymentAccounts_BankFields",
                "\"AccountType\" <> 'Bank' OR (\"BankShortName\" IS NOT NULL AND \"BankName\" IS NOT NULL AND " +
                "\"AccountNumber\" IS NOT NULL AND \"AccountName\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_PaymentAccounts_InitialBalancePair",
                "(\"InitialBalance\" IS NULL AND \"InitialBalanceDate\" IS NULL) OR " +
                "(\"InitialBalance\" IS NOT NULL AND \"InitialBalanceDate\" IS NOT NULL)");
        });
    }
}
