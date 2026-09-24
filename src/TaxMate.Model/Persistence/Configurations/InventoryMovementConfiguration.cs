using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxMate.Model.Entities;

namespace TaxMate.Model.Persistence.Configurations;

public class InventoryMovementConfiguration
    : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements", table =>
        {
            table.HasCheckConstraint(
                "CK_InventoryMovements_ExactlyOneItem",
                "(\"ProductId\" IS NOT NULL AND \"IngredientId\" IS NULL) OR " +
                "(\"ProductId\" IS NULL AND \"IngredientId\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_InventoryMovements_QuantityPositive",
                "\"Quantity\" > 0");
            table.HasCheckConstraint(
                "CK_InventoryMovements_TotalValueNonNegative",
                "\"TotalValue\" IS NULL OR \"TotalValue\" >= 0");
            table.HasCheckConstraint(
                "CK_InventoryMovements_Type",
                "\"MovementType\" IN ('OpeningBalance', 'PurchaseIn', 'OrderOut', 'AdjustmentIn', 'AdjustmentOut')");
            table.HasCheckConstraint(
                "CK_InventoryMovements_ReferenceShape",
                "(\"MovementType\" IN ('PurchaseIn', 'OrderOut') AND \"ReferenceId\" IS NOT NULL) OR " +
                "(\"MovementType\" IN ('OpeningBalance', 'AdjustmentIn', 'AdjustmentOut') AND \"ReferenceId\" IS NULL)");
        });

        builder.HasKey(x => x.InventoryMovementId);

        builder.Property(x => x.MovementType)
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.Quantity)
            .HasPrecision(18, 6);
        builder.Property(x => x.TotalValue)
            .HasPrecision(20, 2);
        builder.Property(x => x.DocumentNumber)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasOne(x => x.Business)
            .WithMany(x => x.InventoryMovements)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Product)
            .WithMany(x => x.InventoryMovements)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Ingredient)
            .WithMany(x => x.InventoryMovements)
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.BusinessId, x.OccurredAt });
        builder.HasIndex(x => new { x.ProductId, x.OccurredAt })
            .HasFilter("\"ProductId\" IS NOT NULL");
        builder.HasIndex(x => new { x.IngredientId, x.OccurredAt })
            .HasFilter("\"IngredientId\" IS NOT NULL");
        builder.HasIndex(x => new { x.MovementType, x.ReferenceId });
        builder.HasIndex(x => new
            {
                x.MovementType,
                x.ReferenceId,
                x.ProductId
            })
            .IsUnique()
            .HasFilter(
                "\"ReferenceId\" IS NOT NULL AND \"ProductId\" IS NOT NULL");
        builder.HasIndex(x => new
            {
                x.MovementType,
                x.ReferenceId,
                x.IngredientId
            })
            .IsUnique()
            .HasFilter(
                "\"ReferenceId\" IS NOT NULL AND \"IngredientId\" IS NOT NULL");
        builder.HasIndex(x => x.ProductId)
            .IsUnique()
            .HasFilter("\"MovementType\" = 'OpeningBalance' AND \"ProductId\" IS NOT NULL");
        builder.HasIndex(x => x.IngredientId)
            .IsUnique()
            .HasFilter("\"MovementType\" = 'OpeningBalance' AND \"IngredientId\" IS NOT NULL");
    }
}
