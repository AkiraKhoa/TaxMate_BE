using TaxMate.Model.DTO.Auth;

namespace TaxMate.Service.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginWithGoogleAsync(string idToken, CancellationToken cancellationToken = default);

    Task<VerifyEmailResponse> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);

    Task ResendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
