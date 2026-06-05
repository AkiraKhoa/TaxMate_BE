namespace TaxMate.Service.Interfaces;

public record GoogleUserInfo(
    string GoogleId,
    string Email,
    string FullName,
    string? AvatarUrl,
    bool EmailVerified);

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
