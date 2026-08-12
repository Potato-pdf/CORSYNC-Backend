using System;
using System.Linq;
using System.Security.Claims;
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
    /// Seccion de clientes: compras realizadas, documentacion del producto adquirido
    /// y gestion de la propia contrasena.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/cliente")]
    public class ClienteController : ControllerBase
    {
        private readonly AdminDbContext _context;
        private readonly IAuthService _authService;

        public ClienteController(AdminDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        private int? UsuarioActualId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : null;
        }

        /// <summary>Compras del cliente autenticado.</summary>
        [HttpGet("compras")]
        public async Task<IActionResult> GetMisCompras()
        {
            var userId = UsuarioActualId();
            if (userId == null)
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var compras = await _context.ComprasClientes
                .Include(c => c.Producto)
                .Where(c => c.UsuarioId == userId.Value)
                .OrderByDescending(c => c.FechaCompra)
                .Select(c => new
                {
                    c.Id,
                    c.Folio,
                    c.ProductoId,
                    Producto = c.Producto != null ? c.Producto.Nombre : "CORSYNC",
                    c.Cantidad,
                    c.Monto,
                    c.Estado,
                    c.NumeroSerie,
                    c.Resenado,
                    c.FechaCompra
                })
                .ToListAsync();

            return Ok(compras);
        }

        /// <summary>
        /// Documentacion disponible para el cliente: manuales y guias de los productos
        /// que efectivamente adquirio.
        /// </summary>
        [HttpGet("documentos")]
        public async Task<IActionResult> GetMisDocumentos()
        {
            var userId = UsuarioActualId();
            if (userId == null)
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var productosAdquiridos = await _context.ComprasClientes
                .Where(c => c.UsuarioId == userId.Value && c.Estado != "Cancelado")
                .Select(c => c.ProductoId)
                .Distinct()
                .ToListAsync();

            if (productosAdquiridos.Count == 0)
            {
                return Ok(Array.Empty<object>());
            }

            var documentos = await _context.DocumentosProductos
                .Include(d => d.Producto)
                .Where(d => productosAdquiridos.Contains(d.ProductoId))
                .OrderBy(d => d.ProductoId).ThenBy(d => d.Id)
                .Select(d => new
                {
                    d.Id,
                    d.ProductoId,
                    Producto = d.Producto != null ? d.Producto.Nombre : "CORSYNC",
                    d.Titulo,
                    d.Descripcion,
                    d.Tipo,
                    d.Url,
                    d.Peso,
                    d.FechaPublicacion
                })
                .ToListAsync();

            return Ok(documentos);
        }

        [HttpPost("cambiar-password")]
        public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = UsuarioActualId();
            if (userId == null)
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var usuario = await _context.Usuarios.FindAsync(userId.Value);
            if (usuario == null || !usuario.Activo)
            {
                return NotFound("Usuario no encontrado o inactivo.");
            }

            if (!_authService.VerifyPassword(request.PasswordActual, usuario.PasswordHash))
            {
                return BadRequest("La contraseña actual no es correcta.");
            }

            usuario.PasswordHash = _authService.HashPassword(request.PasswordNueva);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Contraseña actualizada correctamente." });
        }

        // --- Administracion de las compras de clientes ---

        [Authorize(Roles = "Admin")]
        [HttpGet("/api/admin/compras-clientes")]
        public async Task<IActionResult> GetTodasLasCompras()
        {
            var compras = await _context.ComprasClientes
                .Include(c => c.Usuario)
                .Include(c => c.Producto)
                .OrderByDescending(c => c.FechaCompra)
                .Select(c => new
                {
                    c.Id,
                    c.Folio,
                    c.UsuarioId,
                    Cliente = c.Usuario != null ? c.Usuario.NombreCompleto : string.Empty,
                    ClienteUsername = c.Usuario != null ? c.Usuario.Username : string.Empty,
                    c.ProductoId,
                    Producto = c.Producto != null ? c.Producto.Nombre : "CORSYNC",
                    c.Cantidad,
                    c.Monto,
                    c.Estado,
                    c.NumeroSerie,
                    c.Resenado,
                    c.FechaCompra
                })
                .ToListAsync();

            return Ok(compras);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("/api/admin/compras-clientes")]
        public async Task<IActionResult> RegistrarCompra([FromBody] CompraClienteRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var usuario = await _context.Usuarios.FindAsync(request.UsuarioId);
            if (usuario == null)
            {
                return NotFound("Cliente no encontrado.");
            }

            var producto = await _context.Productos.FindAsync(request.ProductoId);
            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            // La venta sale del almacen de producto terminado, que solo se llena
            // registrando produccion. Vender sin existencias comprometeria unidades
            // que todavia no se han fabricado.
            if (request.Cantidad > producto.Stock)
            {
                return BadRequest(
                    $"No hay unidades suficientes: se piden {request.Cantidad} y hay {producto.Stock} " +
                    "en almacén. Registra producción antes de vender.");
            }

            producto.Stock -= request.Cantidad;

            var ahora = DateTime.UtcNow;
            var compra = new CompraCliente
            {
                UsuarioId = request.UsuarioId,
                ProductoId = request.ProductoId,
                Cantidad = request.Cantidad,
                Monto = request.Monto,
                Estado = request.Estado,
                NumeroSerie = request.NumeroSerie?.Trim(),
                Resenado = false,
                FechaCompra = ahora
            };

            _context.ComprasClientes.Add(compra);
            await _context.SaveChangesAsync();

            compra.Folio = $"VTA-{ahora:yyyy}-{compra.Id:D4}";
            await _context.SaveChangesAsync();

            return Ok(compra);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("/api/admin/compras-clientes/{id}/estado")]
        public async Task<IActionResult> ActualizarEstadoCompra(int id, [FromBody] CambioEstadoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var compra = await _context.ComprasClientes.FindAsync(id);
            if (compra == null)
            {
                return NotFound("Compra no encontrada.");
            }

            var estado = request.Estado.Trim();
            var permitidos = new[] { "Procesando", "Enviado", "Entregado", "Cancelado" };
            if (!permitidos.Contains(estado))
            {
                return BadRequest("Estado inválido. Usa Procesando, Enviado, Entregado o Cancelado.");
            }

            // Cancelar devuelve las unidades al almacen, y reactivar una venta
            // cancelada las vuelve a tomar. Sin esto, cancelar perderia inventario
            // ya fabricado y reactivar lo entregaria dos veces.
            const string Cancelado = "Cancelado";
            bool estabaCancelada = compra.Estado == Cancelado;
            bool quedaCancelada = estado == Cancelado;

            if (!estabaCancelada && quedaCancelada)
            {
                var producto = await _context.Productos.FindAsync(compra.ProductoId);
                if (producto != null)
                {
                    producto.Stock += compra.Cantidad;
                }
            }
            else if (estabaCancelada && !quedaCancelada)
            {
                var producto = await _context.Productos.FindAsync(compra.ProductoId);
                if (producto == null)
                {
                    return NotFound("Producto no encontrado.");
                }
                if (compra.Cantidad > producto.Stock)
                {
                    return BadRequest(
                        $"No se puede reactivar la venta: requiere {compra.Cantidad} unidades y hay " +
                        $"{producto.Stock} en almacén.");
                }
                producto.Stock -= compra.Cantidad;
            }

            compra.Estado = estado;
            await _context.SaveChangesAsync();
            return Ok(compra);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("/api/admin/compras-clientes/{id}")]
        public async Task<IActionResult> EliminarCompra(int id)
        {
            var compra = await _context.ComprasClientes.FindAsync(id);
            if (compra == null)
            {
                return NotFound("Compra no encontrada.");
            }

            // Una venta cancelada ya devolvio sus unidades al cancelarse; solo se
            // reponen las que seguian descontadas.
            if (compra.Estado != "Cancelado")
            {
                var producto = await _context.Productos.FindAsync(compra.ProductoId);
                if (producto != null)
                {
                    producto.Stock += compra.Cantidad;
                }
            }

            _context.ComprasClientes.Remove(compra);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Compra eliminada." });
        }
    }
}
