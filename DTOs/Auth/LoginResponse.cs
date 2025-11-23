namespace InnovaTube.Api.DTOs.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
}
