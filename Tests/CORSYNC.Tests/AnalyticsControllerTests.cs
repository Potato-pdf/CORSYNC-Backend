using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CORSYNC.Api.Controllers;
using CORSYNC.Core.Domain;
using CORSYNC.Core.DTOs;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Tests
{
    public class AnalyticsControllerTests
    {
        private AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Analytics_Test_{Guid.NewGuid()}")
                .Options;
            return new AdminDbContext(options);
        }

        private void AuthenticateController(ControllerBase controller, int userId)
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        [Fact]
        public async Task GetTrends_Daily_ReturnsCorrectDataPoints()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var today = DateTime.UtcNow.Date;
            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 70,
                BpmMaximo = 80,
                BpmMinimo = 60,
                NivelEstres = 30,
                GsrVoltajePromedio = 1.2m,
                DuracionSegundos = 100,
                FechaFin = today.AddHours(12)
            });
            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 80,
                BpmMaximo = 90,
                BpmMinimo = 70,
                NivelEstres = 40,
                GsrVoltajePromedio = 1.4m,
                DuracionSegundos = 200,
                FechaFin = today.AddHours(16)
            });
            await context.SaveChangesAsync();

            var controller = new AnalyticsController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetTrends(period: "daily", days: 7);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<TrendsResponse>(okResult.Value);
            Assert.Equal("daily", response.Period);
            Assert.Single(response.DataPoints);
            var point = response.DataPoints[0];
            Assert.Equal(today.ToString("yyyy-MM-dd"), point.Fecha);
            Assert.Equal(75, point.BpmPromedio);
            Assert.Equal(90, point.BpmMaximo);
            Assert.Equal(60, point.BpmMinimo);
            Assert.Equal(35, point.EstresPromedio);
            Assert.Equal(1.3m, point.GsrPromedio);
            Assert.Equal(2, point.Sesiones);
            Assert.Equal(150, point.DuracionPromedioSeg);
        }

        [Fact]
        public async Task GetDistribution_WithData_ReturnsCorrectAuraAndStressDistribution()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 70,
                NivelEstres = 15,
                AuraDominante = "Verde",
                FechaFin = DateTime.UtcNow
            });
            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 105,
                NivelEstres = 75,
                AuraDominante = "Rojo",
                FechaFin = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = new AnalyticsController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetDistribution();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<DistributionResponse>(okResult.Value);
            
            Assert.Equal(2, response.DistribucionAuras.Count);
            Assert.Equal(1, response.DistribucionAuras["Verde"]);
            Assert.Equal(1, response.DistribucionAuras["Rojo"]);

            Assert.Equal(1, response.DistribucionEstres["Muy Bajo (0-20)"]);
            Assert.Equal(1, response.DistribucionEstres["Alto (60-80)"]);
            Assert.Equal(0, response.DistribucionEstres["Moderado (40-60)"]);

            Assert.Equal(1, response.DistribucionBpm["Normal (60-100)"]);
            Assert.Equal(1, response.DistribucionBpm["Elevado (>100)"]);
        }

        [Fact]
        public async Task GetComparison_Weekly_ReturnsCalculatedAveragesAndTrends()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var now = DateTime.UtcNow;
            // Current week readings
            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 70,
                NivelEstres = 20,
                FechaFin = now.AddDays(-2)
            });
            // Previous week readings
            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 80,
                NivelEstres = 40,
                FechaFin = now.AddDays(-9)
            });
            await context.SaveChangesAsync();

            var controller = new AnalyticsController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetComparison();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ComparisonResponse>(okResult.Value);
            
            Assert.Equal(70, response.SemanaActual.BpmPromedio);
            Assert.Equal(20, response.SemanaActual.EstresPromedio);
            Assert.Equal(1, response.SemanaActual.Sesiones);

            Assert.Equal(80, response.SemanaAnterior.BpmPromedio);
            Assert.Equal(40, response.SemanaAnterior.EstresPromedio);
            Assert.Equal(1, response.SemanaAnterior.Sesiones);

            Assert.Equal(-12.5m, response.BpmCambioPct);
            Assert.Equal(-50m, response.EstresCambioPct);
            Assert.Equal(0m, response.SesionesCambioPct);
            Assert.Equal("Mejorando", response.Tendencia);
        }
    }
}
