using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CORSYNC.Infrastructure.Database
{
    /// <summary>
    /// Evolucion del esquema de la base de datos ya desplegada. EnsureCreated() no
    /// altera bases existentes, asi que las columnas y tablas de la plataforma
    /// comercial se crean aqui con DDL idempotente que puede ejecutarse en cada
    /// arranque sin efectos secundarios.
    /// </summary>
    public static class DatabaseBootstrapper
    {
        /// <summary>
        /// Separador de lotes. SQL Server compila cada lote completo antes de
        /// ejecutarlo, asi que una columna recien agregada no puede usarse en el
        /// mismo lote: cada seccion marcada se envia por separado.
        /// </summary>
        private const string SeparadorLote = "--BATCH--";

        public static void ActualizarEsquemaComercial(AdminDbContext context, ILogger? logger = null)
        {
            if (!context.Database.IsRelational())
            {
                return;
            }

            EjecutarPorLotes(context, EsquemaSql, "esquema", logger);
            EjecutarPorLotes(context, SeedSql, "catalogo base", logger);
        }

        private static void EjecutarPorLotes(AdminDbContext context, string script, string etapa, ILogger? logger)
        {
            var lotes = script.Split(SeparadorLote, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lotes.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lotes[i]))
                {
                    continue;
                }

                try
                {
                    context.Database.ExecuteSqlRaw(lotes[i]);
                }
                catch (Exception ex)
                {
                    // Un lote fallido no debe tumbar el arranque de la API: se registra
                    // y el resto de la actualizacion continua.
                    logger?.LogError(ex, "Fallo el lote {Numero} de la actualizacion de {Etapa}.", i + 1, etapa);
                }
            }
        }

        private const string EsquemaSql = @"
-- ============================================================
-- 1. Proveedores: datos de contacto y estado
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Proveedores') AND name = 'Contacto')
    ALTER TABLE Proveedores ADD Contacto NVARCHAR(120) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Proveedores') AND name = 'Direccion')
    ALTER TABLE Proveedores ADD Direccion NVARCHAR(250) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Proveedores') AND name = 'Pais')
    ALTER TABLE Proveedores ADD Pais NVARCHAR(80) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Proveedores') AND name = 'Activo')
    ALTER TABLE Proveedores ADD Activo BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Proveedores') AND name = 'FechaAlta')
    ALTER TABLE Proveedores ADD FechaAlta DATETIME2 NOT NULL DEFAULT GETUTCDATE();

-- ============================================================
-- 2. Materia prima: descripcion, minimos, proveedor y precision de costeo
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MateriasPrimas') AND name = 'Descripcion')
    ALTER TABLE MateriasPrimas ADD Descripcion NVARCHAR(400) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MateriasPrimas') AND name = 'StockMinimo')
    ALTER TABLE MateriasPrimas ADD StockMinimo DECIMAL(18,4) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MateriasPrimas') AND name = 'ProveedorId')
    ALTER TABLE MateriasPrimas ADD ProveedorId INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MateriasPrimas') AND name = 'Activo')
    ALTER TABLE MateriasPrimas ADD Activo BIT NOT NULL DEFAULT 1;
--BATCH--
-- El costo promedio ponderado necesita 4 decimales para no perder precision al promediar.
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MateriasPrimas') AND name = 'CostoUnidad' AND scale <> 4)
    ALTER TABLE MateriasPrimas ALTER COLUMN CostoUnidad DECIMAL(18,4) NOT NULL;
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MateriasPrimas') AND name = 'Stock' AND scale <> 4)
    ALTER TABLE MateriasPrimas ALTER COLUMN Stock DECIMAL(18,4) NOT NULL;

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_MateriasPrimas_Proveedores')
    ALTER TABLE MateriasPrimas ADD CONSTRAINT FK_MateriasPrimas_Proveedores
        FOREIGN KEY (ProveedorId) REFERENCES Proveedores(Id) ON DELETE SET NULL;

-- ============================================================
-- 3. Productos y explosion de materiales
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'Productos' AND xtype = 'U')
    CREATE TABLE Productos (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        Nombre              NVARCHAR(120)  NOT NULL,
        Descripcion         NVARCHAR(300)  NOT NULL DEFAULT '',
        DescripcionLarga    NVARCHAR(4000) NOT NULL DEFAULT '',
        ManoObraUnitaria    DECIMAL(18,2)  NOT NULL DEFAULT 0,
        OverheadPorcentaje  DECIMAL(9,4)   NOT NULL DEFAULT 0.25,
        MargenUtilidad      DECIMAL(9,4)   NOT NULL DEFAULT 0.40,
        Activo              BIT            NOT NULL DEFAULT 1,
        FechaCreacion       DATETIME2      NOT NULL DEFAULT GETUTCDATE()
    );

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('RecetasProductos') AND name = 'ProductoId')
    ALTER TABLE RecetasProductos ADD ProductoId INT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('RecetasProductos') AND name = 'MermaPorcentaje')
    ALTER TABLE RecetasProductos ADD MermaPorcentaje DECIMAL(9,4) NOT NULL DEFAULT 0;
--BATCH--
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('RecetasProductos') AND name = 'CantidadRequerida' AND scale <> 4)
    ALTER TABLE RecetasProductos ALTER COLUMN CantidadRequerida DECIMAL(18,4) NOT NULL;

-- ============================================================
-- 4. Comentarios: valoracion con estrellas y respuesta de la empresa
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Comentarios') AND name = 'Email')
    ALTER TABLE Comentarios ADD Email NVARCHAR(120) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Comentarios') AND name = 'Calificacion')
    ALTER TABLE Comentarios ADD Calificacion INT NOT NULL DEFAULT 5;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Comentarios') AND name = 'ProductoId')
    ALTER TABLE Comentarios ADD ProductoId INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Comentarios') AND name = 'UsuarioId')
    ALTER TABLE Comentarios ADD UsuarioId INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Comentarios') AND name = 'CompraClienteId')
    ALTER TABLE Comentarios ADD CompraClienteId INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Comentarios') AND name = 'Respuesta')
    ALTER TABLE Comentarios ADD Respuesta NVARCHAR(2000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Comentarios') AND name = 'FechaRespuesta')
    ALTER TABLE Comentarios ADD FechaRespuesta DATETIME2 NULL;

-- ============================================================
-- 5. Cotizaciones: datos del prospecto y desglose del costeo
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Folio')
    ALTER TABLE Cotizaciones ADD Folio NVARCHAR(50) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Empresa')
    ALTER TABLE Cotizaciones ADD Empresa NVARCHAR(150) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Email')
    ALTER TABLE Cotizaciones ADD Email NVARCHAR(120) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Telefono')
    ALTER TABLE Cotizaciones ADD Telefono NVARCHAR(40) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Pais')
    ALTER TABLE Cotizaciones ADD Pais NVARCHAR(80) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'ProductoId')
    ALTER TABLE Cotizaciones ADD ProductoId INT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Cantidad')
    ALTER TABLE Cotizaciones ADD Cantidad INT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'TipoLicencia')
    ALTER TABLE Cotizaciones ADD TipoLicencia NVARCHAR(30) NOT NULL DEFAULT 'Individual';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Servicios')
    ALTER TABLE Cotizaciones ADD Servicios NVARCHAR(400) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Mensaje')
    ALTER TABLE Cotizaciones ADD Mensaje NVARCHAR(2000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'CostoMateriaPrima')
    ALTER TABLE Cotizaciones ADD CostoMateriaPrima DECIMAL(18,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'CostoManoObra')
    ALTER TABLE Cotizaciones ADD CostoManoObra DECIMAL(18,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'CostoIndirecto')
    ALTER TABLE Cotizaciones ADD CostoIndirecto DECIMAL(18,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'CostoUnitario')
    ALTER TABLE Cotizaciones ADD CostoUnitario DECIMAL(18,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'PrecioUnitario')
    ALTER TABLE Cotizaciones ADD PrecioUnitario DECIMAL(18,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Subtotal')
    ALTER TABLE Cotizaciones ADD Subtotal DECIMAL(18,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'DescuentoPorcentaje')
    ALTER TABLE Cotizaciones ADD DescuentoPorcentaje DECIMAL(9,4) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'DescuentoMonto')
    ALTER TABLE Cotizaciones ADD DescuentoMonto DECIMAL(18,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'TotalServicios')
    ALTER TABLE Cotizaciones ADD TotalServicios DECIMAL(18,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Impuestos')
    ALTER TABLE Cotizaciones ADD Impuestos DECIMAL(18,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Estado')
    ALTER TABLE Cotizaciones ADD Estado NVARCHAR(20) NOT NULL DEFAULT 'Nueva';
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'FechaVigencia')
    ALTER TABLE Cotizaciones ADD FechaVigencia DATETIME2 NOT NULL DEFAULT GETUTCDATE();
--BATCH--
-- Ancho y Alto pertenecian al producto anterior (espejo). Se vuelven opcionales
-- para que las cotizaciones de la pulsera no tengan que informarlos.
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Ancho' AND is_nullable = 0)
    ALTER TABLE Cotizaciones ALTER COLUMN Ancho DECIMAL(18,2) NULL;
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cotizaciones') AND name = 'Alto' AND is_nullable = 0)
    ALTER TABLE Cotizaciones ALTER COLUMN Alto DECIMAL(18,2) NULL;

-- ============================================================
-- 6. Compras a proveedores
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'ComprasProveedores' AND xtype = 'U')
    CREATE TABLE ComprasProveedores (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        ProveedorId    INT           NOT NULL,
        Folio          NVARCHAR(50)  NOT NULL DEFAULT '',
        MontoTotal     DECIMAL(18,2) NOT NULL DEFAULT 0,
        Estado         NVARCHAR(20)  NOT NULL DEFAULT 'Pendiente',
        Notas          NVARCHAR(500) NULL,
        FechaCompra    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        FechaRecepcion DATETIME2     NULL,
        CONSTRAINT FK_ComprasProveedores_Proveedores FOREIGN KEY (ProveedorId) REFERENCES Proveedores(Id)
    );

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'DetallesCompraProveedor' AND xtype = 'U')
    CREATE TABLE DetallesCompraProveedor (
        Id                 INT IDENTITY(1,1) PRIMARY KEY,
        CompraProveedorId  INT           NOT NULL,
        MateriaPrimaId     INT           NOT NULL,
        Cantidad           DECIMAL(18,4) NOT NULL,
        CostoUnitario      DECIMAL(18,4) NOT NULL,
        Importe            DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_DetallesCompra_Compra FOREIGN KEY (CompraProveedorId) REFERENCES ComprasProveedores(Id) ON DELETE CASCADE,
        CONSTRAINT FK_DetallesCompra_MateriaPrima FOREIGN KEY (MateriaPrimaId) REFERENCES MateriasPrimas(Id)
    );

-- ============================================================
-- 7. Compras de clientes y documentacion del producto
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'ComprasClientes' AND xtype = 'U')
    CREATE TABLE ComprasClientes (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        UsuarioId    INT           NOT NULL,
        ProductoId   INT           NOT NULL,
        Folio        NVARCHAR(50)  NOT NULL DEFAULT '',
        Cantidad     INT           NOT NULL DEFAULT 1,
        Monto        DECIMAL(18,2) NOT NULL DEFAULT 0,
        Estado       NVARCHAR(20)  NOT NULL DEFAULT 'Procesando',
        NumeroSerie  NVARCHAR(60)  NULL,
        Resenado     BIT           NOT NULL DEFAULT 0,
        FechaCompra  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ComprasClientes_Usuarios FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ComprasClientes_Productos FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
    );

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'DocumentosProductos' AND xtype = 'U')
    CREATE TABLE DocumentosProductos (
        Id                INT IDENTITY(1,1) PRIMARY KEY,
        ProductoId        INT           NOT NULL,
        Titulo            NVARCHAR(150) NOT NULL,
        Descripcion       NVARCHAR(400) NOT NULL DEFAULT '',
        Tipo              NVARCHAR(30)  NOT NULL DEFAULT 'Manual',
        Url               NVARCHAR(500) NOT NULL DEFAULT '',
        Peso              NVARCHAR(20)  NULL,
        FechaPublicacion  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_DocumentosProductos_Productos FOREIGN KEY (ProductoId) REFERENCES Productos(Id) ON DELETE CASCADE
    );

-- ============================================================
-- 8. Contacto, FAQ y bitacora de correos
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'MensajesContacto' AND xtype = 'U')
    CREATE TABLE MensajesContacto (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        Nombre      NVARCHAR(120)  NOT NULL,
        Email       NVARCHAR(120)  NOT NULL,
        Telefono    NVARCHAR(40)   NULL,
        Asunto      NVARCHAR(150)  NOT NULL,
        Mensaje     NVARCHAR(2000) NOT NULL,
        Atendido    BIT            NOT NULL DEFAULT 0,
        FechaEnvio  DATETIME2      NOT NULL DEFAULT GETUTCDATE()
    );

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'PreguntasFrecuentes' AND xtype = 'U')
    CREATE TABLE PreguntasFrecuentes (
        Id         INT IDENTITY(1,1) PRIMARY KEY,
        Pregunta   NVARCHAR(300)  NOT NULL,
        Respuesta  NVARCHAR(2000) NOT NULL,
        Categoria  NVARCHAR(50)   NOT NULL DEFAULT 'Producto',
        Orden      INT            NOT NULL DEFAULT 0,
        Activo     BIT            NOT NULL DEFAULT 1
    );

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'CorreosEnviados' AND xtype = 'U')
    CREATE TABLE CorreosEnviados (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        Destinatario  NVARCHAR(120)  NOT NULL,
        Asunto        NVARCHAR(200)  NOT NULL,
        Cuerpo        NVARCHAR(4000) NOT NULL,
        Tipo          NVARCHAR(40)   NOT NULL DEFAULT 'Notificacion',
        Estado        NVARCHAR(20)   NOT NULL DEFAULT 'Simulado',
        FechaEnvio    DATETIME2      NOT NULL DEFAULT GETUTCDATE()
    );

-- ============================================================
-- 9. Galeria, caracteristicas y especificaciones del producto
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'ImagenesProductos' AND xtype = 'U')
    CREATE TABLE ImagenesProductos (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        ProductoId     INT           NOT NULL,
        Url            NVARCHAR(500) NOT NULL,
        Titulo         NVARCHAR(200) NOT NULL DEFAULT '',
        Descripcion    NVARCHAR(400) NOT NULL DEFAULT '',
        Orden          INT           NOT NULL DEFAULT 0,
        NombreArchivo  NVARCHAR(260) NOT NULL DEFAULT '',
        TamanoBytes    BIGINT        NOT NULL DEFAULT 0,
        FechaSubida    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ImagenesProductos_Productos FOREIGN KEY (ProductoId) REFERENCES Productos(Id) ON DELETE CASCADE
    );

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'CaracteristicasProductos' AND xtype = 'U')
    CREATE TABLE CaracteristicasProductos (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        ProductoId  INT           NOT NULL,
        Texto       NVARCHAR(200) NOT NULL,
        Icono       NVARCHAR(60)  NOT NULL DEFAULT 'check-lg',
        Orden       INT           NOT NULL DEFAULT 0,
        CONSTRAINT FK_CaracteristicasProductos_Productos FOREIGN KEY (ProductoId) REFERENCES Productos(Id) ON DELETE CASCADE
    );

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'EspecificacionesProductos' AND xtype = 'U')
    CREATE TABLE EspecificacionesProductos (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        ProductoId  INT           NOT NULL,
        Grupo       NVARCHAR(80)  NOT NULL,
        Campo       NVARCHAR(120) NOT NULL,
        Valor       NVARCHAR(250) NOT NULL,
        Orden       INT           NOT NULL DEFAULT 0,
        CONSTRAINT FK_EspecificacionesProductos_Productos FOREIGN KEY (ProductoId) REFERENCES Productos(Id) ON DELETE CASCADE
    );
";

        // El prefijo N antes de cada literal es obligatorio: sin el, SQL Server
        // interpreta la cadena con la codificacion por defecto de la conexion y
        // los caracteres acentuados o la ñ se pierden al guardarla en NVARCHAR.
        private const string SeedSql = @"
-- ============================================================
-- Catalogo base de ThinkUp: un unico producto, la pulsera CORSYNC
-- ============================================================

-- Proveedores
IF NOT EXISTS (SELECT 1 FROM Proveedores WHERE Nombre = N'Maxim Components MX')
    INSERT INTO Proveedores (Nombre, Contacto, Email, Telefono, Direccion, Pais, Activo, FechaAlta)
    VALUES (N'Maxim Components MX', N'Ing. Rocío Alvarado', 'ventas@maximcomponents.mx', '+52 33 1188 4400', N'Parque Industrial El Salto 120, Jalisco', N'México', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Proveedores WHERE Nombre = N'SiliconWear Supplies')
    INSERT INTO Proveedores (Nombre, Contacto, Email, Telefono, Direccion, Pais, Activo, FechaAlta)
    VALUES (N'SiliconWear Supplies', N'Laura Beltrán', 'contacto@siliconwear.com', '+52 55 4402 9911', N'Av. Textil 45, Estado de México', N'México', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Proveedores WHERE Nombre = N'NovaPCB Manufacturing')
    INSERT INTO Proveedores (Nombre, Contacto, Email, Telefono, Direccion, Pais, Activo, FechaAlta)
    VALUES (N'NovaPCB Manufacturing', N'Chen Wei', 'sales@novapcb.cn', '+86 755 8899 2200', N'Bao''an District, Shenzhen', N'China', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Proveedores WHERE Nombre = N'Baterías Litio del Norte')
    INSERT INTO Proveedores (Nombre, Contacto, Email, Telefono, Direccion, Pais, Activo, FechaAlta)
    VALUES (N'Baterías Litio del Norte', N'Ing. Omar Treviño', 'compras@bateriasnorte.mx', '+52 81 8340 7755', N'Av. Industrial 900, Monterrey', N'México', 1, GETUTCDATE());

DECLARE @provMaxim INT = (SELECT TOP 1 Id FROM Proveedores WHERE Nombre = N'Maxim Components MX');
DECLARE @provSilicon INT = (SELECT TOP 1 Id FROM Proveedores WHERE Nombre = N'SiliconWear Supplies');
DECLARE @provPcb INT = (SELECT TOP 1 Id FROM Proveedores WHERE Nombre = N'NovaPCB Manufacturing');
DECLARE @provBat INT = (SELECT TOP 1 Id FROM Proveedores WHERE Nombre = N'Baterías Litio del Norte');

-- Materia prima de la pulsera. Los Ids 1 a 5 provienen del catalogo anterior
-- (componentes de espejo) y se reconvierten; el resto se da de alta.
UPDATE MateriasPrimas SET Nombre = N'Correa de silicona hipoalergénica', Descripcion = N'Correa médica de silicona con broche de acero, talla ajustable.', CostoUnidad = 3.10, UnidadMedida = N'pieza', Stock = 1200, StockMinimo = 300, ProveedorId = @provSilicon, Activo = 1 WHERE Id = 1;
UPDATE MateriasPrimas SET Nombre = N'Carcasa de aluminio anodizado 6061', Descripcion = N'Cuerpo mecanizado CNC con acabado anodizado mate.', CostoUnidad = 9.80, UnidadMedida = N'pieza', Stock = 800, StockMinimo = 200, ProveedorId = @provSilicon, Activo = 1 WHERE Id = 2;
UPDATE MateriasPrimas SET Nombre = N'Sensor GSR de respuesta galvánica', Descripcion = N'Módulo de conductancia de la piel para medición de activación fisiológica.', CostoUnidad = 6.50, UnidadMedida = N'pieza', Stock = 640, StockMinimo = 150, ProveedorId = @provMaxim, Activo = 1 WHERE Id = 3;
UPDATE MateriasPrimas SET Nombre = N'Sensor MAX30102 (PPG)', Descripcion = N'Sensor óptico de fotopletismografía para ritmo cardíaco y HRV.', CostoUnidad = 8.00, UnidadMedida = N'pieza', Stock = 700, StockMinimo = 150, ProveedorId = @provMaxim, Activo = 1 WHERE Id = 4;
UPDATE MateriasPrimas SET Nombre = N'Módulo ESP32-C3 (MCU + BLE 5.2)', Descripcion = N'Microcontrolador con Bluetooth Low Energy y Wi-Fi integrado.', CostoUnidad = 12.00, UnidadMedida = N'pieza', Stock = 520, StockMinimo = 120, ProveedorId = @provMaxim, Activo = 1 WHERE Id = 5;

IF NOT EXISTS (SELECT 1 FROM MateriasPrimas WHERE Nombre = N'Batería LiPo 300 mAh')
    INSERT INTO MateriasPrimas (Nombre, Descripcion, CostoUnidad, UnidadMedida, Stock, StockMinimo, ProveedorId, Activo)
    VALUES (N'Batería LiPo 300 mAh', N'Celda de polímero de litio con protección de sobrecarga.', 4.20, N'pieza', 900, 250, @provBat, 1);
IF NOT EXISTS (SELECT 1 FROM MateriasPrimas WHERE Nombre = N'Electrodos de acero inoxidable 316L')
    INSERT INTO MateriasPrimas (Nombre, Descripcion, CostoUnidad, UnidadMedida, Stock, StockMinimo, ProveedorId, Activo)
    VALUES (N'Electrodos de acero inoxidable 316L', N'Par de electrodos de contacto para la lectura galvánica.', 2.40, N'par', 1500, 300, @provSilicon, 1);
IF NOT EXISTS (SELECT 1 FROM MateriasPrimas WHERE Nombre = N'PCB flexible de 4 capas')
    INSERT INTO MateriasPrimas (Nombre, Descripcion, CostoUnidad, UnidadMedida, Stock, StockMinimo, ProveedorId, Activo)
    VALUES (N'PCB flexible de 4 capas', N'Placa flexible que integra sensores, MCU y batería.', 7.60, N'pieza', 450, 150, @provPcb, 1);
IF NOT EXISTS (SELECT 1 FROM MateriasPrimas WHERE Nombre = N'Cargador magnético inalámbrico')
    INSERT INTO MateriasPrimas (Nombre, Descripcion, CostoUnidad, UnidadMedida, Stock, StockMinimo, ProveedorId, Activo)
    VALUES (N'Cargador magnético inalámbrico', N'Base de carga magnética con cable USB-C incluido.', 5.30, N'pieza', 600, 150, @provPcb, 1);
IF NOT EXISTS (SELECT 1 FROM MateriasPrimas WHERE Nombre = N'Empaque premium y manual impreso')
    INSERT INTO MateriasPrimas (Nombre, Descripcion, CostoUnidad, UnidadMedida, Stock, StockMinimo, ProveedorId, Activo)
    VALUES (N'Empaque premium y manual impreso', N'Caja rígida, inserto de espuma y guía de inicio rápido.', 2.90, N'kit', 1000, 250, @provSilicon, 1);

-- Producto unico: la pulsera CORSYNC.
-- Costo primo objetivo: materia prima 61.80 + mano de obra 18.20 = 80.00
-- Gastos indirectos 25% = 20.00 -> costo unitario 100.00
-- Margen 199% -> precio de lista 299.00
IF NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = N'CORSYNC')
    INSERT INTO Productos (Nombre, Descripcion, DescripcionLarga, ManoObraUnitaria, OverheadPorcentaje, MargenUtilidad, Activo, FechaCreacion)
    VALUES (
        N'CORSYNC',
        N'Pulsera biométrica que mide tu actividad galvánica y tu ritmo cardíaco para generar tu aura digital.',
        N'CORSYNC es una pulsera que lee de forma continua dos señales de tu cuerpo: la actividad electrodermal de tu piel, mediante un sensor de respuesta galvánica, y tu ritmo cardíaco, mediante un sensor óptico de fotopletismografía. Ambas señales viajan por Bluetooth a la aplicación móvil, donde se traducen en un aura: una figura viva de color y movimiento que refleja tu estado en ese momento. El aura se puede guardar, revisar en tu historial y compartir con las personas que elijas.',
        18.20, 0.25, 1.99, 1, GETUTCDATE());

DECLARE @productoId INT = (SELECT TOP 1 Id FROM Productos WHERE Nombre = N'CORSYNC');

-- Explosion de materiales: una pieza de cada insumo por pulsera.
DELETE FROM RecetasProductos WHERE ProductoId = @productoId;
INSERT INTO RecetasProductos (ProductoId, NombreProducto, MateriaPrimaId, CantidadRequerida, MermaPorcentaje)
SELECT @productoId, N'CORSYNC', Id, 1, 0
FROM MateriasPrimas
WHERE Nombre IN (
    N'Correa de silicona hipoalergénica',
    N'Carcasa de aluminio anodizado 6061',
    N'Sensor GSR de respuesta galvánica',
    N'Sensor MAX30102 (PPG)',
    N'Módulo ESP32-C3 (MCU + BLE 5.2)',
    N'Batería LiPo 300 mAh',
    N'Electrodos de acero inoxidable 316L',
    N'PCB flexible de 4 capas',
    N'Cargador magnético inalámbrico',
    N'Empaque premium y manual impreso');

-- Recetas del producto anterior (espejo) que ya no forman parte del catalogo.
DELETE FROM RecetasProductos WHERE ProductoId <> @productoId;

-- Documentacion del producto
IF NOT EXISTS (SELECT 1 FROM DocumentosProductos WHERE ProductoId = @productoId)
    INSERT INTO DocumentosProductos (ProductoId, Titulo, Descripcion, Tipo, Url, Peso, FechaPublicacion) VALUES
    (@productoId, N'Manual de usuario CORSYNC', N'Guía completa de uso, cuidados y solución de problemas de la pulsera.', N'Manual', '/docs/corsync-manual-usuario.pdf', '4.2 MB', GETUTCDATE()),
    (@productoId, N'Guía de inicio rápido', N'Primeros pasos: carga, vinculación por Bluetooth y primera lectura de aura.', N'Guia', '/docs/corsync-inicio-rapido.pdf', '1.1 MB', GETUTCDATE()),
    (@productoId, N'Ficha técnica', N'Especificaciones de sensores, autonomía, materiales y conectividad.', N'FichaTecnica', '/docs/corsync-ficha-tecnica.pdf', '820 KB', GETUTCDATE()),
    (@productoId, N'Póliza de garantía', N'Cobertura de 2 años por defectos de fabricación y proceso de devolución.', N'Garantia', '/docs/corsync-garantia.pdf', '310 KB', GETUTCDATE());

-- Preguntas frecuentes
IF NOT EXISTS (SELECT 1 FROM PreguntasFrecuentes)
    INSERT INTO PreguntasFrecuentes (Pregunta, Respuesta, Categoria, Orden, Activo) VALUES
    (N'¿Qué sensores incluye CORSYNC?', N'CORSYNC integra dos sensores: uno de respuesta galvánica de la piel (GSR), que mide la conductancia eléctrica de tu piel, y un sensor óptico de fotopletismografía (PPG) que registra tu ritmo cardíaco. La combinación de ambas señales es la que alimenta el cálculo de tu aura.', N'Producto', 1, 1),
    (N'¿Cómo se genera el aura?', N'La pulsera envía las lecturas de actividad galvánica y ritmo cardíaco a la aplicación móvil. Ahí se procesan en conjunto y se traducen en color, intensidad y movimiento. Un pulso elevado con alta conductancia produce un aura cálida y agitada; un pulso bajo y estable produce tonos fríos y un movimiento sereno.', N'Producto', 2, 1),
    (N'¿Cuánto dura la batería y cómo se carga?', N'La batería de 300 mAh ofrece hasta 7 días de uso continuo. Se carga con la base magnética inalámbrica incluida en la caja y alcanza el 100% en aproximadamente 1.5 horas.', N'Producto', 3, 1),
    (N'¿Es resistente al agua?', N'Sí. CORSYNC cuenta con certificación IP68: resiste polvo y puede sumergirse hasta 1.5 metros durante 30 minutos. Puedes usarla en la ducha o al nadar en superficie.', N'Producto', 4, 1),
    (N'¿Es compatible con iOS y Android?', N'Sí. La aplicación CORSYNC está disponible para iOS 14 o superior y Android 11 o superior, y se conecta a la pulsera por Bluetooth Low Energy 5.2.', N'App móvil', 5, 1),
    (N'¿Puedo compartir mi aura con otras personas?', N'Sí. Desde la aplicación puedes compartir tu aura en tiempo real con las personas que elijas o publicarla en redes sociales. También puedes guardar tu historial y ver cómo ha evolucionado tu aura a lo largo del tiempo.', N'App móvil', 6, 1),
    (N'¿Cuál es la garantía del producto?', N'Todas las pulseras incluyen 2 años de garantía por defectos de fabricación. Además ofrecemos 30 días de garantía de satisfacción: si el producto no te convence, te devolvemos tu dinero.', N'Soporte', 7, 1),
    (N'¿Ofrecen descuentos por volumen?', N'Sí. Aplicamos descuentos progresivos sobre el subtotal: 10% a partir de 10 unidades, 15% a partir de 50 y 20% a partir de 100. Además existen precios preferentes por tipo de licencia Corporativa y Enterprise. Puedes calcular tu precio exacto en el formulario de cotización.', N'Ventas', 8, 1);

-- Valoraciones de ejemplo ya aprobadas, para que la seccion publica no nazca vacia.
IF NOT EXISTS (SELECT 1 FROM Comentarios WHERE Aprobado = 1)
    INSERT INTO Comentarios (NombreUsuario, Email, Contenido, Calificacion, ProductoId, Aprobado, Respuesta, FechaRespuesta, FechaCreacion) VALUES
    (N'Laura S.', 'laura.s@example.com', N'Increíble experiencia. El aura que genera refleja muy bien cómo me siento, sobre todo al final del día. La correa es cómoda y ni la siento al dormir.', 5, @productoId, 1, N'Gracias Laura. Nos alegra que CORSYNC te acompañe también de noche. - ThinkUp', DATEADD(day, -40, GETUTCDATE()), DATEADD(day, -42, GETUTCDATE())),
    (N'Miguel R.', 'miguel.r@example.com', N'Muy buen producto, la app es intuitiva y el diseño es elegante. Le falta más opciones de personalización del aura.', 4, @productoId, 1, NULL, NULL, DATEADD(day, -35, GETUTCDATE())),
    (N'Sofía T.', 'sofia.t@example.com', N'La relación calidad precio es excelente y el servicio al cliente respondió en menos de un día cuando tuve dudas con la vinculación.', 5, @productoId, 1, N'Gracias Sofía. Tu opinión nos motiva a seguir mejorando. - ThinkUp', DATEADD(day, -28, GETUTCDATE()), DATEADD(day, -30, GETUTCDATE())),
    (N'Diego M.', 'diego.m@example.com', N'Desde que uso CORSYNC entiendo mejor mis picos de estrés. Ver la lectura galvánica junto al pulso cambia cómo interpreto mi día.', 5, @productoId, 1, NULL, NULL, DATEADD(day, -21, GETUTCDATE())),
    (N'Valentina P.', 'valentina.p@example.com', N'Buen producto, aunque la batería me dura cinco días y no siete. Espero que lo mejoren con una actualización.', 3, @productoId, 1, N'Gracias por el reporte Valentina. El equipo está optimizando el consumo del sensor GSR. - ThinkUp', DATEADD(day, -12, GETUTCDATE()), DATEADD(day, -14, GETUTCDATE())),
    (N'Andrea L.', 'andrea.l@example.com', N'Compramos 25 pulseras para el programa de bienestar de la empresa. El proceso de cotización fue claro y el descuento por volumen se aplicó sin problema.', 5, @productoId, 1, NULL, NULL, DATEADD(day, -6, GETUTCDATE()));

-- Galeria del producto. Apuntan a wwwroot/img/producto, que viaja con el
-- repositorio; NombreArchivo queda vacio a proposito para que borrarlas desde
-- el panel no intente eliminar un archivo versionado.
IF NOT EXISTS (SELECT 1 FROM ImagenesProductos WHERE ProductoId = @productoId)
    INSERT INTO ImagenesProductos (ProductoId, Url, Titulo, Descripcion, Orden, NombreArchivo, TamanoBytes, FechaSubida) VALUES
    (@productoId, N'/img/producto/01-escaneo-en-vivo.jpg', N'Escaneo en vivo', N'La pulsera transmite el pulso y la conductancia de la piel en tiempo real mientras dura la lectura.', 1, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/02-tu-aura-del-dia.jpg', N'Tu aura del día', N'Las dos señales se cruzan y se traducen en un color con su interpretación y tus valores del momento.', 2, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/03-diario-energetico.jpg', N'Diario energético', N'Historial completo de lecturas con su aura, su pulso y su nivel de estrés.', 3, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/04-analisis-de-tendencias.jpg', N'Análisis de tendencias', N'Evolución del pulso y del estrés por día, semana o mes, con la distribución de auras.', 4, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/05-desafios.jpg', N'Desafíos', N'Misiones que acompañan el hábito de medición y celebran la constancia.', 5, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/06-perfil-y-ajustes.jpg', N'Perfil y ajustes', N'Resumen personal, aura dominante y configuración del dispositivo.', 6, N'', 0, GETUTCDATE());

-- Caracteristicas destacadas de la ficha comercial.
IF NOT EXISTS (SELECT 1 FROM CaracteristicasProductos WHERE ProductoId = @productoId)
    INSERT INTO CaracteristicasProductos (ProductoId, Texto, Icono, Orden) VALUES
    (@productoId, N'Sensor de respuesta galvánica de la piel (GSR)', N'activity', 1),
    (@productoId, N'Sensor óptico de ritmo cardíaco (PPG)', N'heart-pulse', 2),
    (@productoId, N'Generación de aura en tiempo real', N'circle-half', 3),
    (@productoId, N'Aplicación móvil para iOS y Android', N'phone', 4),
    (@productoId, N'Bluetooth Low Energy 5.2', N'bluetooth', 5),
    (@productoId, N'Hasta 7 días de autonomía', N'battery-full', 6),
    (@productoId, N'Resistencia al agua IP68', N'droplet', 7),
    (@productoId, N'Compartir el aura en vivo', N'share', 8);

-- Ficha tecnica agrupada por bloque.
IF NOT EXISTS (SELECT 1 FROM EspecificacionesProductos WHERE ProductoId = @productoId)
    INSERT INTO EspecificacionesProductos (ProductoId, Grupo, Campo, Valor, Orden) VALUES
    (@productoId, N'Físicas', N'Dimensiones', N'40 × 34 × 9,5 mm', 1),
    (@productoId, N'Físicas', N'Peso', N'31 g con correa', 2),
    (@productoId, N'Físicas', N'Carcasa', N'Aluminio anodizado 6061', 3),
    (@productoId, N'Físicas', N'Correa', N'Silicona médica hipoalergénica', 4),
    (@productoId, N'Físicas', N'Resistencia', N'IP68 · 1,5 m durante 30 min', 5),
    (@productoId, N'Sensores', N'Conductancia', N'GSR con electrodos de acero 316L', 6),
    (@productoId, N'Sensores', N'Pulso', N'MAX30102, fotopletismografía', 7),
    (@productoId, N'Sensores', N'Frecuencia de muestreo', N'25 Hz', 8),
    (@productoId, N'Sensores', N'Rango de pulso', N'30 – 220 BPM', 9),
    (@productoId, N'Sistema', N'Procesador', N'ESP32-C3 con BLE 5.2 y Wi-Fi', 10),
    (@productoId, N'Sistema', N'Batería', N'LiPo 300 mAh', 11),
    (@productoId, N'Sistema', N'Autonomía', N'Hasta 7 días de uso continuo', 12),
    (@productoId, N'Sistema', N'Carga', N'Base magnética inalámbrica · 1,5 h', 13),
    (@productoId, N'Sistema', N'Compatibilidad', N'iOS 14+ · Android 11+', 14);

-- Compra de demostracion para el cliente de prueba.
DECLARE @clienteId INT = (SELECT TOP 1 Id FROM Usuarios WHERE Username = 'cliente');
IF @clienteId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ComprasClientes WHERE UsuarioId = @clienteId)
    INSERT INTO ComprasClientes (UsuarioId, ProductoId, Folio, Cantidad, Monto, Estado, NumeroSerie, Resenado, FechaCompra)
    VALUES (@clienteId, @productoId, 'VTA-2026-0001', 1, 346.84, N'Entregado', 'CS-2026-000418', 0, DATEADD(day, -25, GETUTCDATE()));
";
    }
}
