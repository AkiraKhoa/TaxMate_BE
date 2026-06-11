namespace TaxMate.Model.DTO;

public class OrderPaymentResponse
{
    public Guid PaymentId { get; set; }
    public string PaymentMethod { get; set; } = null!;
    public decimal Amount { get; set; }
    public Guid? PaymentAccountId { get; set; }
    public string? BankName { get; set; }
    public DateTime? PaidAt { get; set; }
}
