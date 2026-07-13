using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxMate.Model.DTO;

public class PayOsWebhookRequest
{
    [JsonPropertyName("code")] public string Code { get; set; } = null!;
    [JsonPropertyName("desc")] public string Desc { get; set; } = null!;
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("data")] public PayOsData Data { get; set; } = null!;
    [JsonPropertyName("signature")] public string Signature { get; set; } = null!;
}

public class PayOsData
{
    [JsonPropertyName("orderCode")] public long OrderCode { get; set; }
    [JsonPropertyName("amount")] public int Amount { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = null!;
    [JsonPropertyName("accountNumber")] public string AccountNumber { get; set; } = null!;
    [JsonPropertyName("reference")] public string Reference { get; set; } = null!;
    [JsonPropertyName("transactionDateTime")] public string TransactionDateTime { get; set; } = null!;
    [JsonPropertyName("currency")] public string Currency { get; set; } = null!;
    [JsonPropertyName("paymentLinkId")] public string PaymentLinkId { get; set; } = null!;
}

/// <summary>
/// DTO cho SePay IPN biến động số dư (endpoint /sepay).
/// Auth: Authorization: Apikey &lt;SePay:ApiKey&gt;
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
/// Auth: X-Secret-Key: &lt;SePay:BankHub:SecretKey&gt;
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

public class CassoWebhookRequest
{
    [JsonPropertyName("error")] public int Error { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = null!;
    [JsonPropertyName("data")] public List<CassoData> Data { get; set; } = null!;
}

public class CassoData
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("tid")] public string Tid { get; set; } = null!;
    [JsonPropertyName("description")] public string Description { get; set; } = null!;
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("cusumBalance")] public decimal CusumBalance { get; set; }
    [JsonPropertyName("when")] public string When { get; set; } = null!;
    [JsonPropertyName("bookingDate")] public string BookingDate { get; set; } = null!;
    [JsonPropertyName("bankSubAccId")] public string BankSubAccId { get; set; } = null!;
    [JsonPropertyName("correspName")] public string CorrespName { get; set; } = null!;
    [JsonPropertyName("correspAccId")] public string CorrespAccId { get; set; } = null!;
    [JsonPropertyName("correspBankName")] public string CorrespBankName { get; set; } = null!;
    [JsonPropertyName("correspBankId")] public string CorrespBankId { get; set; } = null!;
}
