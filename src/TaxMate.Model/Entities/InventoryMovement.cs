using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class InventoryMovement : BaseEntity
{
    public Guid InventoryMovementId { get; set; }

    public Guid BusinessId { get; set; }

    public Guid? ProductId { get; set; }

    public Guid? IngredientId { get; set; }

    [Required]
    [MaxLength(30)]
    public string MovementType { get; set; } = null!;

    [Precision(18, 6)]
    public decimal Quantity { get; set; }

    [Precision(20, 2)]
    public decimal? TotalValue { get; set; }

    public DateTime OccurredAt { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = null!;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = null!;

    public Guid? ReferenceId { get; set; }

    public BusinessProfile Business { get; set; } = null!;

    public Product? Product { get; set; }

    public Ingredient? Ingredient { get; set; }
}
