using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Product : BaseEntity
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    public ProductCategory? Category { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = ProductStatus.Active;

    public BusinessProfile Business { get; set; } = null!;

    public ICollection<ProductPrice> ProductPrices { get; set; }
        = new List<ProductPrice>();

    public ICollection<InvoiceDetail> InvoiceDetails { get; set; }
        = new List<InvoiceDetail>();

    public ICollection<ProductIngredient> ProductIngredients { get; set; }
        = new List<ProductIngredient>();
}