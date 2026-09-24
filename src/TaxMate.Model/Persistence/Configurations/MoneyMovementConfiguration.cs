using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Model.Persistence.Configurations;

public class MoneyMovementConfiguration
    : IEntityTypeConfiguration<MoneyMovement>
{
    public void Configure(EntityTypeBuilder<MoneyMovement> builder)
    {
        builder.ToTable("MoneyMovements", table =>
        {
            table.HasCheckConstraint(
                "CK_MoneyMovements_AmountPositive",
                "\"Amount\" > 0");
            table.HasCheckConstraint(
                "CK_MoneyMovements_Type",
                "\"MovementType\" IN ('PaymentIn', 'ManualIncomeIn', 'ExpenseOut')");
        });

        builder.HasKey(x => x.MoneyMovementId);
        builder.Property(x => x.MovementType)
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.Amount)
            .HasPrecision(20, 2);
        builder.Property(x => x.DocumentNumber)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasOne(x => x.PaymentAccount)
            .WithMany(x => x.MoneyMovements)
            .HasForeignKey(x => x.PaymentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MovementType, x.ReferenceId })
            .IsUnique();
        builder.HasIndex(x => new { x.PaymentAccountId, x.MovementDate });
    }
}
