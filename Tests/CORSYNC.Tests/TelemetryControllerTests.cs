using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CORSYNC.Api.Controllers;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Tests
{
    public class TelemetryControllerTests
    {
        private TelemetryDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<TelemetryDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Telemetry_Test_{System.Guid.NewGuid()}")
                .Options;

            return new TelemetryDbContext(options);
        }

        [Fact]
        public async Task GetHeartHistory_ReturnsLastInsertedReadings()
        {
            // Arrange
            using var context = GetDbContext();
            context.LecturasCorazon.Add(new LecturaCorazon { DispositivoId = "ESP32_1", BPM = 70m, IR = 100000 });
            context.LecturasCorazon.Add(new LecturaCorazon { DispositivoId = "ESP32_2", BPM = 80m, IR = 110000 });
            await context.SaveChangesAsync();

            var controller = new TelemetryController(context);

            // Act
            var actionResult = await controller.GetHeartHistory();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var list = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<LecturaCorazon>>(okResult.Value);
            Assert.Equal(2, System.Linq.Enumerable.Count(list));
        }

        [Fact]
        public void GetPielTelemetry_Returns501NotImplementedWithPendingNotice()
        {
            // Arrange
            using var context = GetDbContext();
            var controller = new TelemetryController(context);

            // Act
            var actionResult = controller.GetPielTelemetry();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(501, statusCodeResult.StatusCode);
            
            // Check that the returned object contains pending markers
            var json = System.Text.Json.JsonSerializer.Serialize(statusCodeResult.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal("PENDIENTE", doc.RootElement.GetProperty("Estado").GetString());
            Assert.Contains("GSR", doc.RootElement.GetProperty("Mensaje").GetString());
        }
    }
}
