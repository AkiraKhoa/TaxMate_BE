using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class ExpenseCategory : BaseEntity
{
    public Guid ExpenseCategoryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public Guid? BusinessId { get; set; }

    public BusinessProfile? Business { get; set; }

    public ICollection<Expense> Expenses { get; set; }
        = new List<Expense>();
}