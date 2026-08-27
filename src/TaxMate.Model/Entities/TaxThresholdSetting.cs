using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxThresholdSetting : BaseEntity
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = null!;

    [Precision(18, 2)]
    public decimal Amount { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
