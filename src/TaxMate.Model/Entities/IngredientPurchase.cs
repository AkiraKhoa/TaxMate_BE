using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class IngredientPurchase : BaseEntity
{
    public Guid Id { get; set; }

    public Guid IngredientId { get; set; }

    public Guid BusinessId { get; set; }

    [Precision(18,3)]
    public decimal Quantity { get; set; }

    [Precision(18,2)]
    public decimal TotalCost { get; set; }

    public DateTime PurchaseDate { get; set; }

    public Ingredient Ingredient { get; set; } = null!;

    public BusinessProfile Business { get; set; } = null!;
}