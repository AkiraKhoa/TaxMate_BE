using TaxMate.Model.DTO.Auth;

namespace TaxMate.Service.Interfaces;

public interface IUserProfileService
{
    Task<ProfileOtpResponse> InitiateProfileUpdateAsync(
        Guid userId,
        string taxCode,
        string phone,
        CancellationToken cancellationToken = default);

    Task<VerifyProfileEmailResponse> VerifyAndUpdateProfileAsync(
        Guid userId,
        string otp,
        CancellationToken cancellationToken = default);

    Task<ProfileOtpResponse> ResendProfileOtpAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
