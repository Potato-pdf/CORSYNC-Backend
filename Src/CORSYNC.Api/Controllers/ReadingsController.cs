using System;
using System.Collections.Generic;
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
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReadingsController : ControllerBase
    {
        private readonly AdminDbContext _context;
        private readonly IGamificationService _gamificationService;

        public ReadingsController(AdminDbContext context, IGamificationService gamificationService)
        {
            _context = context;
            _gamificationService = gamificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 20;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var totalItems = await _context.LecturasAura
                .CountAsync(la => la.UsuarioId == userId);

            var items = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .OrderByDescending(la => la.FechaFin)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(la => new ReadingResponse
                {
                    Id = la.Id,
                    DispositivoId = la.DispositivoId,
                    BpmPromedio = la.BpmPromedio,
                    BpmMaximo = la.BpmMaximo,
                    BpmMinimo = la.BpmMinimo,
                    GsrRawPromedio = la.GsrRawPromedio,
                    GsrVoltajePromedio = la.GsrVoltajePromedio,
                    NivelEstres = la.NivelEstres,
                    AuraDominante = la.AuraDominante,
                    Notas = la.Notas,
                    DuracionSegundos = la.DuracionSegundos,
                    FechaInicio = la.FechaInicio,
                    FechaFin = la.FechaFin
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var reading = await _context.LecturasAura.FindAsync(id);
            if (reading == null)
            {
                return NotFound("Lectura no encontrada.");
            }

            if (reading.UsuarioId != userId)
            {
                return Forbid();
            }

            return Ok(new ReadingResponse
            {
                Id = reading.Id,
                DispositivoId = reading.DispositivoId,
                BpmPromedio = reading.BpmPromedio,
                BpmMaximo = reading.BpmMaximo,
                BpmMinimo = reading.BpmMinimo,
                GsrRawPromedio = reading.GsrRawPromedio,
                GsrVoltajePromedio = reading.GsrVoltajePromedio,
                NivelEstres = reading.NivelEstres,
                AuraDominante = reading.AuraDominante,
                Notas = reading.Notas,
                DuracionSegundos = reading.DuracionSegundos,
                FechaInicio = reading.FechaInicio,
                FechaFin = reading.FechaFin
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReadingRequest request)
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

            var reading = new LecturaAura
            {
                UsuarioId = userId,
                DispositivoId = request.DispositivoId.Trim(),
                BpmPromedio = Math.Round(request.BpmPromedio, 1),
                BpmMaximo = Math.Round(request.BpmMaximo, 1),
                BpmMinimo = Math.Round(request.BpmMinimo, 1),
                GsrRawPromedio = request.GsrRawPromedio,
                GsrVoltajePromedio = Math.Round(request.GsrVoltajePromedio, 3),
                NivelEstres = Math.Round(request.NivelEstres, 2),
                AuraDominante = request.AuraDominante.Trim(),
                Notas = request.Notas?.Trim(),
                DuracionSegundos = request.DuracionSegundos,
                FechaInicio = request.FechaInicio,
                FechaFin = request.FechaFin
            };

            _context.LecturasAura.Add(reading);
            await _context.SaveChangesAsync();

            // Trigger gamification updates:
            // 1. Incrementar sesiones
            await _gamificationService.ActualizarProgresoDesafioAsync(userId, "Sesiones", 1);
            
            // 2. Incrementar BPM Bajo si corresponde
            if (reading.BpmPromedio < 65)
            {
                await _gamificationService.ActualizarProgresoDesafioAsync(userId, "BpmBajo", 1);
            }

            // 3. Incrementar Aura Verde si corresponde
            if (reading.AuraDominante.Equals("Verde", StringComparison.OrdinalIgnoreCase))
            {
                await _gamificationService.ActualizarProgresoDesafioAsync(userId, "AuraVerde", 1);
            }

            // 4. Actualizar racha actual
            await _gamificationService.ActualizarProgresoDesafioAsync(userId, "Racha", 0); // La lógica interna de Racha calcula el valor exacto

            // 5. Actualizar exploración de auras distintas
            await _gamificationService.ActualizarProgresoDesafioAsync(userId, "Exploracion", 0); // La lógica interna calcula auras únicas

            // 6. Verificar y desbloquear medallas
            await _gamificationService.VerificarMedallasAsync(userId);

            return CreatedAtAction(nameof(GetById), new { id = reading.Id }, new ReadingResponse
            {
                Id = reading.Id,
                DispositivoId = reading.DispositivoId,
                BpmPromedio = reading.BpmPromedio,
                BpmMaximo = reading.BpmMaximo,
                BpmMinimo = reading.BpmMinimo,
                GsrRawPromedio = reading.GsrRawPromedio,
                GsrVoltajePromedio = reading.GsrVoltajePromedio,
                NivelEstres = reading.NivelEstres,
                AuraDominante = reading.AuraDominante,
                Notas = reading.Notas,
                DuracionSegundos = reading.DuracionSegundos,
                FechaInicio = reading.FechaInicio,
                FechaFin = reading.FechaFin
            });
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
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
                return Ok(new ReadingSummaryResponse
                {
                    BpmPromedioGlobal = 0,
                    NivelEstresPromedio = 0,
                    TotalSesiones = 0,
                    AuraMasFrecuente = "Ninguna",
                    DistribucionAuras = new Dictionary<string, int>()
                });
            }

            var bpmAvg = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .AverageAsync(la => la.BpmPromedio);

            var stressAvg = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .AverageAsync(la => la.NivelEstres);

            var aurasList = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .Select(la => la.AuraDominante)
                .ToListAsync();

            var dist = aurasList
                .GroupBy(a => a)
                .ToDictionary(g => g.Key, g => g.Count());

            var mostFrequent = dist
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .FirstOrDefault() ?? "Ninguna";

            return Ok(new ReadingSummaryResponse
            {
                BpmPromedioGlobal = Math.Round(bpmAvg, 1),
                NivelEstresPromedio = Math.Round(stressAvg, 1),
                TotalSesiones = totalSessions,
                AuraMasFrecuente = mostFrequent,
                DistribucionAuras = dist
            });
        }
    }
}
