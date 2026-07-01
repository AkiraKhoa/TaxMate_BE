using TaxMate.Model.Entities;

namespace TaxMate.Service.Interfaces;

public interface IEInvoiceService
{
    Task<EInvoiceResult> IssueInvoiceAsync(Invoice invoice, EInvoiceConfig config, CancellationToken cancellationToken = default);
    Task CancelInvoiceAsync(string invoiceNumber, EInvoiceConfig config, string reason, CancellationToken cancellationToken = default);
}

public class EInvoiceResult
{
    public string TaxAuthorityCode { get; set; } = null!;
    public string OfficialPdfUrl { get; set; } = null!;
    public string OfficialXmlUrl { get; set; } = null!;
}
