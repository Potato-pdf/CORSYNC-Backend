using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Core.DTOs;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    /// <summary>Formulario publico de contacto y bandeja de mensajes del administrador.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ContactoController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public ContactoController(AdminDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Enviar([FromBody] ContactoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var mensaje = new MensajeContacto
            {
                Nombre = request.Nombre.Trim(),
                Email = request.Email.Trim(),
                Telefono = request.Telefono?.Trim(),
                Asunto = request.Asunto.Trim(),
                Mensaje = request.Mensaje.Trim(),
                Atendido = false,
                FechaEnvio = DateTime.UtcNow
            };

            _context.MensajesContacto.Add(mensaje);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje.Id,
                Message = "Mensaje enviado correctamente. Te contactaremos pronto."
            });
        }

        /// <summary>Datos de contacto publicos de ThinkUp.</summary>
        [HttpGet("informacion")]
        public IActionResult GetInformacion() => Ok(new
        {
            Empresa = "ThinkUp",
            Direccion = "Av. Innovación 789, Col. Tecnológica, CP 45000, Guadalajara, Jalisco, México",
            Telefono = "+52 33 1234 5678",
            Email = "contacto@thinkup.com",
            EmailSoporte = "soporte@thinkup.com",
            EmailVentas = "ventas@thinkup.com",
            Horario = new[]
            {
                new { Dia = "Lunes a Viernes", Horas = "9:00 - 18:00" },
                new { Dia = "Sabado", Horas = "10:00 - 14:00" },
                new { Dia = "Domingo", Horas = "Cerrado" }
            },
            Redes = new[]
            {
                new { Nombre = "Instagram", Url = "https://instagram.com/thinkup", Icono = "instagram" },
                new { Nombre = "X", Url = "https://x.com/thinkup", Icono = "twitter-x" },
                new { Nombre = "LinkedIn", Url = "https://linkedin.com/company/thinkup", Icono = "linkedin" },
                new { Nombre = "YouTube", Url = "https://youtube.com/@thinkup", Icono = "youtube" },
                new { Nombre = "Facebook", Url = "https://facebook.com/thinkup", Icono = "facebook" }
            }
        });

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetMensajes()
        {
            var mensajes = await _context.MensajesContacto
                .OrderByDescending(m => m.FechaEnvio)
                .ToListAsync();
            return Ok(mensajes);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/atendido")]
        public async Task<IActionResult> MarcarAtendido(int id, [FromBody] bool atendido)
        {
            var mensaje = await _context.MensajesContacto.FindAsync(id);
            if (mensaje == null)
            {
                return NotFound("Mensaje no encontrado.");
            }

            mensaje.Atendido = atendido;
            await _context.SaveChangesAsync();
            return Ok(mensaje);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var mensaje = await _context.MensajesContacto.FindAsync(id);
            if (mensaje == null)
            {
                return NotFound("Mensaje no encontrado.");
            }

            _context.MensajesContacto.Remove(mensaje);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Mensaje eliminado." });
        }
    }
}
