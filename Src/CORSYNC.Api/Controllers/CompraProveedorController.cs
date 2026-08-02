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
    /// Compras de materia prima a proveedores. Al recibir una compra el inventario
    /// se incrementa y el costo promedio ponderado de cada insumo se recalcula, lo
    /// que a su vez actualiza el costo del producto y el precio de las cotizaciones.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class CompraProveedorController : ControllerBase
    {
        private readonly AdminDbContext _context;
        private readonly ICosteoService _costeo;

        public CompraProveedorController(AdminDbContext context, ICosteoService costeo)
        {
            _context = context;
            _costeo = costeo;
        }

        [HttpGet]
        public async Task<IActionResult> GetCompras()
        {
            var compras = await _context.ComprasProveedores
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.MateriaPrima)
                .OrderByDescending(c => c.FechaCompra)
                .Select(c => new
                {
                    c.Id,
                    c.Folio,
                    c.ProveedorId,
                    Proveedor = c.Proveedor != null ? c.Proveedor.Nombre : string.Empty,
                    c.MontoTotal,
                    c.Estado,
                    c.Notas,
                    c.FechaCompra,
                    c.FechaRecepcion,
                    Detalles = c.Detalles.Select(d => new
                    {
                        d.Id,
                        d.MateriaPrimaId,
                        MateriaPrima = d.MateriaPrima != null ? d.MateriaPrima.Nombre : string.Empty,
                        UnidadMedida = d.MateriaPrima != null ? d.MateriaPrima.UnidadMedida : string.Empty,
                        d.Cantidad,
                        d.CostoUnitario,
                        d.Importe
                    })
                })
                .ToListAsync();

            return Ok(compras);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCompra(int id)
        {
            var compra = await _context.ComprasProveedores
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.MateriaPrima)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (compra == null)
            {
                return NotFound("Compra no encontrada.");
            }

            return Ok(compra);
        }

        [HttpPost]
        public async Task<IActionResult> CrearCompra([FromBody] CompraProveedorRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.Detalles.Count == 0)
            {
                return BadRequest("La compra debe incluir al menos un insumo.");
            }

            var proveedor = await _context.Proveedores.FindAsync(request.ProveedorId);
            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }

            var idsInsumos = request.Detalles.Select(d => d.MateriaPrimaId).Distinct().ToList();
            var insumos = await _context.MateriasPrimas
                .Where(m => idsInsumos.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

            foreach (var id in idsInsumos)
            {
                if (!insumos.ContainsKey(id))
                {
                    return BadRequest($"La materia prima {id} no existe.");
                }
            }

            var ahora = DateTime.UtcNow;
            var compra = new CompraProveedor
            {
                ProveedorId = request.ProveedorId,
                Notas = request.Notas?.Trim(),
                Estado = "Pendiente",
                FechaCompra = ahora
            };

            foreach (var detalle in request.Detalles)
            {
                decimal importe = Math.Round(detalle.Cantidad * detalle.CostoUnitario, 2, MidpointRounding.AwayFromZero);
                compra.Detalles.Add(new DetalleCompraProveedor
                {
                    MateriaPrimaId = detalle.MateriaPrimaId,
                    Cantidad = detalle.Cantidad,
                    CostoUnitario = detalle.CostoUnitario,
                    Importe = importe
                });
            }

            compra.MontoTotal = compra.Detalles.Sum(d => d.Importe);

            _context.ComprasProveedores.Add(compra);
            await _context.SaveChangesAsync();

            compra.Folio = $"OC-{ahora:yyyy}-{compra.Id:D4}";
            await _context.SaveChangesAsync();

            return Ok(compra);
        }

        /// <summary>
        /// Recibe la compra: suma el inventario y recalcula el costo promedio ponderado
        /// de cada insumo. Devuelve el efecto del recalculo para mostrarlo en el panel.
        /// </summary>
        [HttpPut("{id}/recibir")]
        public async Task<IActionResult> RecibirCompra(int id)
        {
            var compra = await _context.ComprasProveedores
                .Include(c => c.Detalles)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (compra == null)
            {
                return NotFound("Compra no encontrada.");
            }

            if (compra.Estado == "Recibida")
            {
                return BadRequest("Esta compra ya fue recibida; no puede volver a afectar el inventario.");
            }

            if (compra.Estado == "Cancelada")
            {
                return BadRequest("Una compra cancelada no puede recibirse.");
            }

            var impactos = new List<ImpactoCosteoResponse>();
            foreach (var detalle in compra.Detalles)
            {
                var impacto = await _costeo.RegistrarEntradaInventarioAsync(
                    detalle.MateriaPrimaId, detalle.Cantidad, detalle.CostoUnitario);

                if (impacto != null)
                {
                    impactos.Add(impacto);
                }
            }

            compra.Estado = "Recibida";
            compra.FechaRecepcion = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Compra = new { compra.Id, compra.Folio, compra.Estado, compra.FechaRecepcion, compra.MontoTotal },
                MetodoCosteo = "Costo promedio ponderado",
                Impactos = impactos
            });
        }

        [HttpPut("{id}/cancelar")]
        public async Task<IActionResult> CancelarCompra(int id)
        {
            var compra = await _context.ComprasProveedores.FindAsync(id);
            if (compra == null)
            {
                return NotFound("Compra no encontrada.");
            }

            if (compra.Estado == "Recibida")
            {
                return BadRequest("No se puede cancelar una compra que ya afecto el inventario.");
            }

            compra.Estado = "Cancelada";
            await _context.SaveChangesAsync();
            return Ok(compra);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarCompra(int id)
        {
            var compra = await _context.ComprasProveedores
                .Include(c => c.Detalles)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (compra == null)
            {
                return NotFound("Compra no encontrada.");
            }

            if (compra.Estado == "Recibida")
            {
                return BadRequest("No se puede eliminar una compra recibida; cancela el movimiento con un ajuste de inventario.");
            }

            _context.DetallesCompraProveedor.RemoveRange(compra.Detalles);
            _context.ComprasProveedores.Remove(compra);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Compra eliminada." });
        }
    }
}
