namespace TaxMate.Service.Interfaces;

public interface ISmsService
{
    Task SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default);
}
