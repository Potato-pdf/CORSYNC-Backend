using System;
using System.Collections.Generic;
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
    public class MedalsControllerTests
    {
        private AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Medals_Test_{Guid.NewGuid()}")
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
        public async Task GetMyMedals_ReturnsUnlockedMedalsWithDetails()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);

            var m1 = new Medalla { Id = 1, Nombre = "Medalla 1", Descripcion = "Desc 1", Icono = "🥇", Condicion = "PrimeraSesion", ValorCondicion = 1 };
            var m2 = new Medalla { Id = 2, Nombre = "Medalla 2", Descripcion = "Desc 2", Icono = "🥈", Condicion = "SesionesTotales", ValorCondicion = 10 };
            context.Medallas.AddRange(m1, m2);
            await context.SaveChangesAsync();

            // Unlock only m1
            context.MedallasUsuarios.Add(new MedallaUsuario { UsuarioId = user.Id, MedallaId = m1.Id, FechaObtenida = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var controller = new MedalsController(context);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetMyMedals();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responses = Assert.IsType<List<MedalResponse>>(okResult.Value);
            
            Assert.Single(responses);
            Assert.Equal("Medalla 1", responses[0].Nombre);
            Assert.Equal("🥇", responses[0].Icono);
        }
    }
}
