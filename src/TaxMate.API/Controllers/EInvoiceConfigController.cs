using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Quản lý cấu hình kết nối Hóa đơn điện tử (HĐĐT) của cửa hàng qua SePay.</summary>
[ApiController]
[Route("api/[controller]")]
public class EInvoiceConfigController : ControllerBase
{
    private readonly IGenericRepository<EInvoiceConfig> _configs;
    private readonly IGenericRepository<BusinessProfile> _businesses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEInvoiceService _eInvoiceService;

    public EInvoiceConfigController(
        IGenericRepository<EInvoiceConfig> configs,
        IGenericRepository<BusinessProfile> businesses,
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        IEInvoiceService eInvoiceService)
    {
        _configs = configs;
        _businesses = businesses;
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _eInvoiceService = eInvoiceService;
    }

    /// <summary>Lấy thông tin cấu hình HĐĐT của cửa hàng.</summary>
    /// <param name="businessId">ID cửa hàng.</param>
    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetByBusiness(Guid businessId)
    {
        var config = await _configs.FirstOrDefaultAsync(x => x.BusinessId == businessId);
        if (config == null)
        {
            // Trả về HTTP 200 OK với data = null để Frontend không bị lỗi đỏ 404 Console
            return Ok(ApiResponse<EInvoiceConfigResponse?>.Ok(
                null, 
                "No E-Invoice configuration found for this business.", 
                HttpContext.TraceIdentifier
            ));
        }

        var response = new EInvoiceConfigResponse
        {
            BusinessId = config.BusinessId,
            Provider = config.Provider,
            BaseUrl = config.BaseUrl,
            ClientId = config.ClientId,
            ProviderAccountId = config.ProviderAccountId,
            InvoiceTemplateCode = config.InvoiceTemplateCode,
            Symbol = config.Symbol,
            IsEnabled = config.IsEnabled,
            QuotaWarningThreshold = config.QuotaWarningThreshold
        };

        return Ok(ApiResponse<EInvoiceConfigResponse>.Ok(response, "Get configuration successfully", HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy hạn ngạch (quota) còn lại từ SePay.</summary>
    [HttpGet("business/{businessId:guid}/quota")]
    public async Task<IActionResult> GetQuota(Guid businessId)
    {
        var config = await _configs.FirstOrDefaultAsync(x => x.BusinessId == businessId);
        if (config == null)
        {
            return NotFound(ApiResponse<string>.Fail("Chưa cấu hình HĐĐT cho cửa hàng này.", HttpContext.TraceIdentifier));
        }

        var quota = await _eInvoiceService.GetQuotaRemainingAsync(config);
        if (quota == null)
        {
            // Trả về HTTP 200 kèm data = null để Frontend không bị báo lỗi đỏ console khi key sai hoặc hết hạn
            return Ok(ApiResponse<int?>.Ok(null, "Không thể lấy hạn ngạch từ SePay. Vui lòng kiểm tra lại kết nối.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<int?>.Ok(quota.Value, "Tải hạn ngạch thành công.", HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy danh sách nhà cung cấp và mẫu số dựa trên cấu hình đã lưu trong DB.</summary>
    [HttpGet("business/{businessId:guid}/saved-providers-and-templates")]
    public async Task<IActionResult> GetSavedProvidersAndTemplates(Guid businessId)
    {
        var config = await _configs.FirstOrDefaultAsync(x => x.BusinessId == businessId);
        if (config == null)
        {
            return NotFound(ApiResponse<string>.Fail("Chưa cấu hình HĐĐT cho cửa hàng này.", HttpContext.TraceIdentifier));
        }

        try
        {
            var token = await FetchTokenAsync(config.BaseUrl, config.ClientId, config.ClientSecret);
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(ApiResponse<string>.Fail("Không thể xác thực SePay bằng cấu hình hiện tại.", HttpContext.TraceIdentifier));
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Tải danh sách active providers
            var providersUrl = $"{config.BaseUrl.TrimEnd('/')}/v1/provider-accounts?per_page=100";
            var providersResponse = await client.GetAsync(providersUrl);
            List<SePayProviderItem> activeProviders = new();
            if (providersResponse.IsSuccessStatusCode)
            {
                var content = await providersResponse.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SePayProvidersResponse>(content);
                if (result?.Data?.Items != null)
                {
                    activeProviders = result.Data.Items.Where(p => p.Active).ToList();
                }
            }

            // 2. Tải danh sách templates nếu đã lưu ProviderAccountId
            List<SePayTemplateItem> templates = new();
            if (!string.IsNullOrEmpty(config.ProviderAccountId))
            {
                var templatesUrl = $"{config.BaseUrl.TrimEnd('/')}/v1/provider-accounts/{config.ProviderAccountId}?per_page=100";
                var templatesResponse = await client.GetAsync(templatesUrl);
                if (templatesResponse.IsSuccessStatusCode)
                {
                    var content = await templatesResponse.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<SePayProviderDetailResponse>(content);
                    if (result?.Data?.Templates != null)
                    {
                        templates = result.Data.Templates;
                    }
                }
            }

            return Ok(ApiResponse<SavedProvidersAndTemplatesResponse>.Ok(new SavedProvidersAndTemplatesResponse
            {
                Providers = activeProviders,
                Templates = templates
            }, "Tải cấu hình đã lưu thành công.", HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<string>.Fail($"Lỗi hệ thống: {ex.Message}", HttpContext.TraceIdentifier));
        }
    }

    /// <summary>Lưu hoặc cập nhật cấu hình HĐĐT của cửa hàng.</summary>
    /// <param name="businessId">ID cửa hàng.</param>
    /// <param name="request">Thông tin cấu hình.</param>
    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Save(Guid businessId, [FromBody] SaveEInvoiceConfigRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var business = await _businesses.GetByIdAsync(businessId);
        if (business == null)
        {
            return NotFound(new ApiResponse<string>
            {
                Success = false,
                Message = "Business profile not found.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var config = await _configs.FirstOrDefaultAsync(x => x.BusinessId == businessId);
        var isNew = config == null;

        if (config == null)
        {
            config = new EInvoiceConfig
            {
                BusinessId = businessId,
                CreatedAt = DateTime.UtcNow
            };
        }

        config.Provider = request.Provider;
        config.BaseUrl = request.BaseUrl;
        config.ClientId = request.ClientId;
        
        if (isNew && string.IsNullOrEmpty(request.ClientSecret))
        {
            return BadRequest(ApiResponse<string>.Fail("Client Secret là bắt buộc khi tạo cấu hình mới.", HttpContext.TraceIdentifier));
        }

        if (!string.IsNullOrEmpty(request.ClientSecret))
        {
            config.ClientSecret = request.ClientSecret;
        }
        config.ProviderAccountId = request.ProviderAccountId;
        config.InvoiceTemplateCode = request.InvoiceTemplateCode;
        config.Symbol = request.Symbol;
        config.IsEnabled = request.IsEnabled;
        config.QuotaWarningThreshold = request.QuotaWarningThreshold;
        config.UpdatedAt = DateTime.UtcNow;

        if (isNew)
        {
            await _configs.AddAsync(config);
        }
        else
        {
            _configs.Update(config);
        }

        // Đồng bộ trạng thái PreferElectronicInvoice trên BusinessProfile
        business.PreferElectronicInvoice = request.IsEnabled;
        _businesses.Update(business);

        await _unitOfWork.SaveChangesAsync();

        var response = new EInvoiceConfigResponse
        {
            BusinessId = config.BusinessId,
            Provider = config.Provider,
            BaseUrl = config.BaseUrl,
            ClientId = config.ClientId,
            ProviderAccountId = config.ProviderAccountId,
            InvoiceTemplateCode = config.InvoiceTemplateCode,
            Symbol = config.Symbol,
            IsEnabled = config.IsEnabled,
            QuotaWarningThreshold = config.QuotaWarningThreshold
        };

        return Ok(ApiResponse<EInvoiceConfigResponse>.Ok(response, "E-Invoice configuration saved successfully.", HttpContext.TraceIdentifier));
    }

    /// <summary>Kiểm tra kết nối tới SePay và lấy danh sách tài khoản nhà cung cấp.</summary>
    [HttpPost("test-connection-and-get-providers")]
    public async Task<IActionResult> TestConnectionAndGetProviders([FromBody] TestConnectionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var token = await FetchTokenAsync(request.BaseUrl, request.ClientId, request.ClientSecret);
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(ApiResponse<string>.Fail("Không thể kết nối SePay. Vui lòng kiểm tra lại Client ID hoặc Client Secret.", HttpContext.TraceIdentifier));
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"{request.BaseUrl.TrimEnd('/')}/v1/provider-accounts?per_page=100";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return BadRequest(ApiResponse<string>.Fail($"SePay API báo lỗi: HTTP {response.StatusCode} - {error}", HttpContext.TraceIdentifier));
            }

            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[SePay DEBUG] Raw Provider Accounts: {content}");

            var result = JsonSerializer.Deserialize<SePayProvidersResponse>(content);
            if (result == null || result.Data == null)
            {
                return BadRequest(ApiResponse<string>.Fail($"Không nhận được dữ liệu hợp lệ từ SePay. Raw: {content}", HttpContext.TraceIdentifier));
            }

            // Chỉ trả về các tài khoản active
            var activeProviders = result.Data.Items.Where(p => p.Active).ToList();
            return Ok(ApiResponse<List<SePayProviderItem>>.Ok(activeProviders, "Kết nối thành công và tải danh sách nhà cung cấp.", HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<string>.Fail($"Lỗi hệ thống: {ex.Message}", HttpContext.TraceIdentifier));
        }
    }

    /// <summary>Lấy danh sách các mẫu số hóa đơn và ký hiệu được cấp phép của một tài khoản nhà cung cấp.</summary>
    [HttpPost("get-templates")]
    public async Task<IActionResult> GetTemplates([FromBody] GetTemplatesRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var token = await FetchTokenAsync(request.BaseUrl, request.ClientId, request.ClientSecret);
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(ApiResponse<string>.Fail("Không thể xác thực SePay.", HttpContext.TraceIdentifier));
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"{request.BaseUrl.TrimEnd('/')}/v1/provider-accounts/{request.ProviderAccountId}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return BadRequest(ApiResponse<string>.Fail($"SePay API báo lỗi: HTTP {response.StatusCode} - {error}", HttpContext.TraceIdentifier));
            }

            var result = await response.Content.ReadFromJsonAsync<SePayProviderDetailResponse>();
            if (result == null || result.Data == null)
            {
                return BadRequest(ApiResponse<string>.Fail("Không nhận được cấu hình mẫu hóa đơn từ SePay.", HttpContext.TraceIdentifier));
            }

            return Ok(ApiResponse<List<SePayTemplateItem>>.Ok(result.Data.Templates, "Tải danh sách mẫu hóa đơn thành công.", HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<string>.Fail($"Lỗi hệ thống: {ex.Message}", HttpContext.TraceIdentifier));
        }
    }

    private async Task<string?> FetchTokenAsync(string baseUrl, string clientId, string clientSecret)
    {
        var client = _httpClientFactory.CreateClient();
        var authBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
        var authHeader = Convert.ToBase64String(authBytes);

        var url = $"{baseUrl.TrimEnd('/')}/v1/token";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<SePayTokenResponse>();
        return result?.Success == true ? result.Data?.AccessToken : null;
    }

    // --- SePay Controller Models ---

    private class SePayTokenResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("data")]
        public SePayTokenData? Data { get; set; }
    }

    private class SePayTokenData
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = null!;
    }

    private class SePayProvidersResponse
    {
        [JsonPropertyName("data")]
        public SePayProvidersData? Data { get; set; }
    }

    private class SePayProvidersData
    {
        [JsonPropertyName("items")]
        public List<SePayProviderItem> Items { get; set; } = new();
    }

    public class SePayProviderItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = null!;
        [JsonPropertyName("active")]
        public bool Active { get; set; }
        [JsonPropertyName("tax_authority_approved_date")]
        public string? TaxAuthorityApprovedDate { get; set; }
    }

    private class SePayProviderDetailResponse
    {
        [JsonPropertyName("data")]
        public SePayProviderDetailData? Data { get; set; }
    }

    private class SePayProviderDetailData
    {
        [JsonPropertyName("templates")]
        public List<SePayTemplateItem> Templates { get; set; } = new();
    }

    public class SePayTemplateItem
    {
        [JsonPropertyName("template_code")]
        public string TemplateCode { get; set; } = null!;
        [JsonPropertyName("invoice_series")]
        public string InvoiceSeries { get; set; } = null!;
        [JsonPropertyName("invoice_label")]
        public string InvoiceLabel { get; set; } = null!;
    }
}

// ================= DTOs =================
public class SaveEInvoiceConfigRequest
{
    public string Provider { get; set; } = "SePay";
    public string BaseUrl { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string? ClientSecret { get; set; }
    public string? ProviderAccountId { get; set; }
    public string? InvoiceTemplateCode { get; set; }
    public string? Symbol { get; set; }
    public bool IsEnabled { get; set; }
    public int QuotaWarningThreshold { get; set; } = 100;
}

public class EInvoiceConfigResponse
{
    public Guid BusinessId { get; set; }
    public string Provider { get; set; } = "SePay";
    public string BaseUrl { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string? ProviderAccountId { get; set; }
    public string? InvoiceTemplateCode { get; set; }
    public string? Symbol { get; set; }
    public bool IsEnabled { get; set; }
    public int QuotaWarningThreshold { get; set; }
}

public class TestConnectionRequest
{
    public string BaseUrl { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
}

public class GetTemplatesRequest
{
    public string BaseUrl { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string ProviderAccountId { get; set; } = null!;
}

public class SavedProvidersAndTemplatesResponse
{
    public List<EInvoiceConfigController.SePayProviderItem> Providers { get; set; } = new();
    public List<EInvoiceConfigController.SePayTemplateItem> Templates { get; set; } = new();
}
