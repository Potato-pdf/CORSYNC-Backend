using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.DTOs;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    /// <summary>Indicadores agregados para el panel de administracion.</summary>
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/dashboard")]
    public class DashboardController : ControllerBase
    {
        private const int ProductoCorsyncId = 1;

        private readonly AdminDbContext _context;
        private readonly ICosteoService _costeo;

        public DashboardController(AdminDbContext context, ICosteoService costeo)
        {
            _context = context;
            _costeo = costeo;
        }

        [HttpGet]
        public async Task<IActionResult> GetResumen()
        {
            var costo = await _costeo.CalcularCostoProductoAsync(ProductoCorsyncId);

            var calificaciones = await _context.Comentarios
                .Where(c => c.Aprobado)
                .Select(c => c.Calificacion)
                .ToListAsync();

            var insumos = await _context.MateriasPrimas
                .Select(m => new { m.Stock, m.StockMinimo, m.CostoUnidad })
                .ToListAsync();

            var respuesta = new DashboardResponse
            {
                TotalClientes = await _context.Usuarios.CountAsync(u => u.Role == "Cliente"),
                TotalAdministradores = await _context.Usuarios.CountAsync(u => u.Role == "Admin"),
                ComentariosPendientes = await _context.Comentarios.CountAsync(c => !c.Aprobado),
                ComentariosAprobados = calificaciones.Count,
                CalificacionPromedio = calificaciones.Count > 0 ? Math.Round(calificaciones.Average(), 2) : 0,
                CotizacionesTotales = await _context.Cotizaciones.CountAsync(),
                CotizacionesNuevas = await _context.Cotizaciones.CountAsync(c => c.Estado == "Nueva"),
                MontoCotizado = await _context.Cotizaciones.SumAsync(c => (decimal?)c.CostoTotal) ?? 0m,
                MensajesSinAtender = await _context.MensajesContacto.CountAsync(m => !m.Atendido),
                Proveedores = await _context.Proveedores.CountAsync(p => p.Activo),
                InsumosBajoMinimo = insumos.Count(i => i.Stock < i.StockMinimo),
                ValorInventario = Math.Round(insumos.Sum(i => i.Stock * i.CostoUnidad), 2),
                UnidadesFabricables = costo?.UnidadesFabricables ?? 0,
                CostoUnitarioProducto = costo?.CostoUnitario ?? 0m,
                PrecioListaProducto = costo?.PrecioLista ?? 0m
            };

            return Ok(respuesta);
        }

        /// <summary>Cotizaciones agrupadas por mes para la grafica del panel.</summary>
        [HttpGet("cotizaciones-por-mes")]
        public async Task<IActionResult> GetCotizacionesPorMes()
        {
            var desde = DateTime.UtcNow.AddMonths(-5).Date;

            var datos = await _context.Cotizaciones
                .Where(c => c.FechaCotizacion >= desde)
                .GroupBy(c => new { c.FechaCotizacion.Year, c.FechaCotizacion.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Count(),
                    Monto = g.Sum(c => c.CostoTotal)
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

            return Ok(datos);
        }
    }
}
