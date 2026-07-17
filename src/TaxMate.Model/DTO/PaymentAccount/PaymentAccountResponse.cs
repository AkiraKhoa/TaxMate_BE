namespace TaxMate.Model.DTO;

public class PaymentAccountResponse
{
    public Guid PaymentAccountId { get; set; }
    public Guid BusinessId { get; set; }
    public string BankShortName { get; set; } = null!;
    public string BankName { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public bool IsDefault { get; set; }
    public string? Description { get; set; }
    public string? CassoConnectedAccountId { get; set; }
    public string? SePayBankAccountXid { get; set; }
    public bool IsSePayConnected => !string.IsNullOrEmpty(SePayBankAccountXid);
    public DateTime CreatedAt { get; set; }
}

