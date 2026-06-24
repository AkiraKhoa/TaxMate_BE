using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface ITransactionRepository : IGenericRepository<Transaction>
{
    Task<Transaction?> GetByIdWithDetailsAsync(Guid id);
    Task<Transaction?> GetByInvoiceNumberWithDetailsAsync(string invoiceNumber);
    Task<IEnumerable<Transaction>> GetByBusinessIdAsync(
        Guid businessId,
        int page,
        int pageSize,
        string? status = null,
        string? paymentMethod = null,
        decimal? minAmount = null,
        decimal? maxAmount = null);
    Task<string> GenerateTransactionCodeAsync(Guid businessId);
    Task<int> CountByBusinessIdAsync(
        Guid businessId,
        string? status = null,
        string? paymentMethod = null,
        decimal? minAmount = null,
        decimal? maxAmount = null);
}
