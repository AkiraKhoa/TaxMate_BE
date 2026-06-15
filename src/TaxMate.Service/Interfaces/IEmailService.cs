namespace TaxMate.Service.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(
        string toEmail,
        string fullName,
        string verificationToken,
        CancellationToken cancellationToken = default);

    Task SendProfileOtpEmailAsync(
        string toEmail,
        string fullName,
        string otp,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetOtpEmailAsync(
        string toEmail,
        string fullName,
        string otp,
        CancellationToken cancellationToken = default);
}
