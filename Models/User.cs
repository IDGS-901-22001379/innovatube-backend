namespace InnovaTube.Api.Models;

public class User
{
    public uint UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public bool IsActive { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
