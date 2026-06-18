namespace TaxMate.Model.DTO;

public class CreateProductPriceRequest
{
    public decimal Price { get; set; }
    public DateTime ApplyDate { get; set; }
}

public class UpdateProductPriceRequest
{
    public decimal Price { get; set; }
    public DateTime ApplyDate { get; set; }
}

public class ProductPriceResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public decimal Price { get; set; }
    public DateTime ApplyDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
