using TaxMate.Model.Entities;

namespace TaxMate.Service.Interfaces;

public interface IVietQRService
{
    string GenerateQRUrl(PaymentAccount account, decimal amount, string transactionCode);
    string GenerateInvoiceQRUrl(PaymentAccount account, decimal amount, string invoiceNumber);
}
