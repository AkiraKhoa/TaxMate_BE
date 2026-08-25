using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IPaymentAccountRepository : IGenericRepository<PaymentAccount>
{
    Task<PaymentAccount?> GetDefaultByBusinessIdAsync(Guid businessId);
    Task<PaymentAccount?> GetCashByBusinessIdAsync(Guid businessId);
    Task<PaymentAccount?> GetBankByAccountNumberAsync(
        Guid businessId,
        string accountNumber);
    Task<PaymentAccount?> GetBankBySePayXidAsync(
        string sePayBankAccountXid);
    Task<IEnumerable<PaymentAccount>> GetAllByBusinessIdAsync(
        Guid businessId,
        bool includeInactive = false);
    Task<PaymentAccount?> GetFirstActiveBankAsync(
        Guid businessId,
        IReadOnlyCollection<Guid> excludedPaymentAccountIds);
    Task<bool> HasMoneyMovementHistoryAsync(Guid paymentAccountId);
    Task UnsetAllDefaultAsync(Guid businessId);
}
