using TaxMate.Model.Common;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public sealed class OwnerRevenueProjector : IOwnerRevenueProjector
{
    private readonly IAccountingScopeReadRepository _readRepository;

    public OwnerRevenueProjector(IAccountingScopeReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public Task<OwnerRevenueProjection> ProjectCalendarYearAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var (startNaiveUtc, endExclusiveNaiveUtc) =
            BangkokBusinessTime.GetCalendarYearNaiveUtc(year);

        return ProjectAsync(
            authenticatedOwnerId,
            businessId,
            startNaiveUtc,
            endExclusiveNaiveUtc,
            cancellationToken);
    }

    public Task<OwnerRevenueProjection> ProjectAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default) =>
        ProjectScopeAsync(
            authenticatedOwnerId,
            businessId,
            startNaiveUtc,
            endExclusiveNaiveUtc,
            ownerWide: true,
            cancellationToken);

    public Task<OwnerRevenueProjection> ProjectBusinessAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default) =>
        ProjectScopeAsync(
            authenticatedOwnerId,
            businessId,
            startNaiveUtc,
            endExclusiveNaiveUtc,
            ownerWide: false,
            cancellationToken);

    private async Task<OwnerRevenueProjection> ProjectScopeAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        bool ownerWide,
        CancellationToken cancellationToken)
    {
        startNaiveUtc = BangkokBusinessTime.NormalizeNaiveUtc(startNaiveUtc);
        endExclusiveNaiveUtc = BangkokBusinessTime.NormalizeNaiveUtc(
            endExclusiveNaiveUtc);

        if (endExclusiveNaiveUtc <= startNaiveUtc)
        {
            throw new ArgumentException("Revenue window end must be after start.");
        }

        var scope = await _readRepository.ResolveOwnerScopeAsync(
            businessId,
            cancellationToken);

        if (scope is null || !scope.BusinessIds.Contains(businessId))
        {
            throw new NotFoundException("Business profile not found.");
        }

        if (scope.OwnerId != authenticatedOwnerId)
        {
            throw new ForbiddenException();
        }

        IReadOnlyCollection<Guid> businessIds = ownerWide
            ? scope.BusinessIds
            : new[] { businessId };

        var transactions = await _readRepository.GetRevenueTransactionsAsync(
            businessIds,
            startNaiveUtc,
            endExclusiveNaiveUtc,
            cancellationToken);

        // The default repository shares the request-scoped DbContext, so these
        // reads must remain serialized.
        var incomes = await _readRepository.GetRevenueIncomesAsync(
            businessIds,
            startNaiveUtc,
            endExclusiveNaiveUtc,
            cancellationToken);

        var qualifyingTransactions = transactions
            .Where(x =>
                businessIds.Contains(x.BusinessId) &&
                x.TransactionType == TransactionTypes.Sale &&
                x.Status == TransactionStatus.Completed &&
                x.CompletedAt.HasValue &&
                BangkokBusinessTime.ContainsNaiveUtc(
                    startNaiveUtc,
                    endExclusiveNaiveUtc,
                    BangkokBusinessTime.NormalizeNaiveUtc(x.CompletedAt.Value)))
            .ToArray();

        var completedTransactionRevenue = qualifyingTransactions.Sum(x => x.Amount);

        var qualifyingManualIncomes = incomes
            .Where(x =>
                businessIds.Contains(x.BusinessId) &&
                !x.TransactionId.HasValue &&
                x.AccountingType == IncomeAccountingTypes.BusinessRevenue &&
                BangkokBusinessTime.ContainsNaiveUtc(
                    startNaiveUtc,
                    endExclusiveNaiveUtc,
                    BangkokBusinessTime.NormalizeNaiveUtc(x.IncomeDate)))
            .ToArray();

        var manualBusinessRevenue = qualifyingManualIncomes
            .Where(x => x.Amount > 0m)
            .Sum(x => x.Amount);

        var groups = qualifyingTransactions
            .Where(HasBusinessCategory)
            .Select(x => new
            {
                x.BusinessCategoryId,
                x.BusinessCategoryCode,
                x.BusinessCategoryName,
                x.VatRate,
                CompletedRevenue = x.Amount,
                ManualRevenue = 0m
            })
            .Concat(qualifyingManualIncomes
                .Where(x => x.Amount > 0m && HasBusinessCategory(x))
                .Select(x => new
                {
                    x.BusinessCategoryId,
                    x.BusinessCategoryCode,
                    x.BusinessCategoryName,
                    x.VatRate,
                    CompletedRevenue = 0m,
                    ManualRevenue = x.Amount
                }))
            .GroupBy(x => new
            {
                CategoryId = x.BusinessCategoryId!.Value,
                Code = x.BusinessCategoryCode!,
                Name = x.BusinessCategoryName!,
                Rate = x.VatRate!.Value
            })
            .Select(x => new OwnerRevenueGroup(
                x.Key.CategoryId,
                x.Key.Code,
                x.Key.Name,
                x.Key.Rate,
                x.Sum(y => y.CompletedRevenue),
                x.Sum(y => y.ManualRevenue)))
            .OrderBy(x => x.BusinessCategoryCode)
            .ToArray();

        var lines = qualifyingTransactions
            .Where(HasBusinessCategory)
            .Select(x => new OwnerRevenueLine(
                x.BusinessCategoryId!.Value,
                x.BusinessCategoryCode!,
                x.TransactionId,
                "Transaction",
                x.InvoiceNumber ?? AccountingDocumentNumber.FromSource(
                    "BH", x.TransactionId),
                BangkokBusinessTime.NormalizeNaiveUtc(x.CompletedAt!.Value),
                $"Doanh thu đơn hàng {x.TransactionCode}",
                x.Amount))
            .Concat(qualifyingManualIncomes
                .Where(x => x.Amount > 0m && HasBusinessCategory(x))
                .Select(x => new OwnerRevenueLine(
                    x.BusinessCategoryId!.Value,
                    x.BusinessCategoryCode!,
                    x.IncomeId,
                    "ManualIncome",
                    AccountingDocumentNumber.FromSource("PT", x.IncomeId),
                    BangkokBusinessTime.NormalizeNaiveUtc(x.IncomeDate),
                    x.IncomeTitle,
                    x.Amount)))
            .OrderBy(x => x.BusinessCategoryCode)
            .ThenBy(x => x.DocumentDate)
            .ThenBy(x => x.DocumentNumber)
            .ToArray();

        var blockers = qualifyingTransactions
            .Where(x => !x.HasInvoice)
            .Select(x => new OwnerRevenueBlocker(
                OwnerRevenueBlockerCodes.MissingInvoice,
                x.BusinessId,
                x.TransactionId,
                "Giao dịch hoàn tất chưa có hóa đơn."))
            .Concat(qualifyingTransactions
                .Where(x => !HasBusinessCategory(x))
                .Select(x => new OwnerRevenueBlocker(
                    OwnerRevenueBlockerCodes.MissingBusinessCategory,
                    x.BusinessId,
                    x.TransactionId,
                    "Cửa hàng chưa có ngành nghề để phân loại doanh thu S2b.")))
            .Concat(qualifyingManualIncomes
                .Where(x => x.Amount <= 0m)
                .Select(x => new OwnerRevenueBlocker(
                    OwnerRevenueBlockerCodes.NonPositiveManualRevenue,
                    x.BusinessId,
                    x.IncomeId,
                    "Doanh thu nhập thủ công phải lớn hơn 0.")))
            .Concat(qualifyingManualIncomes
                .Where(x => x.Amount > 0m && !HasBusinessCategory(x))
                .Select(x => new OwnerRevenueBlocker(
                    OwnerRevenueBlockerCodes.MissingBusinessCategory,
                    x.BusinessId,
                    x.IncomeId,
                    "Cửa hàng chưa có ngành nghề để phân loại doanh thu S2b.")))
            .OrderBy(x => x.Code)
            .ThenBy(x => x.BusinessId)
            .ThenBy(x => x.SourceId)
            .ToArray();

        return new OwnerRevenueProjection(
            scope.OwnerId,
            startNaiveUtc,
            endExclusiveNaiveUtc,
            completedTransactionRevenue,
            manualBusinessRevenue,
            blockers)
        {
            Groups = groups,
            Lines = lines
        };
    }

    private static bool HasBusinessCategory(RevenueTransactionSource source) =>
        source.BusinessCategoryId.HasValue &&
        !string.IsNullOrWhiteSpace(source.BusinessCategoryCode) &&
        !string.IsNullOrWhiteSpace(source.BusinessCategoryName) &&
        source.VatRate.HasValue;

    private static bool HasBusinessCategory(RevenueIncomeSource source) =>
        source.BusinessCategoryId.HasValue &&
        !string.IsNullOrWhiteSpace(source.BusinessCategoryCode) &&
        !string.IsNullOrWhiteSpace(source.BusinessCategoryName) &&
        source.VatRate.HasValue;
}
