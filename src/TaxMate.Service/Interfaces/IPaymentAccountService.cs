using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IPaymentAccountService
{
    Task<Guid> CreateAsync(Guid businessId, CreatePaymentAccountRequest request);
    Task<IEnumerable<PaymentAccountResponse>> GetByBusinessIdAsync(Guid businessId);
    Task<PaymentAccountResponse> GetByIdAsync(Guid id);
    Task UpdateAsync(Guid id, UpdatePaymentAccountRequest request);
    Task DeleteAsync(Guid id);
    Task SetDefaultAsync(Guid businessId, Guid paymentAccountId);
    Task CreateOrUpdateFromSePayAsync(string companyXid, string bankAccountXid, string bankName, string bankCode, string accountNumber, string accountName);
    Task CreateOrUpdateFromLinkTokenAsync(string linkTokenXid, string bankAccountXid, string bankName, string accountNumber, string accountName);
    Task<(int Synced, int Total)> SyncSePayAccountsAsync(Guid businessId);
    Task CreateMockPaymentAsync(Guid transactionId, Guid paymentAccountId);
    Task<string> GetSePayDisconnectUrlAsync(Guid paymentAccountId, string scheme, string host);
    Task DeleteBySePayBankAccountXidAsync(string bankAccountXid);
    Task<(int Recovered, int Total)> RecoverAllFromSePayAsync();
}



