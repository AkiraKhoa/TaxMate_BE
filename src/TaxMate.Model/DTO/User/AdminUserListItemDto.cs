namespace TaxMate.Model.DTO.User;

public class AdminUserListItemDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string AccountStatus { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? TaxCode { get; set; }
    public string? Phone { get; set; }
    public bool HasProfileInfo { get; set; }
    public int BusinessProfileCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
