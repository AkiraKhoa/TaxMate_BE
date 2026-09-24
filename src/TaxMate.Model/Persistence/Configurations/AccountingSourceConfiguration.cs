using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Model.Persistence.Configurations;

public class TransactionAccountingConfiguration
    : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasIndex(x => new { x.BusinessId, x.CompletedAt });
    }
}

public class IncomeAccountingConfiguration
    : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.Property(x => x.AccountingType)
            .HasMaxLength(30);

        builder.HasOne(x => x.Transaction)
            .WithOne(x => x.GeneratedIncome)
            .HasForeignKey<Income>(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TransactionId)
            .IsUnique()
            .HasFilter("\"TransactionId\" IS NOT NULL");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Incomes_AccountingType",
            "\"AccountingType\" IS NULL OR \"AccountingType\" IN ('BusinessRevenue', 'NonRevenueCashIn')"));
    }
}

public class ExpenseAccountingConfiguration
    : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.Property(x => x.VoucherNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => new { x.BusinessId, x.VoucherNumber })
            .IsUnique();
    }
}

public class ExpenseCategoryAccountingConfiguration
    : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.Property(x => x.S2cGroupCode)
            .HasMaxLength(30);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ExpenseCategories_S2cGroupCode",
            "\"S2cGroupCode\" IS NULL OR \"S2cGroupCode\" IN ('Labor', 'PurchasedServices', 'OtherDirect')"));
    }
}

public class IngredientPurchaseAccountingConfiguration
    : IEntityTypeConfiguration<IngredientPurchase>
{
    public void Configure(EntityTypeBuilder<IngredientPurchase> builder)
    {
        builder.HasOne(x => x.Expense)
            .WithMany(x => x.IngredientPurchases)
            .HasForeignKey(x => x.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ExpenseId);
    }
}
