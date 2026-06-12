using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TaxMate.Infrastructure.Options;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure.Auth;

public class GoogleOAuthService : IGoogleOAuthService
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly GoogleAuthOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleOAuthService(
        IOptions<GoogleAuthOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public string BuildAuthorizationUrl(string state)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("Google ClientId is not configured.");
        }

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "online",
            ["prompt"] = "select_account"
        };

        var queryString = string.Join(
            "&",
            query.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value!)}"));

        return $"{AuthEndpoint}?{queryString}";
    }

    public async Task<string> ExchangeCodeForIdTokenAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("Google OAuth credentials are not configured.");
        }

        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["redirect_uri"] = _options.RedirectUri,
                ["grant_type"] = "authorization_code"
            }),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Google token exchange failed: {errorBody}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(tokenResponse?.IdToken))
        {
            throw new InvalidOperationException("Google did not return an ID token.");
        }

        return tokenResponse.IdToken;
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
    }
}
