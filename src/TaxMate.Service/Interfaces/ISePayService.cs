using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TaxMate.Service.Interfaces;

public class SePayTokenResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonPropertyName("ttl")]
    public int Ttl { get; set; }
}

public class SePayCompanyResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;

    [JsonPropertyName("data")]
    public SePayCompanyData Data { get; set; } = null!;
}

public class SePayCompanyData
{
    [JsonPropertyName("xid")]
    public string Xid { get; set; } = null!;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = null!;
}

public class SePayLinkTokenResponse
{
    [JsonPropertyName("xid")]
    public string Xid { get; set; } = null!;

    [JsonPropertyName("hosted_link_url")]
    public string HostedLinkUrl { get; set; } = null!;

    [JsonPropertyName("link_token")]
    public string LinkToken { get; set; } = null!;
}

/// <summary>Một tài khoản ngân hàng đã liên kết từ SePay Bank Hub API.</summary>
public class SePayBankAccountDto
{
    [JsonPropertyName("xid")]
    public string Xid { get; set; } = null!;

    [JsonPropertyName("company_xid")]
    public string CompanyXid { get; set; } = null!;

    [JsonPropertyName("brand_name")]
    public string BrandName { get; set; } = null!;

    [JsonPropertyName("account_holder_name")]
    public string AccountHolderName { get; set; } = null!;

    [JsonPropertyName("account_number")]
    public string AccountNumber { get; set; } = null!;

    [JsonPropertyName("account_type")]
    public string AccountType { get; set; } = null!;
}

public class SePayBankAccountListResponse
{
    [JsonPropertyName("data")]
    public List<SePayBankAccountDto> Data { get; set; } = new();
}

public interface ISePayService
{
    Task<string> GetOrCreateCompanyXidAsync(Guid businessId, string businessName);

    /// <summary>
    /// Tạo link token và trả về URL hosted link + xid của link token để lưu vào DB.
    /// </summary>
    Task<(string Url, string LinkTokenXid)> GenerateHostedLinkUrlAsync(string companyXid, string redirectUri, string purpose = "LINK_BANK_ACCOUNT", string? bankAccountXid = null);

    /// <summary>
    /// Tạo hosted link kết nối SePay Bank Hub, lưu linkTokenXid vào DB và đăng ký webhook.
    /// </summary>
    Task<string> GetSePayConnectUrlAsync(Guid businessId, string scheme, string host);



    /// <summary>Gọi SePay GET /v1/bank-account để lấy danh sách tài khoản đã liên kết của company.</summary>
    Task<List<SePayBankAccountDto>> GetLinkedBankAccountsAsync(string companyXid);


    /// <summary>
    /// Đăng ký Webhook URL với SePay Bank Hub qua POST /v1/webhook.
    /// Cần gọi mỗi khi webhook URL thay đổi (ví dụ: sau khi khởi động ngrok mới).
    /// </summary>
    Task RegisterWebhookAsync(string webhookUrl, string secretKey);

    /// <summary>
    /// Giả lập một giao dịch chuyển khoản mới ở Sandbox (để test webhook IPN).
    /// </summary>
    Task CreateMockTransactionAsync(string bankAccountXid, decimal amount, string content);
}

