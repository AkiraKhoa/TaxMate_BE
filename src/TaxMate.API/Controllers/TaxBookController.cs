using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Service.Interfaces;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Expense;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.DTO.Tax;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/businesses/{businessId:guid}/tax-books")]
// [Authorize]
public class TaxBookController : ControllerBase
{
    private readonly ITaxBookService _taxBookService;

    public TaxBookController(ITaxBookService taxBookService)
    {
        _taxBookService = taxBookService;
    }

    [HttpGet("qtt/offset-obligations")]
    public async Task<IActionResult> GetQttOffsetObligations(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.GetQttOffsetObligationsAsync(
            GetUserId(), businessId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<QttOffsetObligationOption>>.Ok(
            result,
            "Đã tải danh sách nghĩa vụ có thể bù trừ.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("qtt/declarations/{declarationId:guid}/export")]
    public async Task<IActionResult> ExportQttDeclaration(
        Guid businessId,
        Guid declarationId,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.ExportQttAsync(
            GetUserId(), businessId, declarationId, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPost("qtt/declarations/{declarationId:guid}/confirm")]
    public async Task<IActionResult> ConfirmQttDeclaration(
        Guid businessId,
        Guid declarationId,
        [FromBody] ConfirmQttDeclarationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.ConfirmQttDeclarationAsync(
            GetUserId(), businessId, declarationId, request, cancellationToken);

        return Ok(ApiResponse<QttDeclarationResponse>.Ok(
            result,
            "Đã xác nhận và khóa hồ sơ quyết toán TNCN.",
            HttpContext.TraceIdentifier));
    }

    [HttpPost("qtt/declaration")]
    public async Task<IActionResult> CreateQttDeclaration(
        Guid businessId,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.CreateQttDeclarationAsync(
            GetUserId(), businessId, year, cancellationToken);

        return Ok(ApiResponse<QttDeclarationResponse>.Ok(
            result,
            "Đã tạo hồ sơ quyết toán TNCN nháp.",
            HttpContext.TraceIdentifier));
    }

    [HttpPut("qtt/declarations/{declarationId:guid}/overpayment-allocation")]
    public async Task<IActionResult> UpdateQttOverpaymentAllocation(
        Guid businessId,
        Guid declarationId,
        [FromBody] UpdateQttOverpaymentAllocationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.UpdateQttOverpaymentAllocationAsync(
            GetUserId(), businessId, declarationId, request, cancellationToken);

        return Ok(ApiResponse<QttDeclarationResponse>.Ok(
            result,
            "Đã lưu cách xử lý số PIT nộp thừa.",
            HttpContext.TraceIdentifier));
    }

    [HttpPost("qtt/calculate")]
    public async Task<IActionResult> CalculateQtt(
        Guid businessId,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.CalculateQttAsync(
            GetUserId(), businessId, year, cancellationToken);

        return Ok(ApiResponse<QttCalculationResponse>.Ok(
            result,
            "Đã lưu bản tính quyết toán TNCN năm.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("qtt/calculation-preview")]
    public async Task<IActionResult> GetQttCalculationPreview(
        Guid businessId,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.GetQttCalculationPreviewAsync(
            GetUserId(), businessId, year, cancellationToken);

        return Ok(ApiResponse<QttCalculationPreviewResponse>.Ok(
            result,
            "Đã tính thử quyết toán TNCN năm.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("qtt/preview")]
    public async Task<IActionResult> GetQttPreview(
        Guid businessId,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.GetQttPreviewAsync(
            GetUserId(), businessId, year, cancellationToken);

        return Ok(ApiResponse<QttPreviewResponse>.Ok(
            result,
            "Đã tải dữ liệu quyết toán TNCN năm.",
            HttpContext.TraceIdentifier));
    }

    [HttpPost("s2c/evidence-review")]
    public async Task<IActionResult> ConfirmS2cEvidenceReview(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.ConfirmS2cEvidenceReviewAsync(
            GetUserId(), businessId, year, quarter, cancellationToken);

        return Ok(ApiResponse<S2cBookProjection>.Ok(
            result,
            "Đã lưu xác nhận rà soát chứng từ S2c.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("s2c/preview")]
    public async Task<IActionResult> GetS2cPreview(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.GetS2cPreviewAsync(
            GetUserId(), businessId, year, quarter, cancellationToken);

        return Ok(ApiResponse<S2cBookProjection>.Ok(
            result,
            "Đã tải bản xem trước sổ S2c.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("s2c/export")]
    public async Task<IActionResult> ExportS2c(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.ExportS2cAsync(
            GetUserId(),
            businessId,
            year,
            quarter,
            cancellationToken);

        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("s2b/preview")]
    public async Task<IActionResult> GetS2bPreview(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.GetS2bPreviewAsync(
            GetUserId(), businessId, year, quarter, cancellationToken);

        return Ok(ApiResponse<OwnerRevenueProjection>.Ok(
            result,
            "Đã tải bản xem trước sổ S2b.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("s2b/export")]
    public async Task<IActionResult> ExportS2b(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.ExportS2bAsync(
            GetUserId(), businessId, year, quarter, cancellationToken);

        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("s2e/preview")]
    public async Task<IActionResult> GetS2ePreview(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.GetS2ePreviewAsync(
            GetUserId(), businessId, year, quarter, cancellationToken);
        return Ok(ApiResponse<S2eBookProjection>.Ok(
            result,
            "Đã tải bản xem trước sổ S2e.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("s2d/preview")]
    public async Task<IActionResult> GetS2dPreview(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.GetS2dPreviewAsync(
            GetUserId(),
            businessId,
            year,
            quarter,
            cancellationToken);

        return Ok(ApiResponse<S2dBook>.Ok(
            result,
            "Đã tải bản xem trước sổ S2d.",
            HttpContext.TraceIdentifier));
    }

    [HttpGet("s2e/export")]
    public async Task<IActionResult> ExportS2e(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.ExportS2eAsync(
            GetUserId(),
            businessId,
            year,
            quarter,
            cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("s2d/export")]
    public async Task<IActionResult> ExportS2d(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.ExportS2dAsync(
            GetUserId(),
            businessId,
            year,
            quarter,
            cancellationToken);

        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("s1a/export")]
    public async Task<IActionResult> ExportS1a(
        Guid businessId,
        [FromQuery] int year,
        [FromQuery] int? quarter,
        CancellationToken cancellationToken)
    {
        var result = await _taxBookService.ExportS1aAsync(
            GetUserId(),
            businessId,
            year,
            quarter,
            cancellationToken);

        return File(
            result.Content,
            result.ContentType,
            result.FileName);
    }

    private Guid GetUserId()
    {
        var rawUserId = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Invalid user identifier.");
        }

        return userId;
    }
}
