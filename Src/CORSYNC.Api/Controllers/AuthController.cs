using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Core.DTOs;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AdminDbContext _context;
        private readonly IAuthService _authService;

        public AuthController(AdminDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Normalizar y comprobar si el usuario ya existe
            string normalizedUsername = request.Username.Trim().ToLower();
            string normalizedEmail = request.Email.Trim().ToLower();

            if (await _context.Usuarios.AnyAsync(u => u.Username.ToLower() == normalizedUsername))
            {
                return Conflict("El nombre de usuario ya está en uso.");
            }

            if (await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                return Conflict("El correo electrónico ya está en uso.");
            }

            // Crear el nuevo usuario
            var user = new Usuario
            {
                Username = request.Username.Trim(),
                Email = request.Email.Trim(),
                PasswordHash = _authService.HashPassword(request.Password),
                NombreCompleto = request.NombreCompleto.Trim(),
                NombreEspiritual = string.Empty,
                SignoZodiacal = string.Empty,
                FotoUrl = null,
                Role = "Cliente", // Forzar rol Cliente por seguridad
                FechaRegistro = DateTime.UtcNow,
                Activo = true
            };

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            // Generar tokens
            var token = _authService.GenerateJwtToken(user);
            var refreshToken = _authService.GenerateRefreshToken(user.Id);
            
            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            var expiration = DateTime.UtcNow.AddHours(2);

            return CreatedAtAction(nameof(GetProfile), new {}, new AuthResponse
            {
                Token = token,
                RefreshToken = refreshToken.Token,
                Expiration = expiration,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    NombreCompleto = user.NombreCompleto,
                    NombreEspiritual = user.NombreEspiritual,
                    SignoZodiacal = user.SignoZodiacal,
                    FotoUrl = user.FotoUrl,
                    Role = user.Role,
                    FechaRegistro = user.FechaRegistro
                }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            string normalizedUsername = request.Username.Trim().ToLower();

            // Buscar usuario activo
            var user = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername && u.Activo);

            if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized("Usuario o contraseña incorrectos.");
            }

            // Generar tokens
            var token = _authService.GenerateJwtToken(user);
            var refreshToken = _authService.GenerateRefreshToken(user.Id);

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            var expiration = DateTime.UtcNow.AddHours(2);

            return Ok(new AuthResponse
            {
                Token = token,
                RefreshToken = refreshToken.Token,
                Expiration = expiration,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    NombreCompleto = user.NombreCompleto,
                    NombreEspiritual = user.NombreEspiritual,
                    SignoZodiacal = user.SignoZodiacal,
                    FotoUrl = user.FotoUrl,
                    Role = user.Role,
                    FechaRegistro = user.FechaRegistro
                }
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

            if (storedToken == null || storedToken.Revocado)
            {
                return Unauthorized("Token de refresco inválido o ya revocado.");
            }

            storedToken.Revocado = true;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Sesión cerrada exitosamente." });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var principal = _authService.GetPrincipalFromExpiredToken(request.Token);
            if (principal == null)
            {
                return Unauthorized("Token de acceso inválido.");
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token de acceso.");
            }

            var storedRefreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

            if (storedRefreshToken == null || !storedRefreshToken.EstaActivo || storedRefreshToken.UsuarioId != userId)
            {
                return Unauthorized("Token de refresco inválido, expirado o no corresponde al usuario.");
            }

            var user = await _context.Usuarios.FindAsync(userId);
            if (user == null || !user.Activo)
            {
                return Unauthorized("Usuario inactivo o no encontrado.");
            }

            // Generar nuevos tokens
            var newJwtToken = _authService.GenerateJwtToken(user);
            var newRefreshToken = _authService.GenerateRefreshToken(user.Id);

            // Roto el token viejo
            storedRefreshToken.Revocado = true;
            storedRefreshToken.ReemplazadoPor = newRefreshToken.Token;

            _context.RefreshTokens.Add(newRefreshToken);
            await _context.SaveChangesAsync();

            var expiration = DateTime.UtcNow.AddHours(2);

            return Ok(new AuthResponse
            {
                Token = newJwtToken,
                RefreshToken = newRefreshToken.Token,
                Expiration = expiration,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    NombreCompleto = user.NombreCompleto,
                    NombreEspiritual = user.NombreEspiritual,
                    SignoZodiacal = user.SignoZodiacal,
                    FotoUrl = user.FotoUrl,
                    Role = user.Role,
                    FechaRegistro = user.FechaRegistro
                }
            });
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            // Obtener el ID del usuario desde las Claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Token no contiene el identificador de usuario.");
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                return BadRequest("Identificador de usuario inválido en el token.");
            }

            var user = await _context.Usuarios.FindAsync(userId);
            if (user == null || !user.Activo)
            {
                return NotFound("Usuario no encontrado o inactivo.");
            }

            return Ok(new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                NombreCompleto = user.NombreCompleto,
                NombreEspiritual = user.NombreEspiritual,
                SignoZodiacal = user.SignoZodiacal,
                FotoUrl = user.FotoUrl,
                Role = user.Role,
                FechaRegistro = user.FechaRegistro
            });
        }
    }
}
