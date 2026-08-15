using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class InvoiceRepository : GenericRepository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Invoice?> GetByNumberWithDetailsAsync(string invoiceNumber)
    {
        return await _dbSet
            .Include(x => x.InvoiceDetails)
                .ThenInclude(d => d.Product)
            .Include(x => x.Business)
                .ThenInclude(b => b.Owner)
            .FirstOrDefaultAsync(x => x.InvoiceNumber == invoiceNumber);
    }

    public async Task<int> CountByBusinessAndDateAsync(Guid businessId, DateTime date)
    {
        var dateStr = date.ToString("yyyyMMdd");
        var prefix = $"HD-{dateStr}-";
        var maxInvoice = await _dbSet
            .Where(x => x.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(x => x.InvoiceNumber)
            .Select(x => x.InvoiceNumber)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(maxInvoice))
        {
            var parts = maxInvoice.Split('-');
            if (parts.Length > 0 && int.TryParse(parts[^1], out var lastSeq))
            {
                return lastSeq;
            }
        }

        return await _dbSet.CountAsync(x => x.InvoiceNumber.StartsWith(prefix));
    }
}
