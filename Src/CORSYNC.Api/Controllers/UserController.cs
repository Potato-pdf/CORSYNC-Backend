using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Core.DTOs;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public UserController(AdminDbContext context)
        {
            _context = context;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
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

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var user = await _context.Usuarios.FindAsync(userId);
            if (user == null || !user.Activo)
            {
                return NotFound("Usuario no encontrado o inactivo.");
            }

            // Validar signo zodiacal si se provee
            if (!string.IsNullOrEmpty(request.SignoZodiacal))
            {
                var validSigns = new[] { "Aries", "Tauro", "Géminis", "Cáncer", "Leo", "Virgo", "Libra", "Escorpio", "Sagitario", "Capricornio", "Acuario", "Piscis" };
                var normalizedSign = request.SignoZodiacal.Trim();
                if (!validSigns.Any(s => s.Equals(normalizedSign, StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest("Signo zodiacal inválido.");
                }
                user.SignoZodiacal = normalizedSign;
            }

            if (request.NombreCompleto != null)
            {
                user.NombreCompleto = request.NombreCompleto.Trim();
            }

            if (request.NombreEspiritual != null)
            {
                user.NombreEspiritual = request.NombreEspiritual.Trim();
            }

            if (request.FotoUrl != null)
            {
                user.FotoUrl = request.FotoUrl.Trim();
            }

            await _context.SaveChangesAsync();

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

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var totalSessions = await _context.LecturasAura
                .CountAsync(la => la.UsuarioId == userId);

            if (totalSessions == 0)
            {
                return Ok(new UserStatsResponse
                {
                    BpmPromedio = 0,
                    NivelEstresPromedio = 0,
                    SesionesTotales = 0,
                    AuraDominante = "Ninguna",
                    RachaActualDias = 0,
                    UltimaSesion = null
                });
            }

            var bpmAvg = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .AverageAsync(la => la.BpmPromedio);

            var stressAvg = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .AverageAsync(la => la.NivelEstres);

            var lastSession = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .MaxAsync(la => la.FechaFin);

            var auraDominante = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .GroupBy(la => la.AuraDominante)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefaultAsync() ?? "Ninguna";

            // Calcular racha actual
            var sessionDates = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .Select(la => la.FechaFin.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToListAsync();

            int racha = 0;
            if (sessionDates.Count > 0)
            {
                var today = DateTime.UtcNow.Date;
                var lastDate = sessionDates[0];

                if (lastDate == today || lastDate == today.AddDays(-1))
                {
                    racha = 1;
                    var currentDate = lastDate;
                    for (int i = 1; i < sessionDates.Count; i++)
                    {
                        if (sessionDates[i] == currentDate.AddDays(-1))
                        {
                            racha++;
                            currentDate = sessionDates[i];
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return Ok(new UserStatsResponse
            {
                BpmPromedio = Math.Round(bpmAvg, 1),
                NivelEstresPromedio = Math.Round(stressAvg, 1),
                SesionesTotales = totalSessions,
                AuraDominante = auraDominante,
                RachaActualDias = racha,
                UltimaSesion = lastSession
            });
        }
    }
}
