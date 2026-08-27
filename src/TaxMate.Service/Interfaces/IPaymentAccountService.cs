using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IPaymentAccountService
{
    Task<Guid> CreateAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        CreatePaymentAccountRequest request);
    Task<IEnumerable<PaymentAccountResponse>> GetByBusinessIdAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        bool includeInactive = false);
    Task<IEnumerable<PaymentAccountResponse>> GetAllMoneyAccountsByBusinessIdAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        bool includeInactive = false);
    Task<PaymentAccountResponse> GetCashByBusinessIdAsync(
        Guid authenticatedOwnerId,
        Guid businessId);
    Task<PaymentAccountResponse> GetByIdAsync(
        Guid authenticatedOwnerId,
        Guid id);
    Task UpdateAsync(
        Guid authenticatedOwnerId,
        Guid id,
        UpdatePaymentAccountRequest request);
    Task UpdateInitialBalanceAsync(
        Guid authenticatedOwnerId,
        Guid id,
        UpdatePaymentAccountInitialBalanceRequest request);
    Task DeactivateAsync(Guid authenticatedOwnerId, Guid id);
    Task ActivateAsync(Guid authenticatedOwnerId, Guid id);
    Task SetDefaultAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        Guid paymentAccountId);

    /// <summary>
    /// Ensures the system Cash account exists for a business. Existing-business
    /// migration already backfills it; the new-business coordinator must call
    /// this after creating a BusinessProfile.
    /// </summary>
    Task<Guid> EnsureCashAccountAsync(Guid businessId);

    Task CreateOrUpdateFromSePayAsync(string companyXid, string bankAccountXid, string bankName, string bankCode, string accountNumber, string accountName);
    Task CreateOrUpdateFromLinkTokenAsync(string linkTokenXid, string bankAccountXid, string bankName, string accountNumber, string accountName);
    Task<string> GetSePayConnectUrlAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        bool isMobileApp = true);
    Task<(int Synced, int Total)> SyncSePayAccountsAsync(
        Guid authenticatedOwnerId,
        Guid businessId);
    Task CreateMockPaymentAsync(
        Guid authenticatedOwnerId,
        Guid transactionId,
        Guid paymentAccountId);
    Task<string> GetSePayDisconnectUrlAsync(
        Guid authenticatedOwnerId,
        Guid paymentAccountId);
    Task DeleteBySePayBankAccountXidAsync(string bankAccountXid);
    Task<(int Recovered, int Total)> RecoverAllFromSePayAsync(
        Guid authenticatedOwnerId);
}



