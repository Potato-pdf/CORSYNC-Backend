using System;
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
    /// Modulo de administracion de materia prima. El costo unitario que se almacena
    /// es el costo promedio ponderado vigente, actualizado por las recepciones de
    /// compras a proveedores.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class MateriaPrimaController : ControllerBase
    {
        private readonly AdminDbContext _context;
        private readonly ICosteoService _costeo;

        public MateriaPrimaController(AdminDbContext context, ICosteoService costeo)
        {
            _context = context;
            _costeo = costeo;
        }

        [HttpGet]
        public async Task<IActionResult> GetInventario()
        {
            var inventario = await _context.MateriasPrimas
                .Include(m => m.Proveedor)
                .OrderBy(m => m.Id)
                .Select(m => new
                {
                    m.Id,
                    m.Nombre,
                    m.Descripcion,
                    m.CostoUnidad,
                    m.UnidadMedida,
                    m.Stock,
                    m.StockMinimo,
                    m.ProveedorId,
                    Proveedor = m.Proveedor != null ? m.Proveedor.Nombre : string.Empty,
                    m.Activo,
                    ValorInventario = Math.Round(m.Stock * m.CostoUnidad, 2),
                    BajoMinimo = m.Stock < m.StockMinimo
                })
                .ToListAsync();

            return Ok(inventario);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMateriaPrima(int id)
        {
            var materia = await _context.MateriasPrimas.FindAsync(id);
            if (materia == null)
            {
                return NotFound("Materia prima no encontrada.");
            }
            return Ok(materia);
        }

        [HttpPost]
        public async Task<IActionResult> CrearMateriaPrima([FromBody] MateriaPrima materia)
        {
            if (materia == null || string.IsNullOrWhiteSpace(materia.Nombre))
            {
                return BadRequest("El nombre de la materia prima es obligatorio.");
            }

            if (materia.CostoUnidad < 0 || materia.Stock < 0)
            {
                return BadRequest("El costo y el stock no pueden ser negativos.");
            }

            materia.Id = 0;
            materia.Nombre = materia.Nombre.Trim();
            materia.Proveedor = null;

            _context.MateriasPrimas.Add(materia);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMateriaPrima), new { id = materia.Id }, materia);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarMateriaPrima(int id, [FromBody] MateriaPrima input)
        {
            var materia = await _context.MateriasPrimas.FindAsync(id);
            if (materia == null)
            {
                return NotFound("Materia prima no encontrada.");
            }

            if (string.IsNullOrWhiteSpace(input.Nombre))
            {
                return BadRequest("El nombre de la materia prima es obligatorio.");
            }

            if (input.CostoUnidad < 0 || input.Stock < 0)
            {
                return BadRequest("El costo y el stock no pueden ser negativos.");
            }

            materia.Nombre = input.Nombre.Trim();
            materia.Descripcion = input.Descripcion ?? string.Empty;
            materia.UnidadMedida = input.UnidadMedida ?? string.Empty;
            materia.CostoUnidad = input.CostoUnidad;
            materia.Stock = input.Stock;
            materia.StockMinimo = input.StockMinimo;
            materia.ProveedorId = input.ProveedorId;
            materia.Activo = input.Activo;

            await _context.SaveChangesAsync();
            return Ok(materia);
        }

        /// <summary>Ajuste manual de existencias (mermas, conteos fisicos).</summary>
        [HttpPut("stock/{id}")]
        public async Task<IActionResult> ActualizarStock(int id, [FromBody] decimal nuevoStock)
        {
            if (nuevoStock < 0)
            {
                return BadRequest("El stock no puede ser negativo.");
            }

            var materia = await _context.MateriasPrimas.FindAsync(id);
            if (materia == null)
            {
                return NotFound("Materia prima no encontrada.");
            }

            materia.Stock = nuevoStock;
            await _context.SaveChangesAsync();

            return Ok(materia);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarMateriaPrima(int id)
        {
            var materia = await _context.MateriasPrimas.FindAsync(id);
            if (materia == null)
            {
                return NotFound("Materia prima no encontrada.");
            }

            if (await _context.RecetasProductos.AnyAsync(r => r.MateriaPrimaId == id))
            {
                return BadRequest("El insumo forma parte de la explosión de materiales de un producto. Quítalo de la receta antes de eliminarlo.");
            }

            if (await _context.DetallesCompraProveedor.AnyAsync(d => d.MateriaPrimaId == id))
            {
                return BadRequest("El insumo tiene compras registradas. Desactivalo en lugar de eliminarlo.");
            }

            _context.MateriasPrimas.Remove(materia);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Materia prima eliminada." });
        }

        /// <summary>Explosion de materiales de todos los productos.</summary>
        [HttpGet("recetas")]
        public async Task<IActionResult> GetRecetas()
        {
            var recetas = await _context.RecetasProductos
                .Include(r => r.MateriaPrima)
                .Include(r => r.Producto)
                .OrderBy(r => r.ProductoId).ThenBy(r => r.Id)
                .Select(r => new
                {
                    r.Id,
                    r.ProductoId,
                    Producto = r.Producto != null ? r.Producto.Nombre : r.NombreProducto,
                    r.MateriaPrimaId,
                    MateriaPrima = r.MateriaPrima != null ? r.MateriaPrima.Nombre : string.Empty,
                    UnidadMedida = r.MateriaPrima != null ? r.MateriaPrima.UnidadMedida : string.Empty,
                    CostoUnitario = r.MateriaPrima != null ? r.MateriaPrima.CostoUnidad : 0m,
                    r.CantidadRequerida,
                    r.MermaPorcentaje
                })
                .ToListAsync();

            return Ok(recetas);
        }

        /// <summary>Alta de un renglon en la explosion de materiales.</summary>
        [HttpPost("recetas")]
        public async Task<IActionResult> AgregarReceta([FromBody] RecetaRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var producto = await _context.Productos.FindAsync(request.ProductoId);
            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (!await _context.MateriasPrimas.AnyAsync(m => m.Id == request.MateriaPrimaId))
            {
                return NotFound("Materia prima no encontrada.");
            }

            var existente = await _context.RecetasProductos
                .FirstOrDefaultAsync(r => r.ProductoId == request.ProductoId && r.MateriaPrimaId == request.MateriaPrimaId);

            if (existente != null)
            {
                existente.CantidadRequerida = request.CantidadRequerida;
                existente.MermaPorcentaje = request.MermaPorcentaje;
                await _context.SaveChangesAsync();
                return Ok(existente);
            }

            var receta = new RecetaProducto
            {
                ProductoId = request.ProductoId,
                NombreProducto = producto.Nombre,
                MateriaPrimaId = request.MateriaPrimaId,
                CantidadRequerida = request.CantidadRequerida,
                MermaPorcentaje = request.MermaPorcentaje
            };

            _context.RecetasProductos.Add(receta);
            await _context.SaveChangesAsync();

            return Ok(receta);
        }

        /// <summary>Costeo del producto a partir de su explosion de materiales.</summary>
        [HttpGet("costo/{productoId}")]
        public async Task<IActionResult> GetCosto(int productoId)
        {
            var costo = await _costeo.CalcularCostoProductoAsync(productoId);
            if (costo == null)
            {
                return NotFound("Producto no encontrado.");
            }
            return Ok(costo);
        }
    }
}
