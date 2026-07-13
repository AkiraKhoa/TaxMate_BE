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
        _baseUrl = _configuration["SePay:BaseUrl"] ?? "https://bankhub-api-sandbox.sepay.vn";
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
        var clientId = _configuration["SePay:ClientId"] ?? "";
        var clientSecret = _configuration["SePay:ClientSecret"] ?? "";
        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        _logger.LogInformation("[SePay] Getting token for ClientId={ClientId}", clientId);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        // Per SePay docs: empty body is fine (no body required)
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>());

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[SePay] /v1/token → Status={Status}, Body={Body}",
            (int)response.StatusCode, responseContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"SePay token request failed ({(int)response.StatusCode}): {responseContent}");
        }

        // Use case-insensitive options to handle snake_case "access_token"
        var tokenRes = JsonSerializer.Deserialize<SePayTokenResponse>(responseContent, _jsonOptions);

        if (tokenRes == null || string.IsNullOrEmpty(tokenRes.AccessToken))
        {
            _logger.LogError("[SePay] AccessToken is null after deserialization. Raw body: {Body}", responseContent);
            throw new Exception($"Failed to get SePay access token. Raw response: {responseContent}");
        }

        _logger.LogInformation("[SePay] Got AccessToken (first 10 chars): {Token}",
            tokenRes.AccessToken[..Math.Min(10, tokenRes.AccessToken.Length)]);
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
            _logger.LogInformation("[SePay] Using existing CompanyXid={Xid} for BusinessId={Id}",
                business.SePayCompanyXid, businessId);
            return business.SePayCompanyXid;
        }

        var accessToken = await GetAccessTokenAsync();
        var client = CreateSePayClient();

        var body = new { full_name = businessName, status = "Active" };
        var bodyJson = JsonSerializer.Serialize(body);
        _logger.LogInformation("[SePay] POST /v1/company/create → Body={Body}", bodyJson);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/company/create");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[SePay] /v1/company/create → Status={Status}, Body={Body}",
            (int)response.StatusCode, responseContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"SePay company/create failed ({(int)response.StatusCode}): {responseContent}");
        }

        // Use case-insensitive options so "xid", "full_name" etc. are mapped correctly
        var companyRes = JsonSerializer.Deserialize<SePayCompanyResponse>(responseContent, _jsonOptions);

        _logger.LogInformation("[SePay] Deserialized company: Code={Code}, Data={Data}, Xid={Xid}",
            companyRes?.Code,
            companyRes?.Data != null ? "not null" : "NULL",
            companyRes?.Data?.Xid ?? "NULL");

        if (companyRes == null || companyRes.Data == null || string.IsNullOrEmpty(companyRes.Data.Xid))
        {
            _logger.LogError("[SePay] CompanyXid is null after deserialization. Raw: {Body}", responseContent);
            throw new Exception($"Failed to create SePay company. Raw response: {responseContent}");
        }

        var companyXid = companyRes.Data.Xid;
        _logger.LogInformation("[SePay] Created company with Xid={Xid}", companyXid);

        // Cập nhật cấu hình transaction_amount = Unlimited để nhận được Webhook IPN
        try
        {
            var editBody = new { transaction_amount = "Unlimited" };
            var editBodyJson = JsonSerializer.Serialize(editBody);
            _logger.LogInformation("[SePay] POST /v1/company/edit/{Xid} → Body={Body}", companyXid, editBodyJson);

            var editRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/company/edit/{companyXid}");
            editRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            editRequest.Content = new StringContent(editBodyJson, Encoding.UTF8, "application/json");

            var editResponse = await client.SendAsync(editRequest);
            var editResponseContent = await editResponse.Content.ReadAsStringAsync();

            _logger.LogInformation("[SePay] /v1/company/edit/{Xid} → Status={Status}, Body={Body}",
                companyXid, (int)editResponse.StatusCode, editResponseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SePay] Failed to set transaction_amount to Unlimited for company {Xid}", companyXid);
        }

        business.SePayCompanyXid = companyXid;
        _businessProfiles.Update(business);
        await _unitOfWork.SaveChangesAsync();

        return companyXid;
    }

    public async Task<(string Url, string LinkTokenXid)> GenerateHostedLinkUrlAsync(
        string companyXid, string redirectUri, string purpose = "LINK_BANK_ACCOUNT", string? bankAccountXid = null)
    {
        _logger.LogInformation("[SePay] GenerateHostedLinkUrl called with CompanyXid={Xid}, purpose={Purpose}, bankAccountXid={BankXid}",
            companyXid, purpose, bankAccountXid);

        if (string.IsNullOrEmpty(companyXid))
        {
            throw new Exception("company_xid is null or empty — cannot create link token.");
        }

        var accessToken = await GetAccessTokenAsync();
        var client = CreateSePayClient();

        // Xây dựng request body động dựa vào mục đích (link/unlink)
        var body = new Dictionary<string, object>
        {
            { "company_xid", companyXid },
            { "purpose", purpose },
            { "completion_redirect_uri", redirectUri },
            { "is_mobile_app", 1 },
            { "language", "vi" }
        };

        if (!string.IsNullOrEmpty(bankAccountXid))
        {
            body.Add("bank_account_xid", bankAccountXid);
        }

        var bodyJson = JsonSerializer.Serialize(body);


        _logger.LogInformation("[SePay] POST /v1/link-token/create → Body={Body}", bodyJson);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/link-token/create");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[SePay] /v1/link-token/create → Status={Status}, Body={Body}",
            (int)response.StatusCode, responseContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"SePay link-token/create failed ({(int)response.StatusCode}): {responseContent}");
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

        _logger.LogInformation("[SePay] hosted_link_url={Url}, linkTokenXid={Xid}", hostedLinkUrl ?? "NULL", linkTokenXid ?? "NULL");

        if (string.IsNullOrEmpty(hostedLinkUrl))
        {
            throw new Exception($"hosted_link_url is null in SePay response: {responseContent}");
        }

        return (hostedLinkUrl, linkTokenXid ?? "");
    }

    public async Task<List<SePayBankAccountDto>> GetLinkedBankAccountsAsync(string companyXid)
    {
        _logger.LogInformation("[SePay] GetLinkedBankAccounts for CompanyXid={Xid}", companyXid);

        var accessToken = await GetAccessTokenAsync();
        var client = CreateSePayClient();

        var url = $"{_baseUrl}/v1/bank-account?company_xid={Uri.EscapeDataString(companyXid)}&per_page=100";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[SePay] GET /v1/bank-account → Status={Status}, Body={Body}",
            (int)response.StatusCode, responseContent);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[SePay] GetLinkedBankAccounts failed ({Status}): {Body}",
                (int)response.StatusCode, responseContent);
            return new List<SePayBankAccountDto>();
        }

        var result = JsonSerializer.Deserialize<SePayBankAccountListResponse>(responseContent, _jsonOptions);
        return result?.Data ?? new List<SePayBankAccountDto>();
    }

    public async Task<string> GetSePayConnectUrlAsync(Guid businessId, string scheme, string host)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business == null)
        {
            throw new KeyNotFoundException("Business profile not found.");
        }

        var companyXid = await GetOrCreateCompanyXidAsync(businessId, business.BusinessName);

        var redirectUri = $"{scheme}://{host}/api/PaymentAccount/sepay-callback";

        // Cloudflare WAF blocks JSON bodies containing "localhost" or "127.0.0.1" in URLs to prevent SSRF.
        // We use a public dummy domain for local development. The mobile app intercepts this URL via WebView anyway.
        if (host.Contains("localhost") || host.Contains("127.0.0.1"))
        {
            redirectUri = "https://taxmate.vn/api/PaymentAccount/sepay-callback";
        }

        // Tạo link token và lấy cả URL lẫn linkTokenXid
        var (url, linkTokenXid) = await GenerateHostedLinkUrlAsync(companyXid, redirectUri);

        // Lưu linkTokenXid vào BusinessProfile để sau này trace BANK_ACCOUNT_LINKED webhook
        if (!string.IsNullOrEmpty(linkTokenXid))
        {
            business.LastSePayLinkTokenXid = linkTokenXid;
            _businessProfiles.Update(business);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("[SePay] Saved LinkTokenXid={Xid} for BusinessId={BusinessId}", linkTokenXid, businessId);
        }

        // Đăng ký Webhook URL với SePay Bank Hub
        var webhookBaseUrl = _configuration["SePay:BankHub:WebhookUrl"];
        if (string.IsNullOrEmpty(webhookBaseUrl))
        {
            webhookBaseUrl = $"{scheme}://{host}";
        }
        var webhookUrl = $"{webhookBaseUrl}/api/webhook/payment/bankhub";
        var secretKey = _configuration["SePay:BankHub:SecretKey"] ?? "";


        // Fire-and-forget: thất bại đăng ký webhook không chặn việc trả URL cho mobile
        _ = RegisterWebhookAsync(webhookUrl, secretKey)
            .ContinueWith(t => _logger.LogWarning(t.Exception, "[SePay] RegisterWebhook error"),
                TaskContinuationOptions.OnlyOnFaulted);

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
}

