using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class SePayService : ISePayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SePayService> _logger;
    private readonly string _baseUrl;

    // Use case-insensitive deserialization to handle SePay snake_case responses
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SePayService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IGenericRepository<BusinessProfile> businessProfiles,
        IUnitOfWork unitOfWork,
        ILogger<SePayService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _businessProfiles = businessProfiles;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _baseUrl = GetRequiredHttpsUrl("SePay:BaseUrl").TrimEnd('/');
    }

    // Cloudflare WAF on SePay blocks .NET HttpClient's default User-Agent.
    // Research confirmed: Need browser-like User-Agent + Accept headers + CookieContainer.
    // TLS fingerprinting may still cause issues — IP whitelist via SePay support is the clean fix.
    private HttpClient CreateSePayClient()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new System.Net.CookieContainer(),
            UseCookies = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "application/json, text/plain, */*");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language",
            "en-US,en;q=0.9,vi;q=0.8");
        return client;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var client = CreateSePayClient();
        var clientId = GetRequiredConfiguration("SePay:ClientId");
        var clientSecret = GetRequiredConfiguration("SePay:ClientSecret");
        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        _logger.LogInformation("[SePay] Requesting provider access token.");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        // Per SePay docs: empty body is fine (no body required)
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>());

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation(
            "[SePay] /v1/token completed. Status={Status}",
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"SePay token request failed with status {(int)response.StatusCode}.");
        }

        // Use case-insensitive options to handle snake_case "access_token"
        var tokenRes = JsonSerializer.Deserialize<SePayTokenResponse>(responseContent, _jsonOptions);

        if (tokenRes == null || string.IsNullOrEmpty(tokenRes.AccessToken))
        {
            _logger.LogError("[SePay] Provider token response did not contain an access token.");
            throw new InvalidOperationException(
                "SePay token response did not contain an access token.");
        }

        _logger.LogInformation("[SePay] Provider access token acquired.");
        return tokenRes.AccessToken;
    }

    public async Task<string> GetOrCreateCompanyXidAsync(Guid businessId, string businessName)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business == null)
        {
            throw new Exception("Business profile not found.");
        }

        if (!string.IsNullOrEmpty(business.SePayCompanyXid))
        {
            _logger.LogInformation(
                "[SePay] Reusing provider company for BusinessId={BusinessId}",
                businessId);
            return business.SePayCompanyXid;
        }

        var accessToken = await GetAccessTokenAsync();
        var client = CreateSePayClient();

        var body = new { full_name = businessName, status = "Active" };
        var bodyJson = JsonSerializer.Serialize(body);
        _logger.LogInformation("[SePay] Creating provider company.");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/company/create");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation(
            "[SePay] /v1/company/create completed. Status={Status}",
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"SePay company creation failed with status {(int)response.StatusCode}.");
        }

        // Use case-insensitive options so "xid", "full_name" etc. are mapped correctly
        var companyRes = JsonSerializer.Deserialize<SePayCompanyResponse>(responseContent, _jsonOptions);

        if (companyRes == null || companyRes.Data == null || string.IsNullOrEmpty(companyRes.Data.Xid))
        {
            _logger.LogError("[SePay] Provider company response did not contain an identity.");
            throw new InvalidOperationException(
                "SePay company response did not contain an identity.");
        }

        var companyXid = companyRes.Data.Xid;
        _logger.LogInformation("[SePay] Provider company created.");

        // Cập nhật cấu hình transaction_amount = Unlimited để nhận được Webhook IPN
        try
        {
            var editBody = new { transaction_amount = "Unlimited" };
            var editBodyJson = JsonSerializer.Serialize(editBody);
            _logger.LogInformation("[SePay] Updating provider company settings.");

            var editRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/company/edit/{companyXid}");
            editRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            editRequest.Content = new StringContent(editBodyJson, Encoding.UTF8, "application/json");

            var editResponse = await client.SendAsync(editRequest);
            var editResponseContent = await editResponse.Content.ReadAsStringAsync();

            _logger.LogInformation(
                "[SePay] Provider company settings update completed. Status={Status}",
                (int)editResponse.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SePay] Failed to update provider company settings.");
        }

        business.SePayCompanyXid = companyXid;
        _businessProfiles.Update(business);
        await _unitOfWork.SaveChangesAsync();

        return companyXid;
    }

    public async Task<(string Url, string LinkTokenXid)> GenerateHostedLinkUrlAsync(
        string companyXid, string redirectUri, string purpose = "LINK_BANK_ACCOUNT", string? bankAccountXid = null,
        bool isMobileApp = true)
    {
        ValidateHttpsUrl(redirectUri, nameof(redirectUri));
        _logger.LogInformation(
            "[SePay] Generating hosted link. Purpose={Purpose}",
            purpose);

        if (string.IsNullOrEmpty(companyXid))
        {
            throw new ArgumentException(
                "Provider company identity is required.",
                nameof(companyXid));
        }

        var accessToken = await GetAccessTokenAsync();
        var client = CreateSePayClient();

        // Xây dựng request body động dựa vào mục đích (link/unlink)
        var body = new Dictionary<string, object>
        {
            { "company_xid", companyXid },
            { "purpose", purpose },
            { "completion_redirect_uri", redirectUri },
            { "is_mobile_app", isMobileApp ? 1 : 0 },
            { "language", "vi" }
        };

        if (!string.IsNullOrEmpty(bankAccountXid))
        {
            body.Add("bank_account_xid", bankAccountXid);
        }

        var bodyJson = JsonSerializer.Serialize(body);


        _logger.LogInformation("[SePay] Creating provider link token.");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/link-token/create");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation(
            "[SePay] /v1/link-token/create completed. Status={Status}",
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"SePay link-token creation failed with status {(int)response.StatusCode}.");
        }

        // Response: { xid, hosted_link_url, link_token, expires_at }
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        string? hostedLinkUrl = null;
        if (root.TryGetProperty("hosted_link_url", out var urlProp))
            hostedLinkUrl = urlProp.GetString();

        string? linkTokenXid = null;
        if (root.TryGetProperty("xid", out var xidProp))
            linkTokenXid = xidProp.GetString();

        if (string.IsNullOrEmpty(hostedLinkUrl))
        {
            throw new InvalidOperationException(
                "SePay link-token response did not contain a hosted link URL.");
        }

        ValidateHttpsUrl(hostedLinkUrl, "hostedLinkUrl");

        return (hostedLinkUrl, linkTokenXid ?? "");
    }

    public async Task<List<SePayBankAccountDto>> GetLinkedBankAccountsAsync(string? companyXid = null)
    {
        _logger.LogInformation(
            "[SePay] Fetching linked bank accounts. Scoped={Scoped}",
            !string.IsNullOrEmpty(companyXid));

        var accessToken = await GetAccessTokenAsync();
        var client = CreateSePayClient();

        var url = string.IsNullOrEmpty(companyXid)
            ? $"{_baseUrl}/v1/bank-account?per_page=100"
            : $"{_baseUrl}/v1/bank-account?company_xid={Uri.EscapeDataString(companyXid)}&per_page=100";
            
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation(
            "[SePay] GET /v1/bank-account completed. Status={Status}",
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "[SePay] GetLinkedBankAccounts failed. Status={Status}",
                (int)response.StatusCode);
            return new List<SePayBankAccountDto>();
        }

        var result = JsonSerializer.Deserialize<SePayBankAccountListResponse>(responseContent, _jsonOptions);
        return result?.Data ?? new List<SePayBankAccountDto>();
    }

    public async Task<SePayBankAccountDto?> GetBankAccountDetailAsync(string bankAccountXid)
    {
        if (string.IsNullOrEmpty(bankAccountXid)) return null;

        _logger.LogInformation("[SePay] Fetching bank account detail.");

        var accessToken = await GetAccessTokenAsync();
        var client = CreateSePayClient();

        var url = $"{_baseUrl}/v1/bank-account/{Uri.EscapeDataString(bankAccountXid)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation(
            "[SePay] GET /v1/bank-account detail completed. Status={Status}",
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "[SePay] GetBankAccountDetail failed. Status={Status}",
                (int)response.StatusCode);
            return null;
        }

        using var doc = JsonDocument.Parse(responseContent);
        if (doc.RootElement.TryGetProperty("data", out var dataProp))
        {
            return JsonSerializer.Deserialize<SePayBankAccountDto>(dataProp.GetRawText(), _jsonOptions);
        }

        return null;
    }

    public async Task<string> GetSePayConnectUrlAsync(
        Guid businessId,
        bool isMobileApp = true)
    {
        var callbackUrl = GetRequiredHttpsUrl("SePay:BankHub:CallbackUrl");
        var webhookUrl = GetRequiredHttpsUrl("SePay:BankHub:WebhookUrl");
        var webhookSecret = GetRequiredConfiguration("SePay:BankHub:SecretKey");
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business == null)
        {
            throw new KeyNotFoundException("Business profile not found.");
        }

        var companyXid = await GetOrCreateCompanyXidAsync(businessId, business.BusinessName);

        // Tạo link token và lấy cả URL lẫn linkTokenXid
        var (url, linkTokenXid) = await GenerateHostedLinkUrlAsync(
            companyXid,
            callbackUrl,
            isMobileApp: isMobileApp);

        // Lưu linkTokenXid vào BusinessProfile để sau này trace BANK_ACCOUNT_LINKED webhook
        if (!string.IsNullOrEmpty(linkTokenXid))
        {
            business.LastSePayLinkTokenXid = linkTokenXid;
            _businessProfiles.Update(business);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation(
                "[SePay] Saved link correlation for BusinessId={BusinessId}",
                businessId);
        }

        await RegisterWebhookAsync(webhookUrl, webhookSecret);

        return url;
    }

    /// <summary>

    /// Đăng ký Webhook URL với SePay Bank Hub qua POST /v1/webhook.
    /// SePay sẽ gửi các sự kiện (BANK_ACCOUNT_LINKED, ...) về URL này.
    /// </summary>
    public async Task RegisterWebhookAsync(string webhookUrl, string secretKey)
    {
        if (string.IsNullOrEmpty(webhookUrl))
            throw new ArgumentException("webhookUrl is required.");

        var accessToken = await GetAccessTokenAsync();
        var client = CreateSePayClient();

        var body = new
        {
            webhook_url = webhookUrl,
            auth_type = "SECRET_KEY",
            secret_key = secretKey,
            active = 1,
            allow_events = new[] { "*" }
        };
        var bodyJson = JsonSerializer.Serialize(body);



        _logger.LogInformation("[SePay] RegisterWebhook → POST /v1/webhook, url={Url}", webhookUrl);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/webhook");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[SePay] POST /v1/webhook → Status={Status}, Body={Body}",
            (int)response.StatusCode, responseContent);

        if (!response.IsSuccessStatusCode)
        {
            // Log cảnh báo nhưng không throw — webhook registration thất bại không nên chặn luồng link ngân hàng
            _logger.LogWarning("[SePay] RegisterWebhook failed ({Status}): {Body}",
                (int)response.StatusCode, responseContent);
        }
    }

    /// <summary>
    /// Giả lập một giao dịch chuyển khoản mới ở Sandbox (để test webhook IPN).
    /// </summary>
    public async Task CreateMockTransactionAsync(string bankAccountXid, decimal amount, string content)
    {
        if (string.IsNullOrEmpty(bankAccountXid))
            throw new ArgumentException("bankAccountXid is required.");

        var accessToken = await GetAccessTokenAsync();
        var client = CreateSePayClient();

        var body = new
        {
            bank_account_xid = bankAccountXid,
            transfer_type = "credit",
            amount = amount,
            transaction_content = content
        };
        var bodyJson = JsonSerializer.Serialize(body);

        _logger.LogInformation("[SePay Sandbox] CreateMockTransaction → POST /v1/transaction/create, account={AccXid}, amount={Amount}",
            bankAccountXid, amount);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/transaction/create");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[SePay Sandbox] POST /v1/transaction/create → Status={Status}, Body={Body}",
            (int)response.StatusCode, responseContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to create mock transaction ({(int)response.StatusCode}): {responseContent}");
        }
    }

    private string GetRequiredConfiguration(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required configuration: {key}.");

        return value.Trim();
    }

    private string GetRequiredHttpsUrl(string key)
    {
        var value = GetRequiredConfiguration(key);
        ValidateHttpsUrl(value, key);
        return value;
    }

    private static void ValidateHttpsUrl(string value, string parameterName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("URL must be an absolute HTTPS URL.", parameterName);
        }
    }
}

