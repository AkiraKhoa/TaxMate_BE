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

    private static string BuildPeriodKey(TaxPeriod period)
    {
        return string.Join(
            "|",
            period.PeriodType,
            period.Year,
            period.Month?.ToString() ?? string.Empty,
            period.Quarter?.ToString() ?? string.Empty,
            period.FilingWindow ?? string.Empty,
            period.PeriodStartDate.Ticks,
            period.PeriodEndDate.Ticks);
    }

    private async Task<Guid?> GetOwnerIdByBusinessAsync(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.Id == businessId)
            .Select(x => (Guid?)x.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetOwnerBusinessIdsAsync(
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<TaxPeriod?> ResolveCanonicalPeriodAsync(
        Guid taxPeriodId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var source = tracking
            ? _dbContext.TaxPeriods.AsQueryable()
            : _dbContext.TaxPeriods.AsNoTracking();

        var requested = await source
            .FirstOrDefaultAsync(
                x => x.Id == taxPeriodId,
                cancellationToken);

        if (requested is null)
        {
            return null;
        }

        var ownerId = await GetOwnerIdByBusinessAsync(
            requested.BusinessId,
            cancellationToken);

        if (!ownerId.HasValue)
        {
            return requested;
        }

        var businessIds = await GetOwnerBusinessIdsAsync(
            ownerId.Value,
            cancellationToken);

        var candidates = await source
            .Where(x =>
                businessIds.Contains(x.BusinessId) &&
                x.PeriodType == requested.PeriodType &&
                x.Year == requested.Year &&
                x.Month == requested.Month &&
                x.Quarter == requested.Quarter &&
                x.PeriodStartDate == requested.PeriodStartDate &&
                x.PeriodEndDate == requested.PeriodEndDate)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault() ?? requested;
    }

    public async Task<IReadOnlyList<TaxPeriodSummaryResponse>> GetByBusinessAsync(
        Guid businessId,
        GetTaxPeriodsRequest request,
        CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdByBusinessAsync(
            businessId,
            cancellationToken);

        if (!ownerId.HasValue)
        {
            return Array.Empty<TaxPeriodSummaryResponse>();
        }

        var businessIds = await GetOwnerBusinessIdsAsync(
            ownerId.Value,
            cancellationToken);

        var query = _dbContext.TaxPeriods
            .AsNoTracking()
            .Where(period => businessIds.Contains(period.BusinessId));

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
            query = query.Where(
                period => period.Status == request.Status);
        }

        var periods = await query
            .OrderBy(period => period.CreatedAt)
            .ThenBy(period => period.Id)
            .ToListAsync(cancellationToken);

        /*
         * Compatibility layer:
         * DB hiện vẫn có TaxPeriod theo từng BusinessProfile.
         * Service/API chỉ expose 1 canonical period cho mỗi Owner + kỳ.
         * Canonical = period được tạo sớm nhất.
         */
        return periods
            .GroupBy(BuildPeriodKey)
            .Select(group => group
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .First())
            .OrderByDescending(period => period.Year)
            .ThenByDescending(period => period.FilingWindow == TknFilingWindows.Annual ? 3 :
                period.FilingWindow == TknFilingWindows.SecondHalf ? 2 :
                period.FilingWindow == TknFilingWindows.FirstHalf ? 1 : 0)
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
                FilingWindow = period.FilingWindow,
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
            .ToList();
    }

    public Task<TaxPeriod?> GetByIdAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        return GetCanonicalByIdAsync(
            taxPeriodId,
            cancellationToken);
    }

    public Task<TaxPeriod?> GetCanonicalByIdAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        return ResolveCanonicalPeriodAsync(
            taxPeriodId,
            tracking: true,
            cancellationToken);
    }

    public Task<TaxPeriod?> GetQuarterAsync(
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxPeriods.FirstOrDefaultAsync(
            x =>
                x.BusinessId == businessId &&
                x.PeriodType == TaxPeriodTypes.Quarterly &&
                x.Year == year &&
                x.Quarter == quarter,
            cancellationToken);
    }

    public async Task<TaxPeriod?> GetYearAsync(
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdByBusinessAsync(businessId, cancellationToken);
        if (!ownerId.HasValue) return null;
        return await _dbContext.TaxPeriods
            .Where(x => x.Business.OwnerId == ownerId.Value &&
                x.PeriodType == TaxPeriodTypes.Yearly && x.Year == year)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TaxPeriod?> GetTknAsync(
        Guid ownerId,
        int year,
        string filingWindow,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxPeriods
            .Where(x =>
                x.Business.OwnerId == ownerId &&
                x.PeriodType == TaxPeriodTypes.Tkn &&
                x.Year == year &&
                x.FilingWindow == filingWindow)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OwnerQuarterlyFilingState>>
        GetOwnerQuarterlyFilingStatesAsync(
            Guid ownerId,
            int year,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.TaxPeriods.AsNoTracking()
            .Where(x =>
                x.Business.OwnerId == ownerId &&
                x.PeriodType == TaxPeriodTypes.Quarterly &&
                x.Year == year &&
                x.Quarter.HasValue)
            .OrderBy(x => x.Quarter)
            .ThenBy(x => x.Id)
            .Select(x => new OwnerQuarterlyFilingState(
                x.Id,
                x.Quarter!.Value,
                x.Status,
                x.TaxCalculations.Any(calculation =>
                    calculation.IsCurrent &&
                    calculation.Status == TaxCalculationStatuses.Completed &&
                    calculation.TaxMethod == PersonalIncomeTaxMethods.IncomeBased),
                x.TaxCalculations.Any(calculation =>
                    calculation.IsCurrent &&
                    calculation.Status == TaxCalculationStatuses.Completed &&
                    calculation.TaxMethod == PersonalIncomeTaxMethods.RevenueBased),
                x.TaxDeclarations.Any(declaration =>
                    declaration.IsCurrent &&
                    declaration.Status == TaxDeclarationStatuses.Submitted &&
                    declaration.FormCode == TaxFormCodes.Form01Cnkd)))
            .ToListAsync(cancellationToken);
    }

    public async Task<TaxPeriodIdentity?> GetIdentityAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var period = await ResolveCanonicalPeriodAsync(
            taxPeriodId,
            tracking: false,
            cancellationToken);
        if (period is null)
        {
            return null;
        }

        var ownerId = await GetOwnerIdByBusinessAsync(
            period.BusinessId,
            cancellationToken);
        return ownerId.HasValue
            ? new TaxPeriodIdentity(
                period.Id,
                period.BusinessId,
                ownerId.Value,
                period.Year)
            : null;
    }

    public async Task<TaxPeriodDetailResponse?> GetDetailAsync(
        Guid taxPeriodId,
        CancellationToken cancellationToken = default)
    {
        var period = await ResolveCanonicalPeriodAsync(
            taxPeriodId,
            tracking: false,
            cancellationToken);

        if (period is null)
        {
            return null;
        }

        var ownerId = await GetOwnerIdByBusinessAsync(
            period.BusinessId,
            cancellationToken);

        if (!ownerId.HasValue)
        {
            return null;
        }

        var businessIds = await GetOwnerBusinessIdsAsync(
            ownerId.Value,
            cancellationToken);

        var startDate = period.PeriodStartDate;
        var endExclusive = period.PeriodEndDate;

        var transactionSummary = await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                businessIds.Contains(transaction.BusinessId) &&
                transaction.TransactionDate >= startDate &&
                transaction.TransactionDate < endExclusive &&
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
                businessIds.Contains(transaction.BusinessId) &&
                transaction.TransactionDate >= startDate &&
                transaction.TransactionDate < endExclusive &&
                transaction.TransactionType == TransactionTypes.Sale)
            .CountAsync(
                transaction => transaction.Invoice == null,
                cancellationToken);

        var expenseSummary = await _dbContext.Expenses
            .AsNoTracking()
            .Where(expense =>
                businessIds.Contains(expense.BusinessId) &&
                expense.ExpenseDate >= startDate &&
                expense.ExpenseDate < endExclusive)
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
            FilingWindow = period.FilingWindow,
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
        var period = await ResolveCanonicalPeriodAsync(
            taxPeriodId,
            tracking: false,
            cancellationToken);

        if (period is null)
        {
            return null;
        }

        var ownerId = await GetOwnerIdByBusinessAsync(
            period.BusinessId,
            cancellationToken);

        if (!ownerId.HasValue)
        {
            return null;
        }

        var businessIds = await GetOwnerBusinessIdsAsync(
            ownerId.Value,
            cancellationToken);

        var startDate = period.PeriodStartDate;
        var endExclusive = period.PeriodEndDate;

        var transactions = _dbContext.Transactions
            .AsNoTracking()
            .Where(x =>
                businessIds.Contains(x.BusinessId) &&
                x.TransactionDate >= startDate &&
                x.TransactionDate < endExclusive &&
                x.TransactionType == TransactionTypes.Sale);

        var transactionCount =
            await transactions.CountAsync(cancellationToken);

        var completedTransactionCount =
            await transactions.CountAsync(
                x => x.Status == "Completed",
                cancellationToken);

        var cancelledTransactionCount =
            await transactions.CountAsync(
                x => x.Status == "Cancelled",
                cancellationToken);

        var unpaidTransactionCount =
            await transactions.CountAsync(
                x => x.Status != "Completed" &&
                     x.Status != "Cancelled",
                cancellationToken);

        var salesRevenue = await transactions
            .Where(x => x.Status == "Completed")
            .SumAsync(
                x => (decimal?)x.TotalAmount,
                cancellationToken) ?? 0m;

        var missingInvoiceCount = await transactions
            .CountAsync(
                transaction => transaction.Invoice == null,
                cancellationToken);

        var expensesQuery = _dbContext.Expenses
            .AsNoTracking()
            .Where(x =>
                businessIds.Contains(x.BusinessId) &&
                x.ExpenseDate >= startDate &&
                x.ExpenseDate < endExclusive);

        var expenseCount =
            await expensesQuery.CountAsync(cancellationToken);

        var totalExpense = await expensesQuery
            .SumAsync(
                x => (decimal?)x.Amount,
                cancellationToken) ?? 0m;

        var otherRevenue = 0m;
        var totalRevenue = salesRevenue + otherRevenue;
        var taxableRevenue = totalRevenue;

        var warnings =
            new List<TaxPeriodWarningResponse>();

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

        var endExclusive = start.AddYears(1);

        return await _dbContext.Transactions
                   .AsNoTracking()
                   .Where(x =>
                       x.BusinessId == businessId &&
                       x.TransactionType == TransactionTypes.Sale &&
                       x.Status == "Completed" &&
                       x.TransactionDate >= start &&
                       x.TransactionDate < endExclusive)
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
    
    public async Task<IReadOnlyList<BusinessProfile>> GetBusinessesWithCategoriesByOwnerAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BusinessProfiles
            .AsNoTracking()
            .Include(x => x.MainCategory)
            .Where(x => x.OwnerId == ownerId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.BusinessName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QttTaxPaymentSource>> GetTaxPaymentsByOwnerAsync(
        Guid ownerId,
        DateTime startInclusive,
        DateTime endExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TaxPayments
            .AsNoTracking()
            .Where(x =>
                x.TaxPeriod.Business.OwnerId == ownerId &&
                x.PaymentDate >= startInclusive &&
                x.PaymentDate < endExclusive)
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.PaymentCode)
            .Select(x => new QttTaxPaymentSource(
                x.Id,
                x.PaymentCode,
                x.PaymentDate,
                x.Amount,
                x.TaxType,
                x.Status,
                x.TaxDeclaration != null
                    ? x.TaxDeclaration.TaxCalculation.TaxMethod
                    : null))
            .ToListAsync(cancellationToken);
    }

    public Task<string?> GetAnnualTaxMethodSnapshotAsync(
        Guid ownerId,
        int year,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxCalculations
            .AsNoTracking()
            .Where(x =>
                x.IsCurrent &&
                x.TaxPeriod.Business.OwnerId == ownerId &&
                x.TaxPeriod.PeriodType == TaxPeriodTypes.Yearly &&
                x.TaxPeriod.Year == year)
            .OrderByDescending(x => x.CalculatedAt)
            .Select(x => x.TaxMethod)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OwnerTaxMethodHistoryState>>
        GetOwnerTaxMethodHistoryAsync(
            Guid ownerId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.TaxCalculations
            .AsNoTracking()
            .Where(x =>
                x.IsCurrent &&
                x.Status == TaxCalculationStatuses.Completed &&
                x.TaxMethodEffectiveYear.HasValue &&
                x.TaxPeriod.Business.OwnerId == ownerId)
            .OrderByDescending(x => x.TaxPeriod.Year)
            .ThenByDescending(x => x.CalculatedAt)
            .Select(x => new OwnerTaxMethodHistoryState(
                x.TaxMethod,
                x.TaxMethodEffectiveYear!.Value,
                x.TaxPeriod.Year,
                x.CalculatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasOwnerTaxArtifactsAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TaxPeriods
            .AsNoTracking()
            .AnyAsync(x =>
                x.Business.OwnerId == ownerId &&
                (x.Status != TaxPeriodStatuses.Open ||
                 x.TaxCalculations.Any() ||
                 x.TaxDeclarations.Any()),
                cancellationToken);
    }

    public async Task<decimal> GetRevenueForBusinessInPeriodAsync(
        Guid businessId,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
                   .AsNoTracking()
                   .Where(x =>
                       x.BusinessId == businessId &&
                       x.TransactionType == TransactionTypes.Sale &&
                       x.Status == "Completed" &&
                       x.TransactionDate >= periodStart &&
                       x.TransactionDate < periodEndExclusive)
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

        var endExclusive = start.AddYears(1);

        return await _dbContext.Transactions
                   .AsNoTracking()
                   .Where(t =>
                       t.Business.OwnerId == ownerId &&
                       t.TransactionType == TransactionTypes.Sale &&
                       t.Status == "Completed" &&
                       t.TransactionDate >= start &&
                       t.TransactionDate < endExclusive)
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
