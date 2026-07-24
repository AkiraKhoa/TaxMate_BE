using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxObligationConfiguration : BaseEntity
{
    public Guid Id { get; set; }

    [MaxLength(50)]
    public string TaxType { get; set; } = null!;

    [MaxLength(50)]
    public string ChapterCode { get; set; } = null!;

    [MaxLength(50)]
    public string SubsectionCode { get; set; } = null!;

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }
}