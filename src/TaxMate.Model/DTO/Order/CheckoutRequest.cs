using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

public class CheckoutRequest
{
    [Required]
    public List<PaymentEntry> Payments { get; set; } = new();
}

public class PaymentEntry
{
    [Required]
    public string PaymentMethod { get; set; } = null!;

    [Required]
    public decimal Amount { get; set; }

    public Guid? PaymentAccountId { get; set; }
}
