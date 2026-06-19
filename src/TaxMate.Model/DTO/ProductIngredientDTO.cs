namespace TaxMate.Model.DTO;

public class AddProductIngredientRequest
{
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
}

public class UpdateProductIngredientRequest
{
    public decimal Quantity { get; set; }
}

public class ProductIngredientResponse
{
    public Guid ProductId { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
}
