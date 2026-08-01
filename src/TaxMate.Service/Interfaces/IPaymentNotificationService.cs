using System.Threading.Tasks;

namespace TaxMate.Service.Interfaces;

public interface IPaymentNotificationService
{
    Task NotifyPaymentSuccessAsync(string transactionId);
}
