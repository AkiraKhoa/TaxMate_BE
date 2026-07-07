using Microsoft.EntityFrameworkCore;
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
}   