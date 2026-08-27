using TaxMate.Model.Common;
using TaxMate.Model.DTO.Expense;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.DTO.Tax;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public sealed class AnnualTaxAggregateService : IAnnualTaxAggregateService
{
    private const decimal EqualityTolerance = 0.01m;
    private const decimal OneBillion = 1_000_000_000m;
    private const decimal FiftyBillion = 50_000_000_000m;

    private readonly IOwnerRevenueProjector _revenueProjector;
    private readonly IS2cBookProjector _s2cProjector;
    private readonly IS2dBookProjector _s2dProjector;
    private readonly IInventoryMovementRepository _inventoryMovements;
    private readonly ITaxPeriodRepository _taxPeriods;
    private readonly IGenericRepository<User> _users;

    public AnnualTaxAggregateService(
        IOwnerRevenueProjector revenueProjector,
        IS2cBookProjector s2cProjector,
        IS2dBookProjector s2dProjector,
        IInventoryMovementRepository inventoryMovements,
        ITaxPeriodRepository taxPeriods,
        IGenericRepository<User> users)
    {
        _revenueProjector = revenueProjector;
        _s2cProjector = s2cProjector;
        _s2dProjector = s2dProjector;
        _inventoryMovements = inventoryMovements;
        _taxPeriods = taxPeriods;
        _users = users;
    }

    public async Task<QttPreviewResponse> PreviewAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var owner = await _users.GetByIdAsync(authenticatedOwnerId)
            ?? throw new NotFoundException("User not found.");
        var businesses = await _taxPeriods.GetBusinessesWithCategoriesByOwnerAsync(
            authenticatedOwnerId,
            cancellationToken);
        if (businesses.All(x => x.Id != businessId))
            throw new NotFoundException("Business profile not found.");

        var (yearStart, yearEndExclusive) =
            BangkokBusinessTime.GetCalendarYearNaiveUtc(year);
        var s2b = await _revenueProjector.ProjectCalendarYearAsync(
            authenticatedOwnerId,
            businessId,
            year,
            cancellationToken);
        var s2cBooks = new List<S2cBookProjection>();
        var inventoryBooks = new Dictionary<Guid, IReadOnlyList<S2dBook>>();

        foreach (var business in businesses)
        {
            var quarterBooks = new List<S2dBook>(4);
            var movements = await _inventoryMovements.GetBeforeAsync(
                business.Id,
                yearEndExclusive,
                cancellationToken);

            for (var quarter = 1; quarter <= 4; quarter++)
            {
                s2cBooks.Add(await _s2cProjector.ProjectQuarterAsync(
                    authenticatedOwnerId,
                    business.Id,
                    year,
                    quarter,
                    cancellationToken));
                quarterBooks.Add(_s2dProjector.ProjectQuarter(
                    business.Id,
                    movements,
                    year,
                    quarter));
            }

            inventoryBooks[business.Id] = quarterBooks;
        }

        var inventoryRows = BuildInventoryRows(inventoryBooks);
        var payments = await _taxPeriods.GetTaxPaymentsByOwnerAsync(
            authenticatedOwnerId,
            yearStart,
            yearEndExclusive,
            cancellationToken);
        var paymentLines = payments.Select(x => new QttPitPaymentLine(
            x.TaxPaymentId,
            x.PaymentCode,
            x.PaymentDate,
            x.Amount,
            x.TaxType,
            x.Status,
            x.SourceTaxMethod,
            x.TaxType == TaxTypes.PersonalIncomeTax &&
            x.Status == TaxPaymentStatuses.Completed)).ToList();
        var taxMethodSnapshot = await _taxPeriods.GetAnnualTaxMethodSnapshotAsync(
            authenticatedOwnerId,
            year,
            cancellationToken) ?? ResolveConfiguredTaxMethod(owner, year);

        var s2cRevenue = s2cBooks.Sum(x => x.TotalRevenue);
        var deductibleMaterial = s2cBooks.Sum(x => x.MaterialCost);
        var excludedInventoryCashCost = s2cBooks.Sum(x => x.ExcludedInventoryCashCost);
        var grossS2dOutbound = inventoryRows.Sum(x => x.OutboundValue);
        var checks = new List<QttCrossBookCheck>
        {
            Check(
                "RevenueS2bEqualsS2c",
                "Doanh thu năm giữa S2b và S2c",
                s2b.TotalRevenue,
                s2cRevenue),
            Check(
                "MaterialS2dReconcilesS2c",
                "Giá trị xuất kho S2d và chi phí nguyên vật liệu S2c",
                grossS2dOutbound,
                deductibleMaterial + excludedInventoryCashCost),
            Check(
                "InventoryValueReconciles",
                "Tồn đầu + nhập - xuất bằng tồn cuối",
                inventoryRows.Sum(x => x.OpeningValue + x.InboundValue - x.OutboundValue),
                inventoryRows.Sum(x => x.EndingValue))
        };

        var warnings = BuildWarnings(s2cBooks, paymentLines);
        var hardBlockers = BuildHardBlockers(
            s2b,
            s2cBooks,
            inventoryBooks,
            checks,
            taxMethodSnapshot,
            paymentLines);
        if (s2b.TotalRevenue > FiftyBillion)
        {
            hardBlockers.Add(new QttPreviewIssue(
                "AnnualRevenueOver50B",
                "Doanh thu năm trên 50 tỷ đồng, ngoài phạm vi TaxMate hỗ trợ."));
        }

        var eligibility = ResolveEligibility(
            taxMethodSnapshot,
            s2b.TotalRevenue,
            paymentLines);
        if (eligibility == QttEligibility.NotEligible)
        {
            hardBlockers.Add(new QttPreviewIssue(
                "QttNotEligible",
                "Năm này không thuộc diện quyết toán theo phương pháp thu nhập tính thuế."));
        }

        return new QttPreviewResponse
        {
            OwnerId = authenticatedOwnerId,
            TaxYear = year,
            TaxMethodSnapshot = taxMethodSnapshot,
            TaxMethodEffectiveYear = owner.TaxMethodEffectiveYear,
            Eligibility = eligibility,
            Revenue = new QttRevenueBreakdown(s2b.TotalRevenue, 0m, 0m),
            Expenses = new QttExpenseBreakdown(
                deductibleMaterial,
                s2cBooks.Sum(x => x.LaborCost),
                0m,
                s2cBooks.Sum(x => x.PurchasedServicesCost),
                0m,
                s2cBooks.Sum(x => x.OtherDirectCost),
                s2cBooks.Sum(x => x.ExcludedCashPaymentExpenseAmount),
                excludedInventoryCashCost),
            PitPayments = new QttPitPaymentBreakdown
            {
                Indicator15 = paymentLines
                    .Where(x =>
                        x.IncludedInIndicator15 &&
                        (eligibility != QttEligibility.UnderOneBillionRefund ||
                         x.SourceTaxMethod == PersonalIncomeTaxMethods.IncomeBased))
                    .Sum(x => x.Amount),
                Payments = paymentLines
            },
            Inventory = new QttInventorySummary
            {
                Indicator31OpeningValue = inventoryRows.Sum(x => x.OpeningValue),
                Indicator32InboundValue = inventoryRows.Sum(x => x.InboundValue),
                Indicator33OutboundValue = inventoryRows.Sum(x => x.OutboundValue),
                Indicator34EndingValue = inventoryRows.Sum(x => x.EndingValue),
                Rows = inventoryRows
            },
            CrossBookChecks = checks,
            Warnings = Deduplicate(warnings),
            HardBlockers = Deduplicate(hardBlockers)
        };
    }

    private static List<QttInventoryRow> BuildInventoryRows(
        IReadOnlyDictionary<Guid, IReadOnlyList<S2dBook>> booksByBusiness)
    {
        var result = new List<QttInventoryRow>();
        foreach (var (businessId, quarters) in booksByBusiness)
        {
            var allItems = quarters
                .SelectMany(x => x.Items)
                .GroupBy(x => (x.ProductId, x.IngredientId));
            foreach (var group in allItems)
            {
                var metadata = group.Last();
                var firstQuarter = quarters[0].Items.FirstOrDefault(x =>
                    x.ProductId == group.Key.ProductId &&
                    x.IngredientId == group.Key.IngredientId);
                var lastQuarter = quarters[3].Items.FirstOrDefault(x =>
                    x.ProductId == group.Key.ProductId &&
                    x.IngredientId == group.Key.IngredientId);
                result.Add(new QttInventoryRow(
                    businessId,
                    group.Key.ProductId,
                    group.Key.IngredientId,
                    metadata.ItemCode,
                    metadata.ItemName,
                    firstQuarter?.OpeningValue ?? 0m,
                    group.Sum(x => x.TotalInboundValue),
                    group.Sum(x => x.TotalOutboundValue),
                    lastQuarter?.EndingValue ?? 0m));
            }
        }

        return result
            .OrderBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.BusinessId)
            .ToList();
    }

    private static List<QttPreviewIssue> BuildWarnings(
        IEnumerable<S2cBookProjection> s2cBooks,
        IEnumerable<QttPitPaymentLine> payments)
    {
        var warnings = s2cBooks
            .SelectMany(x => x.Warnings
                .Where(y => y.CanOverride)
                .Select(y => new QttPreviewIssue(
                    y.Code,
                    y.Message,
                    x.BusinessId,
                    y.SourceId)))
            .ToList();
        warnings.AddRange(s2cBooks
            .Where(x =>
                !x.EvidenceReviewedAt.HasValue &&
                (x.Lines.Count > 0 ||
                 x.MaterialCost != 0m ||
                 x.Warnings.Any(y => y.CanOverride)))
            .Select(x => new QttPreviewIssue(
                "EvidenceReviewRequired",
                $"Quý bắt đầu ngày {BangkokBusinessTime.NaiveUtcToBangkokWallClock(x.PeriodStart):dd/MM/yyyy} chưa được xác nhận rà soát chi phí.",
                x.BusinessId)));
        warnings.AddRange(payments
            .Where(x => x.TaxType == TaxTypes.Unknown)
            .Select(x => new QttPreviewIssue(
                "UnclassifiedTaxPayment",
                $"Khoản nộp {x.PaymentCode} chưa được phân loại VAT hay PIT.",
                SourceId: x.TaxPaymentId)));
        warnings.AddRange(payments
            .Where(x =>
                x.TaxType == TaxTypes.PersonalIncomeTax &&
                x.Status != TaxPaymentStatuses.Completed)
            .Select(x => new QttPreviewIssue(
                "PitPaymentNotCompleted",
                $"Khoản PIT {x.PaymentCode} chưa thanh toán thành công nên chưa vào chỉ tiêu [15].",
                SourceId: x.TaxPaymentId)));
        return warnings;
    }

    private static List<QttPreviewIssue> BuildHardBlockers(
        OwnerRevenueProjection s2b,
        IEnumerable<S2cBookProjection> s2cBooks,
        IReadOnlyDictionary<Guid, IReadOnlyList<S2dBook>> inventoryBooks,
        IEnumerable<QttCrossBookCheck> checks,
        string? taxMethodSnapshot,
        IReadOnlyCollection<QttPitPaymentLine> payments)
    {
        var blockers = s2b.Blockers.Select(x => new QttPreviewIssue(
            x.Code,
            x.Message,
            x.BusinessId,
            x.SourceId)).ToList();
        blockers.AddRange(s2cBooks.SelectMany(x => x.Warnings
            .Where(y => !y.CanOverride)
            .Select(y => new QttPreviewIssue(
                y.Code,
                y.Message,
                x.BusinessId,
                y.SourceId))));
        blockers.AddRange(inventoryBooks.SelectMany(pair => pair.Value
            .SelectMany(x => x.Blockers.Select(y => new QttPreviewIssue(
                y.Code,
                y.Message,
                pair.Key,
                y.InventoryMovementId)))));
        blockers.AddRange(checks
            .Where(x => !x.IsMatched)
            .Select(x => new QttPreviewIssue(
                x.Code,
                $"{x.Label} đang lệch {Math.Abs(x.ExpectedAmount - x.ActualAmount):N0} đồng.")));

        if (taxMethodSnapshot != PersonalIncomeTaxMethods.IncomeBased &&
            payments.Any(x =>
                x.IncludedInIndicator15 &&
                string.IsNullOrWhiteSpace(x.SourceTaxMethod)))
        {
            blockers.Add(new QttPreviewIssue(
                "PitPaymentSourceMethodMissing",
                "Có khoản PIT đã nộp nhưng chưa truy được phương pháp tính thuế nguồn."));
        }

        return blockers;
    }

    private static string ResolveEligibility(
        string? taxMethodSnapshot,
        decimal revenue,
        IEnumerable<QttPitPaymentLine> payments)
    {
        if (revenue <= OneBillion)
        {
            return payments.Any(x =>
                    x.IncludedInIndicator15 &&
                    x.SourceTaxMethod == PersonalIncomeTaxMethods.IncomeBased)
                ? QttEligibility.UnderOneBillionRefund
                : QttEligibility.NotEligible;
        }

        if (taxMethodSnapshot == PersonalIncomeTaxMethods.IncomeBased)
            return QttEligibility.NormalIncomeBased;
        return QttEligibility.NotEligible;
    }

    private static string? ResolveConfiguredTaxMethod(User owner, int year) =>
        owner.TaxMethodEffectiveYear.HasValue &&
        owner.TaxMethodEffectiveYear.Value <= year
            ? owner.PersonalIncomeTaxMethod
            : null;

    private static QttCrossBookCheck Check(
        string code,
        string label,
        decimal expected,
        decimal actual) =>
        new(code, label, expected, actual, Math.Abs(expected - actual) <= EqualityTolerance);

    private static IReadOnlyList<QttPreviewIssue> Deduplicate(
        IEnumerable<QttPreviewIssue> issues) =>
        issues.GroupBy(x => new { x.Code, x.Message, x.BusinessId, x.SourceId })
            .Select(x => x.First())
            .ToList();
}
