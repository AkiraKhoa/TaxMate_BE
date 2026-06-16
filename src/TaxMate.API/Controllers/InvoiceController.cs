using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Xem chi tiết và tải PDF hóa đơn.</summary>
[ApiController]
[Route("api/[controller]")]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoicePdfService _invoicePdfService;

    public InvoiceController(IInvoiceService invoiceService, IInvoicePdfService invoicePdfService)
    {
        _invoiceService = invoiceService;
        _invoicePdfService = invoicePdfService;
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
}
