using TaxMate.Model.Common;
using TaxMate.Model.Entities;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class S2dBookProjectorTests
{
    private readonly S2dBookProjector _projector =
        new(new InventoryValuationService(
            new FakeAccountingTransactionLockRepository
            {
                HasActiveTransaction = true
            }));

    [Fact]
    public void Project_IncludesSoftDeletedItemWithOnlyOpeningBalance()
    {
        var businessId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ProductCode = "SP-OLD",
            Name = "Old product",
            Unit = "cái",
            IsDeleted = true
        };
        var opening = Movement(
            businessId,
            product,
            InventoryMovementTypes.OpeningBalance,
            4m,
            400m,
            Utc(2025, 12, 31));

        var result = _projector.ProjectQuarter(
            businessId,
            [opening],
            2026,
            1);

        var item = Assert.Single(result.Items);
        Assert.True(item.IsDeleted);
        Assert.Equal("Old product", item.ItemName);
        Assert.Equal(4m, item.OpeningQuantity);
        Assert.Equal(4m, item.EndingQuantity);
        Assert.Empty(item.Lines);
    }

    [Fact]
    public void Project_UsesHalfOpenPeriodBoundary()
    {
        var businessId = Guid.NewGuid();
        var product = ProductFor(businessId);
        var atEnd = Movement(
            businessId,
            product,
            InventoryMovementTypes.PurchaseIn,
            2m,
            200m,
            Utc(2026, 4, 1));

        var result = _projector.ProjectQuarter(
            businessId,
            [
                Movement(
                    businessId,
                    product,
                    InventoryMovementTypes.OpeningBalance,
                    1m,
                    100m,
                    Utc(2025, 12, 31)),
                atEnd
            ],
            2026,
            1);

        var item = Assert.Single(result.Items);
        Assert.Empty(item.Lines);
        Assert.Equal(1m, item.EndingQuantity);
    }

    [Fact]
    public void Project_PreviewUsesCalculatedOutboundAndMarksItProvisional()
    {
        var businessId = Guid.NewGuid();
        var product = ProductFor(businessId);
        var outbound = Movement(
            businessId,
            product,
            InventoryMovementTypes.OrderOut,
            2m,
            null,
            Utc(2026, 2, 1));

        var result = _projector.ProjectQuarter(
            businessId,
            [
                Movement(
                    businessId,
                    product,
                    InventoryMovementTypes.OpeningBalance,
                    10m,
                    1_000m,
                    Utc(2025, 12, 31)),
                outbound
            ],
            2026,
            1);

        var line = Assert.Single(Assert.Single(result.Items).Lines);
        Assert.True(result.IsProvisional);
        Assert.True(line.IsProvisionalValue);
        Assert.Equal(200m, line.OutboundValue);
        Assert.Null(outbound.TotalValue);
    }

    [Fact]
    public void Project_FinalViewBlocksMissingOutboundValue()
    {
        var businessId = Guid.NewGuid();
        var product = ProductFor(businessId);

        var result = _projector.ProjectQuarter(
            businessId,
            [
                Movement(
                    businessId,
                    product,
                    InventoryMovementTypes.OpeningBalance,
                    10m,
                    1_000m,
                    Utc(2025, 12, 31)),
                Movement(
                    businessId,
                    product,
                    InventoryMovementTypes.OrderOut,
                    2m,
                    null,
                    Utc(2026, 2, 1))
            ],
            2026,
            1,
            requireFinalValues: true);

        Assert.False(result.CanFinalize);
        Assert.True(result.IsProvisional);
        Assert.Contains(
            result.Blockers,
            x => x.Code == InventoryBookBlockerCodes.MissingOutboundValue);
    }

    [Fact]
    public void Project_BlocksMissingUnitAndNegativeRunningInventory()
    {
        var businessId = Guid.NewGuid();
        var product = ProductFor(businessId);
        product.Unit = null;

        var result = _projector.ProjectQuarter(
            businessId,
            [
                Movement(
                    businessId,
                    product,
                    InventoryMovementTypes.OpeningBalance,
                    1m,
                    100m,
                    Utc(2025, 12, 31)),
                Movement(
                    businessId,
                    product,
                    InventoryMovementTypes.OrderOut,
                    2m,
                    null,
                    Utc(2026, 1, 2))
            ],
            2026,
            1);

        Assert.Contains(
            result.Blockers,
            x => x.Code == InventoryBookBlockerCodes.MissingUnit);
        Assert.Contains(
            result.Blockers,
            x => x.Code == InventoryBookBlockerCodes.NegativeInventory);
    }

    [Fact]
    public void Project_FinalizedLinesUseStoredValues()
    {
        var businessId = Guid.NewGuid();
        var product = ProductFor(businessId);
        var outbound = Movement(
            businessId,
            product,
            InventoryMovementTypes.OrderOut,
            2m,
            200m,
            Utc(2026, 2, 1));

        var result = _projector.ProjectQuarter(
            businessId,
            [
                Movement(
                    businessId,
                    product,
                    InventoryMovementTypes.OpeningBalance,
                    10m,
                    1_000m,
                    Utc(2025, 12, 31)),
                outbound
            ],
            2026,
            1,
            requireFinalValues: true);

        Assert.False(result.IsProvisional);
        Assert.True(result.CanFinalize);
        Assert.Equal(200m, Assert.Single(Assert.Single(result.Items).Lines).OutboundValue);
    }

    private static Product ProductFor(Guid businessId) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        ProductCode = "SP01",
        Name = "Product",
        Unit = "cái"
    };

    private static InventoryMovement Movement(
        Guid businessId,
        Product product,
        string type,
        decimal quantity,
        decimal? totalValue,
        DateTime occurredAt) => new()
    {
        InventoryMovementId = Guid.NewGuid(),
        BusinessId = businessId,
        ProductId = product.Id,
        Product = product,
        MovementType = type,
        Quantity = quantity,
        TotalValue = totalValue,
        OccurredAt = occurredAt,
        DocumentNumber = "DOC",
        Description = "Description",
        ReferenceId = type is InventoryMovementTypes.PurchaseIn or InventoryMovementTypes.OrderOut
            ? Guid.NewGuid()
            : null
    };

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
