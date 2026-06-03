using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IInvoiceService
{
    Task<string> GenerateFromOrderAsync(Guid transactionId);
    Task<InvoiceDetailResponse> GetInvoiceDetailAsync(string invoiceNumber);
    Task<InvoicePdfData> GetInvoicePdfDataAsync(string invoiceNumber, Guid? paymentAccountId, bool useDefault);
}
