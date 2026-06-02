using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class IngredientPurchase
{
    public Guid Id { get; set; }

    public Guid IngredientId { get; set; }

    [Precision(18,3)]
    public decimal Quantity { get; set; }

    [Precision(18,2)]
    public decimal TotalCost { get; set; }

    public DateTime PurchaseDate { get; set; }

    public Ingredient Ingredient { get; set; } = null!;
}