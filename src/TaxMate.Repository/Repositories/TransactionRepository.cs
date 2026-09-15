using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class TransactionRepository : GenericRepository<Transaction>, ITransactionRepository
{
    public TransactionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Transaction?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .AsSplitQuery()
            .Include(x => x.TransactionItems)
            .Include(x => x.Payments)
                .ThenInclude(p => p.PaymentAccount)
            .FirstOrDefaultAsync(x => x.TransactionId == id);
    }

    public async Task<Transaction?> GetByInvoiceNumberWithDetailsAsync(string invoiceNumber)
    {
        return await _dbSet
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceNumber);
    }

    public async Task<IEnumerable<Transaction>> GetByBusinessIdAsync(
        Guid businessId,
        int page,
        int pageSize,
        string? status = null,
        string? paymentMethod = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? search = null,
        bool excludeEmptyDrafts = false)
    {
        var query = _dbSet
            .Include(x => x.TransactionItems)
            .Where(x => x.BusinessId == businessId);


        if (startDate.HasValue) query = query.Where(x => x.TransactionDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(x => x.TransactionDate < endDate.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.TransactionCode.ToLower().Contains(term) ||
                (x.InvoiceId != null && x.InvoiceId.ToLower().Contains(term)));
        }
        if (excludeEmptyDrafts) query = query.Where(x => x.Status != "Draft" || x.TransactionItems.Any());

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrEmpty(paymentMethod))
        {
            query = query.Where(x => x.Payments.Any(p => p.PaymentMethod == paymentMethod));
        }

        if (minAmount.HasValue)
        {
            query = query.Where(x => x.TotalAmount >= minAmount.Value);
        }

        if (maxAmount.HasValue)
        {
            query = query.Where(x => x.TotalAmount <= maxAmount.Value);
        }

        return await query
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountByBusinessIdAsync(
        Guid businessId,
        string? status = null,
        string? paymentMethod = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? search = null,
        bool excludeEmptyDrafts = false)
    {
        var query = _dbSet.Where(x => x.BusinessId == businessId);


        if (startDate.HasValue) query = query.Where(x => x.TransactionDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(x => x.TransactionDate < endDate.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.TransactionCode.ToLower().Contains(term) ||
                (x.InvoiceId != null && x.InvoiceId.ToLower().Contains(term)));
        }
        if (excludeEmptyDrafts) query = query.Where(x => x.Status != "Draft" || x.TransactionItems.Any());

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrEmpty(paymentMethod))
        {
            query = query.Where(x => x.Payments.Any(p => p.PaymentMethod == paymentMethod));
        }

        if (minAmount.HasValue)
        {
            query = query.Where(x => x.TotalAmount >= minAmount.Value);
        }

        if (maxAmount.HasValue)
        {
            query = query.Where(x => x.TotalAmount <= maxAmount.Value);
        }

        return await query.CountAsync();
    }

    public async Task<string> GenerateTransactionCodeAsync(Guid businessId)
    {
        var localToday = DateTime.UtcNow.AddHours(7);
        var dateStr = localToday.ToString("yyyyMMdd");
        var prefix = $"DH-{dateStr}-";
        
        var maxCode = await _dbSet
            .Where(x => x.TransactionCode.StartsWith(prefix))
            .OrderByDescending(x => x.TransactionCode)
            .Select(x => x.TransactionCode)
            .FirstOrDefaultAsync();

        var sequence = 1;
        if (!string.IsNullOrEmpty(maxCode))
        {
            var parts = maxCode.Split('-');
            if (parts.Length > 0 && int.TryParse(parts[^1], out var lastSeq))
            {
                sequence = lastSeq + 1;
            }
        }
        
        return $"{prefix}{sequence:D3}";
    }

    public async Task<IEnumerable<Transaction>> GetAwaitingTransactionsWithPaymentsAsync()
    {
        return await _dbSet
            .Include(x => x.Payments)
                .ThenInclude(p => p.PaymentAccount)
            .Where(x => x.Status == TaxMate.Model.Common.TransactionStatus.AwaitingPayment)
            .ToListAsync();
    }

    public async Task<bool> TryTransitionStatusAsync(
        Guid transactionId,
        string expectedStatus,
        string targetStatus)
    {
        var updatedAt = DateTime.UtcNow;
        var affectedRows = await _dbSet
            .Where(x => x.TransactionId == transactionId && x.Status == expectedStatus)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, targetStatus)
                .SetProperty(x => x.UpdatedAt, updatedAt));

        return affectedRows == 1;
    }
}
