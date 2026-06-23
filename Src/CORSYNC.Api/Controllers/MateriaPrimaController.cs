using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MateriaPrimaController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public MateriaPrimaController(AdminDbContext context)
        {
            _context = context;
        }

        // Get inventory list
        [HttpGet]
        public async Task<IActionResult> GetInventario()
        {
            var inventario = await _context.MateriasPrimas.ToListAsync();
            return Ok(inventario);
        }

        // Update stock of a material (purchase replenishment/usage)
        [HttpPut("stock/{id}")]
        public async Task<IActionResult> ActualizarStock(int id, [FromBody] decimal nuevoStock)
        {
            var materia = await _context.MateriasPrimas.FindAsync(id);
            if (materia == null)
            {
                return NotFound("Materia prima no encontrada.");
            }

            materia.Stock = nuevoStock;
            await _context.SaveChangesAsync();

            return Ok(materia);
        }

        // Get BOM recipes (Explosión de materiales)
        [HttpGet("recetas")]
        public async Task<IActionResult> GetRecetas()
        {
            var recetas = await _context.RecetasProductos
                .Include(r => r.MateriaPrima)
                .ToListAsync();
            return Ok(recetas);
        }

        // Add item to a product recipe definition
        [HttpPost("recetas")]
        public async Task<IActionResult> AgregarReceta([FromBody] RecetaProducto receta)
        {
            if (receta == null || receta.CantidadRequerida <= 0)
            {
                return BadRequest("Receta inválida.");
            }

            _context.RecetasProductos.Add(receta);
            await _context.SaveChangesAsync();

            return Ok(receta);
        }
    }
}
