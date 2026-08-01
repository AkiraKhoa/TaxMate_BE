using Microsoft.Extensions.Options;
using TaxMate.Infrastructure.Options;
using TaxMate.Service.Interfaces;
using Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace TaxMate.Infrastructure.Sms;

public class TwilioSmsService : ISmsService
{
    private readonly TwilioOptions _options;

    public TwilioSmsService(IOptions<TwilioOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccountSid)
            || string.IsNullOrWhiteSpace(_options.AuthToken))
        {
            throw new InvalidOperationException("Twilio chưa được cấu hình.");
        }

        var from = ResolveSender();
        if (from is null)
        {
            throw new InvalidOperationException(
                "Twilio chưa được cấu hình. Cần FromPhoneNumber hoặc AlphanumericSenderId.");
        }

        TwilioClient.Init(_options.AccountSid, _options.AuthToken);

        var toNumber = ToE164Vietnamese(phoneNumber);
        var message = $"Ma xac minh TaxMate cua ban la: {otp}. Ma co hieu luc trong 5 phut.";

        try
        {
            await MessageResource.CreateAsync(
                to: new PhoneNumber(toNumber),
                from: from,
                body: message);
        }
        catch (ApiException ex) when (ex.Code == 21612)
        {
            throw new InvalidOperationException(
                "Không thể gửi SMS đến số Việt Nam với số Twilio US hiện tại. " +
                "Vui lòng bật quyền gửi SMS tới Vietnam trong Twilio Geo Permissions, " +
                "đăng ký Alphanumeric Sender ID cho Vietnam (ví dụ: TaxMate), " +
                "và đặt Twilio__AlphanumericSenderId trong .env. " +
                "Tài khoản trial cũng cần xác minh số điện thoại nhận. " +
                $"Chi tiết Twilio: {ex.Message}",
                ex);
        }
        catch (ApiException ex)
        {
            throw new InvalidOperationException($"Twilio từ chối gửi SMS: {ex.Message}", ex);
        }
    }

    private PhoneNumber? ResolveSender()
    {
        if (!string.IsNullOrWhiteSpace(_options.AlphanumericSenderId))
        {
            return new PhoneNumber(_options.AlphanumericSenderId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(_options.FromPhoneNumber))
        {
            return new PhoneNumber(_options.FromPhoneNumber.Trim());
        }

        return null;
    }

    internal static string ToE164Vietnamese(string phoneNumber)
    {
        if (phoneNumber.StartsWith('0') && phoneNumber.Length == 10)
        {
            return $"+84{phoneNumber[1..]}";
        }

        if (phoneNumber.StartsWith("+84"))
        {
            return phoneNumber;
        }

        return phoneNumber;
    }
}
