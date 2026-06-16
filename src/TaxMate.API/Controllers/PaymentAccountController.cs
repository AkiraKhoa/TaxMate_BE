using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Quản lý tài khoản ngân hàng nhận thanh toán.</summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentAccountController : ControllerBase
{
    private readonly IPaymentAccountService _paymentAccountService;

    public PaymentAccountController(IPaymentAccountService paymentAccountService)
    {
        _paymentAccountService = paymentAccountService;
    }

    /// <summary>Tạo tài khoản thanh toán mới.</summary>
    /// <param name="businessId">ID cửa hàng. Chạy SeedTestData để lấy ID thật.</param>
    /// <param name="request">Thông tin tài khoản ngân hàng.</param>
    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(Guid businessId, [FromBody] CreatePaymentAccountRequest request)
    {
        var id = await _paymentAccountService.CreateAsync(businessId, request);
        return Created(
            $"api/PaymentAccount/{id}",
            ApiResponse<Guid>.Ok(
                id,
                "Payment account created successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Danh sách tài khoản theo cửa hàng.</summary>
    /// <param name="businessId">ID cửa hàng.</param>
    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetByBusiness(Guid businessId)
    {
        var result = await _paymentAccountService.GetByBusinessIdAsync(businessId);
        return Ok(
            ApiResponse<IEnumerable<PaymentAccountResponse>>.Ok(
                result,
                "Get payment accounts successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy chi tiết tài khoản.</summary>
    /// <param name="id">ID tài khoản thanh toán.</param>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _paymentAccountService.GetByIdAsync(id);
        return Ok(
            ApiResponse<PaymentAccountResponse>.Ok(
                result,
                "Get payment account successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Cập nhật tài khoản thanh toán.</summary>
    /// <param name="id">ID tài khoản thanh toán.</param>
    /// <param name="request">Thông tin cập nhật.</param>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePaymentAccountRequest request)
    {
        await _paymentAccountService.UpdateAsync(id, request);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Payment account updated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Xóa tài khoản thanh toán.</summary>
    /// <param name="id">ID tài khoản thanh toán.</param>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _paymentAccountService.DeleteAsync(id);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Payment account deleted successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Đặt tài khoản làm mặc định.</summary>
    /// <param name="id">ID tài khoản thanh toán.</param>
    /// <param name="businessId">ID cửa hàng sở hữu tài khoản.</param>
    [HttpPatch("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id, [FromQuery] Guid businessId)
    {
        await _paymentAccountService.SetDefaultAsync(businessId, id);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Default payment account set successfully",
                HttpContext.TraceIdentifier));
    }
}
