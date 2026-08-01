using TaxMate.Model.Entities;

namespace TaxMate.Service.Interfaces;

public interface IEInvoiceService
{
    Task<EInvoiceResult> IssueInvoiceAsync(Invoice invoice, EInvoiceConfig config, CancellationToken cancellationToken = default);
    Task<int?> GetQuotaRemainingAsync(EInvoiceConfig config, CancellationToken cancellationToken = default);
}

public class EInvoiceResult
{
    public bool Success { get; set; }
    public string? TaxAuthorityCode { get; set; }  // Số hóa đơn từ SePay
    public string? OfficialPdfUrl { get; set; }    // Link PDF hóa đơn
    public string? OfficialXmlUrl { get; set; }    // Link XML hóa đơn
    public string? ReferenceCode { get; set; }
    public string? TrackingCode { get; set; }
    public string? ErrorMessage { get; set; }
}
