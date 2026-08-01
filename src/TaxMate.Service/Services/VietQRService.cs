using System.Web;
using TaxMate.Model.Entities;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class VietQRService : IVietQRService
{
    public string GenerateQRUrl(PaymentAccount account, decimal amount, string transactionCode)
    {
        return BuildUrl(account, amount, $"Thanh toan don hang {transactionCode}");
    }

    public string GenerateInvoiceQRUrl(PaymentAccount account, decimal amount, string invoiceNumber)
    {
        return BuildUrl(account, amount, $"Thanh toan hoa don {invoiceNumber}");
    }

    private string BuildUrl(PaymentAccount account, decimal amount, string description)
    {
        var escapedAccountName = HttpUtility.UrlEncode(account.AccountName);
        var escapedAddInfo = HttpUtility.UrlEncode(description);
        var amountLong = (long)Math.Round(amount);
        
        return $"https://img.vietqr.io/image/{account.BankShortName}-{account.AccountNumber}-compact2.png?accountName={escapedAccountName}&amount={amountLong}&addInfo={escapedAddInfo}";
    }
}
