using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using CORSYNC.Api.Controllers;
using CORSYNC.Core.Domain;
using CORSYNC.Core.DTOs;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Tests
{
    public class ReadingsControllerTests
    {
        private AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Readings_Test_{Guid.NewGuid()}")
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
        public async Task GetAll_ReturnsOnlyUserReadings()
        {
            // Arrange
            using var context = GetDbContext();
            var user1 = new Usuario { Username = "user1", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            var user2 = new Usuario { Username = "user2", Email = "c@d.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.AddRange(user1, user2);
            await context.SaveChangesAsync();

            context.LecturasAura.Add(new LecturaAura { UsuarioId = user1.Id, DispositivoId = "ESP1", AuraDominante = "Verde", FechaFin = DateTime.UtcNow });
            context.LecturasAura.Add(new LecturaAura { UsuarioId = user2.Id, DispositivoId = "ESP2", AuraDominante = "Azul", FechaFin = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var gamificationMock = new Mock<IGamificationService>();
            var controller = new ReadingsController(context, gamificationMock.Object);
            AuthenticateController(controller, user1.Id);

            // Act
            var result = await controller.GetAll();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var readings = Assert.IsType<List<ReadingResponse>>(okResult.Value);
            Assert.Single(readings);
            Assert.Equal("ESP1", readings[0].DispositivoId);
        }

        [Fact]
        public async Task GetById_OwnReading_ReturnsOk()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var reading = new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP1", AuraDominante = "Verde", FechaFin = DateTime.UtcNow };
            context.LecturasAura.Add(reading);
            await context.SaveChangesAsync();

            var gamificationMock = new Mock<IGamificationService>();
            var controller = new ReadingsController(context, gamificationMock.Object);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetById(reading.Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ReadingResponse>(okResult.Value);
            Assert.Equal(reading.Id, response.Id);
        }

        [Fact]
        public async Task GetById_OtherUserReading_ReturnsForbidden()
        {
            // Arrange
            using var context = GetDbContext();
            var user1 = new Usuario { Username = "user1", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            var user2 = new Usuario { Username = "user2", Email = "c@d.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.AddRange(user1, user2);
            await context.SaveChangesAsync();

            var reading = new LecturaAura { UsuarioId = user2.Id, DispositivoId = "ESP1", AuraDominante = "Verde", FechaFin = DateTime.UtcNow };
            context.LecturasAura.Add(reading);
            await context.SaveChangesAsync();

            var gamificationMock = new Mock<IGamificationService>();
            var controller = new ReadingsController(context, gamificationMock.Object);
            AuthenticateController(controller, user1.Id); // Auth as user1, accessing user2's reading

            // Act
            var result = await controller.GetById(reading.Id);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Create_ValidRequest_SavesToDbAndTriggersGamification()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var gamificationMock = new Mock<IGamificationService>();
            var controller = new ReadingsController(context, gamificationMock.Object);
            AuthenticateController(controller, user.Id);

            var request = new CreateReadingRequest
            {
                DispositivoId = "ESP_TEST",
                BpmPromedio = 62.5m, // BPM < 65 to check BpmBajo trigger
                BpmMaximo = 75,
                BpmMinimo = 58,
                GsrRawPromedio = 1100,
                GsrVoltajePromedio = 0.88m,
                NivelEstres = 12.5m,
                AuraDominante = "Verde", // Aura Verde to check AuraVerde trigger
                Notas = "Meditación",
                DuracionSegundos = 180,
                FechaInicio = DateTime.UtcNow.AddMinutes(-3),
                FechaFin = DateTime.UtcNow
            };

            // Act
            var result = await controller.Create(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var response = Assert.IsType<ReadingResponse>(createdResult.Value);
            Assert.True(response.Id > 0);
            Assert.Equal("ESP_TEST", response.DispositivoId);

            // Verify DB count
            Assert.Equal(1, await context.LecturasAura.CountAsync());

            // Verify Gamification triggers
            gamificationMock.Verify(g => g.ActualizarProgresoDesafioAsync(user.Id, "Sesiones", 1), Times.Once);
            gamificationMock.Verify(g => g.ActualizarProgresoDesafioAsync(user.Id, "BpmBajo", 1), Times.Once);
            gamificationMock.Verify(g => g.ActualizarProgresoDesafioAsync(user.Id, "AuraVerde", 1), Times.Once);
            gamificationMock.Verify(g => g.ActualizarProgresoDesafioAsync(user.Id, "Racha", 0), Times.Once);
            gamificationMock.Verify(g => g.ActualizarProgresoDesafioAsync(user.Id, "Exploracion", 0), Times.Once);
            gamificationMock.Verify(g => g.VerificarMedallasAsync(user.Id), Times.Once);
        }

        [Fact]
        public async Task GetSummary_ReturnsCorrectAggregates()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            context.LecturasAura.Add(new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP1", BpmPromedio = 70, NivelEstres = 10, AuraDominante = "Verde", FechaFin = DateTime.UtcNow });
            context.LecturasAura.Add(new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP1", BpmPromedio = 80, NivelEstres = 20, AuraDominante = "Azul", FechaFin = DateTime.UtcNow });
            context.LecturasAura.Add(new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP1", BpmPromedio = 90, NivelEstres = 30, AuraDominante = "Verde", FechaFin = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var gamificationMock = new Mock<IGamificationService>();
            var controller = new ReadingsController(context, gamificationMock.Object);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetSummary();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var summary = Assert.IsType<ReadingSummaryResponse>(okResult.Value);
            
            Assert.Equal(80m, summary.BpmPromedioGlobal);
            Assert.Equal(20m, summary.NivelEstresPromedio);
            Assert.Equal(3, summary.TotalSesiones);
            Assert.Equal("Verde", summary.AuraMasFrecuente);
            Assert.Equal(2, summary.DistribucionAuras["Verde"]);
            Assert.Equal(1, summary.DistribucionAuras["Azul"]);
        }
    }
}
