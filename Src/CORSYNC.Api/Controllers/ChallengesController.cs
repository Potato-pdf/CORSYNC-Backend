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
    public class ChallengesController : ControllerBase
    {
        private readonly AdminDbContext _context;
        private readonly IGamificationService _gamificationService;

        public ChallengesController(AdminDbContext context, IGamificationService gamificationService)
        {
            _context = context;
            _gamificationService = gamificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var challenges = await _context.Desafios
                .Where(d => d.Activo)
                .ToListAsync();

            var userProgresses = await _context.ProgresosDesafios
                .Where(pd => pd.UsuarioId == userId)
                .ToDictionaryAsync(pd => pd.DesafioId);

            var result = challenges.Select(c =>
            {
                userProgresses.TryGetValue(c.Id, out var p);
                
                int current = p?.ProgresoActual ?? 0;
                bool completed = p?.Completado ?? false;
                DateTime? dateCompleted = p?.FechaCompletado;
                
                double percentage = c.MetaObjetivo > 0 
                    ? (double)current / c.MetaObjetivo * 100.0 
                    : 0.0;
                
                if (percentage > 100.0) percentage = 100.0;

                return new ChallengeResponse
                {
                    Id = c.Id,
                    Titulo = c.Titulo,
                    Descripcion = c.Descripcion,
                    Icono = c.Icono,
                    Tipo = c.Tipo,
                    MetaObjetivo = c.MetaObjetivo,
                    UnidadMedida = c.UnidadMedida,
                    Puntos = c.Puntos,
                    ProgresoActual = current,
                    Completado = completed,
                    PorcentajeProgreso = Math.Round(percentage, 1),
                    FechaCompletado = dateCompleted
                };
            }).ToList();

            return Ok(result);
        }

        [HttpPut("{id}/progress")]
        public async Task<IActionResult> UpdateProgress(int id, [FromBody] UpdateProgressRequest request)
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

            var challenge = await _context.Desafios.FindAsync(id);
            if (challenge == null || !challenge.Activo)
            {
                return NotFound("Desafío no encontrado.");
            }

            var progress = await _context.ProgresosDesafios
                .FirstOrDefaultAsync(pd => pd.UsuarioId == userId && pd.DesafioId == id);

            if (progress == null)
            {
                progress = new ProgresoDesafio
                {
                    UsuarioId = userId,
                    DesafioId = id,
                    ProgresoActual = 0,
                    Completado = false,
                    FechaInicio = DateTime.UtcNow
                };
                _context.ProgresosDesafios.Add(progress);
            }

            if (progress.Completado)
            {
                return Conflict("El desafío ya ha sido completado y no puede ser modificado.");
            }

            progress.ProgresoActual = request.ProgresoActual;

            if (progress.ProgresoActual >= challenge.MetaObjetivo)
            {
                progress.ProgresoActual = challenge.MetaObjetivo;
                progress.Completado = true;
                progress.FechaCompletado = DateTime.UtcNow;

                // Verificar y otorgar medallas por completar desafíos si corresponde
                await _gamificationService.VerificarMedallasAsync(userId);
            }

            await _context.SaveChangesAsync();

            double percentage = challenge.MetaObjetivo > 0 
                ? (double)progress.ProgresoActual / challenge.MetaObjetivo * 100.0 
                : 0.0;
            if (percentage > 100.0) percentage = 100.0;

            return Ok(new ChallengeResponse
            {
                Id = challenge.Id,
                Titulo = challenge.Titulo,
                Descripcion = challenge.Descripcion,
                Icono = challenge.Icono,
                Tipo = challenge.Tipo,
                MetaObjetivo = challenge.MetaObjetivo,
                UnidadMedida = challenge.UnidadMedida,
                Puntos = challenge.Puntos,
                ProgresoActual = progress.ProgresoActual,
                Completado = progress.Completado,
                PorcentajeProgreso = Math.Round(percentage, 1),
                FechaCompletado = progress.FechaCompletado
            });
        }
    }
}
