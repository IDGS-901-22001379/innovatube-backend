using InnovaTube.Api.DTOs.Auth;

namespace InnovaTube.Api.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> RegisterAsync(
            RegisterRequest request,
            string ip,
            string userAgent);

        Task<LoginResponse> LoginAsync(
            LoginRequest request,
            string ip,
            string userAgent);

        Task<LoginResponse> RefreshTokenAsync(
            string refreshToken,
            string ip,
            string userAgent);

        Task LogoutAsync(long sessionId, int userId);

        // ========== RECUPERACIÓN DE CONTRASEÑA ==========

        // 1) Usuario pide recuperar contraseña (devuelves el código en esta versión)
        Task<string> ForgotPasswordAsync(
            ForgotPasswordRequest request,
            string ip,
            string userAgent);

        // 2) Usuario manda código + nueva contraseña
        Task<LoginResponse> ResetPasswordAsync(
            ResetPasswordRequest request,
            string ip,
            string userAgent);
    }
}
