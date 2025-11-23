namespace InnovaTube.Api.DTOs.Auth;

public class LoginRequest
{
    public string Identifier { get; set; } = ""; // username o email
    public string Password { get; set; } = "";
}
