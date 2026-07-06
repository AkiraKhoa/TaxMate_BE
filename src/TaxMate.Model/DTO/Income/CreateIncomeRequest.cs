using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Income;

public class CreateIncomeRequest
{
    public Guid IncomeCategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    public string IncomeTitle { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime IncomeDate { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [MaxLength(1000)]
    public string? ReceiptImageUrl { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [MaxLength(1000)]
    public string? FileUrl { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? ReceivedDate { get; set; }
}
