using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TaxMate.Model.Entities;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class SePayEInvoiceService : IEInvoiceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SePayEInvoiceService> _logger;
    private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresAt)> TokenCache = new();

    public SePayEInvoiceService(IHttpClientFactory httpClientFactory, ILogger<SePayEInvoiceService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<EInvoiceResult> IssueInvoiceAsync(Invoice invoice, EInvoiceConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                return new EInvoiceResult
                {
                    Success = false,
                    ErrorMessage = "Không thể lấy Access Token từ SePay. Vui lòng kiểm tra lại Client ID/Secret."
                };
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Map payment method
            string paymentMethod = "KHAC";
            if (invoice.InvoiceDetails != null)
            {
                // Mặc định check payment method của transaction liên quan
                // (Trong OrderService, đơn hàng thường có thông tin thanh toán)
                // Chúng ta sẽ dự phòng: Cash -> TM, Transfer / BankTransfer -> CK.
                // Nếu không có thông tin chi tiết, ta lấy mặc định CK/TM tuỳ loại đơn.
                var orderPaymentMethods = invoice.Business?.Transactions
                    ?.FirstOrDefault(t => t.InvoiceId == invoice.InvoiceNumber)
                    ?.Payments?.Select(p => p.PaymentMethod).ToList();

                if (orderPaymentMethods != null && orderPaymentMethods.Any())
                {
                    bool hasCash = orderPaymentMethods.Any(m => m.Equals("Cash", StringComparison.OrdinalIgnoreCase));
                    bool hasTransfer = orderPaymentMethods.Any(m => m.Equals("Transfer", StringComparison.OrdinalIgnoreCase) || m.Equals("Transfer", StringComparison.OrdinalIgnoreCase));

                    if (hasCash && hasTransfer)
                    {
                        paymentMethod = "TM/CK";
                    }
                    else if (hasCash)
                    {
                        paymentMethod = "TM";
                    }
                    else if (hasTransfer)
                    {
                        paymentMethod = "CK";
                    }
                }
                else
                {
                    // Dự phòng nếu không tìm thấy: mặc định CK
                    paymentMethod = "CK";
                }
            }

            // 2. Map items
            var items = new List<SePayInvoiceItem>();
            int lineNumber = 1;
            foreach (var detail in invoice.InvoiceDetails ?? new List<InvoiceDetail>())
            {
                var itemCode = detail.ProductId.ToString();

                items.Add(new SePayInvoiceItem
                {
                    LineNumber = lineNumber++,
                    LineType = "1", // Hàng hoá dịch vụ bình thường
                    ItemCode = itemCode,
                    ItemName = detail.ProductName,
                    Unit = detail.Product?.Unit ?? "Cái",
                    Quantity = (double)detail.Quantity,
                    UnitPrice = (double)detail.UnitPrice
                });
            }

            var buyerType = !string.IsNullOrEmpty(invoice.BuyerTaxCode) ? "company" : "personal";
            var buyerName = !string.IsNullOrEmpty(invoice.BuyerCompanyName)
                ? invoice.BuyerCompanyName
                : "Khách mua lẻ";

            // 3. Chuẩn bị Request Body
            var requestBody = new SePayCreateInvoiceRequest
            {
                TemplateCode = config.InvoiceTemplateCode ?? "2", // Mặc định hóa đơn bán hàng
                InvoiceSeries = config.Symbol ?? "",
                IssuedDate = DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss"), // Giờ Việt Nam
                Currency = "VND",
                ProviderAccountId = config.ProviderAccountId ?? "",
                ReferenceCode = invoice.InvoiceNumber,
                PaymentMethod = paymentMethod,
                IsDraft = false, // Phát hành chính thức luôn
                Buyer = new SePayBuyer
                {
                    Type = buyerType,
                    Name = buyerName,
                    TaxCode = invoice.BuyerTaxCode,
                    Address = invoice.BuyerAddress,
                    Email = invoice.BuyerEmail
                },
                Items = items,
                TotalAmount = (int)Math.Round(invoice.TotalAmount)
            };

            var url = $"{config.BaseUrl.TrimEnd('/')}/v1/invoices/create";
            _logger.LogInformation("Sending create invoice request to SePay: {Url} for Invoice {InvoiceNumber}", url, invoice.InvoiceNumber);

            var response = await client.PostAsJsonAsync(url, requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("SePay create invoice failed with status {StatusCode}. Content: {Content}", response.StatusCode, errorContent);
                return new EInvoiceResult
                {
                    Success = false,
                    ErrorMessage = $"Lỗi kết nối SePay: HTTP {response.StatusCode}. Chi tiết: {errorContent}"
                };
            }

            var createResult = await response.Content.ReadFromJsonAsync<SePayCreateInvoiceResponse>(cancellationToken: cancellationToken);
            if (createResult == null || !createResult.Success || createResult.Data == null)
            {
                return new EInvoiceResult
                {
                    Success = false,
                    ErrorMessage = createResult?.Message ?? "Không nhận được phản hồi hợp lệ từ SePay khi tạo hóa đơn."
                };
            }

            var trackingCode = createResult.Data.TrackingCode;
            _logger.LogInformation("Invoice request accepted by SePay. Tracking Code: {TrackingCode}", trackingCode);

            // 4. Polling check trạng thái (10 lần, mỗi lần 3s)
            var checkUrl = $"{config.BaseUrl.TrimEnd('/')}/v1/invoices/create/check/{trackingCode}";
            for (int i = 1; i <= 10; i++)
            {
                await Task.Delay(3000, cancellationToken);

                _logger.LogInformation("Checking invoice status (Attempt {Attempt}/10): {Url}", i, checkUrl);
                var checkResponse = await client.GetAsync(checkUrl, cancellationToken);
                if (!checkResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to check invoice status. HTTP {StatusCode}", checkResponse.StatusCode);
                    continue;
                }

                var checkResult = await checkResponse.Content.ReadFromJsonAsync<SePayCheckInvoiceResponse>(cancellationToken: cancellationToken);
                if (checkResult == null || !checkResult.Success || checkResult.Data == null)
                {
                    continue;
                }

                var status = checkResult.Data.Status;
                _logger.LogInformation("Invoice status for Tracking {TrackingCode}: {Status}", trackingCode, status);

                if (status.Equals("Success", StringComparison.OrdinalIgnoreCase))
                {
                    var sePayInvoice = checkResult.Data.Invoice;
                    return new EInvoiceResult
                    {
                        Success = true,
                        TaxAuthorityCode = sePayInvoice?.InvoiceNumber, // Số hóa đơn đỏ chính thức
                        OfficialPdfUrl = sePayInvoice?.PdfUrl,
                        OfficialXmlUrl = sePayInvoice?.XmlUrl,
                        ReferenceCode = invoice.InvoiceNumber,
                        TrackingCode = trackingCode
                    };
                }
                else if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    return new EInvoiceResult
                    {
                        Success = false,
                        TrackingCode = trackingCode,
                        ErrorMessage = checkResult.Data.Message ?? "SePay phản hồi lỗi khi xuất hóa đơn điện tử."
                    };
                }
                // Nếu Pending, tiếp tục loop
            }

            // Hết 10 lần vẫn Pending
            return new EInvoiceResult
            {
                Success = false,
                TrackingCode = trackingCode,
                ErrorMessage = "Hóa đơn đang được xử lý (Pending) trên hệ thống SePay. Vui lòng kiểm tra lại sau."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while issuing invoice {InvoiceNumber} via SePay", invoice.InvoiceNumber);
            return new EInvoiceResult
            {
                Success = false,
                ErrorMessage = $"Lỗi hệ thống: {ex.Message}"
            };
        }
    }

    public async Task<int?> GetQuotaRemainingAsync(EInvoiceConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            if (string.IsNullOrEmpty(token)) return null;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"{config.BaseUrl.TrimEnd('/')}/v1/usage";
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch SePay usage quota. HTTP {StatusCode}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<SePayUsageResponse>(cancellationToken: cancellationToken);
            if (result?.Data?.QuotaRemaining != null)
            {
                return result.Data.QuotaRemaining.Value;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching SePay usage quota");
            return null;
        }
    }

    private async Task<string?> GetAccessTokenAsync(EInvoiceConfig config, CancellationToken cancellationToken)
    {
        var cacheKey = config.ClientId;
        if (TokenCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Token;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var authBytes = Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}");
            var authHeader = Convert.ToBase64String(authBytes);

            var url = $"{config.BaseUrl.TrimEnd('/')}/v1/token";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            _logger.LogInformation("Requesting access token from SePay: {Url}", url);
            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get SePay access token. HTTP {StatusCode}. Content: {Content}", response.StatusCode, errContent);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<SePayTokenResponse>(cancellationToken: cancellationToken);
            if (result == null || !result.Success || result.Data == null)
            {
                _logger.LogError("Invalid response from SePay token API.");
                return null;
            }

            var token = result.Data.AccessToken;
            var expiresIn = result.Data.ExpiresIn;
            var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60); // Buffer 60s

            TokenCache[cacheKey] = (token, expiresAt);
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching SePay access token for ClientId {ClientId}", config.ClientId);
            return null;
        }
    }

    // --- SePay Models ---

    private class SePayTokenResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public SePayTokenData? Data { get; set; }
    }

    private class SePayTokenData
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = null!;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = null!;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private class SePayCreateInvoiceRequest
    {
        [JsonPropertyName("template_code")]
        public string TemplateCode { get; set; } = null!;

        [JsonPropertyName("invoice_series")]
        public string InvoiceSeries { get; set; } = null!;

        [JsonPropertyName("issued_date")]
        public string IssuedDate { get; set; } = null!;

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "VND";

        [JsonPropertyName("provider_account_id")]
        public string ProviderAccountId { get; set; } = null!;

        [JsonPropertyName("reference_code")]
        public string ReferenceCode { get; set; } = null!;

        [JsonPropertyName("payment_method")]
        public string PaymentMethod { get; set; } = null!;

        [JsonPropertyName("is_draft")]
        public bool IsDraft { get; set; }

        [JsonPropertyName("buyer")]
        public SePayBuyer Buyer { get; set; } = null!;

        [JsonPropertyName("items")]
        public List<SePayInvoiceItem> Items { get; set; } = null!;

        [JsonPropertyName("total_amount")]
        public int? TotalAmount { get; set; }
    }

    private class SePayBuyer
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "personal";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "Khách mua lẻ";

        [JsonPropertyName("tax_code")]
        public string? TaxCode { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    private class SePayInvoiceItem
    {
        [JsonPropertyName("line_number")]
        public int LineNumber { get; set; }

        [JsonPropertyName("line_type")]
        public string LineType { get; set; } = "1";

        [JsonPropertyName("item_code")]
        public string ItemCode { get; set; } = null!;

        [JsonPropertyName("item_name")]
        public string ItemName { get; set; } = null!;

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("quantity")]
        public double Quantity { get; set; }

        [JsonPropertyName("unit_price")]
        public double UnitPrice { get; set; }
    }

    private class SePayCreateInvoiceResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public SePayCreateInvoiceData? Data { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private class SePayCreateInvoiceData
    {
        [JsonPropertyName("tracking_code")]
        public string TrackingCode { get; set; } = null!;

        [JsonPropertyName("tracking_url")]
        public string TrackingUrl { get; set; } = null!;

        [JsonPropertyName("message")]
        public string Message { get; set; } = null!;
    }

    private class SePayCheckInvoiceResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public SePayCheckInvoiceData? Data { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private class SePayCheckInvoiceData
    {
        [JsonPropertyName("reference_code")]
        public string ReferenceCode { get; set; } = null!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = null!; // "Success", "Failed", "Pending"

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("invoice")]
        public SePayInvoiceDetail? Invoice { get; set; }
    }

    private class SePayInvoiceDetail
    {
        [JsonPropertyName("reference_code")]
        public string ReferenceCode { get; set; } = null!;

        [JsonPropertyName("invoice_number")]
        public string InvoiceNumber { get; set; } = null!;

        [JsonPropertyName("issued_date")]
        public string IssuedDate { get; set; } = null!;

        [JsonPropertyName("pdf_url")]
        public string PdfUrl { get; set; } = null!;

        [JsonPropertyName("xml_url")]
        public string? XmlUrl { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = null!;

        [JsonPropertyName("total_amount")]
        public decimal TotalAmount { get; set; }
    }

    private class SePayUsageResponse
    {
        [JsonPropertyName("data")]
        public SePayUsageData? Data { get; set; }
    }

    private class SePayUsageData
    {
        [JsonPropertyName("quota_remaning")]
        public int? QuotaRemaining { get; set; }
    }
}
