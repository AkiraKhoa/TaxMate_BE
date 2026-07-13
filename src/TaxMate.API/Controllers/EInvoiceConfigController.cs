using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Quản lý cấu hình kết nối Hóa đơn điện tử (HĐĐT) của cửa hàng.</summary>
[ApiController]
[Route("api/[controller]")]
public class EInvoiceConfigController : ControllerBase
{
    private readonly IGenericRepository<EInvoiceConfig> _configs;
    private readonly IGenericRepository<BusinessProfile> _businesses;
    private readonly IUnitOfWork _unitOfWork;

    public EInvoiceConfigController(
        IGenericRepository<EInvoiceConfig> configs,
        IGenericRepository<BusinessProfile> businesses,
        IUnitOfWork unitOfWork)
    {
        _configs = configs;
        _businesses = businesses;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Lấy thông tin cấu hình HĐĐT của cửa hàng.</summary>
    /// <param name="businessId">ID cửa hàng.</param>
    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetByBusiness(Guid businessId)
    {
        var config = await _configs.FirstOrDefaultAsync(x => x.BusinessId == businessId);
        if (config == null)
        {
            return NotFound(new ApiResponse<string>
            {
                Success = false,
                Message = "No E-Invoice configuration found for this business.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var response = new EInvoiceConfigResponse
        {
            BusinessId = config.BusinessId,
            Provider = config.Provider,
            ApiUrl = config.ApiUrl,
            ApiKey = config.ApiKey,
            Username = config.Username,
            InvoiceTemplateCode = config.InvoiceTemplateCode,
            Symbol = config.Symbol,
            IsEnabled = config.IsEnabled
        };

        return Ok(ApiResponse<EInvoiceConfigResponse>.Ok(response, "Get configuration successfully", HttpContext.TraceIdentifier));
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
        config.ApiUrl = request.ApiUrl;
        config.ApiKey = request.ApiKey;
        config.Username = request.Username;
        if (!string.IsNullOrEmpty(request.Password))
        {
            config.Password = request.Password; // Chỉ cập nhật password nếu người dùng truyền lên
        }
        config.InvoiceTemplateCode = request.InvoiceTemplateCode;
        config.Symbol = request.Symbol;
        config.IsEnabled = request.IsEnabled;
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
            ApiUrl = config.ApiUrl,
            ApiKey = config.ApiKey,
            Username = config.Username,
            InvoiceTemplateCode = config.InvoiceTemplateCode,
            Symbol = config.Symbol,
            IsEnabled = config.IsEnabled
        };

        return Ok(ApiResponse<EInvoiceConfigResponse>.Ok(response, "E-Invoice configuration saved successfully.", HttpContext.TraceIdentifier));
    }
}

// ================= DTOs =================
public class SaveEInvoiceConfigRequest
{
    public string Provider { get; set; } = null!; // "VNPT", "FPT", "MISA", "Mock"
    public string ApiUrl { get; set; } = null!;
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? InvoiceTemplateCode { get; set; }
    public string? Symbol { get; set; }
    public bool IsEnabled { get; set; }
}

public class EInvoiceConfigResponse
{
    public Guid BusinessId { get; set; }
    public string Provider { get; set; } = null!;
    public string ApiUrl { get; set; } = null!;
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? InvoiceTemplateCode { get; set; }
    public string? Symbol { get; set; }
    public bool IsEnabled { get; set; }
}
