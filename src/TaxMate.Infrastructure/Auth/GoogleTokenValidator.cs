using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using TaxMate.Infrastructure.Options;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure.Auth;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleAuthOptions _options;

    public GoogleTokenValidator(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public async Task<GoogleUserInfo> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("Google ClientId is not configured.");
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [_options.ClientId]
        };

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            throw new UnauthorizedAccessException("Google token không hợp lệ.", ex);
        }

        if (string.IsNullOrWhiteSpace(payload.Subject))
        {
            throw new UnauthorizedAccessException("Google token thiếu subject.");
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new UnauthorizedAccessException("Google token thiếu email.");
        }

        if (payload.EmailVerified == false)
        {
            throw new UnauthorizedAccessException("Email Google chưa được xác minh.");
        }

        return new GoogleUserInfo(
            payload.Subject,
            payload.Email,
            payload.Name ?? payload.Email,
            payload.Picture,
            payload.EmailVerified);
    }
}
