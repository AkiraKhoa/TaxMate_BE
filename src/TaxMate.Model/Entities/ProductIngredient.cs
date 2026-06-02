using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class ProductIngredient
{
    public Guid ProductId { get; set; }

    public Guid IngredientId { get; set; }

    [Precision(18,3)]
    public decimal Quantity { get; set; }

    public Product Product { get; set; } = null!;

    public Ingredient Ingredient { get; set; } = null!;
}