namespace InnovaTube.Api.DTOs.Auth;

public class ResetPasswordRequest
{
    // Código combinado: "resetId:token"
    public string Code { get; set; } = default!;
    public string NewPassword { get; set; } = default!;
}
