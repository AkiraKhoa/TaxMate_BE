using Microsoft.Extensions.Logging;
using TaxMate.Model.Entities;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class MockEInvoiceService : IEInvoiceService
{
    private readonly ILogger<MockEInvoiceService> _logger;

    public MockEInvoiceService(ILogger<MockEInvoiceService> logger)
    {
        _logger = logger;
    }

    public async Task<EInvoiceResult> IssueInvoiceAsync(Invoice invoice, EInvoiceConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulating E-Invoice issuance for Invoice Number: {InvoiceNumber} via T-VAN Provider: {Provider}", invoice.InvoiceNumber, config.Provider);
        
        // Giả lập thời gian kết nối API T-VAN và ký số bằng Cloud HSM
        await Task.Delay(1000, cancellationToken);

        var random = new Random();
        var taxCodeSuffix = random.Next(100000, 999999).ToString();
        var taxAuthorityCode = $"C{DateTime.UtcNow.Year - 2000}{config.Symbol ?? "TAA"}-{taxCodeSuffix}";

        var officialPdfUrl = $"https://test.einvoice.{config.Provider.ToLower()}.com.vn/view-invoice/{invoice.InvoiceNumber}/pdf";
        var officialXmlUrl = $"https://test.einvoice.{config.Provider.ToLower()}.com.vn/view-invoice/{invoice.InvoiceNumber}/xml";

        _logger.LogInformation("E-Invoice issued successfully. Tax Authority Code: {TaxAuthorityCode}", taxAuthorityCode);

        return new EInvoiceResult
        {
            TaxAuthorityCode = taxAuthorityCode,
            OfficialPdfUrl = officialPdfUrl,
            OfficialXmlUrl = officialXmlUrl
        };
    }

    public async Task CancelInvoiceAsync(string invoiceNumber, EInvoiceConfig config, string reason, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulating E-Invoice cancellation for Invoice Number: {InvoiceNumber} via T-VAN Provider: {Provider}. Reason: {Reason}", invoiceNumber, config.Provider, reason);
        
        await Task.Delay(800, cancellationToken);

        _logger.LogInformation("E-Invoice {InvoiceNumber} cancelled successfully on Tax Department system.", invoiceNumber);
    }
}
