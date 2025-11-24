using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using InnovaTube.Api.DTOs.Auth;
using InnovaTube.Api.Infrastructure;
using InnovaTube.Api.Security;
using InnovaTube.Api.Services.Interfaces;
using MySqlConnector;

namespace InnovaTube.Api.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly MySqlConnectionFactory _factory;
        private readonly JwtTokenService _jwt;
        private readonly IEmailService _emailService;

        public AuthService(
            MySqlConnectionFactory factory,
            JwtTokenService jwt,
            IEmailService emailService)
        {
            _factory = factory;
            _jwt = jwt;
            _emailService = emailService;
        }

        // =====================================================
        // REGISTER
        // =====================================================
        public async Task<LoginResponse> RegisterAsync(
            RegisterRequest request,
            string ip,
            string userAgent)
        {
            // 1) Hashear contraseña (BCrypt -> string -> bytes para VARBINARY)
            var hashString = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var hashBytes = Encoding.UTF8.GetBytes(hashString);

            int userId;
            string username;
            string email;

            // Usamos la conexión SOLO para el SP de registro
            using (var conn = (MySqlConnection)_factory.CreateConnection())
            {
                await conn.OpenAsync();

                using (var cmd = new MySqlCommand("sp_register_user", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("p_first_name", request.FirstName);
                    cmd.Parameters.AddWithValue("p_last_name", request.LastName);
                    cmd.Parameters.AddWithValue("p_username", request.Username);
                    cmd.Parameters.AddWithValue("p_email", request.Email);
                    cmd.Parameters.AddWithValue("p_password_hash", hashBytes);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            throw new Exception("No se pudo registrar el usuario.");

                        userId = reader.GetInt32("user_id");
                        username = reader.GetString("username");
                        email = reader.GetString("email");
                    } // reader cerrado aquí
                }     // cmd cerrado aquí
            }         // conexión cerrada aquí

            // 2) Crear refresh token y sesión con otra conexión
            var (refreshToken, sessionId) = await CreateSessionAsync(userId, ip, userAgent);

            // 3) Generar access token JWT
            var accessToken = _jwt.GenerateAccessToken(userId, username, email);

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = userId,
                Username = username,
                Email = email
            };
        }

        // =====================================================
        // LOGIN
        // =====================================================
        public async Task<LoginResponse> LoginAsync(
            LoginRequest request,
            string ip,
            string userAgent)
        {
            int userId;
            string username;
            string email;
            bool isActive;
            byte[] storedHashBytes;

            // 1) Obtener usuario por username/email
            using (var conn = (MySqlConnection)_factory.CreateConnection())
            {
                await conn.OpenAsync();

                using (var cmd = new MySqlCommand("sp_login_get_user", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("p_identifier", request.Identifier);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            throw new Exception("Usuario o contraseña incorrectos.");

                        userId = reader.GetInt32("user_id");
                        username = reader.GetString("username");
                        email = reader.GetString("email");
                        isActive = reader.GetBoolean("is_active");
                        storedHashBytes = (byte[])reader["password_hash"];
                    } // reader cerrado aquí
                }     // cmd cerrado aquí
            }         // conexión cerrada aquí

            if (!isActive)
                throw new Exception("La cuenta está desactivada.");

            var storedHashString = Encoding.UTF8.GetString(storedHashBytes);

            if (!BCrypt.Net.BCrypt.Verify(request.Password, storedHashString))
                throw new Exception("Usuario o contraseña incorrectos.");

            // 2) Crear sesión y refresh token (otra conexión nueva)
            var (refreshToken, sessionId) = await CreateSessionAsync(userId, ip, userAgent);

            // 3) Generar access token
            var accessToken = _jwt.GenerateAccessToken(userId, username, email);

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = userId,
                Username = username,
                Email = email
            };
        }

        // =====================================================
        // REFRESH TOKEN
        // =====================================================
        public async Task<LoginResponse> RefreshTokenAsync(
            string refreshToken,
            string ip,
            string userAgent)
        {
            int userId = 0;
            string username = string.Empty;
            string email = string.Empty;
            long oldSessionId = 0;
            bool found = false;

            // 1) Buscar la sesión cuyo hash coincida con el refreshToken recibido
            using (var conn = (MySqlConnection)_factory.CreateConnection())
            {
                await conn.OpenAsync();

                // Obtenemos sesiones activas y no expiradas
                using (var cmd = new MySqlCommand(@"
                    SELECT s.session_id,
                           s.user_id,
                           s.refresh_token_hash,
                           u.username,
                           u.email
                    FROM user_sessions s
                    JOIN users u ON u.user_id = s.user_id
                    WHERE s.revoked_at IS NULL
                      AND s.expires_at > NOW();", conn))
                {
                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var storedHashBytes = (byte[])reader["refresh_token_hash"];
                        var storedHashString = Encoding.UTF8.GetString(storedHashBytes);

                        if (BCrypt.Net.BCrypt.Verify(refreshToken, storedHashString))
                        {
                            oldSessionId = reader.GetInt64("session_id");
                            userId = reader.GetInt32("user_id");
                            username = reader.GetString("username");
                            email = reader.GetString("email");
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                    throw new Exception("Refresh token inválido o expirado.");

                // 2) Revocar la sesión anterior
                using (var revokeCmd = new MySqlCommand(@"
                    UPDATE user_sessions
                       SET revoked_at = NOW()
                     WHERE session_id = @sessionId;", conn))
                {
                    revokeCmd.Parameters.AddWithValue("@sessionId", oldSessionId);
                    await revokeCmd.ExecuteNonQueryAsync();
                }
            } // conexión cerrada aquí

            // 3) Crear nueva sesión y nuevo refresh token
            var (newRefreshToken, newSessionId) = await CreateSessionAsync(userId, ip, userAgent);

            // 4) Generar nuevo access token
            var accessToken = _jwt.GenerateAccessToken(userId, username, email);

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                UserId = userId,
                Username = username,
                Email = email
            };
        }

        // =====================================================
        // LOGOUT
        // =====================================================
        public async Task LogoutAsync(long sessionId, int userId)
        {
            using var conn = (MySqlConnection)_factory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_revoke_session", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("p_session_id", sessionId);
            cmd.Parameters.AddWithValue("p_user_id", userId);

            await cmd.ExecuteNonQueryAsync();
        }

        // =====================================================
        // Crear sesión + refresh token
        // =====================================================
        private async Task<(string refreshToken, long sessionId)> CreateSessionAsync(
            int userId,
            string ip,
            string userAgent)
        {
            // Refresh token plano
            var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            // Hash del refresh token para guardar en DB (VARBINARY)
            var refreshTokenHashString = BCrypt.Net.BCrypt.HashPassword(refreshToken);
            var refreshTokenHashBytes = Encoding.UTF8.GetBytes(refreshTokenHashString);

            using var conn = (MySqlConnection)_factory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_create_session", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("p_user_id", userId);
            cmd.Parameters.AddWithValue("p_refresh_token_hash", refreshTokenHashBytes);
            cmd.Parameters.AddWithValue("p_user_agent", userAgent);
            cmd.Parameters.AddWithValue("p_ip_address", ip);
            cmd.Parameters.AddWithValue("p_expires_at", DateTime.UtcNow.AddDays(7));

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new Exception("No se pudo crear la sesión.");

            long sessionId = reader.GetInt64("session_id");

            return (refreshToken, sessionId);
        }

        // =====================================================
        // FORGOT PASSWORD (solicitar recuperación)
        // =====================================================
        public async Task<string> ForgotPasswordAsync(
            ForgotPasswordRequest request,
            string ip,
            string userAgent)
        {
            using var conn = (MySqlConnection)_factory.CreateConnection();
            await conn.OpenAsync();

            int userId;
            string username;
            string email;
            bool isActive;

            // 1) Buscar usuario por correo o username usando sp_login_get_user
            using (var cmd = new MySqlCommand("sp_login_get_user", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_identifier", request.Identifier);

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    // No decimos si existe o no (por seguridad)
                    return "Si el correo o usuario existe en el sistema, se enviaron instrucciones para restablecer tu contraseña.";
                }

                userId = reader.GetInt32("user_id");
                username = reader.GetString("username");
                email = reader.GetString("email");
                isActive = reader.GetBoolean("is_active");
            }

            if (!isActive)
                throw new Exception("La cuenta está desactivada.");

            // 2) Generar token aleatorio
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes);

            // Lo guardamos como hash en la BD
            var tokenHashString = BCrypt.Net.BCrypt.HashPassword(token);
            var tokenHashBytes = Encoding.UTF8.GetBytes(tokenHashString);

            long resetId;

            // 3) Guardar en password_resets usando SP
            using (var cmd = new MySqlCommand("sp_create_password_reset", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_user_id", userId);
                cmd.Parameters.AddWithValue("p_token_hash", tokenHashBytes);
                cmd.Parameters.AddWithValue("p_expires_at", DateTime.UtcNow.AddHours(1));

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new Exception("No se pudo crear el token de recuperación.");

                resetId = reader.GetInt64("reset_id");
            }

            // 4) Log en audit_logs
            using (var logCmd = new MySqlCommand("sp_log_action", conn))
            {
                logCmd.CommandType = CommandType.StoredProcedure;
                logCmd.Parameters.AddWithValue("p_user_id", userId);
                logCmd.Parameters.AddWithValue("p_action", "FORGOT_PASSWORD_REQUEST");
                logCmd.Parameters.AddWithValue("p_entity_type", "USER");
                logCmd.Parameters.AddWithValue("p_entity_id", userId.ToString());
                logCmd.Parameters.AddWithValue(
                    "p_description",
                    $"Solicitud de recuperación de contraseña para {username} ({email})");
                logCmd.Parameters.AddWithValue("p_ip_address", ip);
                logCmd.Parameters.AddWithValue("p_user_agent", userAgent);

                await logCmd.ExecuteNonQueryAsync();
            }

            // 5) Código combinado resetId:token
            var code = $"{resetId}:{token}";

            // 6) Enviar correo con el código
            var subject = "InnovaTube - Recupera tu contraseña";
            var body = $@"
Hola {username},

Hemos recibido una solicitud para restablecer la contraseña de tu cuenta en InnovaTube.

Copia y pega este código en la pantalla de recuperación de contraseña:

{code}

Si tú no solicitaste este cambio, puedes ignorar este mensaje.

Saludos,
Equipo InnovaTube";

            await _emailService.SendEmailAsync(email, subject, body);

            // 7) Mensaje genérico hacia el cliente (sin revelar si existe o no)
            return "Si el correo o usuario existe en el sistema, se enviaron instrucciones para restablecer tu contraseña.";
        }

        // =====================================================
        // RESET PASSWORD (aplicar nueva contraseña)
        // =====================================================
        public async Task<LoginResponse> ResetPasswordAsync(
            ResetPasswordRequest request,
            string ip,
            string userAgent)
        {
            // request.Code = "resetId:token"
            var parts = request.Code.Split(':', 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], out var resetId))
                throw new Exception("Código de recuperación inválido.");

            var token = parts[1];

            using var conn = (MySqlConnection)_factory.CreateConnection();
            await conn.OpenAsync();

            int userId;
            string username;
            string email;
            bool isActive;
            byte[] tokenHashBytes;
            DateTime expiresAt;
            DateTime? usedAt;

            // 1) Obtener el registro de password_resets + usuario
            using (var cmd = new MySqlCommand("sp_get_password_reset", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_reset_id", resetId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new Exception("Token de recuperación no encontrado.");

                userId = reader.GetInt32("user_id");
                username = reader.GetString("username");
                email = reader.GetString("email");
                isActive = reader.GetBoolean("is_active");
                tokenHashBytes = (byte[])reader["token_hash"];
                expiresAt = reader.GetDateTime("expires_at");
                usedAt = reader.IsDBNull(reader.GetOrdinal("used_at"))
                    ? (DateTime?)null
                    : reader.GetDateTime("used_at");
            }

            if (!isActive)
                throw new Exception("La cuenta está desactivada.");

            if (usedAt != null || expiresAt < DateTime.UtcNow)
                throw new Exception("El token de recuperación ya fue usado o ha expirado.");

            var tokenHashString = Encoding.UTF8.GetString(tokenHashBytes);

            if (!BCrypt.Net.BCrypt.Verify(token, tokenHashString))
                throw new Exception("El token de recuperación no es válido.");

            // 2) Hashear la nueva contraseña
            var newHashString = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            var newHashBytes = Encoding.UTF8.GetBytes(newHashString);

            // 3) Actualizar password y marcar el token como usado (transacción)
            using (var tx = await conn.BeginTransactionAsync())
            {
                using (var updateUserCmd = new MySqlCommand(
                    "UPDATE users SET password_hash = @p_password_hash, updated_at = NOW() WHERE user_id = @p_user_id",
                    conn,
                    (MySqlTransaction)tx))
                {
                    updateUserCmd.Parameters.AddWithValue("@p_password_hash", newHashBytes);
                    updateUserCmd.Parameters.AddWithValue("@p_user_id", userId);
                    await updateUserCmd.ExecuteNonQueryAsync();
                }

                using (var updateResetCmd = new MySqlCommand(
                    "UPDATE password_resets SET used_at = NOW() WHERE reset_id = @p_reset_id",
                    conn,
                    (MySqlTransaction)tx))
                {
                    updateResetCmd.Parameters.AddWithValue("@p_reset_id", resetId);
                    await updateResetCmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
            }

            // 4) Log en audit_logs
            using (var logCmd = new MySqlCommand("sp_log_action", conn))
            {
                logCmd.CommandType = CommandType.StoredProcedure;
                logCmd.Parameters.AddWithValue("p_user_id", userId);
                logCmd.Parameters.AddWithValue("p_action", "RESET_PASSWORD");
                logCmd.Parameters.AddWithValue("p_entity_type", "USER");
                logCmd.Parameters.AddWithValue("p_entity_id", userId.ToString());
                logCmd.Parameters.AddWithValue(
                    "p_description",
                    $"El usuario {username} ha restablecido su contraseña.");
                logCmd.Parameters.AddWithValue("p_ip_address", ip);
                logCmd.Parameters.AddWithValue("p_user_agent", userAgent);

                await logCmd.ExecuteNonQueryAsync();
            }

            // 5) Crear nueva sesión + tokens, como en Login
            var (refreshToken, sessionId) = await CreateSessionAsync(userId, ip, userAgent);
            var accessToken = _jwt.GenerateAccessToken(userId, username, email);

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = userId,
                Username = username,
                Email = email
            };
        }
    }
}
