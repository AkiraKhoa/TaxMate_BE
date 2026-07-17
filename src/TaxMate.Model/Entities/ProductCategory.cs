using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class ProductCategory : BaseEntity
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; } = 0;

    public BusinessProfile Business { get; set; } = null!;

    public ICollection<Product> Products { get; set; }
        = new List<Product>();
}
