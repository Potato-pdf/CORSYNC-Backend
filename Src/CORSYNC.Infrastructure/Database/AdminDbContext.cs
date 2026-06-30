using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
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
        }
    }
}
