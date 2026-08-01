using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface ISubscriptionService
{
    // View active subscription plans (public/auth)
    Task<IEnumerable<SubscriptionPlanResponse>> GetActivePlansAsync();

    // View current subscription of a user
    Task<UserSubscriptionResponse?> GetCurrentSubscriptionAsync(Guid userId);

    // Initiate subscription + create PayOS payment link
    Task<SubscribeResponse> SubscribeAsync(Guid userId, SubscribeRequest request);

    // Toggle auto-renew OFF for active subscription
    Task CancelAutoRenewAsync(Guid userId);

    // Cancel subscription immediately (mark Status = Cancelled)
    Task CancelSubscriptionImmediatelyAsync(Guid userId);

    // Called from PaymentController webhook
    Task ProcessWebhookAsync(long orderCode, string payosCode);

    // Verify PayOS webhook signature
    Task<PayOS.Models.Webhooks.WebhookData> VerifyWebhookDataAsync(PayOS.Models.Webhooks.Webhook body);
}
