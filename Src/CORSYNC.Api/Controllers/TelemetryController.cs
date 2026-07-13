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
    public class TelemetryController : ControllerBase
    {
        private readonly TelemetryDbContext _context;

        public TelemetryController(TelemetryDbContext context)
        {
            _context = context;
        }

        // Get historical heart rate data (last 100 consolidated items)
        [HttpGet("corazon")]
        public async Task<IActionResult> GetHeartHistory()
        {
            var readings = await _context.LecturasCorazon
                .OrderByDescending(l => l.FechaHora)
                .Take(100)
                .ToListAsync();
            return Ok(readings);
        }

        // Skins sensor (GSR) endpoint - marked as pending
        [HttpGet("piel")]
        public IActionResult GetPielTelemetry()
        {
            // Expressly returns a pending status for developer and system awareness
            return StatusCode(501, new
            {
                Estado = "PENDIENTE",
                CodigoStatus = 501,
                Mensaje = "[PENDIENTE] El módulo de telemetría para el sensor de respuesta galvánica de la piel (GSR) aún no está desarrollado. Pendiente de integración en el firmware del ESP32 y base de datos.",
                ActividadesPendientes = new[]
                {
                    "Definición de payload JSON para GSR.",
                    "Creación de tabla y entidad LecturaPiel.",
                    "Suscripción al tópico MQTT de piel en el Background Worker.",
                    "Mapeo de datos GSR en el cliente Unity 3D."
                }
            });
        }
    }
}
