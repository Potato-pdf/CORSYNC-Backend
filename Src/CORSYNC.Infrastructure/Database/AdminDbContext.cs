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
        /// manga CORSYNC, y su explosion de materiales define el costo de fabricacion.
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

            // Costos en MXN, expresados como costo promedio ponderado inicial. Salvo la
            // carcasa, cada importe es el precio realmente pagado en la compra del
            // prototipo; las recepciones posteriores lo iran promediando.
            modelBuilder.Entity<MateriaPrima>().HasData(
                new MateriaPrima { Id = 1, Nombre = "Carcasa impresa en 3D", Descripcion = "Carcasa impresa en 3D en filamento PLA, diseñada a medida para alojar los sensores.", CostoUnidad = 100.00m, UnidadMedida = "pieza", Stock = 800, StockMinimo = 200, ProveedorId = 2, Activo = true },
                new MateriaPrima { Id = 2, Nombre = "Sensor MCU-6701 (GSR)", Descripcion = "Módulo de conductancia de la piel para medición de activación fisiológica.", CostoUnidad = 259.96m, UnidadMedida = "pieza", Stock = 640, StockMinimo = 150, ProveedorId = 1, Activo = true },
                new MateriaPrima { Id = 3, Nombre = "Sensor MAX30102", Descripcion = "Sensor de ritmo cardíaco y HRV.", CostoUnidad = 64.24m, UnidadMedida = "pieza", Stock = 700, StockMinimo = 150, ProveedorId = 1, Activo = true },
                new MateriaPrima { Id = 4, Nombre = "Módulo ESP32 (MCU + Wi-Fi)", Descripcion = "Microcontrolador con Wi-Fi integrado; es el que transmite las lecturas a la app.", CostoUnidad = 129.99m, UnidadMedida = "pieza", Stock = 520, StockMinimo = 120, ProveedorId = 1, Activo = true },
                new MateriaPrima { Id = 5, Nombre = "Batería recargable de 9V (500 mAh)", Descripcion = "Batería recargable de 9V y 500 mAh que alimenta la manga durante la sesión de medición.", CostoUnidad = 150.00m, UnidadMedida = "pieza", Stock = 900, StockMinimo = 250, ProveedorId = 4, Activo = true },
                new MateriaPrima { Id = 6, Nombre = "Módulo indicador de carga XW228DKFR4", Descripcion = "Módulo indicador del nivel de carga de la batería.", CostoUnidad = 80.00m, UnidadMedida = "pieza", Stock = 600, StockMinimo = 150, ProveedorId = 3, Activo = true },
                new MateriaPrima { Id = 7, Nombre = "Regulador de voltaje LM2596", Descripcion = "Regulador que estabiliza la salida de la batería de 9V hacia los sensores y el MCU.", CostoUnidad = 95.60m, UnidadMedida = "pieza", Stock = 600, StockMinimo = 150, ProveedorId = 3, Activo = true },
                new MateriaPrima { Id = 8, Nombre = "Electrodos de metal (GSR)", Descripcion = "Electrodos metálicos de contacto directo con la piel para la lectura de conductancia electrodermal.", CostoUnidad = 2.50m, UnidadMedida = "pieza", Stock = 1000, StockMinimo = 200, ProveedorId = 2, Activo = true },
                new MateriaPrima { Id = 9, Nombre = "Cables de protoboard (jumpers)", Descripcion = "Juego de cables jumper de conexión rápida para interconectar los sensores y el ESP32.", CostoUnidad = 1.50m, UnidadMedida = "pieza", Stock = 1500, StockMinimo = 300, ProveedorId = 3, Activo = true }
            );

            // Costo primo: materia prima 914.79 + mano de obra 60.00 = 974.79
            // Gastos indirectos 25% = 243.70  ->  costo unitario 1,218.49
            // Margen 50%                      ->  precio de lista 1,827.74
            modelBuilder.Entity<Producto>().HasData(
                new Producto
                {
                    Id = 1,
                    Nombre = "CORSYNC",
                    Descripcion = "Manga biométrica que mide tu actividad galvánica y tu ritmo cardíaco para generar tu aura digital.",
                    DescripcionLarga = "CORSYNC es una manga que se coloca en el antebrazo y lee de forma continua dos señales de tu cuerpo: la actividad electrodermal de tu piel, mediante el sensor MCU-6701, y tu ritmo cardíaco, mediante el sensor MAX30102. Ambas señales viajan por Wi-Fi a la aplicación móvil, donde se traducen en un aura: una representación de color que refleja tu estado en ese momento. El aura se puede guardar, revisar en tu historial y compartir con las personas que elijas.",
                    ManoObraUnitaria = 60.00m,
                    OverheadPorcentaje = 0.25m,
                    MargenUtilidad = 0.50m,
                    Activo = true,
                    // Lote inicial fabricado menos la venta de demostracion de mas
                    // abajo: 25 - 1 = 24. Sin existencias, esa venta sembrada seria
                    // una que el propio sistema habria rechazado por falta de stock.
                    Stock = 24,
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
                new RecetaProducto { Id = 8, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 8, CantidadRequerida = 2, MermaPorcentaje = 0 },
                new RecetaProducto { Id = 9, ProductoId = 1, NombreProducto = "CORSYNC", MateriaPrimaId = 9, CantidadRequerida = 20, MermaPorcentaje = 0 }
            );

            // Ordenes de compra que dan origen al inventario. Sin ellas el stock y el
            // costo promedio de arriba aparecerian de la nada, sin respaldo en el
            // modulo de compras. Cada renglon cuadra exactamente con las existencias
            // y el costo unitario sembrados.
            //
            // Van como "Recibida": ya afectaron el inventario. El endpoint de
            // recepcion rechaza recibir una compra que ya lo esta, asi que no pueden
            // volver a sumar stock desde el panel.
            modelBuilder.Entity<CompraProveedor>().HasData(
                new CompraProveedor { Id = 1, ProveedorId = 1, Folio = "OC-2026-0001", MontoTotal = 278937.20m, Estado = "Recibida", Notas = "Lote inicial de sensores y microcontroladores.", FechaCompra = alta.AddDays(20), FechaRecepcion = alta.AddDays(27) },
                new CompraProveedor { Id = 2, ProveedorId = 2, Folio = "OC-2026-0002", MontoTotal = 82500.00m, Estado = "Recibida", Notas = "Carcasas impresas en 3D y electrodos de metal del primer lote.", FechaCompra = alta.AddDays(22), FechaRecepcion = alta.AddDays(30) },
                new CompraProveedor { Id = 3, ProveedorId = 3, Folio = "OC-2026-0003", MontoTotal = 107610.00m, Estado = "Recibida", Notas = "Regulación, control de carga y cables de protoboard.", FechaCompra = alta.AddDays(24), FechaRecepcion = alta.AddDays(35) },
                new CompraProveedor { Id = 4, ProveedorId = 4, Folio = "OC-2026-0004", MontoTotal = 135000.00m, Estado = "Recibida", Notas = "Baterías recargables de 9V.", FechaCompra = alta.AddDays(26), FechaRecepcion = alta.AddDays(33) }
            );

            modelBuilder.Entity<DetalleCompraProveedor>().HasData(
                new DetalleCompraProveedor { Id = 1, CompraProveedorId = 1, MateriaPrimaId = 2, Cantidad = 640, CostoUnitario = 259.96m, Importe = 166374.40m },
                new DetalleCompraProveedor { Id = 2, CompraProveedorId = 1, MateriaPrimaId = 3, Cantidad = 700, CostoUnitario = 64.24m, Importe = 44968.00m },
                new DetalleCompraProveedor { Id = 3, CompraProveedorId = 1, MateriaPrimaId = 4, Cantidad = 520, CostoUnitario = 129.99m, Importe = 67594.80m },
                new DetalleCompraProveedor { Id = 4, CompraProveedorId = 2, MateriaPrimaId = 1, Cantidad = 800, CostoUnitario = 100.00m, Importe = 80000.00m },
                new DetalleCompraProveedor { Id = 5, CompraProveedorId = 3, MateriaPrimaId = 6, Cantidad = 600, CostoUnitario = 80.00m, Importe = 48000.00m },
                new DetalleCompraProveedor { Id = 6, CompraProveedorId = 3, MateriaPrimaId = 7, Cantidad = 600, CostoUnitario = 95.60m, Importe = 57360.00m },
                new DetalleCompraProveedor { Id = 7, CompraProveedorId = 4, MateriaPrimaId = 5, Cantidad = 900, CostoUnitario = 150.00m, Importe = 135000.00m },
                new DetalleCompraProveedor { Id = 8, CompraProveedorId = 2, MateriaPrimaId = 8, Cantidad = 1000, CostoUnitario = 2.50m, Importe = 2500.00m },
                new DetalleCompraProveedor { Id = 9, CompraProveedorId = 3, MateriaPrimaId = 9, Cantidad = 1500, CostoUnitario = 1.50m, Importe = 2250.00m }
            );

            // Galeria del producto. Estas imagenes viven en el repositorio bajo
            // wwwroot/img/producto y por eso no llevan NombreArchivo: al borrarlas
            // desde el panel solo desaparece el registro, el archivo se conserva.
            // Las que sube el administrador van a wwwroot/uploads y si se borran
            // del panel tambien se eliminan del disco.
            modelBuilder.Entity<ImagenProducto>().HasData(
                new ImagenProducto { Id = 1, ProductoId = 1, Orden = 1, Url = "/img/producto/01-escaneo-en-vivo.jpg", Titulo = "Escaneo en vivo", Descripcion = "La manga transmite el pulso y la conductancia de la piel en tiempo real mientras dura la lectura.", FechaSubida = alta },
                new ImagenProducto { Id = 2, ProductoId = 1, Orden = 2, Url = "/img/producto/02-tu-aura-del-dia.jpg", Titulo = "Tu aura del día", Descripcion = "Las dos señales se cruzan y se traducen en un color con su interpretación y tus valores del momento.", FechaSubida = alta },
                new ImagenProducto { Id = 3, ProductoId = 1, Orden = 3, Url = "/img/producto/03-diario-energetico.jpg", Titulo = "Diario energético", Descripcion = "Historial completo de lecturas con su aura, su pulso y su nivel de estrés.", FechaSubida = alta },
                new ImagenProducto { Id = 4, ProductoId = 1, Orden = 4, Url = "/img/producto/04-analisis-de-tendencias.jpg", Titulo = "Análisis de tendencias", Descripcion = "Evolución del pulso y del estrés por día, semana o mes, con la distribución de auras.", FechaSubida = alta },
                new ImagenProducto { Id = 5, ProductoId = 1, Orden = 5, Url = "/img/producto/05-desafios.jpg", Titulo = "Desafíos", Descripcion = "Misiones que acompañan el hábito de medición y celebran la constancia.", FechaSubida = alta },
                new ImagenProducto { Id = 6, ProductoId = 1, Orden = 6, Url = "/img/producto/06-perfil-y-ajustes.jpg", Titulo = "Perfil y ajustes", Descripcion = "Resumen personal, aura dominante y configuración del dispositivo.", FechaSubida = alta }
            );

            // Caracteristicas destacadas de la ficha comercial.
            modelBuilder.Entity<CaracteristicaProducto>().HasData(
                new CaracteristicaProducto { Id = 1, ProductoId = 1, Orden = 1, Icono = "activity", Texto = "Sensor MCU-6701 de respuesta galvánica de la piel" },
                new CaracteristicaProducto { Id = 2, ProductoId = 1, Orden = 2, Icono = "heart-pulse", Texto = "Sensor MAX30102 de ritmo cardíaco" },
                new CaracteristicaProducto { Id = 3, ProductoId = 1, Orden = 3, Icono = "circle-half", Texto = "Generación de aura en tiempo real" },
                new CaracteristicaProducto { Id = 4, ProductoId = 1, Orden = 4, Icono = "phone", Texto = "Aplicación móvil para iOS y Android" },
                new CaracteristicaProducto { Id = 5, ProductoId = 1, Orden = 5, Icono = "wifi", Texto = "Conexión Wi-Fi mediante ESP32" },
                new CaracteristicaProducto { Id = 6, ProductoId = 1, Orden = 6, Icono = "battery-full", Texto = "Batería recargable de 9V y 500 mAh, con indicador de nivel de carga XW228DKFR4" },
                new CaracteristicaProducto { Id = 9, ProductoId = 1, Orden = 7, Icono = "lightning-charge", Texto = "Regulador de voltaje LM2596 que estabiliza la alimentación de los sensores" },
                new CaracteristicaProducto { Id = 7, ProductoId = 1, Orden = 8, Icono = "box", Texto = "Carcasa impresa en 3D en filamento PLA" },
                new CaracteristicaProducto { Id = 8, ProductoId = 1, Orden = 9, Icono = "share", Texto = "Compartir el aura en vivo" }
            );

            // Ficha tecnica, agrupada por bloque. Filas que no existen a proposito:
            // Bluetooth (la transmision es por Wi-Fi), Resistencia al agua y Carga
            // inalambrica (la manga no las tiene), Correa (ya no lleva) y Autonomia
            // (con la bateria de 9V no hay una cifra medida; se agrega cuando la haya).
            modelBuilder.Entity<EspecificacionProducto>().HasData(
                new EspecificacionProducto { Id = 1, ProductoId = 1, Orden = 1, Grupo = "Físicas", Campo = "Dimensiones", Valor = "14 × 13.5 × 8 cm" },
                new EspecificacionProducto { Id = 2, ProductoId = 1, Orden = 2, Grupo = "Físicas", Campo = "Peso", Valor = "240 g" },
                new EspecificacionProducto { Id = 3, ProductoId = 1, Orden = 3, Grupo = "Físicas", Campo = "Carcasa", Valor = "Filamento PLA" },
                new EspecificacionProducto { Id = 6, ProductoId = 1, Orden = 6, Grupo = "Sensores", Campo = "Conductancia", Valor = "MCU-6701 (GSR)" },
                new EspecificacionProducto { Id = 7, ProductoId = 1, Orden = 7, Grupo = "Sensores", Campo = "Pulso", Valor = "MAX30102" },
                new EspecificacionProducto { Id = 9, ProductoId = 1, Orden = 9, Grupo = "Sensores", Campo = "Rango de pulso", Valor = "30 – 220 BPM" },
                new EspecificacionProducto { Id = 10, ProductoId = 1, Orden = 10, Grupo = "Sistema", Campo = "Procesador", Valor = "ESP32 con Wi-Fi" },
                new EspecificacionProducto { Id = 11, ProductoId = 1, Orden = 11, Grupo = "Sistema", Campo = "Batería", Valor = "Recargable de 9V · 500 mAh" },
                new EspecificacionProducto { Id = 13, ProductoId = 1, Orden = 13, Grupo = "Sistema", Campo = "Regulador de voltaje", Valor = "LM2596" },
                new EspecificacionProducto { Id = 15, ProductoId = 1, Orden = 15, Grupo = "Sistema", Campo = "Indicador de carga", Valor = "XW228DKFR4" },
                new EspecificacionProducto { Id = 14, ProductoId = 1, Orden = 17, Grupo = "Sistema", Campo = "Compatibilidad", Valor = "iOS 14+ · Android 11+" }
            );

            modelBuilder.Entity<DocumentoProducto>().HasData(
                new DocumentoProducto { Id = 1, ProductoId = 1, Titulo = "Manual de usuario CORSYNC", Descripcion = "Guía completa de uso, cuidados y solución de problemas de la manga.", Tipo = "Manual", Url = "/docs/corsync-manual-usuario.pdf", Peso = "153 KB", FechaPublicacion = alta },
                new DocumentoProducto { Id = 2, ProductoId = 1, Titulo = "Guía de inicio rápido", Descripcion = "Primeros pasos: encendido, conexión Wi-Fi y primera lectura de aura.", Tipo = "Guia", Url = "/docs/corsync-inicio-rapido.pdf", Peso = "69 KB", FechaPublicacion = alta },
                new DocumentoProducto { Id = 3, ProductoId = 1, Titulo = "Ficha técnica", Descripcion = "Especificaciones de sensores, autonomía, materiales y conectividad.", Tipo = "FichaTecnica", Url = "/docs/corsync-ficha-tecnica.pdf", Peso = "168 KB", FechaPublicacion = alta },
                new DocumentoProducto { Id = 4, ProductoId = 1, Titulo = "Póliza de garantía", Descripcion = "Cobertura de 2 años por defectos de fabricación y proceso de devolución.", Tipo = "Garantia", Url = "/docs/corsync-garantia.pdf", Peso = "107 KB", FechaPublicacion = alta }
            );

            // Valoraciones ya moderadas, para que la sección pública no nazca vacía.
            modelBuilder.Entity<Comentario>().HasData(
                new Comentario { Id = 1, NombreUsuario = "Laura S.", Email = "laura.s@example.com", Calificacion = 5, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(120), Respuesta = "Gracias Laura. Nos alegra que CORSYNC te acompañe también de noche. - ThinkUp", FechaRespuesta = alta.AddDays(122), Contenido = "Increíble experiencia. El aura que genera refleja muy bien cómo me siento, sobre todo al final del día. La correa es cómoda y ni la siento al dormir." },
                new Comentario { Id = 2, NombreUsuario = "Miguel R.", Email = "miguel.r@example.com", Calificacion = 4, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(127), Contenido = "Muy buen producto, la app es intuitiva y el diseño es elegante. Le falta más opciones de personalización del aura." },
                new Comentario { Id = 3, NombreUsuario = "Sofía T.", Email = "sofia.t@example.com", Calificacion = 5, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(132), Respuesta = "Gracias Sofía. Tu opinión nos motiva a seguir mejorando. - ThinkUp", FechaRespuesta = alta.AddDays(134), Contenido = "La relación calidad precio es excelente y el servicio al cliente respondió en menos de un día cuando tuve dudas con la vinculación." },
                new Comentario { Id = 4, NombreUsuario = "Diego M.", Email = "diego.m@example.com", Calificacion = 5, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(141), Contenido = "Desde que uso CORSYNC entiendo mejor mis picos de estrés. Ver la lectura galvánica junto al pulso cambia cómo interpreto mi día." },
                new Comentario { Id = 5, NombreUsuario = "Valentina P.", Email = "valentina.p@example.com", Calificacion = 3, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(148), Respuesta = "Gracias por el reporte Valentina. El equipo está optimizando el consumo del sensor GSR. - ThinkUp", FechaRespuesta = alta.AddDays(150), Contenido = "Buen producto, aunque la batería me dura cinco días y no siete. Espero que lo mejoren con una actualización." },
                new Comentario { Id = 6, NombreUsuario = "Andrea L.", Email = "andrea.l@example.com", Calificacion = 5, ProductoId = 1, Aprobado = true, FechaCreacion = alta.AddDays(156), Contenido = "Compramos 25 mangas para el programa de bienestar de la empresa. El proceso de cotización fue claro y el descuento por volumen se aplicó sin problema." },
                new Comentario { Id = 7, NombreUsuario = "Ricardo N.", Email = "ricardo.n@example.com", Calificacion = 4, ProductoId = 1, Aprobado = false, FechaCreacion = alta.AddDays(160), Contenido = "Llevo dos semanas con la manga y el seguimiento del pulso es muy consistente. Me gustaría poder exportar mis lecturas." }
            );

            modelBuilder.Entity<CompraCliente>().HasData(
                new CompraCliente { Id = 1, UsuarioId = 2, ProductoId = 1, Folio = "VTA-2026-0001", Cantidad = 1, Monto = 2044.05m, Estado = "Procesando", NumeroSerie = "CS-2026-000418", Resenado = false, FechaCompra = alta.AddDays(135) }
            );

            modelBuilder.Entity<PreguntaFrecuente>().HasData(
                new PreguntaFrecuente { Id = 1, Categoria = "Producto", Orden = 1, Activo = true, Pregunta = "¿Qué sensores incluye CORSYNC?", Respuesta = "CORSYNC integra dos sensores: el MCU-6701, que mide la conductancia eléctrica de tu piel, y el MAX30102, que registra tu ritmo cardíaco. La combinación de ambas señales es la que alimenta el cálculo de tu aura." },
                new PreguntaFrecuente { Id = 2, Categoria = "Producto", Orden = 2, Activo = true, Pregunta = "¿Cómo se genera el aura?", Respuesta = "La manga envía las lecturas de actividad galvánica y ritmo cardíaco a la aplicación móvil. Ahí se procesan en conjunto y se traducen en color, intensidad y movimiento. Un pulso elevado con alta conductancia produce un aura cálida y agitada; un pulso bajo y estable produce tonos fríos y un movimiento sereno." },
                new PreguntaFrecuente { Id = 3, Categoria = "Producto", Orden = 3, Activo = true, Pregunta = "¿Cuánto dura la batería?", Respuesta = "CORSYNC funciona con una batería recargable de 9V y 500 mAh, con un módulo indicador que muestra el nivel de carga restante. Al agotarse se recarga y la manga vuelve a estar lista para la siguiente sesión. La duración por carga depende del uso; publicaremos la cifra en cuanto termine la caracterización del prototipo." },
                new PreguntaFrecuente { Id = 4, Categoria = "Producto", Orden = 4, Activo = true, Pregunta = "¿Cómo se coloca la manga?", Respuesta = "CORSYNC se desliza sobre el antebrazo hasta que los sensores queden en contacto directo con la piel. No lleva correa ni broche: la propia manga la mantiene en su sitio durante la lectura." },
                new PreguntaFrecuente { Id = 5, Categoria = "App móvil", Orden = 5, Activo = true, Pregunta = "¿Es compatible con iOS y Android?", Respuesta = "Sí. La aplicación CORSYNC está disponible para iOS 14 o superior y Android 11 o superior, y recibe las lecturas por Wi-Fi." },
                new PreguntaFrecuente { Id = 6, Categoria = "App móvil", Orden = 6, Activo = true, Pregunta = "¿Puedo compartir mi aura con otras personas?", Respuesta = "Sí. Desde la aplicación puedes compartir tu aura en tiempo real con las personas que elijas o publicarla en redes sociales. También puedes guardar tu historial y ver cómo ha evolucionado tu aura a lo largo del tiempo." },
                new PreguntaFrecuente { Id = 7, Categoria = "Soporte", Orden = 7, Activo = true, Pregunta = "¿Cuál es la garantía del producto?", Respuesta = "Todas las mangas incluyen 2 años de garantía por defectos de fabricación. Además ofrecemos 30 días de garantía de satisfacción: si el producto no te convence, te devolvemos tu dinero." },
                new PreguntaFrecuente { Id = 8, Categoria = "Ventas", Orden = 8, Activo = true, Pregunta = "¿Ofrecen descuentos por volumen?", Respuesta = "Sí. Aplicamos descuentos progresivos sobre el subtotal: 10% a partir de 5 unidades y 15% a partir de 15. Además existen precios preferentes por tipo de licencia Corporativa y Enterprise. El cotizador en línea admite hasta 100 unidades; si necesitas más, escríbenos y lo vemos contigo. Puedes calcular tu precio exacto en el formulario de cotización." }
            );
        }
    }
}
