using System.Threading.Tasks;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IPaymentWebhookService
{
    Task ProcessPayOsWebhookAsync(PayOsWebhookRequest request);
    Task ProcessSePayIpnWebhookAsync(SePayWebhookRequest request, string authHeader);
    Task ProcessBankHubWebhookAsync(SePayBankHubEventRequest request, string secretKeyHeader);
    Task ProcessCassoWebhookAsync(CassoWebhookRequest request, string secureTokenHeader);
}
