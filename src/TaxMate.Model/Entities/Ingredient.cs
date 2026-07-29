using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Ingredient : BaseEntity
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [MaxLength(50)]
    public string? Unit { get; set; }

    public decimal? EstimatedPrice { get; set; }

    public decimal StockQuantity { get; set; } = 0;

    public bool IsDeleted { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public ICollection<ProductIngredient> ProductIngredients { get; set; }
        = new List<ProductIngredient>();

    public ICollection<IngredientPurchase> IngredientPurchases { get; set; }
        = new List<IngredientPurchase>();
}