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
    public class RecommendationsControllerTests
    {
        private AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Recommendations_Test_{Guid.NewGuid()}")
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
        public async Task GetRecommendations_NoReadings_ReturnsDefaultRecommendations()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var controller = new RecommendationsController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetRecommendations();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<RecommendationsPackageResponse>(okResult.Value);
            
            Assert.Equal("Bajo", response.NivelEstresActual);
            Assert.Equal(0, response.ScoreEstres);
            Assert.NotEmpty(response.Recomendaciones);
            Assert.Contains(response.Recomendaciones, r => r.Titulo == "Respiración Diafragmática");
            Assert.Single(response.DesafiosSugeridos);
            Assert.Equal(1, response.DesafiosSugeridos[0].ChallengeId); // Primera Lectura
        }

        [Fact]
        public async Task GetRecommendations_HighStress_ReturnsStressReliefRecommendations()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var now = DateTime.UtcNow;
            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 95,
                NivelEstres = 75, // High stress
                AuraDominante = "Rojo",
                FechaFin = now
            });
            await context.SaveChangesAsync();

            var controller = new RecommendationsController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetRecommendations();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<RecommendationsPackageResponse>(okResult.Value);
            
            Assert.Equal("Alto", response.NivelEstresActual);
            Assert.Equal(75, response.ScoreEstres);
            Assert.Contains(response.Recomendaciones, r => r.Titulo == "Respiración 4-7-8");
            Assert.Contains(response.DesafiosSugeridos, c => c.ChallengeId == 7); // Aura Verde Pura match
        }

        [Fact]
        public async Task GetRecommendations_LowStress_ReturnsCelebrationAndStreaks()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var now = DateTime.UtcNow;
            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 62,
                NivelEstres = 12, // Very low stress
                AuraDominante = "Verde",
                FechaFin = now
            });
            await context.SaveChangesAsync();

            var controller = new RecommendationsController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetRecommendations();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<RecommendationsPackageResponse>(okResult.Value);
            
            Assert.Equal("Muy Bajo", response.NivelEstresActual);
            Assert.Equal(12, response.ScoreEstres);
            Assert.Contains(response.Recomendaciones, r => r.Titulo == "Celebrar el Equilibrio");
            Assert.Contains(response.DesafiosSugeridos, c => c.ChallengeId == 4); // Semana Zen match
        }
    }
}
