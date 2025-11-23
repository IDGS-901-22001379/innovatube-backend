namespace InnovaTube.Api.DTOs.Auth;

public class ForgotPasswordRequest
{
    // Puede ser correo o username
    public string Identifier { get; set; } = default!;
}
