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
    public class UserControllerTests
    {
        private AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_User_Test_{Guid.NewGuid()}")
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
        public async Task GetProfile_Authenticated_ReturnsExtendedUserInfo()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario
            {
                Username = "testuser",
                Email = "test@corsync.com",
                PasswordHash = "hash",
                NombreCompleto = "Test User",
                NombreEspiritual = "Alma Elevada",
                SignoZodiacal = "Leo",
                FotoUrl = "http://photo.com",
                Activo = true
            };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var controller = new UserController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetProfile();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var userInfo = Assert.IsType<UserInfo>(okResult.Value);
            Assert.Equal(user.Id, userInfo.Id);
            Assert.Equal("Alma Elevada", userInfo.NombreEspiritual);
            Assert.Equal("Leo", userInfo.SignoZodiacal);
            Assert.Equal("http://photo.com", userInfo.FotoUrl);
        }

        [Fact]
        public async Task UpdateProfile_ValidData_ReturnsUpdatedProfile()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario
            {
                Username = "testuser",
                Email = "test@corsync.com",
                PasswordHash = "hash",
                NombreCompleto = "Test User",
                Activo = true
            };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var controller = new UserController(context);
            AuthenticateController(controller, user.Id);

            var request = new UpdateProfileRequest
            {
                NombreCompleto = "Updated Name",
                NombreEspiritual = "Zen Master",
                SignoZodiacal = "Piscis",
                FotoUrl = "http://newphoto.com"
            };

            // Act
            var result = await controller.UpdateProfile(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var userInfo = Assert.IsType<UserInfo>(okResult.Value);
            Assert.Equal("Updated Name", userInfo.NombreCompleto);
            Assert.Equal("Zen Master", userInfo.NombreEspiritual);
            Assert.Equal("Piscis", userInfo.SignoZodiacal);
            Assert.Equal("http://newphoto.com", userInfo.FotoUrl);

            // Verify in DB
            var dbUser = await context.Usuarios.FindAsync(user.Id);
            Assert.NotNull(dbUser);
            Assert.Equal("Zen Master", dbUser.NombreEspiritual);
        }

        [Fact]
        public async Task UpdateProfile_InvalidSigno_ReturnsBadRequest()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario
            {
                Username = "testuser",
                Email = "test@corsync.com",
                PasswordHash = "hash",
                Activo = true
            };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var controller = new UserController(context);
            AuthenticateController(controller, user.Id);

            var request = new UpdateProfileRequest
            {
                SignoZodiacal = "Ofiuco" // Invalid astrological sign
            };

            // Act
            var result = await controller.UpdateProfile(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetStats_NoReadings_ReturnsNeutralStats()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "test", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var controller = new UserController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var stats = Assert.IsType<UserStatsResponse>(okResult.Value);
            Assert.Equal(0, stats.SesionesTotales);
            Assert.Equal(0, stats.BpmPromedio);
            Assert.Equal("Ninguna", stats.AuraDominante);
            Assert.Equal(0, stats.RachaActualDias);
        }

        [Fact]
        public async Task GetStats_WithReadings_ReturnsCorrectCalculations()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "test", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var today = DateTime.UtcNow;
            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 70,
                NivelEstres = 20,
                AuraDominante = "Verde",
                FechaFin = today
            });
            context.LecturasAura.Add(new LecturaAura
            {
                UsuarioId = user.Id,
                DispositivoId = "ESP32",
                BpmPromedio = 80,
                NivelEstres = 30,
                AuraDominante = "Verde",
                FechaFin = today.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var controller = new UserController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var stats = Assert.IsType<UserStatsResponse>(okResult.Value);
            Assert.Equal(2, stats.SesionesTotales);
            Assert.Equal(75, stats.BpmPromedio);
            Assert.Equal(25, stats.NivelEstresPromedio);
            Assert.Equal("Verde", stats.AuraDominante);
            Assert.Equal(2, stats.RachaActualDias);
        }
    }
}
