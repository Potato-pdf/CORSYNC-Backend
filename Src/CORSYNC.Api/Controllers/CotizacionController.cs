using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// Cotizacion de la pulsera CORSYNC. El precio de lista no es un valor fijo: se
    /// deriva del metodo de costeo de la empresa (costo promedio ponderado de la
    /// materia prima segun la explosion de materiales, mas mano de obra, gastos
    /// indirectos y margen de utilidad).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CotizacionController : ControllerBase
    {
        private const int ProductoCorsyncId = 1;

        private readonly AdminDbContext _context;
        private readonly ICosteoService _costeo;

        public CotizacionController(AdminDbContext context, ICosteoService costeo)
        {
            _context = context;
            _costeo = costeo;
        }

        /// <summary>Calcula la cotizacion, la registra y devuelve el desglose completo.</summary>
        [HttpPost("calcular")]
        public async Task<IActionResult> CalcularCotizacion([FromBody] CotizacionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!request.AceptaPrivacidad)
            {
                return BadRequest("Debes aceptar la política de privacidad para solicitar una cotización.");
            }

            // Una sola cotizacion por empresa, identificada por el correo de
            // contacto: el campo Empresa es opcional y el correo siempre viene.
            // Se compara en minusculas a proposito: InMemory distingue
            // mayusculas y SQL Server no, y la regla debe ser la misma en ambos.
            var correo = request.Email.Trim();
            var correoNormalizado = correo.ToLowerInvariant();
            bool yaCotizo = await _context.Cotizaciones
                .AnyAsync(c => c.Email.ToLower() == correoNormalizado);
            if (yaCotizo)
            {
                return Conflict("Ya existe una cotización registrada para este correo. Escríbenos y damos seguimiento a la que ya tienes.");
            }

            var costo = await _costeo.CalcularCostoProductoAsync(ProductoCorsyncId);
            if (costo == null)
            {
                return StatusCode(500, "El catálogo de costeo no está inicializado en la base de datos.");
            }

            string licencia = ReglasComerciales.NormalizarLicencia(request.TipoLicencia);
            decimal factorLicencia = ReglasComerciales.FactorLicencia(licencia);

            decimal precioUnitario = Math.Round(costo.PrecioLista * factorLicencia, 2, MidpointRounding.AwayFromZero);
            decimal subtotal = Math.Round(precioUnitario * request.Cantidad, 2, MidpointRounding.AwayFromZero);

            decimal descuentoPorcentaje = ReglasComerciales.DescuentoPorVolumen(request.Cantidad);
            decimal descuentoMonto = Math.Round(subtotal * descuentoPorcentaje, 2, MidpointRounding.AwayFromZero);

            var serviciosSeleccionados = new List<ConceptoCosto>();
            var clavesServicios = new List<string>();
            foreach (var clave in request.Servicios.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var normalizada = (clave ?? string.Empty).Trim().ToLowerInvariant();
                if (ReglasComerciales.Servicios.TryGetValue(normalizada, out var servicio))
                {
                    serviciosSeleccionados.Add(new ConceptoCosto
                    {
                        Concepto = servicio.Nombre,
                        Detalle = servicio.Detalle,
                        Importe = servicio.Precio
                    });
                    clavesServicios.Add(normalizada);
                }
            }

            decimal totalServicios = serviciosSeleccionados.Sum(s => s.Importe);
            decimal baseGravable = subtotal - descuentoMonto + totalServicios;
            decimal impuestos = Math.Round(baseGravable * ReglasComerciales.TasaImpuesto, 2, MidpointRounding.AwayFromZero);
            decimal total = Math.Round(baseGravable + impuestos, 2, MidpointRounding.AwayFromZero);

            var ahora = DateTime.UtcNow;
            var cotizacion = new Cotizacion
            {
                NombreCliente = request.NombreCliente.Trim(),
                Empresa = (request.Empresa ?? string.Empty).Trim(),
                Email = request.Email.Trim(),
                Telefono = (request.Telefono ?? string.Empty).Trim(),
                Pais = (request.Pais ?? string.Empty).Trim(),
                ProductoId = ProductoCorsyncId,
                NombreProducto = costo.Producto,
                Cantidad = request.Cantidad,
                TipoLicencia = licencia,
                Servicios = string.Join(",", clavesServicios),
                Mensaje = request.Mensaje?.Trim(),
                CostoMateriaPrima = costo.CostoMateriaPrima,
                CostoManoObra = costo.CostoManoObra,
                CostoIndirecto = costo.CostoIndirecto,
                CostoUnitario = costo.CostoUnitario,
                PrecioUnitario = precioUnitario,
                Subtotal = subtotal,
                DescuentoPorcentaje = descuentoPorcentaje,
                DescuentoMonto = descuentoMonto,
                TotalServicios = totalServicios,
                Impuestos = impuestos,
                CostoTotal = total,
                Estado = "Nueva",
                FechaCotizacion = ahora,
                FechaVigencia = ahora.AddDays(30)
            };

            _context.Cotizaciones.Add(cotizacion);
            await _context.SaveChangesAsync();

            cotizacion.Folio = $"COT-{ahora:yyyy}-{cotizacion.Id:D5}";
            await _context.SaveChangesAsync();

            return Ok(new CotizacionResponse
            {
                Id = cotizacion.Id,
                Folio = cotizacion.Folio,
                NombreProducto = costo.Producto,
                Cantidad = request.Cantidad,
                TipoLicencia = licencia,
                DesgloseMateriaPrima = costo.Materiales.Select(m => new ConceptoCosto
                {
                    Concepto = m.MateriaPrima,
                    Detalle = $"{m.CantidadConMerma:0.##} {m.UnidadMedida} x {m.CostoUnitario:0.00}",
                    Importe = m.CostoTotal
                }).ToList(),
                CostoMateriaPrima = costo.CostoMateriaPrima,
                CostoManoObra = costo.CostoManoObra,
                CostoIndirecto = costo.CostoIndirecto,
                CostoUnitario = costo.CostoUnitario,
                MargenUtilidad = costo.MargenUtilidad,
                PrecioLista = costo.PrecioLista,
                PrecioUnitario = precioUnitario,
                Subtotal = subtotal,
                DescuentoPorcentaje = descuentoPorcentaje,
                DescuentoMonto = descuentoMonto,
                Servicios = serviciosSeleccionados,
                TotalServicios = totalServicios,
                Impuestos = impuestos,
                Total = total,
                FechaCotizacion = cotizacion.FechaCotizacion,
                FechaVigencia = cotizacion.FechaVigencia
            });
        }

        /// <summary>Parametros publicos del cotizador para armar el formulario.</summary>
        [HttpGet("parametros")]
        public async Task<IActionResult> GetParametros()
        {
            var costo = await _costeo.CalcularCostoProductoAsync(ProductoCorsyncId);
            if (costo == null)
            {
                return StatusCode(500, "El catálogo de costeo no está inicializado en la base de datos.");
            }

            return Ok(new
            {
                Producto = costo.Producto,
                costo.PrecioLista,
                // Licencias y tramos se derivan de ReglasComerciales para que lo
                // que anuncia el formulario no pueda separarse de lo que se cobra.
                Licencias = ReglasComerciales.Licencias.Select(l => new
                {
                    l.Clave,
                    l.Nombre,
                    Factor = l.Factor,
                    Precio = Math.Round(costo.PrecioLista * l.Factor, 2, MidpointRounding.AwayFromZero),
                    l.Descripcion
                }),
                Servicios = ReglasComerciales.Servicios.Select(s => new
                {
                    Clave = s.Key,
                    s.Value.Nombre,
                    s.Value.Precio,
                    s.Value.Detalle
                }),
                DescuentosVolumen = ReglasComerciales.TramosVolumen.Select(t => new
                {
                    t.Desde,
                    t.Porcentaje
                }),
                CantidadMaxima = ReglasComerciales.CantidadMaxima,
                TasaImpuesto = ReglasComerciales.TasaImpuesto
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetCotizaciones()
        {
            var cotizaciones = await _context.Cotizaciones
                .OrderByDescending(c => c.FechaCotizacion)
                .ToListAsync();
            return Ok(cotizaciones);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> ActualizarEstado(int id, [FromBody] CambioEstadoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var cotizacion = await _context.Cotizaciones.FindAsync(id);
            if (cotizacion == null)
            {
                return NotFound("Cotización no encontrada.");
            }

            var estado = request.Estado.Trim();
            var permitidos = new[] { "Nueva", "Contactado", "Cerrada" };
            if (!permitidos.Contains(estado))
            {
                return BadRequest("Estado inválido. Usa Nueva, Contactado o Cerrada.");
            }

            cotizacion.Estado = estado;
            await _context.SaveChangesAsync();
            return Ok(cotizacion);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarCotizacion(int id)
        {
            var cotizacion = await _context.Cotizaciones.FindAsync(id);
            if (cotizacion == null)
            {
                return NotFound("Cotización no encontrada.");
            }

            _context.Cotizaciones.Remove(cotizacion);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Cotización eliminada." });
        }
    }
}
