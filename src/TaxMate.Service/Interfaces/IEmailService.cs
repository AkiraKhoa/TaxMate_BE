namespace TaxMate.Service.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(
        string toEmail,
        string fullName,
        string verificationToken,
        CancellationToken cancellationToken = default);
}
