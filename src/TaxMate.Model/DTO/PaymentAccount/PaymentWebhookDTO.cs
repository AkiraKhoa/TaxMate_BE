using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxMate.Model.DTO;

/// <summary>
/// DTO cho SePay IPN biến động số dư (endpoint /sepay).
/// Auth: Authorization: Apikey <SePay:ApiKey>
/// Ref: https://developers.sepay.vn/
/// </summary>
public class SePayWebhookRequest
{
    // Trường chuẩn IPN — snake_case theo payload thực tế của SePay
    [JsonPropertyName("gateway")] public string? Gateway { get; set; }
    [JsonPropertyName("transaction_date")] public string? TransactionDate { get; set; }
    [JsonPropertyName("account_number")] public string? AccountNumber { get; set; }
    [JsonPropertyName("bank_account_xid")] public string? BankAccountXid { get; set; }
    [JsonPropertyName("va")] public string? Va { get; set; }
    [JsonPropertyName("payment_code")] public string? PaymentCode { get; set; }
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("transfer_type")] public string? TransferType { get; set; } // "credit" / "debit"
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("reference_code")] public string? ReferenceCode { get; set; }
    [JsonPropertyName("accumulated")] public decimal Accumulated { get; set; }
    [JsonPropertyName("transaction_id")] public string? TransactionId { get; set; }
}

/// <summary>
/// DTO cho SePay Bank Hub Webhook events (endpoint /bankhub).
/// Auth: X-Secret-Key: <SePay:BankHub:SecretKey>
/// Events: BANK_ACCOUNT_LINKED, BANK_ACCOUNT_UNLINKED, BANK_ACCOUNT_INACTIVATED
/// Ref: https://developer.sepay.vn/en/bankhub
/// </summary>
public class SePayBankHubEventRequest
{
    [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
    [JsonPropertyName("xid")] public string? Xid { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("event")] public string? Event { get; set; }
    [JsonPropertyName("metadata")] public SePayBankHubMetadata? Metadata { get; set; }
}

public class SePayBankHubMetadata
{
    [JsonPropertyName("bank_account_xid")] public string? BankAccountXid { get; set; }
    [JsonPropertyName("account_number")] public string? AccountNumber { get; set; }
    [JsonPropertyName("account_holder_name")] public string? AccountHolderName { get; set; }
    [JsonPropertyName("brand_name")] public string? BrandName { get; set; }
    [JsonPropertyName("account_type")] public string? AccountType { get; set; }
    // Dùng để trace lại companyXid khi không có company_xid trực tiếp trong payload
    [JsonPropertyName("link_token_xid")] public string? LinkTokenXid { get; set; }
    [JsonPropertyName("link_session_xid")] public string? LinkSessionXid { get; set; }
}
