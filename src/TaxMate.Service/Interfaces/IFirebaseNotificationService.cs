namespace TaxMate.Service.Interfaces;

public interface IFirebaseNotificationService
{
    Task SendAsync(
        string token,
        string title,
        string body);
}