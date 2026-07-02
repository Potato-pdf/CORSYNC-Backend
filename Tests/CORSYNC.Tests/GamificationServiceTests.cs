using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Database;
using CORSYNC.Infrastructure.Gamification;

namespace CORSYNC.Tests
{
    public class GamificationServiceTests
    {
        private AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_GamificationService_Test_{Guid.NewGuid()}")
                .Options;
            return new AdminDbContext(options);
        }

        [Fact]
        public async Task ActualizarProgresoDesafio_Increment_Works()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);

            var d = new Desafio { Id = 1, Titulo = "Sesiones", Descripcion = "Desc", Icono = "🌟", Tipo = "Sesiones", MetaObjetivo = 3, UnidadMedida = "sesiones", Puntos = 10, Activo = true };
            context.Desafios.Add(d);
            await context.SaveChangesAsync();

            var service = new GamificationService(context);

            // Act: Increment 1
            await service.ActualizarProgresoDesafioAsync(user.Id, "Sesiones", 1);

            // Assert
            var p1 = await context.ProgresosDesafios.FirstOrDefaultAsync(p => p.UsuarioId == user.Id && p.DesafioId == d.Id);
            Assert.NotNull(p1);
            Assert.Equal(1, p1.ProgresoActual);
            Assert.False(p1.Completado);

            // Act: Increment 2 (should complete)
            await service.ActualizarProgresoDesafioAsync(user.Id, "Sesiones", 2);

            // Assert
            var p2 = await context.ProgresosDesafios.FirstOrDefaultAsync(p => p.UsuarioId == user.Id && p.DesafioId == d.Id);
            Assert.NotNull(p2);
            Assert.Equal(3, p2.ProgresoActual);
            Assert.True(p2.Completado);
            Assert.NotNull(p2.FechaCompletado);
        }

        [Fact]
        public async Task ActualizarProgresoDesafio_Exploracion_CalculatesUniqueAuras()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);

            var d = new Desafio { Id = 1, Titulo = "Exploración", Descripcion = "Desc", Icono = "🌈", Tipo = "Exploracion", MetaObjetivo = 5, UnidadMedida = "auras", Puntos = 10, Activo = true };
            context.Desafios.Add(d);
            
            // Add readings with 3 unique auras
            context.LecturasAura.Add(new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP", AuraDominante = "Verde", FechaFin = DateTime.UtcNow });
            context.LecturasAura.Add(new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP", AuraDominante = "Verde", FechaFin = DateTime.UtcNow });
            context.LecturasAura.Add(new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP", AuraDominante = "Azul", FechaFin = DateTime.UtcNow });
            context.LecturasAura.Add(new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP", AuraDominante = "Amarilla", FechaFin = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var service = new GamificationService(context);

            // Act
            await service.ActualizarProgresoDesafioAsync(user.Id, "Exploracion", 0);

            // Assert
            var p = await context.ProgresosDesafios.FirstOrDefaultAsync(pd => pd.UsuarioId == user.Id && pd.DesafioId == d.Id);
            Assert.NotNull(p);
            Assert.Equal(3, p.ProgresoActual);
            Assert.False(p.Completado);
        }

        [Fact]
        public async Task VerificarMedallas_Unlocks_PrimeraSesion_WhenReadingExists()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);

            var medal = new Medalla { Id = 1, Nombre = "Primer Escaneo", Descripcion = "Desc", Icono = "🏅", Condicion = "PrimeraSesion", ValorCondicion = 1 };
            context.Medallas.Add(medal);

            // 1 reading
            context.LecturasAura.Add(new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP", AuraDominante = "Verde", FechaFin = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var service = new GamificationService(context);

            // Act
            await service.VerificarMedallasAsync(user.Id);

            // Assert
            var unlocked = await context.MedallasUsuarios.FirstOrDefaultAsync(mu => mu.UsuarioId == user.Id && mu.MedallaId == medal.Id);
            Assert.NotNull(unlocked);
        }

        [Fact]
        public async Task VerificarMedallas_DoesNotUnlock_WhenConditionNotMet()
        {
            // Arrange
            using var context = GetDbContext();
            var user = new Usuario { Username = "user", Email = "a@b.com", PasswordHash = "hash", Activo = true };
            context.Usuarios.Add(user);

            var medal = new Medalla { Id = 1, Nombre = "Dedicado", Descripcion = "Desc", Icono = "🏅", Condicion = "SesionesTotales", ValorCondicion = 25 };
            context.Medallas.Add(medal);

            // Only 5 readings (need 25)
            for (int i = 0; i < 5; i++)
            {
                context.LecturasAura.Add(new LecturaAura { UsuarioId = user.Id, DispositivoId = "ESP", AuraDominante = "Verde", FechaFin = DateTime.UtcNow });
            }
            await context.SaveChangesAsync();

            var service = new GamificationService(context);

            // Act
            await service.VerificarMedallasAsync(user.Id);

            // Assert
            var unlocked = await context.MedallasUsuarios.FirstOrDefaultAsync(mu => mu.UsuarioId == user.Id && mu.MedallaId == medal.Id);
            Assert.Null(unlocked);
        }
    }
}
