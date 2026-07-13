using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class CassoAccountsApiResponse
{
    public int Error { get; set; }
    public string Message { get; set; } = null!;
    public List<CassoAccountDto> Data { get; set; } = new();
}

public class CassoService : ICassoService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public CassoService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _clientId = configuration["Casso:ClientId"] ?? "YOUR_CASSO_CLIENT_ID";
        _clientSecret = configuration["Casso:ClientSecret"] ?? "YOUR_CASSO_CLIENT_SECRET";
    }

    public string GetAuthorizationUrl(Guid businessId, string redirectUri)
    {
        return $"https://oauth.casso.vn/oauth/authorize?client_id={_clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope=read&state={businessId}";
    }

    public async Task<CassoTokenResponse> ExchangeCodeForTokensAsync(string code, string redirectUri)
    {
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.casso.vn/oauth/token");

        var postData = new Dictionary<string, string>
        {
            { "client_id", _clientId },
            { "client_secret", _clientSecret },
            { "code", code },
            { "redirect_uri", redirectUri },
            { "grant_type", "authorization_code" }
        };

        request.Content = new FormUrlEncodedContent(postData);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        
        var options = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        };
        var tokenResponse = JsonSerializer.Deserialize<CassoTokenResponse>(responseContent, options);

        if (tokenResponse == null)
        {
            throw new Exception("Failed to deserialize Casso token response.");
        }

        return tokenResponse;
    }

    public async Task<IEnumerable<CassoAccountDto>> GetBankAccountsAsync(string accessToken)
    {
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://oauth.casso.vn/v2/accounts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        
        var options = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        };
        var cassoRes = JsonSerializer.Deserialize<CassoAccountsApiResponse>(responseContent, options);

        if (cassoRes == null || cassoRes.Error != 0)
        {
            throw new Exception(cassoRes?.Message ?? "Failed to fetch bank accounts from Casso.");
        }

        return cassoRes.Data;
    }
}
