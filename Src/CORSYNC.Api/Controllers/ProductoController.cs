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
        private readonly IAlmacenImagenes _almacen;

        public ProductoController(AdminDbContext context, ICosteoService costeo, IAlmacenImagenes almacen)
        {
            _context = context;
            _costeo = costeo;
            _almacen = almacen;
        }

        [HttpGet]
        public async Task<IActionResult> GetProductos()
        {
            var productos = await _context.Productos
                .Where(p => p.Activo)
                .OrderBy(p => p.Id)
                .ToListAsync();

            var ids = productos.Select(p => p.Id).ToList();

            // Portada de cada producto: la primera imagen de su galería.
            var portadas = await _context.ImagenesProductos
                .Where(i => ids.Contains(i.ProductoId))
                .GroupBy(i => i.ProductoId)
                .Select(g => new
                {
                    ProductoId = g.Key,
                    Url = g.OrderBy(i => i.Orden).ThenBy(i => i.Id).First().Url
                })
                .ToDictionaryAsync(x => x.ProductoId, x => x.Url);

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
                    CostoUnitario = costo?.CostoUnitario ?? 0m,
                    ImagenPortada = portadas.TryGetValue(producto.Id, out var url) ? url : null
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

            var imagenes = await _context.ImagenesProductos
                .Where(i => i.ProductoId == id)
                .OrderBy(i => i.Orden).ThenBy(i => i.Id)
                .Select(i => new { i.Id, i.Url, i.Titulo, i.Descripcion, i.Orden })
                .ToListAsync();

            var caracteristicas = await _context.CaracteristicasProductos
                .Where(c => c.ProductoId == id)
                .OrderBy(c => c.Orden).ThenBy(c => c.Id)
                .Select(c => new { c.Id, c.Texto, c.Icono, c.Orden })
                .ToListAsync();

            // Se agrupan aquí para que el front pinte una columna por grupo sin
            // tener que reagrupar nada.
            var especificaciones = await _context.EspecificacionesProductos
                .Where(e => e.ProductoId == id)
                .OrderBy(e => e.Orden).ThenBy(e => e.Id)
                .Select(e => new { e.Id, e.Grupo, e.Campo, e.Valor, e.Orden })
                .ToListAsync();

            var gruposEspecificacion = especificaciones
                .GroupBy(e => e.Grupo)
                .Select(g => new
                {
                    Grupo = g.Key,
                    Filas = g.Select(e => new { e.Id, e.Campo, e.Valor, e.Orden }).ToList()
                })
                .ToList();

            return Ok(new
            {
                producto.Id,
                producto.Nombre,
                producto.Descripcion,
                producto.DescripcionLarga,
                producto.Activo,
                PrecioLista = costo?.PrecioLista ?? 0m,
                CostoUnitario = costo?.CostoUnitario ?? 0m,
                Documentos = documentos,
                Imagenes = imagenes,
                Caracteristicas = caracteristicas,
                Especificaciones = gruposEspecificacion
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

        // --- Galeria de imagenes ---

        [HttpGet("{id}/imagenes")]
        public async Task<IActionResult> GetImagenes(int id)
        {
            var imagenes = await _context.ImagenesProductos
                .Where(i => i.ProductoId == id)
                .OrderBy(i => i.Orden).ThenBy(i => i.Id)
                .Select(i => new { i.Id, i.Url, i.Titulo, i.Descripcion, i.Orden, i.TamanoBytes, i.FechaSubida })
                .ToListAsync();
            return Ok(imagenes);
        }

        /// <summary>
        /// Sube una imagen a la galería del producto. El archivo se valida por
        /// extensión, tipo de contenido y firma binaria antes de escribirse.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/imagenes")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> SubirImagen(
            int id,
            IFormFile archivo,
            [FromForm] string? titulo = null,
            [FromForm] string? descripcion = null)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            if (archivo == null || archivo.Length == 0)
            {
                return BadRequest("No se recibió ningún archivo.");
            }

            await using var flujo = archivo.OpenReadStream();
            var resultado = await _almacen.GuardarAsync(
                flujo, archivo.FileName, archivo.ContentType, archivo.Length, id);

            if (!resultado.Exito)
            {
                return BadRequest(resultado.Error);
            }

            // La imagen nueva se coloca al final del carrusel.
            int siguienteOrden = await _context.ImagenesProductos
                .Where(i => i.ProductoId == id)
                .Select(i => (int?)i.Orden)
                .MaxAsync() ?? -1;

            var imagen = new ImagenProducto
            {
                ProductoId = id,
                Url = resultado.Url,
                NombreArchivo = resultado.NombreArchivo,
                TamanoBytes = resultado.TamanoBytes,
                Titulo = (titulo ?? string.Empty).Trim(),
                Descripcion = (descripcion ?? string.Empty).Trim(),
                Orden = siguienteOrden + 1,
                FechaSubida = DateTime.UtcNow
            };

            _context.ImagenesProductos.Add(imagen);
            await _context.SaveChangesAsync();

            return Ok(new { imagen.Id, imagen.Url, imagen.Titulo, imagen.Descripcion, imagen.Orden });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("imagenes/{imagenId}")]
        public async Task<IActionResult> ActualizarImagen(int imagenId, [FromBody] ActualizarImagenRequest request)
        {
            var imagen = await _context.ImagenesProductos.FindAsync(imagenId);
            if (imagen == null)
            {
                return NotFound("Imagen no encontrada.");
            }

            if (request.Titulo != null) imagen.Titulo = request.Titulo.Trim();
            if (request.Descripcion != null) imagen.Descripcion = request.Descripcion.Trim();
            if (request.Orden.HasValue) imagen.Orden = request.Orden.Value;

            await _context.SaveChangesAsync();
            return Ok(new { imagen.Id, imagen.Url, imagen.Titulo, imagen.Descripcion, imagen.Orden });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("imagenes/{imagenId}")]
        public async Task<IActionResult> EliminarImagen(int imagenId)
        {
            var imagen = await _context.ImagenesProductos.FindAsync(imagenId);
            if (imagen == null)
            {
                return NotFound("Imagen no encontrada.");
            }

            _context.ImagenesProductos.Remove(imagen);
            await _context.SaveChangesAsync();

            // El archivo se borra después del commit: si fallara el borrado en disco
            // preferimos un huérfano antes que un registro apuntando a nada.
            _almacen.Eliminar(imagen.ProductoId, imagen.NombreArchivo);

            return Ok(new { Message = "Imagen eliminada." });
        }

        // --- Caracteristicas destacadas ---

        [HttpGet("{id}/caracteristicas")]
        public async Task<IActionResult> GetCaracteristicas(int id)
        {
            var caracteristicas = await _context.CaracteristicasProductos
                .Where(c => c.ProductoId == id)
                .OrderBy(c => c.Orden).ThenBy(c => c.Id)
                .ToListAsync();
            return Ok(caracteristicas);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/caracteristicas")]
        public async Task<IActionResult> AgregarCaracteristica(int id, [FromBody] CaracteristicaRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await _context.Productos.AnyAsync(p => p.Id == id))
            {
                return NotFound("Producto no encontrado.");
            }

            int siguienteOrden = await _context.CaracteristicasProductos
                .Where(c => c.ProductoId == id)
                .Select(c => (int?)c.Orden)
                .MaxAsync() ?? -1;

            var caracteristica = new CaracteristicaProducto
            {
                ProductoId = id,
                Texto = request.Texto.Trim(),
                Icono = string.IsNullOrWhiteSpace(request.Icono) ? "check-lg" : request.Icono.Trim(),
                Orden = request.Orden ?? siguienteOrden + 1
            };

            _context.CaracteristicasProductos.Add(caracteristica);
            await _context.SaveChangesAsync();
            return Ok(caracteristica);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("caracteristicas/{caracteristicaId}")]
        public async Task<IActionResult> EliminarCaracteristica(int caracteristicaId)
        {
            var caracteristica = await _context.CaracteristicasProductos.FindAsync(caracteristicaId);
            if (caracteristica == null)
            {
                return NotFound("Característica no encontrada.");
            }

            _context.CaracteristicasProductos.Remove(caracteristica);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Característica eliminada." });
        }

        // --- Especificaciones tecnicas ---

        [HttpGet("{id}/especificaciones")]
        public async Task<IActionResult> GetEspecificaciones(int id)
        {
            var especificaciones = await _context.EspecificacionesProductos
                .Where(e => e.ProductoId == id)
                .OrderBy(e => e.Orden).ThenBy(e => e.Id)
                .ToListAsync();
            return Ok(especificaciones);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/especificaciones")]
        public async Task<IActionResult> AgregarEspecificacion(int id, [FromBody] EspecificacionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await _context.Productos.AnyAsync(p => p.Id == id))
            {
                return NotFound("Producto no encontrado.");
            }

            int siguienteOrden = await _context.EspecificacionesProductos
                .Where(e => e.ProductoId == id)
                .Select(e => (int?)e.Orden)
                .MaxAsync() ?? -1;

            var especificacion = new EspecificacionProducto
            {
                ProductoId = id,
                Grupo = request.Grupo.Trim(),
                Campo = request.Campo.Trim(),
                Valor = request.Valor.Trim(),
                Orden = request.Orden ?? siguienteOrden + 1
            };

            _context.EspecificacionesProductos.Add(especificacion);
            await _context.SaveChangesAsync();
            return Ok(especificacion);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("especificaciones/{especificacionId}")]
        public async Task<IActionResult> EliminarEspecificacion(int especificacionId)
        {
            var especificacion = await _context.EspecificacionesProductos.FindAsync(especificacionId);
            if (especificacion == null)
            {
                return NotFound("Especificación no encontrada.");
            }

            _context.EspecificacionesProductos.Remove(especificacion);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Especificación eliminada." });
        }
    }
}
