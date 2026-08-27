using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Quản lý tài khoản ngân hàng nhận thanh toán.</summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentAccountController : ControllerBase
{
    private readonly IPaymentAccountService _paymentAccountService;
    private readonly ISePayService _sePayService;
    private readonly IBusinessProfileService _businessProfileService;
    private readonly ILogger<PaymentAccountController> _logger;

    public PaymentAccountController(
        IPaymentAccountService paymentAccountService, 
        ISePayService sePayService,
        IBusinessProfileService businessProfileService,
        ILogger<PaymentAccountController> logger)
    {
        _paymentAccountService = paymentAccountService;
        _sePayService = sePayService;
        _businessProfileService = businessProfileService;
        _logger = logger;
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

    /// <summary>Lấy link WebView để liên kết tài khoản ngân hàng qua SePay Bank Hub.</summary>
    /// <param name="businessId">ID cửa hàng cần liên kết.</param>
    /// <param name="isMobileApp">True cho ứng dụng mobile; false cho web.</param>
    [HttpGet("sepay-connect-url")]
    public async Task<IActionResult> GetSePayConnectUrl(
        [FromQuery] Guid businessId,
        [FromQuery] bool isMobileApp = true)
    {
        var scheme = Request.Scheme;
        var host = Request.Host.ToString();

        var url = await _sePayService.GetSePayConnectUrlAsync(businessId, scheme, host, isMobileApp);

        return Ok(ApiResponse<string>.Ok(url, "Get SePay connect URL successfully", HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy link WebView để hủy liên kết tài khoản ngân hàng qua SePay Bank Hub.</summary>
    /// <param name="paymentAccountId">ID tài khoản ngân hàng cần hủy liên kết.</param>
    [HttpGet("sepay-disconnect-url")]
    public async Task<IActionResult> GetSePayDisconnectUrl([FromQuery] Guid paymentAccountId)
    {
        var scheme = Request.Scheme;
        var host = Request.Host.ToString();

        var url = await _paymentAccountService.GetSePayDisconnectUrlAsync(paymentAccountId, scheme, host);

        return Ok(ApiResponse<string>.Ok(url, "Get SePay disconnect URL successfully", HttpContext.TraceIdentifier));
    }


    /// <summary>
    /// Đồng bộ tài khoản ngân hàng từ SePay Bank Hub về DB.
    /// App gọi endpoint này sau khi WebView báo FINISHED_BANK_ACCOUNT_LINK (không cần chờ webhook).
    /// </summary>
    [HttpPost("sepay-sync")]
    public async Task<IActionResult> SyncSePayAccounts([FromQuery] Guid businessId)
    {
        var (synced, total) = await _paymentAccountService.SyncSePayAccountsAsync(businessId);

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
        var (recovered, total) = await _paymentAccountService.RecoverAllFromSePayAsync();

        return Ok(ApiResponse<object>.Ok(
            new { recovered, total },
            $"Recovered {recovered}/{total} bank accounts directly from SePay Sandbox to local DB.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Callback xử lý sau khi liên kết ngân hàng thành công từ SePay.</summary>
    [HttpGet("sepay-callback")]
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
        await _paymentAccountService.CreateMockPaymentAsync(transactionId, paymentAccountId);
        return Ok(ApiResponse<string>.Ok("Mock payment generated successfully. The Webhook IPN will process and confirm this order shortly.", "Mock payment triggered.", HttpContext.TraceIdentifier));
    }
}
