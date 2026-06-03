using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;
    public ILegalDocumentRepository LegalDocuments { get; }
    public IPaymentAccountRepository PaymentAccounts { get; }
    public ITransactionRepository Transactions { get; }
    public IInvoiceRepository Invoices { get; }
    
    public UnitOfWork(
        DbContext context,
        ILegalDocumentRepository legalDocumentRepository,
        IPaymentAccountRepository paymentAccountRepository,
        ITransactionRepository transactionRepository,
        IInvoiceRepository invoiceRepository)
    {
        _context = context;
        LegalDocuments = legalDocumentRepository;
        PaymentAccounts = paymentAccountRepository;
        Transactions = transactionRepository;
        Invoices = invoiceRepository;
    }

    public IGenericRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);

        if (!_repositories.ContainsKey(type))
        {
            _repositories[type] = new GenericRepository<T>(_context);
        }

        return (IGenericRepository<T>)_repositories[type];
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
