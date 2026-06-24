using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.ExpenseCategory;

public class UpdateExpenseCategoryRequest
{
    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }
}
