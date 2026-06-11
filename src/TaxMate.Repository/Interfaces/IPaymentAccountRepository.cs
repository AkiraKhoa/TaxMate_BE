using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IPaymentAccountRepository : IGenericRepository<PaymentAccount>
{
    Task<PaymentAccount?> GetDefaultByBusinessIdAsync(Guid businessId);
    Task<IEnumerable<PaymentAccount>> GetAllByBusinessIdAsync(Guid businessId);
    Task UnsetAllDefaultAsync(Guid businessId);
}
