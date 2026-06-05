namespace TaxMate.Model.DTO.Auth;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string AccountStatus { get; set; } = null!;
    public string Role { get; set; } = null!;
}
