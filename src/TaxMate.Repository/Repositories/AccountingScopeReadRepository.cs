using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public sealed class AccountingScopeReadRepository : IAccountingScopeReadRepository
{
    private readonly AppDbContext _dbContext;

    public AccountingScopeReadRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OwnerBusinessScope?> ResolveOwnerScopeAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        var ownerId = await _dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.Id == businessId)
            .Select(x => (Guid?)x.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ownerId.HasValue)
        {
            return null;
        }

        // Historical revenue and locked periods remain owner-scoped even when a
        // business is later deactivated.
        var businessIds = await _dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId.Value)
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        return new OwnerBusinessScope(ownerId.Value, businessIds);
    }

    public async Task<IReadOnlyList<RevenueTransactionSource>> GetRevenueTransactionsAsync(
        IReadOnlyCollection<Guid> businessIds,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureNaiveUtcWindow(startNaiveUtc, endExclusiveNaiveUtc);

        if (businessIds.Count == 0)
        {
            return Array.Empty<RevenueTransactionSource>();
        }

        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(x =>
                businessIds.Contains(x.BusinessId) &&
                x.CompletedAt.HasValue &&
                x.CompletedAt.Value >= startNaiveUtc &&
                x.CompletedAt.Value < endExclusiveNaiveUtc)
            .Select(x => new RevenueTransactionSource(
                x.BusinessId,
                x.TransactionId,
                x.TransactionType,
                x.Status,
                x.CompletedAt,
                x.TotalAmount,
                x.InvoiceId != null)
            {
                InvoiceNumber = x.InvoiceId,
                TransactionCode = x.TransactionCode,
                BusinessCategoryId = x.Business.MainCategoryId,
                BusinessCategoryCode = x.Business.MainCategory != null
                    ? x.Business.MainCategory.Code
                    : null,
                BusinessCategoryName = x.Business.MainCategory != null
                    ? x.Business.MainCategory.Name
                    : null,
                VatRate = x.Business.MainCategory != null
                    ? x.Business.MainCategory.VatRate
                    : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RevenueIncomeSource>> GetRevenueIncomesAsync(
        IReadOnlyCollection<Guid> businessIds,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureNaiveUtcWindow(startNaiveUtc, endExclusiveNaiveUtc);

        if (businessIds.Count == 0)
        {
            return Array.Empty<RevenueIncomeSource>();
        }

        return await _dbContext.Incomes
            .AsNoTracking()
            .Where(x =>
                businessIds.Contains(x.BusinessId) &&
                x.IncomeDate >= startNaiveUtc &&
                x.IncomeDate < endExclusiveNaiveUtc)
            .Select(x => new RevenueIncomeSource(
                x.BusinessId,
                x.IncomeId,
                x.TransactionId,
                x.AccountingType,
                x.IncomeDate,
                x.Amount)
            {
                IncomeTitle = x.IncomeTitle,
                BusinessCategoryId = x.Business.MainCategoryId,
                BusinessCategoryCode = x.Business.MainCategory != null
                    ? x.Business.MainCategory.Code
                    : null,
                BusinessCategoryName = x.Business.MainCategory != null
                    ? x.Business.MainCategory.Name
                    : null,
                VatRate = x.Business.MainCategory != null
                    ? x.Business.MainCategory.VatRate
                    : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountingTaxPeriodSource>> GetTaxPeriodsIntersectingAsync(
        IReadOnlyCollection<Guid> businessIds,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureNaiveUtcWindow(startNaiveUtc, endExclusiveNaiveUtc);

        if (businessIds.Count == 0)
        {
            return Array.Empty<AccountingTaxPeriodSource>();
        }

        return await _dbContext.TaxPeriods
            .AsNoTracking()
            .Where(x =>
                businessIds.Contains(x.BusinessId) &&
                x.PeriodEndDate > startNaiveUtc &&
                x.PeriodStartDate < endExclusiveNaiveUtc)
            .Select(x => new AccountingTaxPeriodSource(
                x.Id,
                x.BusinessId,
                x.PeriodStartDate,
                x.PeriodEndDate,
                x.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<S2cExpenseSource>> GetS2cExpensesAsync(
        Guid businessId,
        DateTime startNaiveUtc,
        DateTime endExclusiveNaiveUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureNaiveUtcWindow(startNaiveUtc, endExclusiveNaiveUtc);

        return await _dbContext.Expenses
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.ExpenseDate >= startNaiveUtc &&
                x.ExpenseDate < endExclusiveNaiveUtc)
            .Select(x => new S2cExpenseSource(
                x.ExpenseId,
                x.VoucherNumber,
                x.ExpenseDate,
                x.ExpenseTitle,
                x.Amount,
                x.ExpenseCategory.CategoryName,
                x.ExpenseCategory.S2cGroupCode,
                x.PaymentMethod,
                !string.IsNullOrEmpty(x.ReceiptImageUrl) ||
                    !string.IsNullOrEmpty(x.FileUrl),
                x.VoucherNumber.StartsWith("PNK-") ||
                    x.IngredientPurchases.Any()))
            .ToListAsync(cancellationToken);
    }

    private static void EnsureNaiveUtcWindow(DateTime start, DateTime endExclusive)
    {
        if (start.Kind != DateTimeKind.Unspecified ||
            endExclusive.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException(
                "Accounting query windows must be UTC instants encoded as DateTimeKind.Unspecified.");
        }

        if (endExclusive <= start)
        {
            throw new ArgumentException("Accounting query window end must be after start.");
        }
    }
}
