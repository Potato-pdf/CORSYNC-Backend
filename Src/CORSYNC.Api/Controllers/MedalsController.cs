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
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MedalsController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public MedalsController(AdminDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyMedals()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var medals = await _context.MedallasUsuarios
                .Where(mu => mu.UsuarioId == userId)
                .Include(mu => mu.Medalla)
                .OrderByDescending(mu => mu.FechaObtenida)
                .Select(mu => new MedalResponse
                {
                    Id = mu.Medalla.Id,
                    Nombre = mu.Medalla.Nombre,
                    Descripcion = mu.Medalla.Descripcion,
                    Icono = mu.Medalla.Icono,
                    FechaObtenida = mu.FechaObtenida
                })
                .ToListAsync();

            return Ok(medals);
        }
    }
}
