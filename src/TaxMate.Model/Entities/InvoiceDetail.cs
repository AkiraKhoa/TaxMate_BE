using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class InvoiceDetail
{
    public Guid ProductId { get; set; }

    [MaxLength(50)]
    public string InvoiceId { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string ProductName { get; set; } = null!;

    [Precision(18,2)]
    public decimal UnitPrice { get; set; }

    [Precision(18,3)]
    public decimal Quantity { get; set; }

    [Precision(18,2)]
    public decimal LineTotal { get; set; }

    public Product Product { get; set; } = null!;

    public Invoice Invoice { get; set; } = null!;
}