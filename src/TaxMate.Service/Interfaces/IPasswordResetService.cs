using TaxMate.Model.DTO.Auth;

namespace TaxMate.Service.Interfaces;

public interface IPasswordResetService
{
    Task<ProfileOtpResponse> InitiateResetAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<VerifyResetPasswordOtpResponse> VerifyResetOtpAsync(
        string email,
        string otp,
        CancellationToken cancellationToken = default);

    Task<string> ResetPasswordAsync(
        string email,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default);

    Task<ProfileOtpResponse> ResendResetOtpAsync(
        string email,
        CancellationToken cancellationToken = default);
}
