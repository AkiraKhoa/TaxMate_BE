using System.Threading.Tasks;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IPaymentWebhookService
{
    Task ProcessSePayIpnWebhookAsync(SePayWebhookRequest request, string authHeader);
    Task ProcessBankHubWebhookAsync(SePayBankHubEventRequest request, string secretKeyHeader);
}
