using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace TaxMate.API.Hubs;

public class PaymentHub : Hub
{
    private readonly ILogger<PaymentHub> _logger;

    public PaymentHub(ILogger<PaymentHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinOrderGroup(string transactionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, transactionId);
        _logger.LogInformation("[SignalR] Client {ConnectionId} joined group={GroupId}", Context.ConnectionId, transactionId);
    }
}
