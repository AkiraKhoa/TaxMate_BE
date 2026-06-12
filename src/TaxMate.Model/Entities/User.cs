using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class User : BaseEntity
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [MaxLength(20)]
    public string? TaxCode { get; set; }

    [MaxLength(500)]
    public string? PasswordHash { get; set; }

    [MaxLength(100)]
    public string? GoogleId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = null!;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "Owner";

    [MaxLength(1000)]
    public string? AvatarUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string AccountStatus { get; set; } = Common.AccountStatus.Active;

    [MaxLength(128)]
    public string? EmailVerificationToken { get; set; }

    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    public ICollection<BusinessProfile> BusinessProfiles { get; set; }
        = new List<BusinessProfile>();

    public ICollection<Notification> Notifications { get; set; }
        = new List<Notification>();

    public ICollection<UserSubscription> UserSubscriptions { get; set; }
        = new List<UserSubscription>();
}
