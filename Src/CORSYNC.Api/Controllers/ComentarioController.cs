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
    /// <summary>
    /// Valoraciones de clientes sobre la pulsera CORSYNC. Toda opinion entra como
    /// pendiente y solo se publica cuando un administrador la aprueba.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ComentarioController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public ComentarioController(AdminDbContext context)
        {
            _context = context;
        }

        /// <summary>Valoraciones publicadas en el sitio.</summary>
        [HttpGet("aprobados")]
        public async Task<IActionResult> GetAprobados()
        {
            var aprobados = await _context.Comentarios
                .Where(c => c.Aprobado)
                .OrderByDescending(c => c.FechaCreacion)
                .Select(c => new
                {
                    c.Id,
                    c.NombreUsuario,
                    c.Contenido,
                    c.Calificacion,
                    c.ProductoId,
                    c.Respuesta,
                    c.FechaRespuesta,
                    c.FechaCreacion
                })
                .ToListAsync();
            return Ok(aprobados);
        }

        /// <summary>Promedio y distribucion de estrellas de las valoraciones publicadas.</summary>
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen()
        {
            var calificaciones = await _context.Comentarios
                .Where(c => c.Aprobado)
                .Select(c => c.Calificacion)
                .ToListAsync();

            var distribucion = new Dictionary<int, int>();
            for (int estrellas = 5; estrellas >= 1; estrellas--)
            {
                distribucion[estrellas] = calificaciones.Count(c => c == estrellas);
            }

            return Ok(new ResumenValoracionesResponse
            {
                Total = calificaciones.Count,
                Promedio = calificaciones.Count > 0 ? Math.Round(calificaciones.Average(), 2) : 0,
                Distribucion = distribucion
            });
        }

        /// <summary>Todas las valoraciones, incluidas las pendientes de moderar.</summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("todos")]
        public async Task<IActionResult> GetTodos()
        {
            var todos = await _context.Comentarios
                .OrderByDescending(c => c.FechaCreacion)
                .ToListAsync();
            return Ok(todos);
        }

        /// <summary>Envia una valoracion para moderacion.</summary>
        [HttpPost]
        public async Task<IActionResult> EnviarComentario([FromBody] ComentarioRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var comentario = new Comentario
            {
                NombreUsuario = request.NombreUsuario.Trim(),
                Email = (request.Email ?? string.Empty).Trim(),
                Contenido = request.Contenido.Trim(),
                Calificacion = request.Calificacion,
                ProductoId = request.ProductoId ?? 1,
                CompraClienteId = request.CompraClienteId,
                Aprobado = false,
                FechaCreacion = DateTime.UtcNow
            };

            // Si la opinion llega desde el panel de cliente, se liga al usuario y a su compra.
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                comentario.UsuarioId = userId;

                if (request.CompraClienteId.HasValue)
                {
                    var compra = await _context.ComprasClientes
                        .FirstOrDefaultAsync(c => c.Id == request.CompraClienteId.Value && c.UsuarioId == userId);
                    if (compra == null)
                    {
                        return BadRequest("La compra indicada no pertenece a tu cuenta.");
                    }
                    compra.Resenado = true;
                }
            }

            _context.Comentarios.Add(comentario);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                comentario.Id,
                Message = "Gracias por tu opinion. Sera publicada una vez que la revise nuestro equipo."
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("aprobar/{id}")]
        public async Task<IActionResult> AprobarComentario(int id)
        {
            var comentario = await _context.Comentarios.FindAsync(id);
            if (comentario == null)
            {
                return NotFound("Comentario no encontrado.");
            }

            comentario.Aprobado = true;
            await _context.SaveChangesAsync();

            return Ok(comentario);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("rechazar/{id}")]
        public async Task<IActionResult> RechazarComentario(int id)
        {
            var comentario = await _context.Comentarios.FindAsync(id);
            if (comentario == null)
            {
                return NotFound("Comentario no encontrado.");
            }

            comentario.Aprobado = false;
            await _context.SaveChangesAsync();

            return Ok(comentario);
        }

        /// <summary>Publica la respuesta de ThinkUp a una valoracion.</summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/responder")]
        public async Task<IActionResult> ResponderComentario(int id, [FromBody] ResponderComentarioRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var comentario = await _context.Comentarios.FindAsync(id);
            if (comentario == null)
            {
                return NotFound("Comentario no encontrado.");
            }

            comentario.Respuesta = request.Respuesta.Trim();
            comentario.FechaRespuesta = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(comentario);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarComentario(int id)
        {
            var comentario = await _context.Comentarios.FindAsync(id);
            if (comentario == null)
            {
                return NotFound("Comentario no encontrado.");
            }

            _context.Comentarios.Remove(comentario);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Comentario eliminado con exito." });
        }
    }
}
