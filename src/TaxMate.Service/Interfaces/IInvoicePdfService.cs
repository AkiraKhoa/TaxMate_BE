using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IInvoicePdfService
{
    Task<byte[]> GeneratePdfAsync(InvoicePdfData data);
}
