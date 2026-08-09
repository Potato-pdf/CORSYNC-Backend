using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    /// <summary>Preguntas frecuentes publicadas en el sitio.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class FaqController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public FaqController(AdminDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPreguntas()
        {
            var preguntas = await _context.PreguntasFrecuentes
                .Where(p => p.Activo)
                .OrderBy(p => p.Orden)
                .ToListAsync();
            return Ok(preguntas);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("todas")]
        public async Task<IActionResult> GetTodas()
        {
            var preguntas = await _context.PreguntasFrecuentes
                .OrderBy(p => p.Orden)
                .ToListAsync();
            return Ok(preguntas);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] PreguntaFrecuente pregunta)
        {
            if (string.IsNullOrWhiteSpace(pregunta.Pregunta) || string.IsNullOrWhiteSpace(pregunta.Respuesta))
            {
                return BadRequest("La pregunta y la respuesta son obligatorias.");
            }

            pregunta.Id = 0;
            _context.PreguntasFrecuentes.Add(pregunta);
            await _context.SaveChangesAsync();
            return Ok(pregunta);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Actualizar(int id, [FromBody] PreguntaFrecuente input)
        {
            var pregunta = await _context.PreguntasFrecuentes.FindAsync(id);
            if (pregunta == null)
            {
                return NotFound("Pregunta no encontrada.");
            }

            pregunta.Pregunta = input.Pregunta;
            pregunta.Respuesta = input.Respuesta;
            pregunta.Categoria = input.Categoria;
            pregunta.Orden = input.Orden;
            pregunta.Activo = input.Activo;

            await _context.SaveChangesAsync();
            return Ok(pregunta);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var pregunta = await _context.PreguntasFrecuentes.FindAsync(id);
            if (pregunta == null)
            {
                return NotFound("Pregunta no encontrada.");
            }

            _context.PreguntasFrecuentes.Remove(pregunta);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Pregunta eliminada." });
        }
    }
}
