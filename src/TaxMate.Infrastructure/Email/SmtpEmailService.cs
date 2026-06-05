using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TaxMate.Infrastructure.Options;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure.Email;

public class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _smtpOptions;
    private readonly AppOptions _appOptions;

    public SmtpEmailService(
        IOptions<SmtpOptions> smtpOptions,
        IOptions<AppOptions> appOptions)
    {
        _smtpOptions = smtpOptions.Value;
        _appOptions = appOptions.Value;
    }

    public async Task SendVerificationEmailAsync(
        string toEmail,
        string fullName,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _appOptions.FrontendBaseUrl.TrimEnd('/');
        var path = _appOptions.VerificationPath.StartsWith('/')
            ? _appOptions.VerificationPath
            : $"/{_appOptions.VerificationPath}";
        var verificationUrl =
            $"{baseUrl}{path}?token={Uri.EscapeDataString(verificationToken)}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtpOptions.FromName, _smtpOptions.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Xác minh email TaxMate";

        var body = new BodyBuilder
        {
            HtmlBody = $"""
                <p>Xin chào {fullName},</p>
                <p>Vui lòng nhấp vào liên kết sau để xác minh email (hết hạn sau 5 phút):</p>
                <p><a href="{verificationUrl}">Xác minh email</a></p>
                <p>Hoặc mở liên kết: {verificationUrl}</p>
                """
        };
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _smtpOptions.Host,
            _smtpOptions.Port,
            _smtpOptions.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_smtpOptions.Username))
        {
            await client.AuthenticateAsync(
                _smtpOptions.Username,
                _smtpOptions.Password,
                cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
