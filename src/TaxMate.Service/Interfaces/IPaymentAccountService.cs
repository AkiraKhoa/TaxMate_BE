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
}
