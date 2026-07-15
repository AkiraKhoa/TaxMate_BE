using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Xem chi tiết, tải PDF và phát hành lại hóa đơn.</summary>
[ApiController]
[Route("api/[controller]")]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoicePdfService _invoicePdfService;
    private readonly IEInvoiceService _eInvoiceService;
    private readonly IInvoiceRepository _invoices;
    private readonly IGenericRepository<EInvoiceConfig> _eInvoiceConfigs;
    private readonly IGenericRepository<BusinessProfile> _businesses;
    private readonly IUnitOfWork _unitOfWork;

    public InvoiceController(
        IInvoiceService invoiceService, 
        IInvoicePdfService invoicePdfService,
        IEInvoiceService eInvoiceService,
        IInvoiceRepository invoices,
        IGenericRepository<EInvoiceConfig> eInvoiceConfigs,
        IGenericRepository<BusinessProfile> businesses,
        IUnitOfWork unitOfWork)
    {
        _invoiceService = invoiceService;
        _invoicePdfService = invoicePdfService;
        _eInvoiceService = eInvoiceService;
        _invoices = invoices;
        _eInvoiceConfigs = eInvoiceConfigs;
        _businesses = businesses;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Lấy chi tiết hóa đơn theo số hóa đơn.</summary>
    /// <param name="invoiceNumber">Số hóa đơn (ví dụ HD-20240606-001).</param>
    [HttpGet("{invoiceNumber}")]
    public async Task<IActionResult> GetInvoice(string invoiceNumber)
    {
        var result = await _invoiceService.GetInvoiceDetailAsync(invoiceNumber);
        return Ok(
            ApiResponse<InvoiceDetailResponse>.Ok(
                result,
                "Get invoice successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Tải file PDF hóa đơn. Mã VietQR lấy từ tài khoản đã ghi nhận lúc checkout.</summary>
    /// <param name="invoiceNumber">Số hóa đơn.</param>
    [HttpGet("{invoiceNumber}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(string invoiceNumber)
    {
        var data = await _invoiceService.GetInvoicePdfDataAsync(invoiceNumber);
        var pdfBytes = await _invoicePdfService.GeneratePdfAsync(data);
        
        return File(pdfBytes, "application/pdf", $"{invoiceNumber}.pdf");
    }

    /// <summary>Thử lại phát hành HĐĐT cho hóa đơn bị lỗi.</summary>
    /// <param name="invoiceNumber">Số hóa đơn.</param>
    [HttpPost("{invoiceNumber}/retry-issue")]
    public async Task<IActionResult> RetryIssue(string invoiceNumber)
    {
        var invoice = await _invoices.GetByNumberWithDetailsAsync(invoiceNumber);
        if (invoice == null)
        {
            return NotFound(ApiResponse<string>.Fail("Không tìm thấy hóa đơn.", HttpContext.TraceIdentifier));
        }

        if (invoice.Status == Model.Common.InvoiceStatus.Issued)
        {
            return BadRequest(ApiResponse<string>.Fail("Hóa đơn này đã được phát hành thành công từ trước.", HttpContext.TraceIdentifier));
        }

        var business = await _businesses.GetByIdAsync(invoice.BusinessId);
        if (business == null)
        {
            return NotFound(ApiResponse<string>.Fail("Không tìm thấy thông tin cửa hàng.", HttpContext.TraceIdentifier));
        }

        var eInvoiceConfig = await _eInvoiceConfigs.FirstOrDefaultAsync(c => c.BusinessId == invoice.BusinessId && c.IsEnabled);
        if (eInvoiceConfig == null)
        {
            return BadRequest(ApiResponse<string>.Fail("Cửa hàng chưa kích hoạt hoặc chưa cấu hình hóa đơn điện tử.", HttpContext.TraceIdentifier));
        }

        try
        {
            invoice.Business = business;
            invoice.Status = Model.Common.InvoiceStatus.Processing;
            invoice.SePayMessage = "Đang thử lại phát hành...";
            await _unitOfWork.SaveChangesAsync();

            var eInvoiceResult = await _eInvoiceService.IssueInvoiceAsync(invoice, eInvoiceConfig);
            
            invoice.SePayTrackingCode = eInvoiceResult.TrackingCode;
            invoice.SePayReferenceCode = eInvoiceResult.ReferenceCode;
            invoice.SePayMessage = eInvoiceResult.ErrorMessage;

            if (eInvoiceResult.Success)
            {
                invoice.TaxAuthorityCode = eInvoiceResult.TaxAuthorityCode;
                invoice.OfficialPdfUrl = eInvoiceResult.OfficialPdfUrl;
                invoice.OfficialXmlUrl = eInvoiceResult.OfficialXmlUrl;
                invoice.Status = Model.Common.InvoiceStatus.Issued;
            }
            else
            {
                invoice.Status = Model.Common.InvoiceStatus.Failed;
            }
            await _unitOfWork.SaveChangesAsync();

            var result = await _invoiceService.GetInvoiceDetailAsync(invoiceNumber);
            return Ok(ApiResponse<InvoiceDetailResponse>.Ok(result, eInvoiceResult.Success ? "Phát hành hóa đơn điện tử thành công." : $"Phát hành thất bại: {eInvoiceResult.ErrorMessage}", HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            invoice.Status = Model.Common.InvoiceStatus.Failed;
            invoice.SePayMessage = $"Lỗi hệ thống khi phát hành: {ex.Message}";
            await _unitOfWork.SaveChangesAsync();

            return StatusCode(500, ApiResponse<string>.Fail($"Lỗi hệ thống: {ex.Message}", HttpContext.TraceIdentifier));
        }
    }
}
