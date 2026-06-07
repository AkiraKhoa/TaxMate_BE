using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface ITransactionRepository : IGenericRepository<Transaction>
{
    Task<Transaction?> GetByIdWithDetailsAsync(Guid id);
    Task<Transaction?> GetByInvoiceNumberWithDetailsAsync(string invoiceNumber);
    Task<IEnumerable<Transaction>> GetByBusinessIdAsync(Guid businessId, int page, int pageSize);
    Task<string> GenerateTransactionCodeAsync(Guid businessId);
    Task<int> CountByBusinessIdAsync(Guid businessId);
}
