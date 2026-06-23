using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ComentarioController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public ComentarioController(AdminDbContext context)
        {
            _context = context;
        }

        // Public: Get all approved comments
        [HttpGet("aprobados")]
        public async Task<IActionResult> GetAprobados()
        {
            var aprobados = await _context.Comentarios
                .Where(c => c.Aprobado)
                .OrderByDescending(c => c.FechaCreacion)
                .ToListAsync();
            return Ok(aprobados);
        }

        // Admin: Get all comments (both approved and pending moderation)
        [HttpGet("todos")]
        public async Task<IActionResult> GetTodos()
        {
            var todos = await _context.Comentarios
                .OrderByDescending(c => c.FechaCreacion)
                .ToListAsync();
            return Ok(todos);
        }

        // Public: Submit a comment for moderation
        [HttpPost]
        public async Task<IActionResult> EnviarComentario([FromBody] Comentario comentario)
        {
            if (comentario == null || string.IsNullOrWhiteSpace(comentario.Contenido))
            {
                return BadRequest("El comentario no puede estar vacío.");
            }

            comentario.Aprobado = false; // Requires admin moderation
            comentario.FechaCreacion = DateTime.UtcNow;

            _context.Comentarios.Add(comentario);
            await _context.SaveChangesAsync();

            return Ok(comentario);
        }

        // Admin: Approve a comment
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

        // Admin: Delete/Reject a comment
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

            return Ok(new { Message = "Comentario eliminado con éxito." });
        }
    }
}
