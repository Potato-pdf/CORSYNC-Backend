using System;
using System.Linq;
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
    /// <summary>
    /// Modulo de administracion de usuarios (administradores y clientes). Al dar de
    /// alta un cliente el sistema genera sus credenciales y le envia un correo con
    /// los datos de acceso; el envio queda registrado en la bitacora del panel.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/usuarios")]
    public class AdminUsuarioController : ControllerBase
    {
        private readonly AdminDbContext _context;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;

        public AdminUsuarioController(AdminDbContext context, IAuthService authService, IEmailService emailService)
        {
            _context = context;
            _authService = authService;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsuarios([FromQuery] string? role = null, [FromQuery] string? buscar = null)
        {
            var consulta = _context.Usuarios.AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
            {
                consulta = consulta.Where(u => u.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                string termino = buscar.Trim().ToLower();
                consulta = consulta.Where(u =>
                    u.Username.ToLower().Contains(termino) ||
                    u.Email.ToLower().Contains(termino) ||
                    u.NombreCompleto.ToLower().Contains(termino));
            }

            var usuarios = await consulta
                .OrderByDescending(u => u.FechaRegistro)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.NombreCompleto,
                    u.Role,
                    u.FechaRegistro,
                    u.Activo
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return NotFound("Usuario no encontrado.");
            }

            return Ok(new
            {
                usuario.Id,
                usuario.Username,
                usuario.Email,
                usuario.NombreCompleto,
                usuario.Role,
                usuario.FechaRegistro,
                usuario.Activo
            });
        }

        /// <summary>
        /// Da de alta un usuario. Si no se indica contrasena el sistema genera una
        /// temporal y la envia por correo al cliente.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            string role = request.Role.Trim();
            if (role != "Admin" && role != "Cliente")
            {
                return BadRequest("El rol debe ser Admin o Cliente.");
            }

            string username = request.Username.Trim();
            string email = request.Email.Trim();

            if (await _context.Usuarios.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
            {
                return Conflict("El nombre de usuario ya está en uso.");
            }

            if (await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
            {
                return Conflict("El correo electrónico ya está en uso.");
            }

            bool passwordGenerada = string.IsNullOrWhiteSpace(request.Password);
            string password = passwordGenerada ? _emailService.GenerarPasswordTemporal() : request.Password!.Trim();

            if (password.Length < 8)
            {
                return BadRequest("La contraseña debe tener al menos 8 caracteres.");
            }

            var usuario = new Usuario
            {
                Username = username,
                Email = email,
                NombreCompleto = request.NombreCompleto?.Trim() ?? string.Empty,
                PasswordHash = _authService.HashPassword(password),
                Role = role,
                NombreEspiritual = string.Empty,
                SignoZodiacal = string.Empty,
                FechaRegistro = DateTime.UtcNow,
                Activo = request.Activo
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            string cuerpo =
                $"Hola {(string.IsNullOrWhiteSpace(usuario.NombreCompleto) ? usuario.Username : usuario.NombreCompleto)},\n\n" +
                "Tu cuenta en el portal de clientes de ThinkUp ya está activa.\n\n" +
                $"Usuario: {usuario.Username}\n" +
                $"Contraseña temporal: {password}\n\n" +
                "Ingresa en http://localhost:4200/login y cambia tu contraseña desde tu perfil.\n\n" +
                "Equipo ThinkUp";

            var resultado = await _emailService.EnviarAsync(
                usuario.Email, "Tus datos de acceso al portal de ThinkUp", cuerpo, "Credenciales");

            return Ok(new UsuarioCreadoResponse
            {
                Usuario = new UserInfo
                {
                    Id = usuario.Id,
                    Username = usuario.Username,
                    Email = usuario.Email,
                    NombreCompleto = usuario.NombreCompleto,
                    Role = usuario.Role,
                    FechaRegistro = usuario.FechaRegistro
                },
                PasswordTemporal = passwordGenerada ? password : null,
                CorreoEnviado = resultado.Enviado,
                MensajeCorreo = resultado.Mensaje
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarUsuario(int id, [FromBody] ActualizarUsuarioRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return NotFound("Usuario no encontrado.");
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                string email = request.Email.Trim();
                if (await _context.Usuarios.AnyAsync(u => u.Id != id && u.Email.ToLower() == email.ToLower()))
                {
                    return Conflict("El correo electrónico ya está en uso por otro usuario.");
                }
                usuario.Email = email;
            }

            if (request.NombreCompleto != null)
            {
                usuario.NombreCompleto = request.NombreCompleto.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                string role = request.Role.Trim();
                if (role != "Admin" && role != "Cliente")
                {
                    return BadRequest("El rol debe ser Admin o Cliente.");
                }

                // Evita que el sistema se quede sin ningun administrador activo.
                if (usuario.Role == "Admin" && role != "Admin")
                {
                    int adminsActivos = await _context.Usuarios.CountAsync(u => u.Role == "Admin" && u.Activo && u.Id != id);
                    if (adminsActivos == 0)
                    {
                        return BadRequest("No puedes quitar el rol al último administrador activo.");
                    }
                }

                usuario.Role = role;
            }

            if (request.Activo.HasValue)
            {
                if (!request.Activo.Value && usuario.Role == "Admin")
                {
                    int adminsActivos = await _context.Usuarios.CountAsync(u => u.Role == "Admin" && u.Activo && u.Id != id);
                    if (adminsActivos == 0)
                    {
                        return BadRequest("No puedes desactivar al último administrador activo.");
                    }
                }
                usuario.Activo = request.Activo.Value;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                usuario.Id,
                usuario.Username,
                usuario.Email,
                usuario.NombreCompleto,
                usuario.Role,
                usuario.FechaRegistro,
                usuario.Activo
            });
        }

        /// <summary>Genera una contrasena temporal nueva y la reenvia al usuario.</summary>
        [HttpPost("{id}/restablecer-password")]
        public async Task<IActionResult> RestablecerPassword(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return NotFound("Usuario no encontrado.");
            }

            string password = _emailService.GenerarPasswordTemporal();
            usuario.PasswordHash = _authService.HashPassword(password);

            // Invalida las sesiones abiertas del usuario.
            var tokens = await _context.RefreshTokens.Where(t => t.UsuarioId == id && !t.Revocado).ToListAsync();
            foreach (var token in tokens)
            {
                token.Revocado = true;
            }

            await _context.SaveChangesAsync();

            string cuerpo =
                $"Hola {(string.IsNullOrWhiteSpace(usuario.NombreCompleto) ? usuario.Username : usuario.NombreCompleto)},\n\n" +
                "Un administrador restableció tu contraseña del portal de ThinkUp.\n\n" +
                $"Usuario: {usuario.Username}\n" +
                $"Contraseña temporal: {password}\n\n" +
                "Por seguridad, cambiala desde tu perfil despues de ingresar.\n\n" +
                "Equipo ThinkUp";

            var resultado = await _emailService.EnviarAsync(
                usuario.Email, "Restablecimiento de contraseña - ThinkUp", cuerpo, "RestablecerPassword");

            return Ok(new
            {
                PasswordTemporal = password,
                CorreoEnviado = resultado.Enviado,
                MensajeCorreo = resultado.Mensaje
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return NotFound("Usuario no encontrado.");
            }

            var actualClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (actualClaim != null && int.TryParse(actualClaim.Value, out int actualId) && actualId == id)
            {
                return BadRequest("No puedes eliminar tu propia cuenta.");
            }

            if (usuario.Role == "Admin")
            {
                int adminsActivos = await _context.Usuarios.CountAsync(u => u.Role == "Admin" && u.Activo && u.Id != id);
                if (adminsActivos == 0)
                {
                    return BadRequest("No puedes eliminar al último administrador activo.");
                }
            }

            if (await _context.ComprasClientes.AnyAsync(c => c.UsuarioId == id))
            {
                return BadRequest("El usuario tiene compras registradas. Desactivalo en lugar de eliminarlo.");
            }

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Usuario eliminado." });
        }

        /// <summary>Bitacora de correos generados por el sistema.</summary>
        [HttpGet("/api/admin/correos")]
        public async Task<IActionResult> GetCorreos()
        {
            var correos = await _context.CorreosEnviados
                .OrderByDescending(c => c.FechaEnvio)
                .Take(200)
                .ToListAsync();
            return Ok(correos);
        }
    }
}
