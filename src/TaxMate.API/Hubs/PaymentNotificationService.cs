using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Hubs;

public class PaymentNotificationService : IPaymentNotificationService
{
    private readonly IHubContext<PaymentHub> _hubContext;
    private readonly ILogger<PaymentNotificationService> _logger;

    public PaymentNotificationService(IHubContext<PaymentHub> hubContext, ILogger<PaymentNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyPaymentSuccessAsync(string transactionId)
    {
        _logger.LogInformation("[SignalR] Sending PaymentConfirmed to group={GroupId}", transactionId);
        // Gửi sự kiện PaymentConfirmed tới tất cả client trong group của đơn hàng này
        await _hubContext.Clients.Group(transactionId).SendAsync("PaymentConfirmed", transactionId);
        _logger.LogInformation("[SignalR] PaymentConfirmed sent successfully to group={GroupId}", transactionId);
    }
}
