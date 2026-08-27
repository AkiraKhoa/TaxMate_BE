using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.TaxProfile;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/tax-profile/business/{businessId:guid}")]
public sealed class TaxProfileController : ControllerBase
{
    private readonly IOwnerTaxProfileService _service;

    public TaxProfileController(IOwnerTaxProfileService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrent(
        Guid businessId,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<OwnerTaxProfileResponse>.Ok(
            await _service.GetCurrentAsync(
                GetUserId(), businessId, cancellationToken),
            "Đã tải hồ sơ thuế của chủ hộ.",
            HttpContext.TraceIdentifier));

    [HttpPut]
    public async Task<IActionResult> UpdateCurrent(
        Guid businessId,
        [FromBody] UpdateOwnerTaxProfileRequest request,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<OwnerTaxProfileResponse>.Ok(
            await _service.UpdateCurrentAsync(
                GetUserId(), businessId, request, cancellationToken),
            "Đã cập nhật hồ sơ thuế của chủ hộ.",
            HttpContext.TraceIdentifier));

    [HttpGet("threshold-reviews")]
    public async Task<IActionResult> GetThresholdReviews(
        Guid businessId,
        [FromQuery] int year,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<RevenueThresholdReviewResponse>>.Ok(
            await _service.GetThresholdReviewsAsync(
                GetUserId(), businessId, year, cancellationToken),
            "Đã kiểm tra các mốc doanh thu cần rà soát.",
            HttpContext.TraceIdentifier));

    [HttpPost("threshold-reviews/{alertId:guid}/confirm")]
    public async Task<IActionResult> ConfirmThresholdReview(
        Guid businessId,
        Guid alertId,
        [FromBody] ConfirmRevenueThresholdReviewRequest request,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<RevenueThresholdReviewResponse>.Ok(
            await _service.ConfirmThresholdReviewAsync(
                GetUserId(), businessId, alertId, request, cancellationToken),
            "Đã ghi nhận xử lý mốc doanh thu.",
            HttpContext.TraceIdentifier));

    [HttpPost("threshold-reviews/{alertId:guid}/dismiss")]
    public async Task<IActionResult> DismissThresholdReview(
        Guid businessId,
        Guid alertId,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<RevenueThresholdReviewResponse>.Ok(
            await _service.DismissThresholdReviewAsync(
                GetUserId(), businessId, alertId, cancellationToken),
            "Đã đóng cảnh báo không còn vượt ngưỡng.",
            HttpContext.TraceIdentifier));

    [HttpGet("annual-conclusion")]
    public async Task<IActionResult> PreviewAnnualConclusion(
        Guid businessId,
        [FromQuery] int year,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<AnnualRevenueConclusionPreviewResponse>.Ok(
            await _service.PreviewAnnualConclusionAsync(
                GetUserId(), businessId, year, cancellationToken),
            "Đã kiểm tra điều kiện kết luận doanh thu năm.",
            HttpContext.TraceIdentifier));

    [HttpPost("annual-conclusion/confirm")]
    public async Task<IActionResult> ConfirmAnnualConclusion(
        Guid businessId,
        [FromQuery] int year,
        [FromBody] ConfirmAnnualRevenueConclusionRequest request,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<AnnualRevenueConclusionPreviewResponse>.Ok(
            await _service.ConfirmAnnualConclusionAsync(
                GetUserId(), businessId, year, request, cancellationToken),
            "Đã xác nhận doanh thu năm không quá 1 tỷ đồng.",
            HttpContext.TraceIdentifier));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token invalid.");
    }
}
