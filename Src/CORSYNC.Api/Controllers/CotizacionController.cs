using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CotizacionController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public CotizacionController(AdminDbContext context)
        {
            _context = context;
        }

        public class CotizacionRequest
        {
            public string NombreCliente { get; set; } = string.Empty;
            public string NombreProducto { get; set; } = "Espejo CORSYNC Standard";
            public decimal AnchoCm { get; set; }
            public decimal AltoCm { get; set; }
            public int DensidadLedId { get; set; } = 3; // MateriaPrimaId for LED strip
        }

        [HttpPost("calcular")]
        public async Task<IActionResult> CalcularCotizacion([FromBody] CotizacionRequest request)
        {
            if (request == null || request.AnchoCm <= 0 || request.AltoCm <= 0)
            {
                return BadRequest("Dimensiones inválidas.");
            }

            // Fetch raw materials from DB to perform costing
            var vidrio = await _context.MateriasPrimas.FirstOrDefaultAsync(m => m.Id == 1);
            var marco = await _context.MateriasPrimas.FirstOrDefaultAsync(m => m.Id == 2);
            var led = await _context.MateriasPrimas.FirstOrDefaultAsync(m => m.Id == request.DensidadLedId);
            var sensor = await _context.MateriasPrimas.FirstOrDefaultAsync(m => m.Id == 4);
            var esp32 = await _context.MateriasPrimas.FirstOrDefaultAsync(m => m.Id == 5);

            if (vidrio == null || marco == null || led == null || sensor == null || esp32 == null)
            {
                return StatusCode(500, "Componentes de costeo no inicializados en base de datos.");
            }

            // Calculate costs based on area and perimeter
            decimal areaCm2 = request.AnchoCm * request.AltoCm;
            decimal perimetroMetros = (2 * (request.AnchoCm + request.AltoCm)) / 100m;

            decimal costoVidrio = areaCm2 * vidrio.CostoUnidad;
            decimal costoMarco = perimetroMetros * marco.CostoUnidad;
            decimal costoLed = perimetroMetros * led.CostoUnidad;
            decimal costoElectronica = sensor.CostoUnidad + esp32.CostoUnidad;

            decimal costoMateriales = costoVidrio + costoMarco + costoLed + costoElectronica;
            
            // Assembly overhead: 30% of material cost
            decimal recargoEnsamblaje = costoMateriales * 0.30m;
            decimal total = costoMateriales + recargoEnsamblaje;

            total = Math.Round(total, 2);

            var cotizacion = new Cotizacion
            {
                NombreCliente = request.NombreCliente,
                NombreProducto = request.NombreProducto,
                Ancho = request.AnchoCm,
                Alto = request.AltoCm,
                CostoTotal = total,
                FechaCotizacion = DateTime.UtcNow
            };

            _context.Cotizaciones.Add(cotizacion);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Cotizacion = cotizacion,
                Desglose = new
                {
                    CostoVidrio = Math.Round(costoVidrio, 2),
                    CostoMarco = Math.Round(costoMarco, 2),
                    CostoLed = Math.Round(costoLed, 2),
                    CostoElectronica = Math.Round(costoElectronica, 2),
                    CostoMateriaPrimaTotal = Math.Round(costoMateriales, 2),
                    RecargoEnsamblaje = Math.Round(recargoEnsamblaje, 2),
                    Total = total
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCotizaciones()
        {
            var cotizaciones = await _context.Cotizaciones.ToListAsync();
            return Ok(cotizaciones);
        }
    }
}
