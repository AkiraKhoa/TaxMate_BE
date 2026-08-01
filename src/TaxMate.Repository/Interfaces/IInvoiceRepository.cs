using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IInvoiceRepository : IGenericRepository<Invoice>
{
    Task<Invoice?> GetByNumberWithDetailsAsync(string invoiceNumber);
    Task<int> CountByBusinessAndDateAsync(Guid businessId, DateTime date);
}
