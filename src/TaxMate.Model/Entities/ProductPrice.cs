using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class ProductPrice : BaseEntity
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    [Precision(18,2)]
    public decimal Price { get; set; }

    public DateTime ApplyDate { get; set; }

    public Product Product { get; set; } = null!;
}