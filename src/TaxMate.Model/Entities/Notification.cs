using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class Notification : BaseEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    [MaxLength(100)]
    public string? ReferenceId { get; set; }

    [MaxLength(100)]
    public string? ReferenceType { get; set; }
    
    public User User { get; set; } = null!;
}