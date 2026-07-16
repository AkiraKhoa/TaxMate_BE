namespace TaxMate.Service.Interfaces;

public interface INotificationService
{
    Task SendTelegramAsync(string chatId, string message, CancellationToken cancellationToken = default);
    Task SendFcmPushAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default);
}
