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
    /// Catalogo de productos de ThinkUp y su explosion de materiales (BOM).
    /// La empresa comercializa un unico producto: la pulsera CORSYNC.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductoController : ControllerBase
    {
        private readonly AdminDbContext _context;
        private readonly ICosteoService _costeo;

        public ProductoController(AdminDbContext context, ICosteoService costeo)
        {
            _context = context;
            _costeo = costeo;
        }

        [HttpGet]
        public async Task<IActionResult> GetProductos()
        {
            var productos = await _context.Productos
                .Where(p => p.Activo)
                .OrderBy(p => p.Id)
                .ToListAsync();

            var resultado = new List<object>();
            foreach (var producto in productos)
            {
                var costo = await _costeo.CalcularCostoProductoAsync(producto.Id);
                resultado.Add(new
                {
                    producto.Id,
                    producto.Nombre,
                    producto.Descripcion,
                    producto.DescripcionLarga,
                    producto.Activo,
                    PrecioLista = costo?.PrecioLista ?? 0m,
                    CostoUnitario = costo?.CostoUnitario ?? 0m
                });
            }

            return Ok(resultado);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            var costo = await _costeo.CalcularCostoProductoAsync(id);
            var documentos = await _context.DocumentosProductos
                .Where(d => d.ProductoId == id)
                .OrderBy(d => d.Id)
                .ToListAsync();

            return Ok(new
            {
                producto.Id,
                producto.Nombre,
                producto.Descripcion,
                producto.DescripcionLarga,
                producto.Activo,
                PrecioLista = costo?.PrecioLista ?? 0m,
                CostoUnitario = costo?.CostoUnitario ?? 0m,
                Documentos = documentos
            });
        }

        /// <summary>Explosion de materiales valuada con el metodo de costeo de la empresa.</summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("{id}/costo")]
        public async Task<IActionResult> GetCosto(int id)
        {
            var costo = await _costeo.CalcularCostoProductoAsync(id);
            if (costo == null)
            {
                return NotFound("Producto no encontrado.");
            }
            return Ok(costo);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CrearProducto([FromBody] ProductoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var producto = new Producto
            {
                Nombre = request.Nombre.Trim(),
                Descripcion = (request.Descripcion ?? string.Empty).Trim(),
                DescripcionLarga = (request.DescripcionLarga ?? string.Empty).Trim(),
                ManoObraUnitaria = request.ManoObraUnitaria,
                OverheadPorcentaje = request.OverheadPorcentaje,
                MargenUtilidad = request.MargenUtilidad,
                Activo = request.Activo,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, producto);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarProducto(int id, [FromBody] ProductoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            producto.Nombre = request.Nombre.Trim();
            producto.Descripcion = (request.Descripcion ?? string.Empty).Trim();
            producto.DescripcionLarga = (request.DescripcionLarga ?? string.Empty).Trim();
            producto.ManoObraUnitaria = request.ManoObraUnitaria;
            producto.OverheadPorcentaje = request.OverheadPorcentaje;
            producto.MargenUtilidad = request.MargenUtilidad;
            producto.Activo = request.Activo;

            await _context.SaveChangesAsync();
            return Ok(producto);
        }

        // --- Explosion de materiales (receta) ---

        [Authorize(Roles = "Admin")]
        [HttpPost("receta")]
        public async Task<IActionResult> AgregarRenglonReceta([FromBody] RecetaRequest request)
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

            var insumo = await _context.MateriasPrimas.FindAsync(request.MateriaPrimaId);
            if (insumo == null)
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

        [Authorize(Roles = "Admin")]
        [HttpDelete("receta/{recetaId}")]
        public async Task<IActionResult> EliminarRenglonReceta(int recetaId)
        {
            var receta = await _context.RecetasProductos.FindAsync(recetaId);
            if (receta == null)
            {
                return NotFound("Renglon de receta no encontrado.");
            }

            _context.RecetasProductos.Remove(receta);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Renglón eliminado de la explosión de materiales." });
        }

        // --- Documentacion del producto ---

        [HttpGet("{id}/documentos")]
        public async Task<IActionResult> GetDocumentos(int id)
        {
            var documentos = await _context.DocumentosProductos
                .Where(d => d.ProductoId == id)
                .OrderBy(d => d.Id)
                .ToListAsync();
            return Ok(documentos);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/documentos")]
        public async Task<IActionResult> AgregarDocumento(int id, [FromBody] DocumentoProducto documento)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (string.IsNullOrWhiteSpace(documento.Titulo))
            {
                return BadRequest("El título del documento es obligatorio.");
            }

            documento.Id = 0;
            documento.ProductoId = id;
            documento.FechaPublicacion = DateTime.UtcNow;

            _context.DocumentosProductos.Add(documento);
            await _context.SaveChangesAsync();
            return Ok(documento);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("documentos/{documentoId}")]
        public async Task<IActionResult> EliminarDocumento(int documentoId)
        {
            var documento = await _context.DocumentosProductos.FindAsync(documentoId);
            if (documento == null)
            {
                return NotFound("Documento no encontrado.");
            }

            _context.DocumentosProductos.Remove(documento);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Documento eliminado." });
        }
    }
}
