using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using System;

namespace CORSYNC.Infrastructure.Database
{
    public class AdminDbContext : DbContext
    {
        public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; } = null!;
        public DbSet<Comentario> Comentarios { get; set; } = null!;
        public DbSet<Proveedor> Proveedores { get; set; } = null!;
        public DbSet<MateriaPrima> MateriasPrimas { get; set; } = null!;
        public DbSet<RecetaProducto> RecetasProductos { get; set; } = null!;
        public DbSet<Cotizacion> Cotizaciones { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<DispositivoUsuario> DispositivosUsuarios { get; set; } = null!;
        public DbSet<LecturaAura> LecturasAura { get; set; } = null!;
        public DbSet<Desafio> Desafios { get; set; } = null!;
        public DbSet<ProgresoDesafio> ProgresosDesafios { get; set; } = null!;
        public DbSet<Medalla> Medallas { get; set; } = null!;
        public DbSet<MedallaUsuario> MedallasUsuarios { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(rt => rt.Token).IsUnique();
                entity.HasOne(rt => rt.Usuario)
                      .WithMany()
                      .HasForeignKey(rt => rt.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DispositivoUsuario>(entity =>
            {
                entity.HasIndex(du => du.DispositivoId).IsUnique();
                entity.HasOne(du => du.Usuario)
                      .WithMany()
                      .HasForeignKey(du => du.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LecturaAura>(entity =>
            {
                entity.HasOne(la => la.Usuario)
                      .WithMany()
                      .HasForeignKey(la => la.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProgresoDesafio>(entity =>
            {
                entity.HasIndex(pd => new { pd.UsuarioId, pd.DesafioId }).IsUnique();
                entity.HasOne(pd => pd.Usuario)
                      .WithMany()
                      .HasForeignKey(pd => pd.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(pd => pd.Desafio)
                      .WithMany()
                      .HasForeignKey(pd => pd.DesafioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<MedallaUsuario>(entity =>
            {
                entity.HasIndex(mu => new { mu.UsuarioId, mu.MedallaId }).IsUnique();
                entity.HasOne(mu => mu.Usuario)
                      .WithMany()
                      .HasForeignKey(mu => mu.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(mu => mu.Medalla)
                      .WithMany()
                      .HasForeignKey(mu => mu.MedallaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed initial data for costing (vidrio, marcos, etc.)
            modelBuilder.Entity<MateriaPrima>().HasData(
                new MateriaPrima { Id = 1, Nombre = "Vidrio de dos vias (cm2)", CostoUnidad = 0.05m, UnidadMedida = "cm2", Stock = 100000 },
                new MateriaPrima { Id = 2, Nombre = "Marco de Madera Rustico (m)", CostoUnidad = 15.00m, UnidadMedida = "m", Stock = 500 },
                new MateriaPrima { Id = 3, Nombre = "Tira LED RGB (m)", CostoUnidad = 4.50m, UnidadMedida = "m", Stock = 1000 },
                new MateriaPrima { Id = 4, Nombre = "Sensor MAX30102", CostoUnidad = 8.00m, UnidadMedida = "unidad", Stock = 200 },
                new MateriaPrima { Id = 5, Nombre = "Placa ESP32", CostoUnidad = 12.00m, UnidadMedida = "unidad", Stock = 150 }
            );

            modelBuilder.Entity<RecetaProducto>().HasData(
                new RecetaProducto { Id = 1, NombreProducto = "Espejo CORSYNC Standard", MateriaPrimaId = 4, CantidadRequerida = 1 }, // 1 sensor MAX30102
                new RecetaProducto { Id = 2, NombreProducto = "Espejo CORSYNC Standard", MateriaPrimaId = 5, CantidadRequerida = 1 }  // 1 ESP32
            );

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<Usuario>().HasData(
                new Usuario 
                { 
                    Id = 1, 
                    Username = "admin", 
                    Email = "admin@corsync.com",
                    PasswordHash = "$2a$11$UZ8mNYO7Ss0T41oYzfqHt.ILCFlrmVxEUZr6/i1cdBZ1qAxBhrBj.", 
                    NombreCompleto = "Administrador CORSYNC",
                    Role = "Admin",
                    FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Activo = true
                },
                new Usuario 
                { 
                    Id = 2, 
                    Username = "cliente", 
                    Email = "cliente@corsync.com",
                    PasswordHash = "$2a$11$fOK8ihp4BxXTrxjzGqw8Gu6Zdv1ZFFmA4XMX5KD26UjdsyLaovOfO", 
                    NombreCompleto = "Cliente Demostración",
                    Role = "Cliente",
                    FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Activo = true
                }
            );

            // Seed initial Challenges (Desafios)
            modelBuilder.Entity<Desafio>().HasData(
                new Desafio { Id = 1, Titulo = "Primera Lectura", Descripcion = "Realiza tu primer escaneo de aura", Icono = "🌟", Tipo = "Sesiones", MetaObjetivo = 1, UnidadMedida = "sesiones", Puntos = 10, Activo = true },
                new Desafio { Id = 2, Titulo = "Explorador del Aura", Descripcion = "Completa 10 sesiones de escaneo", Icono = "🔮", Tipo = "Sesiones", MetaObjetivo = 10, UnidadMedida = "sesiones", Puntos = 50, Activo = true },
                new Desafio { Id = 3, Titulo = "Maestro del Aura", Descripcion = "Completa 50 sesiones de escaneo", Icono = "👁️", Tipo = "Sesiones", MetaObjetivo = 50, UnidadMedida = "sesiones", Puntos = 200, Activo = true },
                new Desafio { Id = 4, Titulo = "Semana Zen", Descripcion = "Completa sesiones de escaneo durante 7 días seguidos", Icono = "🧘", Tipo = "Racha", MetaObjetivo = 7, UnidadMedida = "días", Puntos = 100, Activo = true },
                new Desafio { Id = 5, Titulo = "Mes de Constancia", Descripcion = "Completa sesiones de escaneo durante 30 días seguidos", Icono = "📅", Tipo = "Racha", MetaObjetivo = 30, UnidadMedida = "días", Puntos = 500, Activo = true },
                new Desafio { Id = 6, Titulo = "Corazón Sereno", Descripcion = "Logra 5 sesiones con BPM promedio bajo (menos de 65 BPM)", Icono = "💚", Tipo = "BpmBajo", MetaObjetivo = 5, UnidadMedida = "sesiones", Puntos = 75, Activo = true },
                new Desafio { Id = 7, Titulo = "Aura Verde Pura", Descripcion = "Logra el aura Verde (Calma) 10 veces en tus lecturas", Icono = "🌿", Tipo = "AuraVerde", MetaObjetivo = 10, UnidadMedida = "sesiones", Puntos = 150, Activo = true },
                new Desafio { Id = 8, Titulo = "Explorador Cromático", Descripcion = "Descubre 5 colores de aura distintos en tus escaneos", Icono = "🌈", Tipo = "Exploracion", MetaObjetivo = 5, UnidadMedida = "auras", Puntos = 100, Activo = true }
            );

            // Seed initial Medals (Medallas)
            modelBuilder.Entity<Medalla>().HasData(
                new Medalla { Id = 1, Nombre = "Primer Escaneo", Descripcion = "Completaste tu primera lectura de aura", Icono = "🏅", Condicion = "PrimeraSesion", ValorCondicion = 1 },
                new Medalla { Id = 2, Nombre = "Dedicado", Descripcion = "Realizaste 25 lecturas de aura en total", Icono = "🥈", Condicion = "SesionesTotales", ValorCondicion = 25 },
                new Medalla { Id = 3, Nombre = "Veterano", Descripcion = "Realizaste 100 lecturas de aura en total", Icono = "🥇", Condicion = "SesionesTotales", ValorCondicion = 100 },
                new Medalla { Id = 4, Nombre = "Consistente", Descripcion = "Lograste una racha de 14 días consecutivos", Icono = "🔥", Condicion = "RachaDias", ValorCondicion = 14 },
                new Medalla { Id = 5, Nombre = "Imparable", Descripcion = "Lograste una racha de 30 días consecutivos", Icono = "⚡", Condicion = "RachaDias", ValorCondicion = 30 },
                new Medalla { Id = 6, Nombre = "Completista", Descripcion = "Completaste 5 desafíos espirituales", Icono = "🏆", Condicion = "DesafiosCompletados", ValorCondicion = 5 }
            );
        }
    }
}
