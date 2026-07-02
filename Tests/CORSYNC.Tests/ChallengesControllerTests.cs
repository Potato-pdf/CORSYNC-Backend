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
    public class ChallengesControllerTests
    {
        private AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Challenges_Test_{Guid.NewGuid()}")
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
        public async Task GetAll_ReturnsAllActiveChallengesWithProgress()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);

            var c1 = new Desafio { Id = 1, Titulo = "D1", Descripcion = "D1 desc", Icono = "1", Tipo = "Sesiones", MetaObjetivo = 5, UnidadMedida = "sesiones", Puntos = 10, Activo = true };
            var c2 = new Desafio { Id = 2, Titulo = "D2", Descripcion = "D2 desc", Icono = "2", Tipo = "Sesiones", MetaObjetivo = 10, UnidadMedida = "sesiones", Puntos = 20, Activo = true };
            var c3 = new Desafio { Id = 3, Titulo = "D3", Descripcion = "D3 desc", Icono = "3", Tipo = "Sesiones", MetaObjetivo = 10, UnidadMedida = "sesiones", Puntos = 30, Activo = false }; // Inactive
            context.Desafios.AddRange(c1, c2, c3);
            await context.SaveChangesAsync();

            // Progress for c1 only
            context.ProgresosDesafios.Add(new ProgresoDesafio { UsuarioId = user.Id, DesafioId = c1.Id, ProgresoActual = 2, Completado = false });
            await context.SaveChangesAsync();

            var gamificationMock = new Mock<IGamificationService>();
            var controller = new ChallengesController(context, gamificationMock.Object);
            AuthenticateController(controller, user.Id);

            // Act
            var result = await controller.GetAll();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responses = Assert.IsType<List<ChallengeResponse>>(okResult.Value);
            
            Assert.Equal(2, responses.Count); // Only active ones
            
            var r1 = responses.Find(r => r.Id == c1.Id);
            Assert.NotNull(r1);
            Assert.Equal(2, r1.ProgresoActual);
            Assert.Equal(40.0, r1.PorcentajeProgreso);
            Assert.False(r1.Completado);

            var r2 = responses.Find(r => r.Id == c2.Id);
            Assert.NotNull(r2);
            Assert.Equal(0, r2.ProgresoActual);
            Assert.Equal(0.0, r2.PorcentajeProgreso);
            Assert.False(r2.Completado);
        }

        [Fact]
        public async Task UpdateProgress_ReachesMeta_MarksCompletedAndTriggersMedalsCheck()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);

            var challenge = new Desafio { Id = 1, Titulo = "D1", Descripcion = "D1 desc", Icono = "1", Tipo = "Sesiones", MetaObjetivo = 5, UnidadMedida = "sesiones", Puntos = 10, Activo = true };
            context.Desafios.Add(challenge);
            await context.SaveChangesAsync();

            var gamificationMock = new Mock<IGamificationService>();
            var controller = new ChallengesController(context, gamificationMock.Object);
            AuthenticateController(controller, user.Id);

            var request = new UpdateProgressRequest { ProgresoActual = 6 }; // Greater than meta

            // Act
            var result = await controller.UpdateProgress(challenge.Id, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ChallengeResponse>(okResult.Value);
            Assert.Equal(5, response.ProgresoActual); // Capped at meta
            Assert.True(response.Completado);
            Assert.NotNull(response.FechaCompletado);

            // Verify in DB
            var dbProgress = await context.ProgresosDesafios.FirstOrDefaultAsync(pd => pd.UsuarioId == user.Id && pd.DesafioId == challenge.Id);
            Assert.NotNull(dbProgress);
            Assert.True(dbProgress.Completado);

            // Verify medals check triggered
            gamificationMock.Verify(g => g.VerificarMedallasAsync(user.Id), Times.Once);
        }

        [Fact]
        public async Task UpdateProgress_AlreadyCompleted_ReturnsConflict()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);

            var challenge = new Desafio { Id = 1, Titulo = "D1", Descripcion = "D1 desc", Icono = "1", Tipo = "Sesiones", MetaObjetivo = 5, UnidadMedida = "sesiones", Puntos = 10, Activo = true };
            context.Desafios.Add(challenge);
            await context.SaveChangesAsync();

            context.ProgresosDesafios.Add(new ProgresoDesafio { UsuarioId = user.Id, DesafioId = challenge.Id, ProgresoActual = 5, Completado = true, FechaCompletado = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var gamificationMock = new Mock<IGamificationService>();
            var controller = new ChallengesController(context, gamificationMock.Object);
            AuthenticateController(controller, user.Id);

            var request = new UpdateProgressRequest { ProgresoActual = 3 };

            // Act
            var result = await controller.UpdateProgress(challenge.Id, request);

            // Assert
            Assert.IsType<ConflictObjectResult>(result);
        }
    }
}
