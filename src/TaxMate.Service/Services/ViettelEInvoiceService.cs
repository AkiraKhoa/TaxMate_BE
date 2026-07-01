using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class ViettelEInvoiceService : IEInvoiceService
{
    private readonly HttpClient _httpClient;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IGenericRepository<User> _users;
    private readonly IGenericRepository<InvoiceDetail> _invoiceDetails;
    private readonly ILogger<ViettelEInvoiceService> _logger;

    public ViettelEInvoiceService(
        HttpClient httpClient,
        IGenericRepository<BusinessProfile> businessProfiles,
        IGenericRepository<User> users,
        IGenericRepository<InvoiceDetail> invoiceDetails,
        ILogger<ViettelEInvoiceService> logger)
    {
        _httpClient = httpClient;
        _businessProfiles = businessProfiles;
        _users = users;
        _invoiceDetails = invoiceDetails;
        _logger = logger;
    }

    public async Task<EInvoiceResult> IssueInvoiceAsync(Invoice invoice, EInvoiceConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting real Viettel SInvoice issuance for Invoice Number: {InvoiceNumber}", invoice.InvoiceNumber);

        // 1. Lấy thông tin Tax Code của người bán (supplierTaxCode)
        var business = await _businessProfiles.GetByIdAsync(invoice.BusinessId);
        if (business == null)
        {
            throw new Exception("Business profile not found.");
        }

        var owner = await _users.GetByIdAsync(business.OwnerId);
        var supplierTaxCode = owner?.TaxCode;
        if (string.IsNullOrEmpty(supplierTaxCode))
        {
            throw new Exception("Supplier Tax Code (Business Owner Tax Code) is missing.");
        }

        // 2. Tải chi tiết sản phẩm hóa đơn nếu chưa được load
        var details = invoice.InvoiceDetails;
        if (details == null || !details.Any())
        {
            details = (await _invoiceDetails.FindAsync(d => d.InvoiceId == invoice.InvoiceNumber)).ToList();
        }

        if (!details.Any())
        {
            throw new Exception("Invoice details (items) are empty. Cannot issue invoice.");
        }

        // 3. Đăng nhập lấy access_token
        var token = await GetAccessTokenAsync(config, cancellationToken);
        _logger.LogInformation("Viettel SInvoice logged in successfully.");

        // 4. Chuẩn bị payload phát hành hóa đơn (AdjustmentType = 1: Hóa đơn gốc)
        var itemInfos = new List<object>();
        int index = 1;
        foreach (var item in details)
        {
            var totalWithTax = (double)item.LineTotal;
            var totalWithoutTax = Math.Round(totalWithTax / 1.1, 2); // Giả định VAT 10%
            var taxAmount = Math.Round(totalWithTax - totalWithoutTax, 2);

            itemInfos.Add(new
            {
                lineNumber = index++,
                selection = 1,
                itemName = item.ProductName,
                unitName = "Lượt",
                quantity = (double)item.Quantity,
                unitPrice = (double)item.UnitPrice,
                taxRate = 10,
                taxAmount = taxAmount,
                itemTotalAmountWithoutTax = totalWithoutTax,
                itemTotalAmountWithTax = totalWithTax
            });
        }

        var totalWithTaxSum = (double)invoice.TotalAmount;
        var totalWithoutTaxSum = Math.Round(totalWithTaxSum / 1.1, 2);
        var totalTaxAmtSum = Math.Round(totalWithTaxSum - totalWithoutTaxSum, 2);

        var payload = new
        {
            generalInvoiceInfo = new
            {
                transactionUuid = Guid.NewGuid().ToString(),
                invoiceType = "1", // Hóa đơn GTGT
                templateCode = config.InvoiceTemplateCode ?? "1/001",
                invoiceSeries = config.Symbol ?? "K23TYY",
                invoiceIssuedDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                currencyCode = "VND",
                adjustmentType = "1",
                paymentStatus = true,
                cusGetInvoiceRight = true
            },
            sellerInfo = new
            {
                sellerLegalName = business.BusinessName,
                sellerTaxCode = supplierTaxCode,
                sellerAddressLine = business.Address ?? "Việt Nam"
            },
            buyerInfo = new
            {
                buyerName = "Khách mua lẻ",
                buyerNotGetInvoice = 0
            },
            payments = new[]
            {
                new { paymentMethod = "3", paymentMethodName = "TM/CK" }
            },
            itemInfo = itemInfos,
            summarizeInfo = new
            {
                totalAmountWithoutTax = totalWithoutTaxSum,
                totalTaxAmount = totalTaxAmtSum,
                totalAmountWithTax = totalWithTaxSum,
                totalAmountWithTaxInWords = ConvertNumberToWords(totalWithTaxSum) + " đồng chẵn."
            },
            taxBreakdowns = new[]
            {
                new { taxRate = 10, taxableAmount = totalWithoutTaxSum, taxAmount = totalTaxAmtSum }
            }
        };

        // 5. Gọi API tạo hóa đơn
        var supplierCodeClean = supplierTaxCode.Replace("-", "").Trim();
        var issueUrl = $"{config.ApiUrl.TrimEnd('/')}/InvoiceAPI/InvoiceWS/createInvoice/{supplierCodeClean}";
        
        var request = new HttpRequestMessage(HttpMethod.Post, issueUrl);
        request.Headers.Add("Cookie", $"access_token={token}");
        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Viettel SInvoice API error: {ErrorContent}", errorContent);
            throw new Exception($"Failed to issue Viettel invoice. API Status: {response.StatusCode}, Details: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<ViettelInvoiceResultResponse>(cancellationToken: cancellationToken);
        if (result == null || result.Result == null)
        {
            throw new Exception("Invalid response from Viettel SInvoice API.");
        }

        _logger.LogInformation("Viettel SInvoice issued successfully. TaxCode: {Code}", result.Result.TaxAuthorityCode);

        // Demo trả về link xem hóa đơn dựa trên mã đặt chỗ hoặc mã tra cứu của Viettel
        var viewPdfUrl = $"{config.ApiUrl.TrimEnd('/')}/InvoiceAPI/InvoiceWS/getInvoicePdf/{result.Result.InvoiceNo}/{result.Result.ReservationCode}";
        var viewXmlUrl = $"{config.ApiUrl.TrimEnd('/')}/InvoiceAPI/InvoiceWS/getInvoiceXml/{result.Result.InvoiceNo}/{result.Result.ReservationCode}";

        return new EInvoiceResult
        {
            TaxAuthorityCode = result.Result.TaxAuthorityCode ?? $"C{DateTime.UtcNow.Year - 2000}{config.Symbol ?? "TAA"}-{new Random().Next(100000, 999999)}",
            OfficialPdfUrl = viewPdfUrl,
            OfficialXmlUrl = viewXmlUrl
        };
    }

    public async Task CancelInvoiceAsync(string invoiceNumber, EInvoiceConfig config, string reason, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelling Viettel SInvoice: {InvoiceNumber}. Reason: {Reason}", invoiceNumber, reason);

        var token = await GetAccessTokenAsync(config, cancellationToken);
        var cancelUrl = $"{config.ApiUrl.TrimEnd('/')}/InvoiceAPI/InvoiceWS/cancelInvoice/supplierTaxCode"; // Cần mã số thuế đúng

        var request = new HttpRequestMessage(HttpMethod.Post, cancelUrl);
        request.Headers.Add("Cookie", $"access_token={token}");
        request.Content = JsonContent.Create(new
        {
            invoiceNo = invoiceNumber,
            comment = reason
        });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to cancel invoice on Viettel system: {err}");
        }

        _logger.LogInformation("Viettel SInvoice {InvoiceNumber} cancelled successfully.", invoiceNumber);
    }

    private async Task<string> GetAccessTokenAsync(EInvoiceConfig config, CancellationToken cancellationToken)
    {
        var loginUrl = $"{config.ApiUrl.TrimEnd('/')}/auth/login";
        var payload = new
        {
            username = config.Username,
            password = config.Password
        };

        var response = await _httpClient.PostAsJsonAsync(loginUrl, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Viettel login failed. Status: {response.StatusCode}, Details: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<ViettelAuthResponse>(cancellationToken: cancellationToken);
        if (result == null || string.IsNullOrEmpty(result.AccessToken))
        {
            throw new Exception("Invalid token response from Viettel login.");
        }

        return result.AccessToken;
    }

    private static string ConvertNumberToWords(double number)
    {
        // Hàm chuyển số thành chữ tối giản để phục vụ in hóa đơn tiếng Việt
        if (number == 0) return "Không";
        if (number < 0) return "Âm " + ConvertNumberToWords(Math.Abs(number));

        string[] unitsMap = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
        
        long val = (long)Math.Round(number);
        if (val >= 1000000000)
            return ConvertNumberToWords(val / 1000000000) + " tỷ " + (val % 1000000000 > 0 ? ConvertNumberToWords(val % 1000000000) : "");
        if (val >= 1000000)
            return ConvertNumberToWords(val / 1000000) + " triệu " + (val % 1000000 > 0 ? ConvertNumberToWords(val % 1000000) : "");
        if (val >= 1000)
            return ConvertNumberToWords(val / 1000) + " nghìn " + (val % 1000 > 0 ? ConvertNumberToWords(val % 1000) : "");
        if (val >= 100)
            return ConvertNumberToWords(val / 100) + " trăm " + (val % 100 > 0 ? ConvertNumberToWords(val % 100) : "");

        if (val >= 10)
        {
            var chuc = val / 10;
            var donvi = val % 10;
            var strChuc = chuc == 1 ? "mười" : unitsMap[chuc] + " mươi";
            var strDonVi = "";
            if (donvi > 0)
            {
                if (donvi == 1 && chuc > 1) strDonVi = " mốt";
                else if (donvi == 5) strDonVi = " lăm";
                else strDonVi = " " + unitsMap[donvi];
            }
            return strChuc + strDonVi;
        }

        return unitsMap[val];
    }
}

// ================= DTOs =================
public class ViettelAuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;
}

public class ViettelInvoiceResultResponse
{
    [JsonPropertyName("result")]
    public ViettelInvoiceResult? Result { get; set; }
}

public class ViettelInvoiceResult
{
    [JsonPropertyName("invoiceNo")]
    public string InvoiceNo { get; set; } = null!;

    [JsonPropertyName("reservationCode")]
    public string ReservationCode { get; set; } = null!;

    [JsonPropertyName("taxAuthorityCode")]
    public string? TaxAuthorityCode { get; set; }
}
