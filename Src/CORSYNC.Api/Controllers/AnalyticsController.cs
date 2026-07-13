using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.DTOs;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public AnalyticsController(AdminDbContext context)
        {
            _context = context;
        }

        [HttpGet("trends")]
        public async Task<IActionResult> GetTrends([FromQuery] string period = "weekly", [FromQuery] int days = 30, [FromQuery] int weeks = 4, [FromQuery] int months = 6)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var now = DateTime.UtcNow;
            period = period.ToLower().Trim();

            List<TrendDataPointResponse> dataPoints = new();

            if (period == "daily")
            {
                if (days <= 0 || days > 365) days = 30;
                var limitDate = now.AddDays(-days);

                var readings = await _context.LecturasAura
                    .Where(la => la.UsuarioId == userId && la.FechaFin >= limitDate)
                    .ToListAsync();

                dataPoints = readings
                    .GroupBy(la => la.FechaFin.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new TrendDataPointResponse
                    {
                        Fecha = g.Key.ToString("yyyy-MM-dd"),
                        BpmPromedio = Math.Round(g.Average(la => la.BpmPromedio), 1),
                        BpmMaximo = Math.Round(g.Max(la => la.BpmMaximo), 1),
                        BpmMinimo = Math.Round(g.Min(la => la.BpmMinimo), 1),
                        EstresPromedio = Math.Round(g.Average(la => la.NivelEstres), 1),
                        GsrPromedio = Math.Round(g.Average(la => la.GsrVoltajePromedio), 3),
                        Sesiones = g.Count(),
                        DuracionPromedioSeg = (int)g.Average(la => la.DuracionSegundos)
                    })
                    .ToList();
            }
            else if (period == "weekly")
            {
                if (weeks <= 0 || weeks > 52) weeks = 4;
                var limitDate = now.AddDays(-weeks * 7);

                var readings = await _context.LecturasAura
                    .Where(la => la.UsuarioId == userId && la.FechaFin >= limitDate)
                    .ToListAsync();

                dataPoints = readings
                    .GroupBy(la => StartOfWeek(la.FechaFin, DayOfWeek.Monday))
                    .OrderBy(g => g.Key)
                    .Select(g => new TrendDataPointResponse
                    {
                        Fecha = g.Key.ToString("yyyy-MM-dd"),
                        BpmPromedio = Math.Round(g.Average(la => la.BpmPromedio), 1),
                        BpmMaximo = Math.Round(g.Max(la => la.BpmMaximo), 1),
                        BpmMinimo = Math.Round(g.Min(la => la.BpmMinimo), 1),
                        EstresPromedio = Math.Round(g.Average(la => la.NivelEstres), 1),
                        GsrPromedio = Math.Round(g.Average(la => la.GsrVoltajePromedio), 3),
                        Sesiones = g.Count(),
                        DuracionPromedioSeg = (int)g.Average(la => la.DuracionSegundos)
                    })
                    .ToList();
            }
            else if (period == "monthly")
            {
                if (months <= 0 || months > 24) months = 6;
                var limitDate = now.AddMonths(-months);

                var readings = await _context.LecturasAura
                    .Where(la => la.UsuarioId == userId && la.FechaFin >= limitDate)
                    .ToListAsync();

                dataPoints = readings
                    .GroupBy(la => new DateTime(la.FechaFin.Year, la.FechaFin.Month, 1))
                    .OrderBy(g => g.Key)
                    .Select(g => new TrendDataPointResponse
                    {
                        Fecha = g.Key.ToString("yyyy-MM-dd"),
                        BpmPromedio = Math.Round(g.Average(la => la.BpmPromedio), 1),
                        BpmMaximo = Math.Round(g.Max(la => la.BpmMaximo), 1),
                        BpmMinimo = Math.Round(g.Min(la => la.BpmMinimo), 1),
                        EstresPromedio = Math.Round(g.Average(la => la.NivelEstres), 1),
                        GsrPromedio = Math.Round(g.Average(la => la.GsrVoltajePromedio), 3),
                        Sesiones = g.Count(),
                        DuracionPromedioSeg = (int)g.Average(la => la.DuracionSegundos)
                    })
                    .ToList();
            }

            return Ok(new TrendsResponse
            {
                Period = period,
                DataPoints = dataPoints
            });
        }

        [HttpGet("distribution")]
        public async Task<IActionResult> GetDistribution()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var readings = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .ToListAsync();

            if (!readings.Any())
            {
                return Ok(new DistributionResponse());
            }

            var distribucionAuras = readings
                .GroupBy(la => la.AuraDominante)
                .ToDictionary(g => g.Key, g => g.Count());

            var distribucionEstres = new Dictionary<string, int>
            {
                { "Muy Bajo (0-20)", readings.Count(la => la.NivelEstres < 20) },
                { "Bajo (20-40)", readings.Count(la => la.NivelEstres >= 20 && la.NivelEstres < 40) },
                { "Moderado (40-60)", readings.Count(la => la.NivelEstres >= 40 && la.NivelEstres < 60) },
                { "Alto (60-80)", readings.Count(la => la.NivelEstres >= 60 && la.NivelEstres < 80) },
                { "Muy Alto (80-100)", readings.Count(la => la.NivelEstres >= 80) }
            };

            var distribucionBpm = new Dictionary<string, int>
            {
                { "Bradicardia (<60)", readings.Count(la => la.BpmPromedio < 60) },
                { "Normal (60-100)", readings.Count(la => la.BpmPromedio >= 60 && la.BpmPromedio <= 100) },
                { "Elevado (>100)", readings.Count(la => la.BpmPromedio > 100) }
            };

            return Ok(new DistributionResponse
            {
                DistribucionAuras = distribucionAuras,
                DistribucionEstres = distribucionEstres,
                DistribucionBpm = distribucionBpm
            });
        }

        [HttpGet("comparison")]
        public async Task<IActionResult> GetComparison()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            var now = DateTime.UtcNow;
            var semanaActualStart = now.AddDays(-7);
            var semanaAnteriorStart = now.AddDays(-14);

            var readingsActual = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId && la.FechaFin >= semanaActualStart)
                .ToListAsync();

            var readingsAnterior = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId && la.FechaFin >= semanaAnteriorStart && la.FechaFin < semanaActualStart)
                .ToListAsync();

            var actualBpm = readingsActual.Any() ? readingsActual.Average(la => la.BpmPromedio) : 0;
            var actualEstres = readingsActual.Any() ? readingsActual.Average(la => la.NivelEstres) : 0;
            var actualSesiones = readingsActual.Count;

            var anteriorBpm = readingsAnterior.Any() ? readingsAnterior.Average(la => la.BpmPromedio) : 0;
            var anteriorEstres = readingsAnterior.Any() ? readingsAnterior.Average(la => la.NivelEstres) : 0;
            var anteriorSesiones = readingsAnterior.Count;

            decimal bpmCambioPct = anteriorBpm > 0 ? Math.Round((actualBpm - anteriorBpm) / anteriorBpm * 100, 1) : 0;
            decimal estresCambioPct = anteriorEstres > 0 ? Math.Round((actualEstres - anteriorEstres) / anteriorEstres * 100, 1) : 0;
            decimal sesionesCambioPct = anteriorSesiones > 0 ? Math.Round((decimal)(actualSesiones - anteriorSesiones) / anteriorSesiones * 100, 1) : 0;

            string tendencia = "Estable";
            if (actualEstres < anteriorEstres && actualBpm <= anteriorBpm)
            {
                tendencia = "Mejorando";
            }
            else if (actualEstres > anteriorEstres + 5)
            {
                tendencia = "Necesita Cuidado";
            }

            return Ok(new ComparisonResponse
            {
                SemanaActual = new WeekSummaryResponse
                {
                    BpmPromedio = Math.Round(actualBpm, 1),
                    EstresPromedio = Math.Round(actualEstres, 1),
                    Sesiones = actualSesiones
                },
                SemanaAnterior = new WeekSummaryResponse
                {
                    BpmPromedio = Math.Round(anteriorBpm, 1),
                    EstresPromedio = Math.Round(anteriorEstres, 1),
                    Sesiones = anteriorSesiones
                },
                BpmCambioPct = bpmCambioPct,
                EstresCambioPct = estresCambioPct,
                SesionesCambioPct = sesionesCambioPct,
                Tendencia = tendencia
            });
        }

        private static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }
}
