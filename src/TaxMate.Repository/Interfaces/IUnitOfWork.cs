using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<T> Repository<T>() where T : class;
    ILegalDocumentRepository LegalDocuments { get; }
    IPaymentAccountRepository PaymentAccounts { get; }
    ITransactionRepository Transactions { get; }
    IInvoiceRepository Invoices { get; }
    IGenericRepository<BusinessProfile> BusinessProfiles { get; }
    IGenericRepository<Product> Products { get; }
    IGenericRepository<ProductPrice> ProductPrices { get; }
    IGenericRepository<TransactionItem> TransactionItems { get; }
    IGenericRepository<Payment> Payments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
