using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.Entities;

public class User
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [MaxLength(20)]
    public string? TaxCode { get; set; }

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = null!;

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

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<BusinessProfile> BusinessProfiles { get; set; }
        = new List<BusinessProfile>();

    public ICollection<Notification> Notifications { get; set; }
        = new List<Notification>();

    public ICollection<UserSubscription> UserSubscriptions { get; set; }
        = new List<UserSubscription>();
}