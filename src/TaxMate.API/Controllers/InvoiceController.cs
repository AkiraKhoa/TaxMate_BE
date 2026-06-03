using Microsoft.AspNetCore.Mvc;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[Controller]
[Route("api/invoice")]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoicePdfService _invoicePdfService;

    public InvoiceController(IInvoiceService invoiceService, IInvoicePdfService invoicePdfService)
    {
        _invoiceService = invoiceService;
        _invoicePdfService = invoicePdfService;
    }

    [HttpGet("{invoiceNumber}")]
    public async Task<IActionResult> GetInvoice(string invoiceNumber)
    {
        var result = await _invoiceService.GetInvoiceDetailAsync(invoiceNumber);
        return Ok(result);
    }

    [HttpGet("{invoiceNumber}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(string invoiceNumber, [FromQuery] Guid? paymentAccountId, [FromQuery] bool useDefault = false)
    {
        var data = await _invoiceService.GetInvoicePdfDataAsync(invoiceNumber, paymentAccountId, useDefault);
        var pdfBytes = await _invoicePdfService.GeneratePdfAsync(data);
        
        return File(pdfBytes, "application/pdf", $"{invoiceNumber}.pdf");
    }
}
