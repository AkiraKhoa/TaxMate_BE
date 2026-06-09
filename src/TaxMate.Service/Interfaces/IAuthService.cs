using TaxMate.Model.DTO.Auth;

namespace TaxMate.Service.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginWithPasswordAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginWithGoogleAsync(string idToken, CancellationToken cancellationToken = default);

    Task<string> ConfirmEmailVerificationAsync(string token, CancellationToken cancellationToken = default);

    Task<VerifyEmailResponse> CompleteEmailVerificationAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task ResendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
