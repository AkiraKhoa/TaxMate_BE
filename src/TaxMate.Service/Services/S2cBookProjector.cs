using TaxMate.Model.Common;
using TaxMate.Model.DTO.Expense;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class S2cBookProjector : IS2cBookProjector
{
    private const decimal CashPaymentDeductionThreshold = 5_000_000m;
    private readonly IOwnerRevenueProjector _revenueProjector;
    private readonly IAccountingScopeReadRepository _accountingSources;
    private readonly IInventoryMovementRepository _inventoryMovements;
    private readonly IS2dBookProjector _s2dProjector;
    private readonly ITaxPeriodRepository _taxPeriods;

    public S2cBookProjector(
        IOwnerRevenueProjector revenueProjector,
        IAccountingScopeReadRepository accountingSources,
        IInventoryMovementRepository inventoryMovements,
        IS2dBookProjector s2dProjector,
        ITaxPeriodRepository taxPeriods)
    {
        _revenueProjector = revenueProjector;
        _accountingSources = accountingSources;
        _inventoryMovements = inventoryMovements;
        _s2dProjector = s2dProjector;
        _taxPeriods = taxPeriods;
    }

    public async Task<S2cBookProjection> ProjectQuarterAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        var (periodStart, periodEndExclusive) =
            BangkokBusinessTime.GetQuarterNaiveUtc(year, quarter);
        var revenue = await _revenueProjector.ProjectBusinessAsync(
            ownerId,
            businessId,
            periodStart,
            periodEndExclusive,
            cancellationToken);
        var movements = await _inventoryMovements.GetBeforeAsync(
            businessId,
            periodEndExclusive,
            cancellationToken);
        var s2d = _s2dProjector.ProjectQuarter(
            businessId,
            movements,
            year,
            quarter);
        var historyStart = movements.Count == 0
            ? periodStart
            : movements.Min(x => BangkokBusinessTime.NormalizeNaiveUtc(x.OccurredAt));
        var expenseHistory = await _accountingSources.GetS2cExpensesAsync(
            businessId,
            historyStart,
            periodEndExclusive,
            cancellationToken);
        var expenses = expenseHistory
            .Where(x => x.ExpenseDate >= periodStart && x.ExpenseDate < periodEndExclusive)
            .ToList();
        var taxPeriod = await _taxPeriods.GetQuarterAsync(
            businessId,
            year,
            quarter,
            cancellationToken);

        // Phiếu nhập kho không được cộng lần hai: chi phí vật liệu của S2c
        // luôn lấy theo giá trị thực tế đã xuất dùng do S2d tính.
        var mappedExpenses = expenses
            .Where(x => !x.IsInventoryPurchase)
            .Where(x => !string.IsNullOrWhiteSpace(x.S2cGroupCode))
            .Where(x => x.S2cGroupCode != S2cGroupCodes.Labor)
            .ToList();
        var excludedCashExpenses = mappedExpenses
            .Where(x => x.Amount >= CashPaymentDeductionThreshold)
            .Where(x => string.Equals(
                x.PaymentMethod,
                PaymentMethods.Cash,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var excludedCashIds = excludedCashExpenses
            .Select(x => x.ExpenseId)
            .ToHashSet();
        var outboundItemKeys = s2d.Items
            .Where(x => x.TotalOutboundQuantity > 0m)
            .Select(x => (x.ProductId, x.IngredientId))
            .ToHashSet();
        var inventoryPurchaseIdsUsedByS2d = movements
            .Where(x => x.MovementType == InventoryMovementTypes.PurchaseIn)
            .Where(x => x.ReferenceId.HasValue)
            .Where(x => outboundItemKeys.Contains((x.ProductId, x.IngredientId)))
            .Select(x => x.ReferenceId!.Value)
            .ToHashSet();
        var excludedInventoryCashCost = CalculateExcludedInventoryCashCost(
            movements,
            expenseHistory,
            year,
            quarter);
        var lines = mappedExpenses
            .Where(x => !excludedCashIds.Contains(x.ExpenseId))
            .Select(x => new S2cExpenseLine(
                x.ExpenseId,
                x.VoucherNumber,
                x.ExpenseDate,
                x.ExpenseTitle,
                x.CategoryName,
                x.S2cGroupCode!,
                x.Amount,
                x.HasEvidence))
            .OrderBy(x => x.ExpenseDate)
            .ThenBy(x => x.VoucherNumber)
            .ToList();

        var warnings = revenue.Blockers
            .Select(x => new S2cBookWarning(x.Code, x.Message, x.SourceId))
            .Concat(s2d.Blockers.Select(x => new S2cBookWarning(
                x.Code,
                x.Message,
                x.InventoryMovementId)))
            .Concat(lines
                .Where(x => !x.HasEvidence)
                .Select(x => new S2cBookWarning(
                    "MissingExpenseEvidence",
                    $"Khoản chi {x.VoucherNumber} chưa có ảnh hoặc tệp chứng từ.",
                    x.ExpenseId,
                    true)))
            .Concat(expenses
                .Where(x =>
                    !x.IsInventoryPurchase &&
                    string.IsNullOrWhiteSpace(x.S2cGroupCode))
                .Select(x => new S2cBookWarning(
                    "ExpenseNotMappedToS2c",
                    $"Khoản chi {x.VoucherNumber} chưa chọn nhóm S2c nên chưa được đưa vào chi phí dự kiến được trừ.",
                    x.ExpenseId,
                    true)))
            .Concat(expenseHistory
                .Where(x =>
                    x.IsInventoryPurchase &&
                    !x.HasEvidence &&
                    inventoryPurchaseIdsUsedByS2d.Contains(x.ExpenseId))
                .Select(x => new S2cBookWarning(
                    "MissingInventoryPurchaseEvidence",
                    $"Phiếu nhập {x.VoucherNumber} đang được S2d dùng để tính giá xuất nhưng chưa có ảnh hoặc tệp chứng từ.",
                    x.ExpenseId,
                    true)))
            .ToList();

        return new S2cBookProjection
        {
            BusinessId = businessId,
            PeriodStart = periodStart,
            PeriodEndExclusive = periodEndExclusive,
            TotalRevenue = revenue.TotalRevenue,
            MaterialCost = Math.Max(
                0m,
                s2d.Items.Sum(x => x.TotalOutboundValue) - excludedInventoryCashCost),
            // TaxMate chưa có payroll engine nên không tự đưa chi phí nhân công
            // vào S2c/QTT. Chỉ tiêu pháp luật vẫn được xuất với giá trị 0.
            LaborCost = 0m,
            PurchasedServicesCost = SumGroup(lines, S2cGroupCodes.PurchasedServices),
            OtherDirectCost = SumGroup(lines, S2cGroupCodes.OtherDirect),
            ExcludedCashPaymentExpenseCount = excludedCashExpenses.Count,
            ExcludedCashPaymentExpenseAmount = excludedCashExpenses.Sum(x => x.Amount),
            ExcludedInventoryCashCost = excludedInventoryCashCost,
            EvidenceReviewedAt = taxPeriod?.EvidenceReviewedAt,
            EvidenceReviewedByUserId = taxPeriod?.EvidenceReviewedByUserId,
            Lines = lines,
            Warnings = warnings
        };
    }

    private static decimal SumGroup(
        IEnumerable<S2cExpenseLine> lines,
        string groupCode) =>
        lines.Where(x => x.GroupCode == groupCode).Sum(x => x.Amount);

    private static decimal CalculateExcludedInventoryCashCost(
        IReadOnlyCollection<InventoryMovement> movements,
        IReadOnlyCollection<S2cExpenseSource> expenseHistory,
        int targetYear,
        int targetQuarter)
    {
        var cashPurchaseIds = expenseHistory
            .Where(x => x.IsInventoryPurchase)
            .Where(x => x.Amount >= CashPaymentDeductionThreshold)
            .Where(x => string.Equals(
                x.PaymentMethod,
                PaymentMethods.Cash,
                StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ExpenseId)
            .ToHashSet();
        if (cashPurchaseIds.Count == 0)
            return 0m;

        var excludedOutbound = 0m;
        var validMovements = movements
            .Where(x => x.ProductId.HasValue != x.IngredientId.HasValue)
            .Where(x => x.Quantity > 0m)
            .OrderBy(x => BangkokBusinessTime.NormalizeNaiveUtc(x.OccurredAt))
            .ThenBy(x => x.InventoryMovementId)
            .ToList();

        foreach (var item in validMovements.GroupBy(x => (x.ProductId, x.IngredientId)))
        {
            var openingQuantity = 0m;
            var openingExcludedValue = 0m;
            var quarters = item.GroupBy(x =>
            {
                var wallClock = BangkokBusinessTime.NaiveUtcToBangkokWallClock(
                    BangkokBusinessTime.NormalizeNaiveUtc(x.OccurredAt));
                return (wallClock.Year, Quarter: ((wallClock.Month - 1) / 3) + 1);
            });

            foreach (var period in quarters)
            {
                var inbound = period.Where(x => x.MovementType is
                    InventoryMovementTypes.OpeningBalance or
                    InventoryMovementTypes.PurchaseIn or
                    InventoryMovementTypes.AdjustmentIn).ToList();
                var outboundQuantity = period
                    .Where(x => x.MovementType is
                        InventoryMovementTypes.OrderOut or
                        InventoryMovementTypes.AdjustmentOut)
                    .Sum(x => x.Quantity);
                var inboundQuantity = inbound.Sum(x => x.Quantity);
                var inboundExcludedValue = inbound
                    .Where(x =>
                        x.MovementType == InventoryMovementTypes.PurchaseIn &&
                        x.ReferenceId.HasValue &&
                        cashPurchaseIds.Contains(x.ReferenceId.Value))
                    .Sum(x => x.TotalValue ?? 0m);
                var valuationQuantity = openingQuantity + inboundQuantity;
                var valuationExcludedValue = openingExcludedValue + inboundExcludedValue;
                var periodExcludedOutbound = valuationQuantity > 0m
                    ? outboundQuantity == valuationQuantity
                        ? decimal.Round(valuationExcludedValue, 2, MidpointRounding.AwayFromZero)
                        : decimal.Round(
                            valuationExcludedValue / valuationQuantity * outboundQuantity,
                            2,
                            MidpointRounding.AwayFromZero)
                    : 0m;

                if (period.Key.Year == targetYear && period.Key.Quarter == targetQuarter)
                    excludedOutbound += periodExcludedOutbound;

                openingQuantity = valuationQuantity - outboundQuantity;
                openingExcludedValue = decimal.Round(
                    valuationExcludedValue - periodExcludedOutbound,
                    2,
                    MidpointRounding.AwayFromZero);
            }
        }

        return decimal.Round(excludedOutbound, 2, MidpointRounding.AwayFromZero);
    }
}
