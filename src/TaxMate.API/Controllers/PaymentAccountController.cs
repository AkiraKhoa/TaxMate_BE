using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Quản lý tài khoản ngân hàng nhận thanh toán.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Owner)]
[Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
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
        var id = await _paymentAccountService.CreateAsync(
            GetUserId(),
            businessId,
            request);
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
    public async Task<IActionResult> GetByBusiness(
        Guid businessId,
        [FromQuery] bool includeInactive = false)
    {
        var result = await _paymentAccountService.GetByBusinessIdAsync(
            GetUserId(),
            businessId,
            includeInactive);
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
        var result = await _paymentAccountService.GetByIdAsync(GetUserId(), id);
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
        await _paymentAccountService.UpdateAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Payment account updated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Ngừng sử dụng tài khoản ngân hàng, không xóa lịch sử.</summary>
    /// <param name="id">ID tài khoản thanh toán.</param>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _paymentAccountService.DeactivateAsync(GetUserId(), id);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Payment account deactivated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Danh sách tài khoản tiền Cash và Bank cho luồng S2e.
    /// Endpoint bank-only phía trên được giữ tương thích cho màn Transfer hiện tại.
    /// </summary>
    [HttpGet("business/{businessId:guid}/money-accounts")]
    public async Task<IActionResult> GetAllMoneyAccounts(
        Guid businessId,
        [FromQuery] bool includeInactive = false)
    {
        var result = await _paymentAccountService.GetAllMoneyAccountsByBusinessIdAsync(
            GetUserId(),
            businessId,
            includeInactive);
        return Ok(ApiResponse<IEnumerable<PaymentAccountResponse>>.Ok(
            result,
            "Get money accounts successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy tài khoản Cash hệ thống của cửa hàng cho luồng S2e.</summary>
    [HttpGet("business/{businessId:guid}/cash")]
    public async Task<IActionResult> GetCash(Guid businessId)
    {
        var result = await _paymentAccountService.GetCashByBusinessIdAsync(
            GetUserId(),
            businessId);
        return Ok(ApiResponse<PaymentAccountResponse>.Ok(
            result,
            "Get cash account successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Kích hoạt lại tài khoản ngân hàng.</summary>
    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        await _paymentAccountService.ActivateAsync(GetUserId(), id);
        return Ok(ApiResponse<string>.Ok(
            "Success",
            "Payment account activated successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Xác nhận hoặc xóa số dư đầu kỳ S2e.</summary>
    [HttpPut("{id:guid}/initial-balance")]
    public async Task<IActionResult> UpdateInitialBalance(
        Guid id,
        [FromBody] UpdatePaymentAccountInitialBalanceRequest request)
    {
        await _paymentAccountService.UpdateInitialBalanceAsync(
            GetUserId(),
            id,
            request);
        return Ok(ApiResponse<string>.Ok(
            "Success",
            "Initial balance updated successfully",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Đặt tài khoản làm mặc định.</summary>
    /// <param name="id">ID tài khoản thanh toán.</param>
    /// <param name="businessId">ID cửa hàng sở hữu tài khoản.</param>
    [HttpPatch("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id, [FromQuery] Guid businessId)
    {
        await _paymentAccountService.SetDefaultAsync(GetUserId(), businessId, id);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Default payment account set successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy link WebView để liên kết tài khoản ngân hàng qua SePay Bank Hub.</summary>
    /// <param name="businessId">ID cửa hàng cần liên kết.</param>
    /// <param name="isMobileApp">True cho ứng dụng mobile; false cho web.</param>
    [HttpGet("sepay-connect-url")]
    public async Task<IActionResult> GetSePayConnectUrl(
        [FromQuery] Guid businessId,
        [FromQuery] bool isMobileApp = true)
    {
        var url = await _paymentAccountService.GetSePayConnectUrlAsync(
            GetUserId(),
            businessId,
            isMobileApp);

        return Ok(ApiResponse<string>.Ok(url, "Get SePay connect URL successfully", HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy link WebView để hủy liên kết tài khoản ngân hàng qua SePay Bank Hub.</summary>
    /// <param name="paymentAccountId">ID tài khoản ngân hàng cần hủy liên kết.</param>
    [HttpGet("sepay-disconnect-url")]
    public async Task<IActionResult> GetSePayDisconnectUrl([FromQuery] Guid paymentAccountId)
    {
        var url = await _paymentAccountService.GetSePayDisconnectUrlAsync(
            GetUserId(),
            paymentAccountId);

        return Ok(ApiResponse<string>.Ok(url, "Get SePay disconnect URL successfully", HttpContext.TraceIdentifier));
    }


    /// <summary>
    /// Đồng bộ tài khoản ngân hàng từ SePay Bank Hub về DB.
    /// App gọi endpoint này sau khi WebView báo FINISHED_BANK_ACCOUNT_LINK (không cần chờ webhook).
    /// </summary>
    [HttpPost("sepay-sync")]
    public async Task<IActionResult> SyncSePayAccounts([FromQuery] Guid businessId)
    {
        var (synced, total) = await _paymentAccountService.SyncSePayAccountsAsync(
            GetUserId(),
            businessId);

        return Ok(ApiResponse<object>.Ok(
            new { synced, total },
            $"Synced {synced}/{total} bank accounts from SePay.",
            HttpContext.TraceIdentifier));
    }


    /// <summary>
    /// Khôi phục toàn bộ các tài khoản ngân hàng và CompanyXID bị kẹt trên SePay Sandbox về lại DB local.
    /// Dành cho Dev khi DB local bị reset/xóa nhưng SePay Sandbox vẫn giữ dữ liệu.
    /// </summary>
    [HttpPost("sepay-recover-all")]
    public async Task<IActionResult> RecoverAllSePayAccounts()
    {
        var (recovered, total) = await _paymentAccountService.RecoverAllFromSePayAsync(
            GetUserId());

        return Ok(ApiResponse<object>.Ok(
            new { recovered, total },
            $"Recovered {recovered}/{total} bank accounts directly from SePay Sandbox to local DB.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Callback xử lý sau khi liên kết ngân hàng thành công từ SePay.</summary>
    [HttpGet("sepay-callback")]
    [AllowAnonymous]
    public IActionResult SePayCallback()
    {
        var html = @"
            <!DOCTYPE html>
            <html lang='vi'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Liên kết thành công</title>
                <style>
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                        display: flex;
                        flex-direction: column;
                        align-items: center;
                        justify-content: center;
                        height: 100vh;
                        margin: 0;
                        background-color: #f4f7f6;
                        color: #333;
                    }
                    .container {
                        text-align: center;
                        padding: 30px;
                        background: white;
                        border-radius: 12px;
                        box-shadow: 0 4px 15px rgba(0,0,0,0.05);
                        max-width: 90%;
                        width: 320px;
                    }
                    .icon {
                        font-size: 60px;
                        color: #2ecc71;
                        margin-bottom: 20px;
                    }
                    h1 {
                        font-size: 22px;
                        margin-bottom: 10px;
                    }
                    p {
                        font-size: 14px;
                        color: #666;
                        line-height: 1.5;
                    }
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='icon'>✓</div>
                    <h1>Liên kết thành công!</h1>
                    <p>Tài khoản ngân hàng của bạn đang được đồng bộ với TaxMate.<br>Bạn có thể đóng cửa sổ này bây giờ.</p>
                </div>
                <script>
                    if (window.ReactNativeWebView) {
                        window.ReactNativeWebView.postMessage(JSON.stringify({ status: 'success' }));
                    }
                </script>
            </body>
            </html>";

        return Content(html, "text/html");
    }

    /// <summary>
    /// Giả lập thanh toán chuyển khoản SePay Sandbox (phục vụ Demo).
    /// Endpoint này gọi Sandbox API để sinh giao dịch giả lập, từ đó SePay sẽ tự động bắn webhook IPN về backend để đối soát.
    /// </summary>
    [HttpPost("sepay-mock-payment")]
    public async Task<IActionResult> CreateSePayMockPayment([FromQuery] Guid transactionId, [FromQuery] Guid paymentAccountId)
    {
        await _paymentAccountService.CreateMockPaymentAsync(
            GetUserId(),
            transactionId,
            paymentAccountId);
        return Ok(ApiResponse<string>.Ok("Mock payment generated successfully. The Webhook IPN will process and confirm this order shortly.", "Mock payment triggered.", HttpContext.TraceIdentifier));
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (sub is null || !Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Token invalid.");
        }

        return userId;
    }
}
