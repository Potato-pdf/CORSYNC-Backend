using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    /// <summary>Modulo de administracion de proveedores de materia prima.</summary>
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class ProveedorController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public ProveedorController(AdminDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetProveedores([FromQuery] bool soloActivos = false)
        {
            var consulta = _context.Proveedores.AsQueryable();
            if (soloActivos)
            {
                consulta = consulta.Where(p => p.Activo);
            }

            var proveedores = await consulta.OrderBy(p => p.Nombre).ToListAsync();

            // Numero de insumos que surte cada proveedor, util en el listado del panel.
            var conteos = await _context.MateriasPrimas
                .Where(m => m.ProveedorId != null)
                .GroupBy(m => m.ProveedorId!.Value)
                .Select(g => new { ProveedorId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(g => g.ProveedorId, g => g.Total);

            return Ok(proveedores.Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Contacto,
                p.Email,
                p.Telefono,
                p.Direccion,
                p.Pais,
                p.Activo,
                p.FechaAlta,
                InsumosSuministrados = conteos.TryGetValue(p.Id, out var total) ? total : 0
            }));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProveedor(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }
            return Ok(proveedor);
        }

        [HttpPost]
        public async Task<IActionResult> CrearProveedor([FromBody] Proveedor proveedor)
        {
            if (proveedor == null || string.IsNullOrWhiteSpace(proveedor.Nombre))
            {
                return BadRequest("El nombre del proveedor es obligatorio.");
            }

            proveedor.Id = 0;
            proveedor.Nombre = proveedor.Nombre.Trim();
            proveedor.FechaAlta = DateTime.UtcNow;

            _context.Proveedores.Add(proveedor);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProveedor), new { id = proveedor.Id }, proveedor);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarProveedor(int id, [FromBody] Proveedor input)
        {
            if (input == null)
            {
                return BadRequest("Datos inválidos.");
            }

            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }

            if (string.IsNullOrWhiteSpace(input.Nombre))
            {
                return BadRequest("El nombre del proveedor es obligatorio.");
            }

            proveedor.Nombre = input.Nombre.Trim();
            proveedor.Contacto = input.Contacto ?? string.Empty;
            proveedor.Email = input.Email ?? string.Empty;
            proveedor.Telefono = input.Telefono ?? string.Empty;
            proveedor.Direccion = input.Direccion ?? string.Empty;
            proveedor.Pais = input.Pais ?? string.Empty;
            proveedor.Activo = input.Activo;

            await _context.SaveChangesAsync();
            return Ok(proveedor);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarProveedor(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }

            if (await _context.ComprasProveedores.AnyAsync(c => c.ProveedorId == id))
            {
                return BadRequest("El proveedor tiene compras registradas. Desactivalo en lugar de eliminarlo.");
            }

            if (await _context.MateriasPrimas.AnyAsync(m => m.ProveedorId == id))
            {
                return BadRequest("El proveedor surte insumos del catálogo. Reasígnalos antes de eliminarlo.");
            }

            _context.Proveedores.Remove(proveedor);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Proveedor eliminado con exito." });
        }
    }
}
