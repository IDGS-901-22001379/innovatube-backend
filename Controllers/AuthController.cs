using System.Linq;
using InnovaTube.Api.DTOs.Auth;
using InnovaTube.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InnovaTube.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // ================== REGISTER ==================
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();

            try
            {
                var result = await _authService.RegisterAsync(request, ip, userAgent);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ================== LOGIN ==================
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();

            try
            {
                var result = await _authService.LoginAsync(request, ip, userAgent);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // credenciales incorrectas u otro error -> 401
                return Unauthorized(new { message = ex.Message });
            }
        }

        // ================== REFRESH TOKEN ==================
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();

            try
            {
                var result = await _authService.RefreshTokenAsync(
                    request.RefreshToken,
                    ip,
                    userAgent);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        // ================== FORGOT PASSWORD ==================
        // Solicita la recuperación: genera un código y (en esta versión) lo devuelve en la respuesta
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();

            try
            {
                var code = await _authService.ForgotPasswordAsync(request, ip, userAgent);

                // En producción se enviaría por correo.
                // Aquí lo devolvemos para que lo puedas probar desde Swagger.
                return Ok(new
                {
                    message = "Si el usuario existe, se ha generado un enlace de recuperación.",
                    resetCode = code
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ================== RESET PASSWORD ==================
        // Aplica la nueva contraseña usando el código de recuperación
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();

            try
            {
                var result = await _authService.ResetPasswordAsync(request, ip, userAgent);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ================== LOGOUT ==================
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromQuery] long sessionId)
        {
            // "sub" viene del claim que pusimos en el JWT (userId)
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub");

            if (subClaim == null || !int.TryParse(subClaim.Value, out var userId))
                return Unauthorized(new { message = "No se pudo obtener el usuario del token." });

            await _authService.LogoutAsync(sessionId, userId);
            return NoContent(); // 204
        }
    }
}
