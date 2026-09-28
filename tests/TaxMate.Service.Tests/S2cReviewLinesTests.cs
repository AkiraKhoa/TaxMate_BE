using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class S2cReviewLinesTests
{
    [Fact]
    public async Task ReviewLines_IncludeExcludedAndUnmappedExpensesWithoutChangingTotals()
    {
        var owner = Guid.NewGuid();
        var business = Guid.NewGuid();
        var revenue = new Mock<IOwnerRevenueProjector>();
        var sources = new Mock<IAccountingScopeReadRepository>();
        var movements = new Mock<IInventoryMovementRepository>();
        var inventory = new Mock<IS2dBookProjector>();
        var periods = new Mock<ITaxPeriodRepository>();
        revenue.Setup(x => x.ProjectBusinessAsync(owner, business, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(owner, DateTime.MinValue, DateTime.MaxValue, 0m, 0m, []));
        movements.Setup(x => x.GetBeforeAsync(business, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        inventory.Setup(x => x.ProjectQuarter(business, It.IsAny<IReadOnlyList<InventoryMovement>>(), 2026, 1, It.IsAny<bool>()))
            .Returns(new S2dBook { BusinessId = business });
        var date = new DateTime(2026, 1, 15);
        sources.Setup(x => x.GetS2cExpensesAsync(business, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new S2cExpenseSource(Guid.NewGuid(), "PC-1", date, "Dịch vụ", 1_000_000m, "Dịch vụ", S2cGroupCodes.PurchasedServices, "Transfer", false, false),
                new S2cExpenseSource(Guid.NewGuid(), "PC-2", date, "Chi tiền mặt", 6_000_000m, "Khác", S2cGroupCodes.OtherDirect, PaymentMethods.Cash, true, false),
                new S2cExpenseSource(Guid.NewGuid(), "PC-3", date, "Chưa phân loại", 2_000_000m, "Khác", null, "Transfer", true, false),
                new S2cExpenseSource(Guid.NewGuid(), "PNK-1", date, "Nhập hàng", 9_000_000m, "Hàng hóa", null, "Transfer", true, true)
            ]);
        var service = new S2cBookProjector(revenue.Object, sources.Object, movements.Object, inventory.Object, periods.Object);

        var book = await service.ProjectQuarterAsync(owner, business, 2026, 1);

        Assert.Equal(1_000_000m, book.TotalExpense);
        Assert.Single(book.Lines);
        Assert.Equal(4, book.ReviewLines.Count);
        var excluded = book.ReviewLines.Single(x => x.VoucherNumber == "PC-2");
        Assert.Equal(0m, excluded.IncludedAmount);
        Assert.Contains("CashExpenseExcluded", excluded.IssueCodes);
        Assert.Equal(0m, book.ReviewLines.Single(x => x.VoucherNumber == "PC-3").IncludedAmount);
        Assert.Null(book.ReviewLines.Single(x => x.VoucherNumber == "PNK-1").IncludedAmount);
        Assert.Contains("MissingExpenseEvidence", book.ReviewLines.Single(x => x.VoucherNumber == "PC-1").IssueCodes);
    }
}
