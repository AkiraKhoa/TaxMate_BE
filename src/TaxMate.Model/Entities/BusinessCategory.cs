using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class BusinessCategory : BaseEntity
{
    public Guid BusinessCategoryId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Precision(5,2)]
    public decimal VatRate { get; set; }

    [Precision(5,2)]
    public decimal PitRate { get; set; }
    
    public ICollection<BusinessProfile> BusinessProfiles { get; set; }
        = new List<BusinessProfile>();

    public ICollection<Product> Products { get; set; }
        = new List<Product>();
}