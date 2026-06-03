using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class InvoiceRepository : GenericRepository<Invoice>, IInvoiceRepository
{
    private readonly AppDbContext _context;

    public InvoiceRepository(AppDbContext context) : base(context)
    {
        _context = context;
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
        return await _dbSet.CountAsync(x => x.BusinessId == businessId && x.InvoiceNumber.StartsWith(prefix));
    }
}
