using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class Ingredient
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Precision(18,2)]
    public decimal? EstimatedPrice { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<ProductIngredient> ProductIngredients { get; set; }
        = new List<ProductIngredient>();

    public ICollection<IngredientPurchase> IngredientPurchases { get; set; }
        = new List<IngredientPurchase>();
}