using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Model.Data;

namespace TaxMate.Repository.Repositories;

public class TaxPeriodRepository : GenericRepository<TaxPeriod>, ITaxPeriodRepository
{
    private readonly AppDbContext _dbContext;

    public TaxPeriodRepository(AppDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> BusinessBelongsToUserAsync(
        Guid businessId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.BusinessProfiles
            .AsNoTracking()
            .AnyAsync(
                business =>
                    business.Id == businessId &&
                    business.OwnerId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TaxPeriodSummaryResponse>> GetByBusinessAsync(
        Guid businessId,
        GetTaxPeriodsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TaxPeriods
            .AsNoTracking()
            .Where(period => period.BusinessId == businessId);

        if (request.Year.HasValue)
        {
            query = query.Where(period => period.Year == request.Year.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.PeriodType))
        {
            query = query.Where(
                period => period.PeriodType == request.PeriodType);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(period => period.Status == request.Status);
        }

        return await query
            .OrderByDescending(period => period.Year)
            .ThenByDescending(period => period.Quarter)
            .ThenByDescending(period => period.Month)
            .Select(period => new TaxPeriodSummaryResponse
            {
                Id = period.Id,
                BusinessId = period.BusinessId,
                PeriodType = period.PeriodType,
                Year = period.Year,
                Month = period.Month,
                Quarter = period.Quarter,
                PeriodStartDate = period.PeriodStartDate,
                PeriodEndDate = period.PeriodEndDate,
                DueDate = period.DueDate,
                Status = period.Status,
                TotalRevenue = period.TotalRevenue,
                TaxableRevenue = period.TaxableRevenue,
                EstimatedTax = period.EstimatedTax,
                TaxAmountDebt = period.TaxAmountDebt,
                PaidDate = period.PaidDate
            })
            .ToListAsync(cancellationToken);
    }

    public Task<TaxPeriod?> GetByIdAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxPeriods
            .FirstOrDefaultAsync(
                period => period.Id == taxPeriodId,
                cancellationToken);
    }

    public async Task<TaxPeriodDetailResponse?> GetDetailAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var period = await _dbContext.TaxPeriods
            .AsNoTracking()
            .Where(period => period.Id == taxPeriodId)
            .Select(period => new
            {
                period.Id,
                period.BusinessId,
                period.PeriodType,
                period.Year,
                period.Month,
                period.Quarter,
                period.PeriodStartDate,
                period.PeriodEndDate,

                period.SalesRevenue,
                period.OtherRevenue,
                period.TotalRevenue,
                period.TaxableRevenue,

                period.VatTaxAmount,
                period.PersonalIncomeTaxAmount,
                period.EstimatedTax,
                period.TaxAmountDebt,

                period.Status,
                period.DueDate,
                period.PaidDate,
                period.ClosedAt,
                period.CalculatedAt,
                period.SubmittedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (period is null)
        {
            return null;
        }

        var startDate = period.PeriodStartDate;
        var endDate = period.PeriodEndDate;

        var transactionSummary = await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BusinessId == period.BusinessId &&
                transaction.TransactionDate >= startDate &&
                transaction.TransactionDate <= endDate &&
                transaction.TransactionType == TransactionTypes.Sale)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TransactionCount = group.Count(),

                PaidTransactionCount = group.Count(transaction =>
                    transaction.Status == "Completed"),

                UnpaidTransactionCount = group.Count(transaction =>
                    transaction.Status != "Completed")
            })
            .FirstOrDefaultAsync(cancellationToken);

        var missingInvoiceCount = await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BusinessId == period.BusinessId &&
                transaction.TransactionDate >= startDate &&
                transaction.TransactionDate <= endDate &&
                transaction.TransactionType == TransactionTypes.Sale)
            .CountAsync(
                transaction => transaction.Invoice == null,
                cancellationToken);

        var expenseSummary = await _dbContext.Expenses
            .AsNoTracking()
            .Where(expense =>
                expense.BusinessId == period.BusinessId &&
                expense.ExpenseDate >= startDate &&
                expense.ExpenseDate <= endDate)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                ExpenseCount = group.Count(),
                TotalExpense = group.Sum(expense => expense.Amount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var transactionCount =
            transactionSummary?.TransactionCount ?? 0;

        var paidTransactionCount =
            transactionSummary?.PaidTransactionCount ?? 0;

        var unpaidTransactionCount =
            transactionSummary?.UnpaidTransactionCount ?? 0;

        var totalExpense =
            expenseSummary?.TotalExpense ?? 0m;

        var expenseCount =
            expenseSummary?.ExpenseCount ?? 0;

        var dataCheckStatus = GetDataCheckStatus(
            transactionCount,
            unpaidTransactionCount,
            missingInvoiceCount);

        return new TaxPeriodDetailResponse
        {
            Id = period.Id,
            BusinessId = period.BusinessId,
            PeriodType = period.PeriodType,
            Year = period.Year,
            Month = period.Month,
            Quarter = period.Quarter,

            PeriodStartDate = period.PeriodStartDate,
            PeriodEndDate = period.PeriodEndDate,

            SalesRevenue = period.SalesRevenue,
            OtherRevenue = period.OtherRevenue,
            TotalRevenue = period.TotalRevenue,
            TaxableRevenue = period.TaxableRevenue,

            VatTaxAmount = period.VatTaxAmount,
            PersonalIncomeTaxAmount =
                period.PersonalIncomeTaxAmount,
            EstimatedTax = period.EstimatedTax,
            TaxAmountDebt = period.TaxAmountDebt,

            TotalExpense = totalExpense,
            EstimatedProfit = period.TotalRevenue - totalExpense,

            TransactionCount = transactionCount,
            PaidTransactionCount = paidTransactionCount,
            UnpaidTransactionCount = unpaidTransactionCount,
            MissingInvoiceCount = missingInvoiceCount,
            ExpenseCount = expenseCount,
            DataCheckStatus = dataCheckStatus,

            Status = period.Status,
            DueDate = period.DueDate,
            PaidDate = period.PaidDate,
            ClosedAt = period.ClosedAt,
            CalculatedAt = period.CalculatedAt,
            SubmittedAt = period.SubmittedAt
        };
    }

    private static string GetDataCheckStatus(
        int transactionCount,
        int unpaidTransactionCount,
        int missingInvoiceCount)
    {
        if (transactionCount == 0)
        {
            return "NeedReview";
        }

        if (unpaidTransactionCount > 0 ||
            missingInvoiceCount > 0)
        {
            return "Warning";
        }

        return "Good";
    }
    
    public async Task<TaxPeriodPreviewResponse?> GetPreviewAsync(
    Guid taxPeriodId,
    CancellationToken cancellationToken = default)
{
    var period = await _dbContext.TaxPeriods
        .AsNoTracking()
        .FirstOrDefaultAsync(
            x => x.Id == taxPeriodId,
            cancellationToken);

    if (period is null)
    {
        return null;
    }

    var startDate = period.PeriodStartDate;
    var endDate = period.PeriodEndDate;

    var transactions = _dbContext.Transactions
        .AsNoTracking()
        .Where(x =>
            x.BusinessId == period.BusinessId &&
            x.TransactionDate >= startDate &&
            x.TransactionDate <= endDate &&
            x.TransactionType == TransactionTypes.Sale);

    var transactionCount = await transactions.CountAsync(cancellationToken);

    var completedTransactionCount = await transactions.CountAsync(
        x => x.Status == "Completed",
        cancellationToken);

    var cancelledTransactionCount = await transactions.CountAsync(
        x => x.Status == "Cancelled",
        cancellationToken);

    var unpaidTransactionCount = await transactions.CountAsync(
        x => x.Status != "Completed" &&
             x.Status != "Cancelled",
        cancellationToken);

    var salesRevenue = await transactions
        .Where(x => x.Status == "Completed")
        .SumAsync(
            x => (decimal?)x.TotalAmount,
            cancellationToken) ?? 0m;

    var missingInvoiceCount = await transactions.CountAsync(
        transaction =>
            !_dbContext.Invoices.Any(invoice =>
                invoice.InvoiceNumber == transaction.InvoiceId),
        cancellationToken);

    var expensesQuery = _dbContext.Expenses
        .AsNoTracking()
        .Where(x =>
            x.BusinessId == period.BusinessId &&
            x.ExpenseDate >= startDate &&
            x.ExpenseDate <= endDate);

    var expenseCount = await expensesQuery.CountAsync(cancellationToken);

    var totalExpense = await expensesQuery
        .SumAsync(
            x => (decimal?)x.Amount,
            cancellationToken) ?? 0m;

    var otherRevenue = 0m;

    var totalRevenue = salesRevenue + otherRevenue;

    var taxableRevenue = totalRevenue;

    var warnings = new List<TaxPeriodWarningResponse>();

    if (transactionCount == 0)
    {
        warnings.Add(new TaxPeriodWarningResponse
        {
            Code = "NO_TRANSACTIONS",
            Message = "Kỳ thuế chưa có giao dịch bán hàng."
        });
    }

    if (unpaidTransactionCount > 0)
    {
        warnings.Add(new TaxPeriodWarningResponse
        {
            Code = "UNPAID_TRANSACTIONS",
            Message =
                $"Có {unpaidTransactionCount} giao dịch chưa hoàn tất."
        });
    }

    if (missingInvoiceCount > 0)
    {
        warnings.Add(new TaxPeriodWarningResponse
        {
            Code = "MISSING_INVOICES",
            Message =
                $"Có {missingInvoiceCount} giao dịch chưa có hóa đơn."
        });
    }

    if (cancelledTransactionCount > 0)
    {
        warnings.Add(new TaxPeriodWarningResponse
        {
            Code = "CANCELLED_TRANSACTIONS",
            Message =
                $"Có {cancelledTransactionCount} giao dịch đã hủy."
        });
    }

    var dataCheckStatus =
        transactionCount == 0
            ? "NeedReview"
            : warnings.Count > 0
                ? "Warning"
                : "Good";

    return new TaxPeriodPreviewResponse
    {
        TaxPeriodId = period.Id,
        BusinessId = period.BusinessId,
        Status = period.Status,

        SalesRevenue = salesRevenue,
        OtherRevenue = otherRevenue,
        TotalRevenue = totalRevenue,
        TaxableRevenue = taxableRevenue,

        TotalExpense = totalExpense,

        TransactionCount = transactionCount,
        CompletedTransactionCount = completedTransactionCount,
        UnpaidTransactionCount = unpaidTransactionCount,
        CancelledTransactionCount = cancelledTransactionCount,
        MissingInvoiceCount = missingInvoiceCount,

        ExpenseCount = expenseCount,

        DataCheckStatus = dataCheckStatus,

        CanClose = transactionCount > 0,

        Warnings = warnings
    };
}
    
    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
    
    public async Task<int> GetNextCalculationVersionAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var maxVersion = await _dbContext.TaxCalculations
            .Where(x => x.TaxPeriodId == taxPeriodId)
            .MaxAsync(
                x => (int?)x.Version,
                cancellationToken);

        return (maxVersion ?? 0) + 1;
    }
    
    public async Task SetPreviousCalculationsAsSupersededAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var calculations = await _dbContext.TaxCalculations
            .Where(x =>
                x.TaxPeriodId == taxPeriodId &&
                x.IsCurrent)
            .ToListAsync(cancellationToken);

        foreach (var calculation in calculations)
        {
            calculation.IsCurrent = false;
            calculation.Status = TaxCalculationStatuses.Superseded;
        }
    }
    
    public Task<BusinessProfile?> GetBusinessWithCategoryAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.BusinessProfiles
            .Include(x => x.MainCategory)
            .Include(x => x.Owner)
            .FirstOrDefaultAsync(
                x => x.Id == businessId,
                cancellationToken);
    }
    
    public async Task<decimal> GetAnnualRevenueAsync(
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var start = new DateTime(
            year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var end = start.AddYears(1);

        return await _dbContext.Transactions
                   .AsNoTracking()
                   .Where(x =>
                       x.BusinessId == businessId &&
                       x.TransactionType == TransactionTypes.Sale &&
                       x.Status == "Completed" &&
                       x.TransactionDate >= start &&
                       x.TransactionDate < end)
                   .SumAsync(
                       x => (decimal?)x.TotalAmount,
                       cancellationToken)
               ?? 0m;
    }
    
    public async Task<decimal> GetAnnualRevenueBeforePeriodAsync(
        Guid businessId,
        int year,
        DateTime periodStart,
        CancellationToken cancellationToken = default)
    {
        var yearStart = new DateTime(
            year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return await _dbContext.Transactions
                   .AsNoTracking()
                   .Where(x =>
                       x.BusinessId == businessId &&
                       x.TransactionType == TransactionTypes.Sale &&
                       x.Status == "Completed" &&
                       x.TransactionDate >= yearStart &&
                       x.TransactionDate < periodStart)
                   .SumAsync(
                       x => (decimal?)x.TotalAmount,
                       cancellationToken)
               ?? 0m;
    }
    
    public async Task<decimal> GetAnnualRevenueByOwnerAsync(
        Guid ownerId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var start = new DateTime(
            year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var end = start.AddYears(1);

        return await _dbContext.Transactions
                   .AsNoTracking()
                   .Where(t =>
                       t.Business.OwnerId == ownerId &&
                       t.TransactionType == TransactionTypes.Sale &&
                       t.Status == "Completed" &&
                       t.TransactionDate >= start &&
                       t.TransactionDate < end)
                   .SumAsync(
                       t => (decimal?)t.TotalAmount,
                       cancellationToken)
               ?? 0m;
    }
    
    public async Task<decimal> GetAnnualRevenueBeforePeriodByOwnerAsync(
        Guid ownerId,
        int year,
        DateTime periodStart,
        CancellationToken cancellationToken = default)
    {
        var yearStart = new DateTime(
            year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return await _dbContext.Transactions
                   .AsNoTracking()
                   .Where(x =>
                       x.Business.OwnerId == ownerId &&
                       x.TransactionType == TransactionTypes.Sale &&
                       x.Status == "Completed" &&
                       x.TransactionDate >= yearStart &&
                       x.TransactionDate < periodStart)
                   .SumAsync(
                       x => (decimal?)x.TotalAmount,
                       cancellationToken)
               ?? 0m;
    }
}