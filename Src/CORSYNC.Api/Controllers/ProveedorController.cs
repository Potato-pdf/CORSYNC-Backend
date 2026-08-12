using System;
using System.Linq;
using System.Text.RegularExpressions;
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

            if (!TelefonoValido(proveedor.Telefono, out string errorTelefono))
            {
                return BadRequest(errorTelefono);
            }

            proveedor.Id = 0;
            proveedor.Nombre = proveedor.Nombre.Trim();
            proveedor.Telefono = (proveedor.Telefono ?? string.Empty).Trim();
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

            if (!TelefonoValido(input.Telefono, out string errorTelefono))
            {
                return BadRequest(errorTelefono);
            }

            proveedor.Nombre = input.Nombre.Trim();
            proveedor.Contacto = input.Contacto ?? string.Empty;
            proveedor.Email = input.Email ?? string.Empty;
            proveedor.Telefono = (input.Telefono ?? string.Empty).Trim();
            proveedor.Direccion = input.Direccion ?? string.Empty;
            proveedor.Pais = input.Pais ?? string.Empty;
            proveedor.Activo = input.Activo;

            await _context.SaveChangesAsync();
            return Ok(proveedor);
        }

        /// <summary>
        /// Da de baja al proveedor desactivandolo. No se borra fisicamente: sus
        /// ordenes de compra son el respaldo del inventario y del costo promedio, y
        /// quitar el renglon dejaria ese historial sin origen. Desactivar lo saca de
        /// las listas de alta y es reversible.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DesactivarProveedor(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
            {
                return NotFound("Proveedor no encontrado.");
            }

            if (!proveedor.Activo)
            {
                return Ok(new { Message = $"El proveedor {proveedor.Nombre} ya estaba desactivado." });
            }

            proveedor.Activo = false;
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Se desactivó al proveedor {proveedor.Nombre}." });
        }

        /// <summary>
        /// El telefono es opcional, pero si viene tiene que ser un numero marcable:
        /// entre 10 y 15 digitos, que es el rango de la E.164 (10 de un numero
        /// nacional, 15 el maximo internacional). Los separadores + - ( ) . y los
        /// espacios se admiten y no cuentan como digitos.
        /// </summary>
        private static bool TelefonoValido(string? telefono, out string mensaje)
        {
            mensaje = string.Empty;

            if (string.IsNullOrWhiteSpace(telefono))
            {
                return true;
            }

            if (!Regex.IsMatch(telefono, @"^[0-9\s\-\(\)\+\.]+$"))
            {
                mensaje = "El teléfono sólo admite dígitos y los separadores + - ( ) . y espacios.";
                return false;
            }

            int digitos = telefono.Count(char.IsDigit);
            if (digitos < 10 || digitos > 15)
            {
                mensaje = $"El teléfono debe tener entre 10 y 15 dígitos; recibimos {digitos}.";
                return false;
            }

            return true;
        }
    }
}
