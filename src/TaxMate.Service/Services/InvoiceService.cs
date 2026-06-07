using Microsoft.EntityFrameworkCore;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionRepository _transactions;
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentAccountRepository _paymentAccounts;
    private readonly IVietQRService _vietQRService;

    public InvoiceService(
        IUnitOfWork unitOfWork,
        ITransactionRepository transactions,
        IInvoiceRepository invoices,
        IPaymentAccountRepository paymentAccounts,
        IVietQRService vietQRService)
    {
        _unitOfWork = unitOfWork;
        _transactions = transactions;
        _invoices = invoices;
        _paymentAccounts = paymentAccounts;
        _vietQRService = vietQRService;
    }

    public async Task<string> GenerateFromOrderAsync(Guid transactionId)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null)
        {
            throw new Exception("Order not found.");
        }

        var issueDate = DateTime.UtcNow;
        var localIssueDate = issueDate.AddHours(7);
        var dateStr = localIssueDate.ToString("yyyyMMdd");

        var count = await _invoices.CountByBusinessAndDateAsync(order.BusinessId, localIssueDate);
        var sequence = count + 1;
        var invoiceNumber = $"HD-{dateStr}-{sequence:D3}";

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            BusinessId = order.BusinessId,
            TotalAmount = order.TotalAmount,
            IssueDate = issueDate,
            Status = "Issued",
            CreatedAt = issueDate
        };

        foreach (var item in order.TransactionItems)
        {
            if (!item.ProductId.HasValue)
            {
                throw new Exception($"Cannot checkout item '{item.ProductName}' as its associated product no longer exists.");
            }

            var detail = new InvoiceDetail
            {
                InvoiceId = invoiceNumber,
                ProductId = item.ProductId.Value,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            };
            invoice.InvoiceDetails.Add(detail);
        }

        order.InvoiceId = invoiceNumber;

        await _invoices.AddAsync(invoice);
        await _unitOfWork.SaveChangesAsync();

        return invoiceNumber;
    }

    public async Task<InvoiceDetailResponse> GetInvoiceDetailAsync(string invoiceNumber)
    {
        var invoice = await _invoices.GetByNumberWithDetailsAsync(invoiceNumber);
        if (invoice == null)
        {
            throw new Exception("Invoice not found.");
        }

        var transaction = await _transactions.FirstOrDefaultAsync(x => x.InvoiceId == invoiceNumber);
        if (transaction == null)
        {
            throw new Exception("Associated transaction not found for this invoice.");
        }

        return new InvoiceDetailResponse
        {
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            Status = invoice.Status,
            BusinessName = invoice.Business.BusinessName,
            Address = invoice.Business.Address,
            Items = invoice.InvoiceDetails.Select(x => new InvoiceItemResponse
            {
                ProductName = x.ProductName,
                Unit = x.Product?.Unit,
                UnitPrice = x.UnitPrice,
                Quantity = x.Quantity,
                LineTotal = x.LineTotal
            }).ToList(),
            SubTotal = transaction.SubTotal,
            DiscountAmount = transaction.DiscountAmount,
            SurchargeAmount = transaction.SurchargeAmount,
            TotalAmount = invoice.TotalAmount,
            PdfUrl = $"/api/Invoice/{invoice.InvoiceNumber}/pdf"
        };
    }

    public async Task<InvoicePdfData> GetInvoicePdfDataAsync(string invoiceNumber)
    {
        var invoice = await _invoices.GetByNumberWithDetailsAsync(invoiceNumber);
        if (invoice == null)
        {
            throw new Exception("Invoice not found.");
        }

        var transaction = await _transactions.GetByInvoiceNumberWithDetailsAsync(invoiceNumber);
        if (transaction == null)
        {
            throw new Exception("Associated transaction not found for this invoice.");
        }

        PaymentAccount? paymentAccount = null;
        var checkoutPaymentAccountId = transaction.Payments
            .Where(p => p.PaymentAccountId.HasValue)
            .Select(p => p.PaymentAccountId)
            .FirstOrDefault();

        if (checkoutPaymentAccountId.HasValue)
        {
            paymentAccount = await _paymentAccounts.GetByIdAsync(checkoutPaymentAccountId.Value);
        }

        string? qrCodeUrl = null;
        if (paymentAccount != null)
        {
            qrCodeUrl = _vietQRService.GenerateInvoiceQRUrl(paymentAccount, invoice.TotalAmount, invoiceNumber);
        }

        return new InvoicePdfData
        {
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            BusinessName = invoice.Business.BusinessName,
            Address = invoice.Business.Address,
            TaxCode = invoice.Business.Owner.TaxCode,
            Phone = invoice.Business.Owner.Phone,
            Items = invoice.InvoiceDetails.Select(x => new InvoiceItemResponse
            {
                ProductName = x.ProductName,
                Unit = x.Product?.Unit,
                UnitPrice = x.UnitPrice,
                Quantity = x.Quantity,
                LineTotal = x.LineTotal
            }).ToList(),
            SubTotal = transaction.SubTotal,
            DiscountType = transaction.DiscountType,
            DiscountValue = transaction.DiscountValue,
            DiscountAmount = transaction.DiscountAmount,
            SurchargeName = transaction.SurchargeName,
            SurchargeAmount = transaction.SurchargeAmount,
            TotalAmount = invoice.TotalAmount,
            QRCodeUrl = qrCodeUrl,
            BankName = paymentAccount?.BankName,
            AccountNumber = paymentAccount?.AccountNumber,
            AccountName = paymentAccount?.AccountName
        };
    }
}
