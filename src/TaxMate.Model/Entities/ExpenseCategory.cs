using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.Entities;

public class ExpenseCategory
{
    public Guid ExpenseCategoryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Expense> Expenses { get; set; }
        = new List<Expense>();
}