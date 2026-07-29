using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.DTO;

public class CreateProductRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;
    public Guid? ProductCategoryId { get; set; }
    [MaxLength(2000)]
    public string? Description { get; set; }
    [MaxLength(50)]
    public string? Unit { get; set; }
    [MaxLength(1000)]
    public string? ImageUrl { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? StockQuantity { get; set; }
}

public class UpdateProductRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;
    public Guid? ProductCategoryId { get; set; }
    [MaxLength(2000)]
    public string? Description { get; set; }
    [MaxLength(50)]
    public string? Unit { get; set; }
    [MaxLength(1000)]
    public string? ImageUrl { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? StockQuantity { get; set; }
}

public class UpdateProductCostPriceRequest
{
    [Required]
    public decimal IncomingQuantity { get; set; }

    [Required]
    public decimal IncomingCostPrice { get; set; }
}

public class ProductResponse
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = null!;
    public Guid? ProductCategoryId { get; set; }
    public string? ProductCategoryName { get; set; }
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = null!;
    public decimal? CurrentPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? StockQuantity { get; set; }
    public bool HasRecipe { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
