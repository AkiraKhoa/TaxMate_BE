using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TransactionItem : BaseEntity
{
    public Guid TransactionItemId { get; set; }

    public Guid TransactionId { get; set; }

    public Guid? ProductId { get; set; }

    [Required]
    [MaxLength(300)]
    public string ProductName { get; set; } = null!;

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Precision(18, 2)]
    public decimal UnitPrice { get; set; }

    [Precision(18, 3)]
    public decimal Quantity { get; set; }

    [MaxLength(20)]
    public string? DiscountType { get; set; }

    [Precision(18, 2)]
    public decimal? DiscountValue { get; set; }

    [Precision(18, 2)]
    public decimal DiscountAmount { get; set; }

    [Precision(18, 2)]
    public decimal LineTotal { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public Transaction Transaction { get; set; } = null!;
    public Product? Product { get; set; }
}
