using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Product : BaseEntity
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ProductCode { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    public Guid? ProductCategoryId { get; set; }
    public ProductCategory? ProductCategory { get; set; }

    public Guid? BusinessCategoryId { get; set; }
    public BusinessCategory? BusinessCategory { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    public decimal? CostPrice { get; set; }

    public decimal? StockQuantity { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = ProductStatus.Active;

    public bool IsDeleted { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public ICollection<ProductPrice> ProductPrices { get; set; }
        = new List<ProductPrice>();

    public ICollection<InvoiceDetail> InvoiceDetails { get; set; }
        = new List<InvoiceDetail>();

    public ICollection<ProductIngredient> ProductIngredients { get; set; }
        = new List<ProductIngredient>();
}