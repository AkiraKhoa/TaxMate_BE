using TaxMate.Model.Common;

namespace TaxMate.Model.DTO;

public class CreateProductRequest
{
    public string Name { get; set; } = null!;
    public ProductCategory? Category { get; set; }
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public string? ImageUrl { get; set; }
}

public class UpdateProductRequest
{
    public string Name { get; set; } = null!;
    public ProductCategory? Category { get; set; }
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public string? ImageUrl { get; set; }
}

public class ProductResponse
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = null!;
    public ProductCategory? Category { get; set; }
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = null!;
    public decimal? CurrentPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
