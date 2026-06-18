namespace TaxMate.Model.DTO;

public class CreateIngredientPurchaseRequest
{
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime PurchaseDate { get; set; }
}

public class UpdateIngredientPurchaseRequest
{
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime PurchaseDate { get; set; }
}

public class IngredientPurchaseResponse
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = null!;
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = null!;
    public string? IngredientUnit { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
