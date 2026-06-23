using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CORSYNC.Api.Controllers;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Tests
{
    public class CotizacionControllerTests
    {
        private AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Admin_Test_{System.Guid.NewGuid()}")
                .Options;

            var context = new AdminDbContext(options);
            context.Database.EnsureCreated(); // Seeds the initial materials data
            return context;
        }

        [Fact]
        public async Task CalcularCotizacion_WithValidParameters_ReturnsCorrectCostBreakdown()
        {
            // Arrange
            using var context = GetDbContext();
            var controller = new CotizacionController(context);

            var request = new CotizacionController.CotizacionRequest
            {
                NombreCliente = "Cliente Prueba",
                NombreProducto = "Espejo CORSYNC Standard",
                AnchoCm = 50m,
                AltoCm = 80m,
                DensidadLedId = 3 // LED strip
            };

            // Act
            var actionResult = await controller.CalcularCotizacion(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var cotizacionJson = doc.RootElement.GetProperty("Cotizacion").GetRawText();
            var cotizacion = System.Text.Json.JsonSerializer.Deserialize<Cotizacion>(cotizacionJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(cotizacion);
            Assert.Equal("Cliente Prueba", cotizacion!.NombreCliente);
            Assert.Equal(50m, cotizacion.Ancho);
            Assert.Equal(80m, cotizacion.Alto);

            // Calculations verification:
            // Area = 50 * 80 = 4000 cm2. Vidrio cost = 4000 * 0.05 = 200.00
            // Perimeter = 2 * (50 + 80) = 260 cm = 2.6 m. Marco cost = 2.6 * 15 = 39.00
            // LED cost = 2.6 * 4.5 = 11.70
            // Electronics cost = 8 (sensor) + 12 (ESP32) = 20.00
            // Material cost total = 200 + 39 + 11.7 + 20 = 270.70
            // Assembly overhead (30%) = 81.21
            // Total = 270.70 + 81.21 = 351.91

            Assert.Equal(351.91m, cotizacion.CostoTotal);
        }

        [Fact]
        public async Task CalcularCotizacion_WithNegativeDimensions_ReturnsBadRequest()
        {
            // Arrange
            using var context = GetDbContext();
            var controller = new CotizacionController(context);

            var request = new CotizacionController.CotizacionRequest
            {
                NombreCliente = "Cliente Prueba",
                AnchoCm = -10m,
                AltoCm = 50m
            };

            // Act
            var actionResult = await controller.CalcularCotizacion(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(actionResult);
        }
    }
}
