namespace TaxMate.Model.DTO;

public class PaymentAccountResponse
{
    public Guid PaymentAccountId { get; set; }
    public Guid BusinessId { get; set; }
    public string AccountType { get; set; } = null!;
    public string? BankShortName { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public decimal? InitialBalance { get; set; }
    public DateOnly? InitialBalanceDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public string? Description { get; set; }
    public string? CassoConnectedAccountId { get; set; }
    public string? SePayBankAccountXid { get; set; }
    public bool IsSePayConnected => !string.IsNullOrEmpty(SePayBankAccountXid);
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

