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

        // --- Plataforma comercial ---
        public DbSet<Producto> Productos { get; set; } = null!;
        public DbSet<CompraProveedor> ComprasProveedores { get; set; } = null!;
        public DbSet<DetalleCompraProveedor> DetallesCompraProveedor { get; set; } = null!;
        public DbSet<CompraCliente> ComprasClientes { get; set; } = null!;
        public DbSet<DocumentoProducto> DocumentosProductos { get; set; } = null!;
        public DbSet<MensajeContacto> MensajesContacto { get; set; } = null!;
        public DbSet<PreguntaFrecuente> PreguntasFrecuentes { get; set; } = null!;
        public DbSet<CorreoEnviado> CorreosEnviados { get; set; } = null!;
        public DbSet<ImagenProducto> ImagenesProductos { get; set; } = null!;
        public DbSet<CaracteristicaProducto> CaracteristicasProductos { get; set; } = null!;
        public DbSet<EspecificacionProducto> EspecificacionesProductos { get; set; } = null!;

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

            // --- Relaciones de la plataforma comercial ---

            modelBuilder.Entity<MateriaPrima>(entity =>
            {
                entity.HasOne(mp => mp.Proveedor)
                      .WithMany()
                      .HasForeignKey(mp => mp.ProveedorId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<RecetaProducto>(entity =>
            {
                entity.HasOne(r => r.Producto)
                      .WithMany(p => p.Receta)
                      .HasForeignKey(r => r.ProductoId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(r => r.MateriaPrima)
                      .WithMany()
                      .HasForeignKey(r => r.MateriaPrimaId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CompraProveedor>(entity =>
            {
                entity.HasOne(c => c.Proveedor)
                      .WithMany()
                      .HasForeignKey(c => c.ProveedorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DetalleCompraProveedor>(entity =>
            {
                entity.HasOne(d => d.CompraProveedor)
                      .WithMany(c => c.Detalles)
                      .HasForeignKey(d => d.CompraProveedorId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.MateriaPrima)
                      .WithMany()
                      .HasForeignKey(d => d.MateriaPrimaId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CompraCliente>(entity =>
            {
                entity.HasOne(c => c.Usuario)
                      .WithMany()
                      .HasForeignKey(c => c.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(c => c.Producto)
                      .WithMany()
                      .HasForeignKey(c => c.ProductoId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DocumentoProducto>(entity =>
            {
                entity.HasOne(d => d.Producto)
                      .WithMany()
                      .HasForeignKey(d => d.ProductoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Comentario>(entity =>
            {
                entity.HasOne(c => c.Producto)
                      .WithMany()
                      .HasForeignKey(c => c.ProductoId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ImagenProducto>(entity =>
            {
                entity.HasOne(i => i.Producto)
                      .WithMany()
                      .HasForeignKey(i => i.ProductoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CaracteristicaProducto>(entity =>
            {
                entity.HasOne(c => c.Producto)
                      .WithMany()
                      .HasForeignKey(c => c.ProductoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EspecificacionProducto>(entity =>
            {
                entity.HasOne(e => e.Producto)
                      .WithMany()
                      .HasForeignKey(e => e.ProductoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            SeedComercial(modelBuilder);

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
                    Email = "admin@thinkup.com",
                    PasswordHash = "$2a$11$UZ8mNYO7Ss0T41oYzfqHt.ILCFlrmVxEUZr6/i1cdBZ1qAxBhrBj.",
                    NombreCompleto = "Administrador ThinkUp",
                    Role = "Admin",
                    FechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Activo = true
                },
                new Usuario
                {
                    Id = 2,
                    Username = "cliente",
                    Email = "cliente@thinkup.com",
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

        /// <summary>
        /// Catalogo base de la plataforma comercial: el unico producto de ThinkUp es la
        /// pulsera CORSYNC, y su explosion de materiales define el costo de fabricacion.
        /// </summary>
        private static void SeedComercial(ModelBuilder modelBuilder)
        {
            var alta = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<Proveedor>().HasData(
                new Proveedor { Id = 1, Nombre = "Maxim Components MX", Contacto = "Ing. Rocío Alvarado", Email = "ventas@maximcomponents.mx", Telefono = "+52 33 1188 4400", Direccion = "Parque Industrial El Salto 120, Jalisco", Pais = "México", Activo = true, FechaAlta = alta },
                new Proveedor { Id = 2, Nombre = "SiliconWear Supplies", Contacto = "Laura Beltrán", Email = "contacto@siliconwear.com", Telefono = "+52 55 4402 9911", Direccion = "Av. Textil 45, Estado de México", Pais = "México", Activo = true, FechaAlta = alta },
                new Proveedor { Id = 3, Nombre = "NovaPCB Manufacturing", Contacto = "Chen Wei", Email = "sales@novapcb.cn", Telefono = "+86 755 8899 2200", Direccion = "Bao'an District, Shenzhen", Pais = "China", Activo = true, FechaAlta = alta },
                new Proveedor { Id = 4, Nombre = "Baterías Litio del Norte", Contacto = "Ing. Omar Treviño", Email = "compras@bateriasnorte.mx", Telefono = "+52 81 8340 7755", Direccion = "Av. Industrial 900, Monterrey", Pais = "México", Activo = true, FechaAlta = alta }
            );

            // Costos expresados como costo promedio ponderado inicial.
            modelBuilder.Entity<MateriaPrima>().HasData(
                new MateriaPrima { Id = 1, Nombre = "Correa de silicona hipoalergénica", Descripcion = "Correa médica de silicona con broche de acero, talla ajustable.", CostoUnidad = 3.10m, UnidadMedida = "pieza", Stock = 1200, StockMinimo = 300, ProveedorId = 2, Activo = true },
                new MateriaPrima { Id = 2, Nombre = "Carcasa de aluminio anodizado 6061", Descripcion = "Cuerpo mecanizado CNC con acabado anodizado mate.", CostoUnidad = 9.80m, UnidadMedida = "pieza", Stock = 800, StockMinimo = 200, ProveedorId = 2, Activo = true },
                new MateriaPrima { Id = 3, Nombre = "Sensor GSR de respuesta galvánica", Descripcion = "Módulo de conductancia de la piel para medición de activación fisiológica.", CostoUnidad = 6.50m, UnidadMedida = "pieza", Stock = 640, StockMinimo = 150, ProveedorId = 1, Activo = true },
                new MateriaPrima { Id = 4, Nombre = "Sensor MAX30102 (PPG)", Descripcion = "Sensor óptico de fotopletismografía para ritmo cardíaco y HRV.", CostoUnidad = 8.00m, UnidadMedida = "pieza", Stock = 700, StockMinimo = 150, ProveedorId = 1, Activo = true },
                new MateriaPrima { Id = 5, Nombre = "Módulo ESP32-C3 (MCU + BLE 5.2)", Descripcion = "Microcontrolador con Bluetooth Low Energy y Wi-Fi integrado.", CostoUnidad = 12.00m, UnidadMedida = "pieza", Stock = 520, StockMinimo = 120, ProveedorId = 1, Activo = true },
                new MateriaPrima { Id = 6, Nombre = "Batería LiPo 300 mAh", Descripcion = "Celda de polímero de litio con protección de sobrecarga.", CostoUnidad = 4.20m, UnidadMedida = "pieza", Stock = 900, StockMinimo = 250, ProveedorId = 4, Activo = true },
                new MateriaPrima { Id = 7, Nombre = "Electrodos de acero inoxidable 316L", Descripcion = "Par de electrodos de contacto para la lectura galvánica.", CostoUnidad = 2.40m, UnidadMedida = "par", Stock = 1500, StockMinimo = 300, ProveedorId = 2, Activo = true },
                new MateriaPrima { Id = 8, Nombre = "PCB flexible de 4 capas", Descripcion = "Placa flexible que integra sensores, MCU y batería.", CostoUnidad = 7.60m, UnidadMedida = "pieza", Stock = 450, StockMinimo = 150, ProveedorId = 3, Activo = true },
                new MateriaPrima { Id = 9, Nombre = "Cargador magnético inalámbrico", Descripcion = "Base de carga magnética con cable USB-C incluido.", CostoUnidad = 5.30m, UnidadMedida = "pieza", Stock = 600, StockMinimo = 150, ProveedorId = 3, Activo = true },
                new MateriaPrima { Id = 10, Nombre = "Empaque premium y manual impreso", Descripcion = "Caja rígida, inserto de espuma y guía de inicio rápido.", CostoUnidad = 2.90m, UnidadMedida = "kit", Stock = 1000, StockMinimo = 250, ProveedorId = 2, Activo = true }
            );

            // Costo primo objetivo: materia prima 61.80 + mano de obra 18.20 = 80.00
            // Gastos indirectos 25% = 20.00  ->  costo unitario 100.00
            // Margen 199%                    ->  precio de lista 299.00
            modelBuilder.Entity<Producto>().HasData(
                new Producto
                {
                    Id = 1,
                    Nombre = "CORSYNC",
                    Descripcion = "Pulsera biométrica que mide tu actividad galvánica y tu ritmo cardíaco para generar tu aura digital.",
                    DescripcionLarga = "CORSYNC es una pulsera que lee de forma continua dos señales de tu cuerpo: la actividad electrodermal de tu piel, mediante un sensor de respuesta galvánica, y tu ritmo cardíaco, mediante un sensor óptico de fotopletismografía. Ambas señales viajan por Bluetooth a la aplicación móvil, donde se traducen en un aura: una figura viva de color y movimiento que refleja tu estado en ese momento. El aura se puede guardar, revisar en tu historial y compartir con las personas que elijas.",
                    ManoObraUnitaria = 18.20m,
                    OverheadPorcentaje = 0.25m,
                    MargenUtilidad = 1.99m,
                    Activo = true,
                    FechaCreacion = alta
                }
            );

            modelBuilder.Entity<RecetaProducto>().HasData(
                new RecetaProducto { Id = 1, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 1, CantidadRequerida = 1, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 2, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 2, CantidadRequerida = 1, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 3, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 3, CantidadRequerida = 1, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 4, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 4, CantidadRequerida = 1, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 5, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 5, CantidadRequerida = 1, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 6, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 6, CantidadRequerida = 1, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 7, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 7, CantidadRequerida = 1, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 8, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 8, CantidadRequerida = 1, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 9, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 9, CantidadRequerida = 1, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 10, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 10, CantidadRequerida = 1, MermaPorcentaje = 0 }
            );

            // Caracteristicas destacadas de la ficha comercial.
            modelBuilder.Entity<CaracteristicaProducto>().HasData(
                new CaracteristicaProducto { Id = 1, ProductoId = 1, Orden = 1, Icono = "activity", Texto = "Sensor de respuesta galvánica de la piel (GSR)" },
                new CaracteristicaProducto { Id = 2, ProductoId = 1, Orden = 2, Icono = "heart-pulse", Texto = "Sensor óptico de ritmo cardíaco (PPG)" },
                new CaracteristicaProducto { Id = 3, ProductoId = 1, Orden = 3, Icono = "circle-half", Texto = "Generación de aura en tiempo real" },
                new CaracteristicaProducto { Id = 4, ProductoId = 1, Orden = 4, Icono = "phone", Texto = "Aplicación móvil para iOS y Android" },
                new CaracteristicaProducto { Id = 5, ProductoId = 1, Orden = 5, Icono = "bluetooth", Texto = "Bluetooth Low Energy 5.2" },
                new CaracteristicaProducto { Id = 6, ProductoId = 1, Orden = 6, Icono = "battery-full", Texto = "Hasta 7 días de autonomía" },
                new CaracteristicaProducto { Id = 7, ProductoId = 1, Orden = 7, Icono = "droplet", Texto = "Resistencia al agua IP68" },
                new CaracteristicaProducto { Id = 8, ProductoId = 1, Orden = 8, Icono = "share", Texto = "Compartir el aura en vivo" }
            );

            // Ficha tecnica, agrupada por bloque.
            modelBuilder.Entity<EspecificacionProducto>().HasData(
                new EspecificacionProducto { Id = 1, ProductoId = 1, Orden = 1, Grupo = "Físicas", Campo = "Dimensiones", Valor = "40 × 34 × 9,5 mm" },
                new EspecificacionProducto { Id = 2, ProductoId = 1, Orden = 2, Grupo = "Físicas", Campo = "Peso", Valor = "31 g con correa" },
                new EspecificacionProducto { Id = 3, ProductoId = 1, Orden = 3, Grupo = "Físicas", Campo = "Carcasa", Valor = "Aluminio anodizado 6061" },
                new EspecificacionProducto { Id = 4, ProductoId = 1, Orden = 4, Grupo = "Físicas", Campo = "Correa", Valor = "Silicona médica hipoalergénica" },
                new EspecificacionProducto { Id = 5, ProductoId = 1, Orden = 5, Grupo = "Físicas", Campo = "Resistencia", Valor = "IP68 · 1,5 m durante 30 min" },
                new EspecificacionProducto { Id = 6, ProductoId = 1, Orden = 6, Grupo = "Sensores", Campo = "Conductancia", Valor = "GSR con electrodos de acero 316L" },
                new EspecificacionProducto { Id = 7, ProductoId = 1, Orden = 7, Grupo = "Sensores", Campo = "Pulso", Valor = "MAX30102, fotopletismografía" },
                new EspecificacionProducto { Id = 8, ProductoId = 1, Orden = 8, Grupo = "Sensores", Campo = "Frecuencia de muestreo", Valor = "25 Hz" },
                new EspecificacionProducto { Id = 9, ProductoId = 1, Orden = 9, Grupo = "Sensores", Campo = "Rango de pulso", Valor = "30 – 220 BPM" },
                new EspecificacionProducto { Id = 10, ProductoId = 1, Orden = 10, Grupo = "Sistema", Campo = "Procesador", Valor = "ESP32-C3 con BLE 5.2 y Wi-Fi" },
                new EspecificacionProducto { Id = 11, ProductoId = 1, Orden = 11, Grupo = "Sistema", Campo = "Batería", Valor = "LiPo 300 mAh" },
                new EspecificacionProducto { Id = 12, ProductoId = 1, Orden = 12, Grupo = "Sistema", Campo = "Autonomía", Valor = "Hasta 7 días de uso continuo" },
                new EspecificacionProducto { Id = 13, ProductoId = 1, Orden = 13, Grupo = "Sistema", Campo = "Carga", Valor = "Base magnética inalámbrica · 1,5 h" },
                new EspecificacionProducto { Id = 14, ProductoId = 1, Orden = 14, Grupo = "Sistema", Campo = "Compatibilidad", Valor = "iOS 14+ · Android 11+" }
            );

            modelBuilder.Entity<DocumentoProducto>().HasData(
                new DocumentoProducto { Id = 1, ProductoId = 1, Titulo = "Manual de usuario CORSYNC", Descripcion = "Guía completa de uso, cuidados y solución de problemas de la pulsera.", Tipo = "Manual", Url = "/docs/corsync-manual-usuario.pdf", Peso = "4.2 MB", FechaPublicacion = alta },
                new DocumentoProducto { Id = 2, ProductoId = 1, Titulo = "Guía de inicio rápido", Descripcion = "Primeros pasos: carga, vinculación por Bluetooth y primera lectura de aura.", Tipo = "Guia", Url = "/docs/corsync-inicio-rapido.pdf", Peso = "1.1 MB", FechaPublicacion = alta },
                new DocumentoProducto { Id = 3, ProductoId = 1, Titulo = "Ficha técnica", Descripcion = "Especificaciones de sensores, autonomía, materiales y conectividad.", Tipo = "FichaTecnica", Url = "/docs/corsync-ficha-tecnica.pdf", Peso = "820 KB", FechaPublicacion = alta },
                new DocumentoProducto { Id = 4, ProductoId = 1, Titulo = "Póliza de garantía", Descripcion = "Cobertura de 2 años por defectos de fabricación y proceso de devolución.", Tipo = "Garantia", Url = "/docs/corsync-garantia.pdf", Peso = "310 KB", FechaPublicacion = alta }
            );

            // Valoraciones ya moderadas, para que la sección pública no nazca vacía.
            modelBuilder.Entity<Comentario>().HasData(
                new Comentario { Id = 1, NombreUsuario = "Laura S.", Email = "laura.s@example.com", Calificacion = 5, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(120), Respuesta = "Gracias Laura. Nos alegra que CORSYNC te acompañe también de noche. - ThinkUp", FechaRespuesta = alta.AddDays(122), Contenido = "Increíble experiencia. El aura que genera refleja muy bien cómo me siento, sobre todo al final del día. La correa es cómoda y ni la siento al dormir." },
                new Comentario { Id = 2, NombreUsuario = "Miguel R.", Email = "miguel.r@example.com", Calificacion = 4, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(127), Contenido = "Muy buen producto, la app es intuitiva y el diseño es elegante. Le falta más opciones de personalización del aura." },
                new Comentario { Id = 3, NombreUsuario = "Sofía T.", Email = "sofia.t@example.com", Calificacion = 5, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(132), Respuesta = "Gracias Sofía. Tu opinión nos motiva a seguir mejorando. - ThinkUp", FechaRespuesta = alta.AddDays(134), Contenido = "La relación calidad precio es excelente y el servicio al cliente respondió en menos de un día cuando tuve dudas con la vinculación." },
                new Comentario { Id = 4, NombreUsuario = "Diego M.", Email = "diego.m@example.com", Calificacion = 5, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(141), Contenido = "Desde que uso CORSYNC entiendo mejor mis picos de estrés. Ver la lectura galvánica junto al pulso cambia cómo interpreto mi día." },
                new Comentario { Id = 5, NombreUsuario = "Valentina P.", Email = "valentina.p@example.com", Calificacion = 3, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(148), Respuesta = "Gracias por el reporte Valentina. El equipo está optimizando el consumo del sensor GSR. - ThinkUp", FechaRespuesta = alta.AddDays(150), Contenido = "Buen producto, aunque la batería me dura cinco días y no siete. Espero que lo mejoren con una actualización." },
                new Comentario { Id = 6, NombreUsuario = "Andrea L.", Email = "andrea.l@example.com", Calificacion = 5, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(156), Contenido = "Compramos 25 pulseras para el programa de bienestar de la empresa. El proceso de cotización fue claro y el descuento por volumen se aplicó sin problema." },
                new Comentario { Id = 7, NombreUsuario = "Ricardo N.", Email = "ricardo.n@example.com", Calificacion = 4, ProductoId = 1, Aprobado = false, FechaCreacion = alta.AddDays(160), Contenido = "Llevo dos semanas con la pulsera y el seguimiento del pulso es muy consistente. Me gustaría poder exportar mis lecturas." }
            );

            modelBuilder.Entity<CompraCliente>().HasData(
                new CompraCliente { Id = 1, UsuarioId = 2, ProductoId = 1, Folio = "VTA-2026-0001", Cantidad = 1, Monto = 346.84m, Estado = "Entregado", NumeroSerie = "CS-2026-000418", Resenado = false, FechaCompra = alta.AddDays(135) }
            );

            modelBuilder.Entity<PreguntaFrecuente>().HasData(
                new PreguntaFrecuente { Id = 1, Categoria = "Producto", Orden = 1, Activo = true, Pregunta = "¿Qué sensores incluye CORSYNC?", Respuesta = "CORSYNC integra dos sensores: uno de respuesta galvánica de la piel (GSR), que mide la conductancia eléctrica de tu piel, y un sensor óptico de fotopletismografía (PPG) que registra tu ritmo cardíaco. La combinación de ambas señales es la que alimenta el cálculo de tu aura." },
                new PreguntaFrecuente { Id = 2, Categoria = "Producto", Orden = 2, Activo = true, Pregunta = "¿Cómo se genera el aura?", Respuesta = "La pulsera envía las lecturas de actividad galvánica y ritmo cardíaco a la aplicación móvil. Ahí se procesan en conjunto y se traducen en color, intensidad y movimiento. Un pulso elevado con alta conductancia produce un aura cálida y agitada; un pulso bajo y estable produce tonos fríos y un movimiento sereno." },
                new PreguntaFrecuente { Id = 3, Categoria = "Producto", Orden = 3, Activo = true, Pregunta = "¿Cuánto dura la batería y cómo se carga?", Respuesta = "La batería de 300 mAh ofrece hasta 7 días de uso continuo. Se carga con la base magnética inalámbrica incluida en la caja y alcanza el 100% en aproximadamente 1.5 horas." },
                new PreguntaFrecuente { Id = 4, Categoria = "Producto", Orden = 4, Activo = true, Pregunta = "¿Es resistente al agua?", Respuesta = "Sí. CORSYNC cuenta con certificación IP68: resiste polvo y puede sumergirse hasta 1.5 metros durante 30 minutos. Puedes usarla en la ducha o al nadar en superficie." },
                new PreguntaFrecuente { Id = 5, Categoria = "App móvil", Orden = 5, Activo = true, Pregunta = "¿Es compatible con iOS y Android?", Respuesta = "Sí. La aplicación CORSYNC está disponible para iOS 14 o superior y Android 11 o superior, y se conecta a la pulsera por Bluetooth Low Energy 5.2." },
                new PreguntaFrecuente { Id = 6, Categoria = "App móvil", Orden = 6, Activo = true, Pregunta = "¿Puedo compartir mi aura con otras personas?", Respuesta = "Sí. Desde la aplicación puedes compartir tu aura en tiempo real con las personas que elijas o publicarla en redes sociales. También puedes guardar tu historial y ver cómo ha evolucionado tu aura a lo largo del tiempo." },
                new PreguntaFrecuente { Id = 7, Categoria = "Soporte", Orden = 7, Activo = true, Pregunta = "¿Cuál es la garantía del producto?", Respuesta = "Todas las pulseras incluyen 2 años de garantía por defectos de fabricación. Además ofrecemos 30 días de garantía de satisfacción: si el producto no te convence, te devolvemos tu dinero." },
                new PreguntaFrecuente { Id = 8, Categoria = "Ventas", Orden = 8, Activo = true, Pregunta = "¿Ofrecen descuentos por volumen?", Respuesta = "Sí. Aplicamos descuentos progresivos sobre el subtotal: 10% a partir de 10 unidades, 15% a partir de 50 y 20% a partir de 100. Además existen precios preferentes por tipo de licencia Corporativa y Enterprise. Puedes calcular tu precio exacto en el formulario de cotización." }
            );
        }
    }
}
