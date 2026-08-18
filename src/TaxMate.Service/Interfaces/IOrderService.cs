using TaxMate.Model.Common;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IOrderService
{
    Task<Guid> CreateOrderAsync(Guid businessId, CreateOrderRequest request);
    Task<OrderDetailResponse> GetOrderDetailAsync(Guid transactionId);
    Task<PagedResult<OrderSummaryResponse>> GetOrdersByBusinessAsync(
        Guid businessId,
        int page,
        int pageSize,
        string? status = null,
        string? paymentMethod = null,
        decimal? minAmount = null,
        decimal? maxAmount = null);

    Task AddItemAsync(Guid transactionId, AddOrderItemRequest request);
    Task UpdateItemAsync(Guid transactionId, Guid itemId, UpdateOrderItemRequest request);
    Task RemoveItemAsync(Guid transactionId, Guid itemId);

    /*
    Task ApplyDiscountAsync(Guid transactionId, ApplyDiscountRequest request);
    Task RemoveDiscountAsync(Guid transactionId);
    Task ApplySurchargeAsync(Guid transactionId, ApplySurchargeRequest request);
    Task RemoveSurchargeAsync(Guid transactionId);
    */

    Task<InvoiceDetailResponse> CheckoutAsync(Guid transactionId, CheckoutRequest request);
    Task ReopenForEditingAsync(Guid transactionId);
    Task CancelOrderAsync(Guid transactionId);
    Task CancelAllDraftsAsync(Guid businessId);
    Task<InvoiceDetailResponse> ConfirmPaymentAsync(Guid transactionId);
}
