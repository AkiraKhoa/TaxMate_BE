using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class TaxPeriod
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(20)]
    public string PeriodType { get; set; } = null!;

    public int Year { get; set; }

    public int? Month { get; set; }

    public int? Quarter { get; set; }

    [Precision(18,2)]
    public decimal TotalRevenue { get; set; }

    [Precision(18,2)]
    public decimal TaxableRevenue { get; set; }

    [Precision(18,2)]
    public decimal EstimatedTax { get; set; }

    [Precision(18,2)]
    public decimal TaxAmountDebt { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Open";

    public DateTime? DueDate { get; set; }

    public DateTime? PaidDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public ICollection<TaxPayment> TaxPayments { get; set; }
        = new List<TaxPayment>();
}