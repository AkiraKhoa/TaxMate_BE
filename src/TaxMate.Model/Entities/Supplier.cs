using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Supplier : BaseEntity
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public ICollection<IngredientPurchase> IngredientPurchases { get; set; }
        = new List<IngredientPurchase>();

    public ICollection<Expense> Expenses { get; set; }
        = new List<Expense>();
}
