using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class ProductPrice
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    [Precision(18,2)]
    public decimal Price { get; set; }

    public DateTime ApplyDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Product Product { get; set; } = null!;
}