using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.DTO.Reports;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;

    public ReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SalesDashboardSummaryResponse> GetSalesSummaryAsync(
        Guid businessId,
        DateTime startDate,
        DateTime endDate)
    {
        var transactions = _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed" &&
                x.TransactionDate >= startDate &&
                x.TransactionDate < endDate);

        return new SalesDashboardSummaryResponse
        {
            TotalRevenue = await transactions.SumAsync(x => x.TotalAmount),

            TotalOrders = await transactions.CountAsync(),

            TotalProductsSold = await _context.TransactionItems
                .Where(x =>
                    x.Transaction.BusinessId == businessId &&
                    x.Transaction.Status == "Completed" &&
                    x.Transaction.TransactionDate >= startDate &&
                    x.Transaction.TransactionDate < endDate)
                .SumAsync(x => x.Quantity)
        };
    }

    public async Task<List<ProductRevenueDistributionResponse>> GetRevenueDistributionAsync(
        Guid businessId,
        DateTime startDate,
        DateTime endDate)
    {
        var totalRevenue = await _context.TransactionItems
            .Where(x =>
                x.Transaction.BusinessId == businessId &&
                x.Transaction.Status == "Completed" &&
                x.Transaction.TransactionDate >= startDate &&
                x.Transaction.TransactionDate < endDate)
            .SumAsync(x => x.LineTotal);

        if (totalRevenue == 0)
        {
            return [];
        }

        return await _context.TransactionItems
            .Where(x =>
                x.Transaction.BusinessId == businessId &&
                x.Transaction.Status == "Completed" &&
                x.Transaction.TransactionDate >= startDate &&
                x.Transaction.TransactionDate < endDate)
            .GroupBy(x => x.Product.Name)
            .Select(g => new ProductRevenueDistributionResponse
            {
                ProductName = g.Key ?? "Chưa phân loại",
                Revenue = g.Sum(x => x.LineTotal),
                Percentage = Math.Round(
                    g.Sum(x => x.LineTotal) / totalRevenue * 100,
                    2)
            })
            .ToListAsync();
    }

    public async Task<List<TopSellingProductResponse>> GetTopSellingProductsAsync(
        Guid businessId,
        DateTime startDate,
        DateTime endDate,
        int top = 3)
    {
        var items = await _context.TransactionItems
            .Where(x =>
                x.Transaction.BusinessId == businessId &&
                x.Transaction.Status == "Completed" &&
                x.Transaction.TransactionDate >= startDate &&
                x.Transaction.TransactionDate < endDate)
            .GroupBy(x => x.ProductName)
            .Select(g => new
            {
                ProductName = g.Key,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(top)
            .ToListAsync();

        return items
            .Select((x, index) => new TopSellingProductResponse
            {
                Rank = index + 1,
                ProductName = x.ProductName,
                QuantitySold = x.QuantitySold,
                Revenue = x.Revenue
            })
            .ToList();
    }

    public async Task<List<SalesTrendResponse>> GetQuarterSalesTrendAsync(
    Guid businessId,
    int year,
    int month)
    {
        var currentQuarter = ((month - 1) / 3) + 1;

        var currentQuarterStartMonth =
            ((currentQuarter - 1) * 3) + 1;

        var currentQuarterStart =
            new DateTime(
                year,
                currentQuarterStartMonth,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var previousQuarterStart =
            currentQuarterStart.AddMonths(-3);

        var currentQuarterEnd =
            currentQuarterStart.AddMonths(3);

        var transactions = await _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed" &&
                x.TransactionDate >= previousQuarterStart &&
                x.TransactionDate < currentQuarterEnd)
            .Select(x => new
            {
                x.TransactionDate,
                x.TotalAmount
            })
            .ToListAsync();

        var result = new List<SalesTrendResponse>();

        for (var i = 0; i < 3; i++)
        {
            var currentMonthStart =
                currentQuarterStart.AddMonths(i);

            var currentMonthEnd =
                currentMonthStart.AddMonths(1);

            var previousMonthStart =
                previousQuarterStart.AddMonths(i);

            var previousMonthEnd =
                previousMonthStart.AddMonths(1);

            result.Add(new SalesTrendResponse
            {
                Label = i switch
                {
                    0 => "Tháng thứ nhất",
                    1 => "Tháng thứ hai",
                    _ => "Tháng thứ ba"
                },

                CurrentQuarterRevenue = transactions
                    .Where(x =>
                        x.TransactionDate >= currentMonthStart &&
                        x.TransactionDate < currentMonthEnd)
                    .Sum(x => x.TotalAmount),

                PreviousQuarterRevenue = transactions
                    .Where(x =>
                        x.TransactionDate >= previousMonthStart &&
                        x.TransactionDate < previousMonthEnd)
                    .Sum(x => x.TotalAmount)
            });
        }

        return result;
    }
    
    public async Task<List<ActiveSalesMonthResponse>> GetActiveSalesMonthsAsync(
        Guid businessId)
    {
        var result = await _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed")
            .GroupBy(x => new
            {
                x.TransactionDate.Year,
                x.TransactionDate.Month
            })
            .Select(g => new ActiveSalesMonthResponse
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalOrders = g.Count(),
                TotalRevenue = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync();

        foreach (var item in result)
        {
            item.Label = $"Tháng {item.Month}/{item.Year}";
        }

        return result;
    }
    
    public async Task<EstimatedProfitSummaryResponse> GetEstimatedProfitSummaryAsync(
        Guid businessId,
        int year,
        int quarter)
    {
        var startMonth = ((quarter - 1) * 3) + 1;

        var startDate = new DateTime(
            year,
            startMonth,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var endDate = startDate.AddMonths(3);

        var revenue = await _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed" &&
                x.TransactionDate >= startDate &&
                x.TransactionDate < endDate)
            .SumAsync(x => x.TotalAmount);

        var costOfGoodsSold = await _context.TransactionItems
            .Where(x =>
                x.Transaction.BusinessId == businessId &&
                x.Transaction.Status == "Completed" &&
                x.Transaction.TransactionDate >= startDate &&
                x.Transaction.TransactionDate < endDate)
            .SumAsync(x => x.CostAmount);

        return new EstimatedProfitSummaryResponse
        {
            Revenue = revenue,
            CostOfGoodsSold = costOfGoodsSold,
            Profit = revenue - costOfGoodsSold
        };
    }
    
    public async Task<List<EstimatedProfitTrendResponse>> GetEstimatedProfitTrendAsync(
        Guid businessId,
        int year)
    {
        var startDate = new DateTime(
            year,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var endDate = startDate.AddYears(1);

        var monthlyData = await _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed" &&
                x.TransactionDate >= startDate &&
                x.TransactionDate < endDate)
            .Select(x => new
            {
                Month = x.TransactionDate.Month,
                Revenue = x.TotalAmount,
                Cost = x.TransactionItems.Sum(i => i.CostAmount)
            })
            .ToListAsync();

        var result = new List<EstimatedProfitTrendResponse>();

        for (var month = 1; month <= 12; month++)
        {
            var revenue = monthlyData
                .Where(x => x.Month == month)
                .Sum(x => x.Revenue);

            var cost = monthlyData
                .Where(x => x.Month == month)
                .Sum(x => x.Cost);

            result.Add(new EstimatedProfitTrendResponse
            {
                Month = month,
                Label = $"T{month}",
                Profit = revenue - cost
            });
        }

        return result;
    }
    
    public async Task<List<ActiveSalesQuarterResponse>> GetActiveSalesQuartersAsync(
        Guid businessId)
    {
        var result = await _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed")
            .GroupBy(x => new
            {
                x.TransactionDate.Year,
                Quarter = ((x.TransactionDate.Month - 1) / 3) + 1
            })
            .Select(g => new ActiveSalesQuarterResponse
            {
                Year = g.Key.Year,
                Quarter = g.Key.Quarter,
                TotalOrders = g.Count(),
                TotalRevenue = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Quarter)
            .ToListAsync();

        foreach (var item in result)
        {
            item.StartMonth = ((item.Quarter - 1) * 3) + 1;
            item.EndMonth = item.StartMonth + 2;

            item.Label =
                $"Quý {ToRomanQuarter(item.Quarter)}/{item.Year} ({item.StartMonth:00}-{item.EndMonth:00}/{item.Year})";
        }

        return result;
    }

    private static string ToRomanQuarter(int quarter)
    {
        return quarter switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            _ => quarter.ToString()
        };
    }
    
    public async Task<CashFlowSummaryResponse> GetCashFlowSummaryAsync(
        Guid businessId,
        int year,
        int quarter)
    {
        var startMonth = ((quarter - 1) * 3) + 1;

        var startDate = new DateTime(
            year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);

        var endDate = startDate.AddMonths(3);

        var totalIncome = await _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed" &&
                x.TransactionDate >= startDate &&
                x.TransactionDate < endDate)
            .SumAsync(x => x.TotalAmount);

        var totalExpense = await _context.Expenses
            .Where(x =>
                x.BusinessId == businessId &&
                x.ExpenseDate >= startDate &&
                x.ExpenseDate < endDate)
            .SumAsync(x => x.Amount);

        return new CashFlowSummaryResponse
        {
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            NetAmount = totalIncome - totalExpense
        };
    }
    
    public async Task<List<ExpenseDistributionResponse>> GetExpenseDistributionAsync(
        Guid businessId,
        int year,
        int quarter)
    {
        var startMonth = ((quarter - 1) * 3) + 1;

        var startDate = new DateTime(
            year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);

        var endDate = startDate.AddMonths(3);

        var totalExpense = await _context.Expenses
            .Where(x =>
                x.BusinessId == businessId &&
                x.ExpenseDate >= startDate &&
                x.ExpenseDate < endDate)
            .SumAsync(x => x.Amount);

        if (totalExpense == 0)
        {
            return [];
        }

        return await _context.Expenses
            .Where(x =>
                x.BusinessId == businessId &&
                x.ExpenseDate >= startDate &&
                x.ExpenseDate < endDate)
            .GroupBy(x => x.ExpenseCategory.CategoryName)
            .Select(g => new ExpenseDistributionResponse
            {
                CategoryName = g.Key,
                Amount = g.Sum(x => x.Amount),
                Percentage = Math.Round(
                    g.Sum(x => x.Amount) / totalExpense * 100,
                    2)
            })
            .OrderByDescending(x => x.Amount)
            .ToListAsync();
    }
    
    public async Task<List<CashFlowTrendResponse>> GetCashFlowTrendAsync(
        Guid businessId,
        int year,
        int quarter)
    {
        var startMonth = ((quarter - 1) * 3) + 1;

        var startDate = new DateTime(
            year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);

        var endDate = startDate.AddMonths(3);

        var incomes = await _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed" &&
                x.TransactionDate >= startDate &&
                x.TransactionDate < endDate)
            .GroupBy(x => x.TransactionDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                Income = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var expenses = await _context.Expenses
            .Where(x =>
                x.BusinessId == businessId &&
                x.ExpenseDate >= startDate &&
                x.ExpenseDate < endDate)
            .GroupBy(x => x.ExpenseDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                Expense = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        var result = new List<CashFlowTrendResponse>();

        for (var i = 0; i < 3; i++)
        {
            var month = startMonth + i;

            result.Add(new CashFlowTrendResponse
            {
                Month = month,
                Label = $"Tháng {month}",
                Income = incomes.FirstOrDefault(x => x.Month == month)?.Income ?? 0,
                Expense = expenses.FirstOrDefault(x => x.Month == month)?.Expense ?? 0
            });
        }

        return result;
    }
    
    public async Task<decimal> GetAccumulatedRevenueAsync(
        Guid businessId,
        int year)
    {
        var startDate = new DateTime(
            year,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var endDate = startDate.AddYears(1);

        return await _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed" &&
                x.TransactionDate >= startDate &&
                x.TransactionDate < endDate)
            .SumAsync(x => x.TotalAmount);
    }
    
    public async Task<List<TaxQuarterRevenueResponse>> GetQuarterRevenuesAsync(
        Guid businessId,
        int year)
    {
        var startDate = new DateTime(
            year,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var endDate = startDate.AddYears(1);

        var revenues = await _context.Transactions
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == "Completed" &&
                x.TransactionDate >= startDate &&
                x.TransactionDate < endDate)
            .GroupBy(x => ((x.TransactionDate.Month - 1) / 3) + 1)
            .Select(g => new
            {
                Quarter = g.Key,
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var result = new List<TaxQuarterRevenueResponse>();

        for (var quarter = 1; quarter <= 4; quarter++)
        {
            result.Add(new TaxQuarterRevenueResponse
            {
                Quarter = quarter,
                Revenue = revenues
                    .FirstOrDefault(x => x.Quarter == quarter)
                    ?.Revenue ?? 0
            });
        }

        return result;
    }
    
    public async Task<decimal> GetAccumulatedRevenueByOwnerAsync(
        Guid ownerId,
        int year)
    {
        var startDate = new DateTime(
            year,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var endDate = startDate.AddYears(1);

        return await _context.Transactions
                   .Where(x =>
                       x.Business.OwnerId == ownerId &&
                       x.Status == TransactionStatus.Completed &&
                       x.TransactionDate >= startDate &&
                       x.TransactionDate < endDate)
                   .SumAsync(x => (decimal?)x.TotalAmount)
               ?? 0m;
    }
    
    public async Task<List<TaxQuarterRevenueResponse>>
        GetQuarterRevenuesByOwnerAsync(
            Guid ownerId,
            int year)
    {
        var startDate = new DateTime(
            year,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var endDate = startDate.AddYears(1);

        var revenues = await _context.Transactions
            .Where(x =>
                x.Business.OwnerId == ownerId &&
                x.Status == "Completed" &&
                x.TransactionDate >= startDate &&
                x.TransactionDate < endDate)
            .GroupBy(x =>
                ((x.TransactionDate.Month - 1) / 3) + 1)
            .Select(g => new
            {
                Quarter = g.Key,
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var result = new List<TaxQuarterRevenueResponse>();

        for (var quarter = 1; quarter <= 4; quarter++)
        {
            result.Add(
                new TaxQuarterRevenueResponse
                {
                    Quarter = quarter,

                    Revenue = revenues
                        .FirstOrDefault(x =>
                            x.Quarter == quarter)
                        ?.Revenue ?? 0m
                });
        }

        return result;
    }

    public async Task<List<OwnerProfileRevenueRow>> GetOwnerRevenueByProfileAsync(
        Guid ownerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.BusinessProfiles
            .AsNoTracking()
            .Where(profile => profile.OwnerId == ownerId && profile.IsActive)
            .Select(profile => new OwnerProfileRevenueRow
            {
                BusinessId = profile.Id,
                BusinessName = profile.BusinessName,
                Revenue = profile.Transactions
                    .Where(transaction =>
                        transaction.TransactionType == TransactionTypes.Sale &&
                        transaction.Status == "Completed" &&
                        transaction.TransactionDate >= startDate &&
                        transaction.TransactionDate < endDate)
                    .Sum(transaction => (decimal?)transaction.TotalAmount) ?? 0m
            })
            .OrderBy(row => row.BusinessName)
            .ToListAsync(cancellationToken);
    }
}   