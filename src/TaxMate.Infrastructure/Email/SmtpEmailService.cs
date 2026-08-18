using System.Globalization;
using System.Net;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TaxMate.Infrastructure.Options;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Reports;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure.Email;

public class SmtpEmailService : IEmailService
{
    private const string BrandRed = "#d32f2f";
    private const string BrandRedDark = "#b71c1c";

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
        var linkBase = string.IsNullOrWhiteSpace(_appOptions.VerificationLinkBaseUrl)
            ? _appOptions.ApiBaseUrl
            : _appOptions.VerificationLinkBaseUrl;
        var baseUrl = linkBase.TrimEnd('/');
        var path = _appOptions.VerificationPath.StartsWith('/')
            ? _appOptions.VerificationPath
            : $"/{_appOptions.VerificationPath}";
        var verificationUrl =
            $"{baseUrl}{path}?token={Uri.EscapeDataString(verificationToken)}";

        var html = $"""
            <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
            <p>Vui lòng nhấp vào liên kết sau để xác minh email (hết hạn sau 24 giờ):</p>
            <p><a href="{verificationUrl}">Xác minh email</a></p>
            <p>Hoặc mở liên kết: {verificationUrl}</p>
            """;

        await SendHtmlAsync(toEmail, "Xác minh email TaxMate", html, cancellationToken);
    }

    public Task SendProfileOtpEmailAsync(
        string toEmail,
        string fullName,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var html = $"""
            <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
            <p>Mã xác minh cập nhật số căn cước và số điện thoại của bạn là:</p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:4px;">{WebUtility.HtmlEncode(otp)}</p>
            <p>Mã có hiệu lực trong 5 phút. Không chia sẻ mã này với bất kỳ ai.</p>
            """;

        return SendHtmlAsync(
            toEmail,
            "Mã xác minh cập nhật thông tin TaxMate",
            html,
            cancellationToken);
    }

    public Task SendPasswordResetOtpEmailAsync(
        string toEmail,
        string fullName,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var html = $"""
            <p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>
            <p>Mã xác minh đặt lại mật khẩu của bạn là:</p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:4px;">{WebUtility.HtmlEncode(otp)}</p>
            <p>Mã có hiệu lực trong 5 phút. Không chia sẻ mã này với bất kỳ ai.</p>
            """;

        return SendHtmlAsync(
            toEmail,
            "Mã đặt lại mật khẩu TaxMate",
            html,
            cancellationToken);
    }

    public Task SendRevenueThresholdEmailAsync(
        string toEmail,
        string fullName,
        int currentYear,
        int currentQuarter,
        DateTime windowStart,
        DateTime windowEnd,
        decimal threshold,
        IReadOnlyList<OwnerProfileRevenueRow> profiles,
        decimal total,
        CancellationToken cancellationToken = default)
    {
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        var currentPeriod = TaxPeriodWindow.FormatQuarterPeriod(currentYear, currentQuarter);
        var windowLabel =
            $"{windowStart:dd/MM/yyyy} – {windowEnd.AddTicks(-1):dd/MM/yyyy}";
        var frontendUrl = string.IsNullOrWhiteSpace(_appOptions.FrontendBaseUrl)
            ? "https://localhost:5173"
            : _appOptions.FrontendBaseUrl.TrimEnd('/');

        var rows = new StringBuilder();
        foreach (var profile in profiles)
        {
            rows.Append($"""
                <tr>
                  <td style="padding:10px 14px;border-bottom:1px solid #f0d0d0;color:#333;">{WebUtility.HtmlEncode(profile.BusinessName)}</td>
                  <td style="padding:10px 14px;border-bottom:1px solid #f0d0d0;text-align:right;color:#333;white-space:nowrap;">{FormatVnd(profile.Revenue, vi)}</td>
                </tr>
                """);
        }

        var html = $"""
            <div style="margin:0;padding:0;background:#faf5f5;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#faf5f5;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #f0d0d0;">
                      <tr>
                        <td style="background:{BrandRed};padding:22px 28px;">
                          <p style="margin:0;color:#ffffff;font-size:20px;font-weight:bold;letter-spacing:0.4px;">TaxMate</p>
                          <p style="margin:6px 0 0;color:#ffe8e8;font-size:13px;">Thông báo ngưỡng doanh thu 1 tỷ đồng</p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px;">
                          <p style="margin:0 0 12px;color:#222;font-size:16px;">Xin chào {WebUtility.HtmlEncode(fullName)},</p>
                          <p style="margin:0 0 16px;color:#333;font-size:14px;line-height:1.6;">
                            Tổng doanh thu của <strong>tất cả hồ sơ kinh doanh</strong> trong
                            <strong>{currentPeriod}</strong> và 3 kỳ trước
                            ({WebUtility.HtmlEncode(windowLabel)}) đã đạt hoặc vượt
                            <strong>{FormatVnd(threshold, vi)}</strong>.
                          </p>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;border:1px solid #f0d0d0;border-radius:8px;overflow:hidden;">
                            <thead>
                              <tr>
                                <th align="left" style="background:{BrandRed};color:#ffffff;padding:10px 14px;font-size:13px;">Hồ sơ kinh doanh</th>
                                <th align="right" style="background:{BrandRed};color:#ffffff;padding:10px 14px;font-size:13px;">Doanh thu 4 kỳ</th>
                              </tr>
                            </thead>
                            <tbody>
                              {rows}
                              <tr>
                                <td style="padding:12px 14px;background:#fff5f5;font-weight:bold;color:{BrandRedDark};">Tổng cộng</td>
                                <td style="padding:12px 14px;background:#fff5f5;font-weight:bold;text-align:right;color:{BrandRedDark};white-space:nowrap;">{FormatVnd(total, vi)}</td>
                              </tr>
                            </tbody>
                          </table>
                          <div style="margin:20px 0 0;padding:14px 16px;background:#fff5f5;border-left:4px solid {BrandRed};border-radius:4px;">
                            <p style="margin:0;color:{BrandRedDark};font-size:14px;line-height:1.6;font-weight:bold;">
                              Từ bây giờ, đối với doanh thu trên 1 tỷ bạn phải xuất file S2A để nộp thuế GTGT và TNCN.
                            </p>
                          </div>
                          <p style="margin:22px 0 0;">
                            <a href="{frontendUrl}" style="display:inline-block;background:{BrandRed};color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:bold;font-size:14px;">
                              Xuất S2A-HKD trên TaxMate
                            </a>
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="background:{BrandRedDark};padding:12px 28px;color:#ffd6d6;font-size:11px;">
                          Email này được gửi tự động từ TaxMate.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </div>
            """;

        return SendHtmlAsync(
            toEmail,
            "Doanh thu 4 kỳ đã đạt 1 tỷ đồng - TaxMate",
            html,
            cancellationToken);
    }

    private static string FormatVnd(decimal amount, CultureInfo culture)
        => $"{amount.ToString("N0", culture)} ₫";

    private async Task SendHtmlAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtpOptions.FromName, _smtpOptions.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

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
