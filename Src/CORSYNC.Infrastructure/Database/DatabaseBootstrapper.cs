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
        Stock               INT            NOT NULL DEFAULT 0,
        FechaCreacion       DATETIME2      NOT NULL DEFAULT GETUTCDATE()
    );

-- Almacen de producto terminado. Sube con la produccion y baja con las ventas.
-- Se agrega aparte para las bases creadas antes de existir esta columna.
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Productos') AND name = 'Stock')
    ALTER TABLE Productos ADD Stock INT NOT NULL DEFAULT 0;

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
-- para que las cotizaciones de la manga no tengan que informarlos.
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
-- Catalogo base de ThinkUp: un unico producto, la manga CORSYNC
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

-- Materia prima de la manga, valuada en MXN. Cada importe es el costo promedio
-- ponderado de arranque; las recepciones de compra lo van promediando. Los Ids 1
-- a 5 se reconvierten en su lugar para que coincidan con el seed de EF, y los dos
-- modulos restantes se dan de alta por nombre.
-- Valores OPERATIVOS (existencias y costo promedio): se siembran una sola vez.
-- El guardia es el nombre viejo, que solo coincide antes de reconvertir el
-- renglon. Reaplicarlos en cada arranque borraria las recepciones de compra, la
-- produccion registrada y el costo promedio que esas compras fueron formando.
UPDATE MateriasPrimas SET CostoUnidad = 100.00, Stock = 800 WHERE Id = 1 AND Nombre <> N'Carcasa impresa en 3D';
UPDATE MateriasPrimas SET CostoUnidad = 259.96, Stock = 640 WHERE Id = 2 AND Nombre <> N'Sensor MCU-6701 (GSR)';
UPDATE MateriasPrimas SET CostoUnidad = 64.24,  Stock = 700 WHERE Id = 3 AND Nombre <> N'Sensor MAX30102';
UPDATE MateriasPrimas SET CostoUnidad = 129.99, Stock = 520 WHERE Id = 4 AND Nombre <> N'Módulo ESP32 (MCU + Wi-Fi)';
UPDATE MateriasPrimas SET CostoUnidad = 150.00, Stock = 900 WHERE Id = 5 AND Nombre <> N'Batería recargable de 9V (500 mAh)';

-- Datos DESCRIPTIVOS: se reaplican siempre, porque son correcciones de catalogo
-- y no arrastran informacion de inventario.
UPDATE MateriasPrimas SET Nombre = N'Carcasa impresa en 3D', Descripcion = N'Carcasa impresa en 3D en filamento PLA, diseñada a medida para alojar los sensores.', UnidadMedida = N'pieza', StockMinimo = 200, ProveedorId = @provSilicon, Activo = 1 WHERE Id = 1;
UPDATE MateriasPrimas SET Nombre = N'Sensor MCU-6701 (GSR)', Descripcion = N'Módulo de conductancia de la piel para medición de activación fisiológica.', UnidadMedida = N'pieza', StockMinimo = 150, ProveedorId = @provMaxim, Activo = 1 WHERE Id = 2;
UPDATE MateriasPrimas SET Nombre = N'Sensor MAX30102', Descripcion = N'Sensor de ritmo cardíaco y HRV.', UnidadMedida = N'pieza', StockMinimo = 150, ProveedorId = @provMaxim, Activo = 1 WHERE Id = 3;
UPDATE MateriasPrimas SET Nombre = N'Módulo ESP32 (MCU + Wi-Fi)', Descripcion = N'Microcontrolador con Wi-Fi integrado; es el que transmite las lecturas a la app.', UnidadMedida = N'pieza', StockMinimo = 120, ProveedorId = @provMaxim, Activo = 1 WHERE Id = 4;
UPDATE MateriasPrimas SET Nombre = N'Batería recargable de 9V (500 mAh)', Descripcion = N'Batería recargable de 9V y 500 mAh que alimenta la manga durante la sesión de medición.', UnidadMedida = N'pieza', StockMinimo = 250, ProveedorId = @provBat, Activo = 1 WHERE Id = 5;

IF NOT EXISTS (SELECT 1 FROM MateriasPrimas WHERE Nombre = N'Módulo indicador de carga XW228DKFR4')
    INSERT INTO MateriasPrimas (Nombre, Descripcion, CostoUnidad, UnidadMedida, Stock, StockMinimo, ProveedorId, Activo)
    VALUES (N'Módulo indicador de carga XW228DKFR4', N'Módulo indicador del nivel de carga de la batería.', 80.00, N'pieza', 600, 150, @provPcb, 1);
-- El regulador se sembro sin modelo en una version anterior; se renombra en su
-- lugar para no insertar un duplicado con el nombre nuevo.
UPDATE MateriasPrimas SET Nombre = N'Regulador de voltaje LM2596' WHERE Nombre = N'Regulador de voltaje';
IF NOT EXISTS (SELECT 1 FROM MateriasPrimas WHERE Nombre = N'Regulador de voltaje LM2596')
    INSERT INTO MateriasPrimas (Nombre, Descripcion, CostoUnidad, UnidadMedida, Stock, StockMinimo, ProveedorId, Activo)
    VALUES (N'Regulador de voltaje LM2596', N'Regulador que estabiliza la salida de la batería de 9V hacia los sensores y el MCU.', 95.60, N'pieza', 600, 150, @provPcb, 1);

-- Insumos que salieron del diseño de la manga. Los renglones 1 a 5 ya fueron
-- reconvertidos arriba, asi que aqui solo caen los que quedaron sueltos del
-- catalogo anterior. Se limpian sus dependencias antes de borrarlos para no
-- dejar llaves foraneas huerfanas.
DECLARE @obsoletos TABLE (Id INT);
INSERT INTO @obsoletos (Id)
SELECT Id FROM MateriasPrimas
WHERE Nombre IN (
    N'Correa de silicona hipoalergénica',
    N'Tela elástica para manga',
    N'Carcasa de plástico impresa en 3D',
    N'Pila recargable de 9V',
    N'Electrodos de acero inoxidable 316L',
    N'PCB flexible de 4 capas',
    N'Cargador magnético inalámbrico',
    N'Empaque premium y manual impreso');

DELETE FROM RecetasProductos WHERE MateriaPrimaId IN (SELECT Id FROM @obsoletos);
DELETE FROM DetallesCompraProveedor WHERE MateriaPrimaId IN (SELECT Id FROM @obsoletos);
DELETE FROM MateriasPrimas WHERE Id IN (SELECT Id FROM @obsoletos);

-- Producto unico: la manga CORSYNC.
-- Costo primo: materia prima 879.79 + mano de obra 60.00 = 939.79
-- Gastos indirectos 25% = 234.95 -> costo unitario 1,174.74
-- Margen 50% -> precio de lista 1,762.11
IF NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = N'CORSYNC')
    INSERT INTO Productos (Nombre, Descripcion, DescripcionLarga, ManoObraUnitaria, OverheadPorcentaje, MargenUtilidad, Activo, FechaCreacion)
    VALUES (
        N'CORSYNC',
        N'Manga biométrica que mide tu actividad galvánica y tu ritmo cardíaco para generar tu aura digital.',
        N'CORSYNC es una manga que se coloca en el antebrazo y lee de forma continua dos señales de tu cuerpo: la actividad electrodermal de tu piel, mediante el sensor MCU-6701, y tu ritmo cardíaco, mediante el sensor MAX30102. Ambas señales viajan por Wi-Fi a la aplicación móvil, donde se traducen en un aura: una representación de color que refleja tu estado en ese momento. El aura se puede guardar, revisar en tu historial y compartir con las personas que elijas.',
        60.00, 0.25, 0.50, 1, GETUTCDATE());

DECLARE @productoId INT = (SELECT TOP 1 Id FROM Productos WHERE Nombre = N'CORSYNC');

-- Parametros de costeo vigentes. Se reaplican en cada arranque para que una base
-- creada con los valores anteriores quede alineada con el seed de EF.
UPDATE Productos SET ManoObraUnitaria = 60.00, OverheadPorcentaje = 0.25, MargenUtilidad = 0.50 WHERE Id = @productoId;

-- La descripcion tambien se reaplica: el INSERT de arriba solo corre en una base
-- vacia, asi que una base ya creada seguiria anunciando la version anterior (la
-- de pulsera, con Bluetooth y fotopletismografia) que es justo lo que se corrigio.
UPDATE Productos
SET Descripcion = N'Manga biométrica que mide tu actividad galvánica y tu ritmo cardíaco para generar tu aura digital.',
    DescripcionLarga = N'CORSYNC es una manga que se coloca en el antebrazo y lee de forma continua dos señales de tu cuerpo: la actividad electrodermal de tu piel, mediante el sensor MCU-6701, y tu ritmo cardíaco, mediante el sensor MAX30102. Ambas señales viajan por Wi-Fi a la aplicación móvil, donde se traducen en un aura: una representación de color que refleja tu estado en ese momento. El aura se puede guardar, revisar en tu historial y compartir con las personas que elijas.'
WHERE Id = @productoId;

-- Explosion de materiales: una pieza de cada insumo por manga.
DELETE FROM RecetasProductos WHERE ProductoId = @productoId;
INSERT INTO RecetasProductos (ProductoId, NombreProducto, MateriaPrimaId, CantidadRequerida, MermaPorcentaje)
SELECT @productoId, N'CORSYNC', Id, 1, 0
FROM MateriasPrimas
WHERE Nombre IN (
    N'Carcasa impresa en 3D',
    N'Sensor MCU-6701 (GSR)',
    N'Sensor MAX30102',
    N'Módulo ESP32 (MCU + Wi-Fi)',
    N'Batería recargable de 9V (500 mAh)',
    N'Módulo indicador de carga XW228DKFR4',
    N'Regulador de voltaje LM2596');

-- Recetas del producto anterior (espejo) que ya no forman parte del catalogo.
DELETE FROM RecetasProductos WHERE ProductoId <> @productoId;

-- Ordenes de compra que dan origen al inventario. Sin ellas el stock y el costo
-- promedio de cada insumo apareceren sin respaldo en el modulo de compras. Cada
-- renglon cuadra con las existencias y el costo sembrados, de modo que el valor
-- del inventario es la suma de estas cuatro ordenes.
--
-- Van como 'Recibida': ya afectaron el inventario. El endpoint de recepcion
-- rechaza recibir una compra que ya lo esta, asi que no pueden volver a sumar.
-- Solo se siembran si no hay ninguna compra: si el administrador ya registro las
-- suyas, no se toca su historial.
IF NOT EXISTS (SELECT 1 FROM ComprasProveedores)
BEGIN
    DECLARE @compras TABLE (Folio NVARCHAR(50), ProveedorId INT, CompraId INT);

    INSERT INTO ComprasProveedores (ProveedorId, Folio, MontoTotal, Estado, Notas, FechaCompra, FechaRecepcion)
    OUTPUT INSERTED.Folio, INSERTED.ProveedorId, INSERTED.Id INTO @compras
    VALUES
        (@provMaxim,   N'OC-2026-0001', 278937.20, N'Recibida', N'Lote inicial de sensores y microcontroladores.', DATEADD(day, -80, GETUTCDATE()), DATEADD(day, -73, GETUTCDATE())),
        (@provSilicon, N'OC-2026-0002',  80000.00, N'Recibida', N'Carcasas impresas en 3D del primer lote.',       DATEADD(day, -78, GETUTCDATE()), DATEADD(day, -70, GETUTCDATE())),
        (@provPcb,     N'OC-2026-0003', 105360.00, N'Recibida', N'Regulación y control de carga.',                 DATEADD(day, -76, GETUTCDATE()), DATEADD(day, -65, GETUTCDATE())),
        (@provBat,     N'OC-2026-0004', 135000.00, N'Recibida', N'Baterías recargables de 9V.',                    DATEADD(day, -74, GETUTCDATE()), DATEADD(day, -67, GETUTCDATE()));

    -- Los renglones se arman contra el catalogo por nombre, para que apunten a los
    -- Ids reales que quedaron tras la reconversion de insumos de arriba.
    INSERT INTO DetallesCompraProveedor (CompraProveedorId, MateriaPrimaId, Cantidad, CostoUnitario, Importe)
    SELECT c.CompraId, m.Id, d.Cantidad, d.CostoUnitario, d.Cantidad * d.CostoUnitario
    FROM (VALUES
        (N'OC-2026-0001', N'Sensor MCU-6701 (GSR)',                640, CAST(259.96 AS DECIMAL(18,4))),
        (N'OC-2026-0001', N'Sensor MAX30102',                      700, CAST( 64.24 AS DECIMAL(18,4))),
        (N'OC-2026-0001', N'Módulo ESP32 (MCU + Wi-Fi)',           520, CAST(129.99 AS DECIMAL(18,4))),
        (N'OC-2026-0002', N'Carcasa impresa en 3D',                800, CAST(100.00 AS DECIMAL(18,4))),
        (N'OC-2026-0003', N'Módulo indicador de carga XW228DKFR4', 600, CAST( 80.00 AS DECIMAL(18,4))),
        (N'OC-2026-0003', N'Regulador de voltaje LM2596',          600, CAST( 95.60 AS DECIMAL(18,4))),
        (N'OC-2026-0004', N'Batería recargable de 9V (500 mAh)',   900, CAST(150.00 AS DECIMAL(18,4)))
    ) AS d (Folio, Insumo, Cantidad, CostoUnitario)
    JOIN @compras c ON c.Folio = d.Folio
    JOIN MateriasPrimas m ON m.Nombre = d.Insumo;
END

-- Documentacion del producto
IF NOT EXISTS (SELECT 1 FROM DocumentosProductos WHERE ProductoId = @productoId)
    INSERT INTO DocumentosProductos (ProductoId, Titulo, Descripcion, Tipo, Url, Peso, FechaPublicacion) VALUES
    (@productoId, N'Manual de usuario CORSYNC', N'Guía completa de uso, cuidados y solución de problemas de la manga.', N'Manual', '/docs/corsync-manual-usuario.pdf', '4.2 MB', GETUTCDATE()),
    (@productoId, N'Guía de inicio rápido', N'Primeros pasos: encendido, conexión Wi-Fi y primera lectura de aura.', N'Guia', '/docs/corsync-inicio-rapido.pdf', '1.1 MB', GETUTCDATE()),
    (@productoId, N'Ficha técnica', N'Especificaciones de sensores, autonomía, materiales y conectividad.', N'FichaTecnica', '/docs/corsync-ficha-tecnica.pdf', '820 KB', GETUTCDATE()),
    (@productoId, N'Póliza de garantía', N'Cobertura de 2 años por defectos de fabricación y proceso de devolución.', N'Garantia', '/docs/corsync-garantia.pdf', '310 KB', GETUTCDATE());

-- Preguntas frecuentes
IF NOT EXISTS (SELECT 1 FROM PreguntasFrecuentes)
    INSERT INTO PreguntasFrecuentes (Pregunta, Respuesta, Categoria, Orden, Activo) VALUES
    (N'¿Qué sensores incluye CORSYNC?', N'CORSYNC integra dos sensores: el MCU-6701, que mide la conductancia eléctrica de tu piel, y el MAX30102, que registra tu ritmo cardíaco. La combinación de ambas señales es la que alimenta el cálculo de tu aura.', N'Producto', 1, 1),
    (N'¿Cómo se genera el aura?', N'La manga envía las lecturas de actividad galvánica y ritmo cardíaco a la aplicación móvil. Ahí se procesan en conjunto y se traducen en color, intensidad y movimiento. Un pulso elevado con alta conductancia produce un aura cálida y agitada; un pulso bajo y estable produce tonos fríos y un movimiento sereno.', N'Producto', 2, 1),
    (N'¿Cuánto dura la batería?', N'CORSYNC funciona con una batería recargable de 9V y 500 mAh, con un módulo indicador que muestra el nivel de carga restante. Al agotarse se recarga y la manga vuelve a estar lista para la siguiente sesión. La duración por carga depende del uso; publicaremos la cifra en cuanto termine la caracterización del prototipo.', N'Producto', 3, 1),
    (N'¿Cómo se coloca la manga?', N'CORSYNC se desliza sobre el antebrazo hasta que los sensores queden en contacto directo con la piel. No lleva correa ni broche: la propia manga la mantiene en su sitio durante la lectura.', N'Producto', 4, 1),
    (N'¿Es compatible con iOS y Android?', N'Sí. La aplicación CORSYNC está disponible para iOS 14 o superior y Android 11 o superior, y recibe las lecturas por Wi-Fi.', N'App móvil', 5, 1),
    (N'¿Puedo compartir mi aura con otras personas?', N'Sí. Desde la aplicación puedes compartir tu aura en tiempo real con las personas que elijas o publicarla en redes sociales. También puedes guardar tu historial y ver cómo ha evolucionado tu aura a lo largo del tiempo.', N'App móvil', 6, 1),
    (N'¿Cuál es la garantía del producto?', N'Todas las mangas incluyen 2 años de garantía por defectos de fabricación. Además ofrecemos 30 días de garantía de satisfacción: si el producto no te convence, te devolvemos tu dinero.', N'Soporte', 7, 1),
    (N'¿Ofrecen descuentos por volumen?', N'Sí. Aplicamos descuentos progresivos sobre el subtotal: 10% a partir de 5 unidades y 15% a partir de 15. Además existen precios preferentes por tipo de licencia Corporativa y Enterprise. El cotizador en línea admite hasta 100 unidades; si necesitas más, escríbenos y lo vemos contigo. Puedes calcular tu precio exacto en el formulario de cotización.', N'Ventas', 8, 1);

-- Las preguntas frecuentes no se reescriben en cada arranque porque el administrador
-- las edita desde el panel. Estas dos si, porque anunciaban datos que el sistema no
-- cumple: se corrigen en su lugar sin tocar las demas.

-- Describia la pila anterior y una autonomia que ya no sostenemos.
UPDATE PreguntasFrecuentes
SET Respuesta = N'CORSYNC funciona con una batería recargable de 9V y 500 mAh, con un módulo indicador que muestra el nivel de carga restante. Al agotarse se recarga y la manga vuelve a estar lista para la siguiente sesión. La duración por carga depende del uso; publicaremos la cifra en cuanto termine la caracterización del prototipo.'
WHERE Pregunta = N'¿Cuánto dura la batería?';

-- Prometia un 20% que el cotizador nunca aplica y pedia mas unidades de las
-- necesarias. Los tramos reales viven en ReglasComerciales.TramosVolumen.
UPDATE PreguntasFrecuentes
SET Respuesta = N'Sí. Aplicamos descuentos progresivos sobre el subtotal: 10% a partir de 5 unidades y 15% a partir de 15. Además existen precios preferentes por tipo de licencia Corporativa y Enterprise. El cotizador en línea admite hasta 100 unidades; si necesitas más, escríbenos y lo vemos contigo. Puedes calcular tu precio exacto en el formulario de cotización.'
WHERE Pregunta = N'¿Ofrecen descuentos por volumen?';

-- Valoraciones de ejemplo ya aprobadas, para que la seccion publica no nazca vacia.
IF NOT EXISTS (SELECT 1 FROM Comentarios WHERE Aprobado = 1)
    INSERT INTO Comentarios (NombreUsuario, Email, Contenido, Calificacion, ProductoId, Aprobado, Respuesta, FechaRespuesta, FechaCreacion) VALUES
    (N'Laura S.', 'laura.s@example.com', N'Increíble experiencia. El aura que genera refleja muy bien cómo me siento, sobre todo al final del día. La correa es cómoda y ni la siento al dormir.', 5, @productoId, 1, N'Gracias Laura. Nos alegra que CORSYNC te acompañe también de noche. - ThinkUp', DATEADD(day, -40, GETUTCDATE()), DATEADD(day, -42, GETUTCDATE())),
    (N'Miguel R.', 'miguel.r@example.com', N'Muy buen producto, la app es intuitiva y el diseño es elegante. Le falta más opciones de personalización del aura.', 4, @productoId, 1, NULL, NULL, DATEADD(day, -35, GETUTCDATE())),
    (N'Sofía T.', 'sofia.t@example.com', N'La relación calidad precio es excelente y el servicio al cliente respondió en menos de un día cuando tuve dudas con la vinculación.', 5, @productoId, 1, N'Gracias Sofía. Tu opinión nos motiva a seguir mejorando. - ThinkUp', DATEADD(day, -28, GETUTCDATE()), DATEADD(day, -30, GETUTCDATE())),
    (N'Diego M.', 'diego.m@example.com', N'Desde que uso CORSYNC entiendo mejor mis picos de estrés. Ver la lectura galvánica junto al pulso cambia cómo interpreto mi día.', 5, @productoId, 1, NULL, NULL, DATEADD(day, -21, GETUTCDATE())),
    (N'Valentina P.', 'valentina.p@example.com', N'Buen producto, aunque la batería me dura cinco días y no siete. Espero que lo mejoren con una actualización.', 3, @productoId, 1, N'Gracias por el reporte Valentina. El equipo está optimizando el consumo del sensor GSR. - ThinkUp', DATEADD(day, -12, GETUTCDATE()), DATEADD(day, -14, GETUTCDATE())),
    (N'Andrea L.', 'andrea.l@example.com', N'Compramos 25 mangas para el programa de bienestar de la empresa. El proceso de cotización fue claro y el descuento por volumen se aplicó sin problema.', 5, @productoId, 1, NULL, NULL, DATEADD(day, -6, GETUTCDATE()));

-- Galeria del producto. Apuntan a wwwroot/img/producto, que viaja con el
-- repositorio; NombreArchivo queda vacio a proposito para que borrarlas desde
-- el panel no intente eliminar un archivo versionado.
IF NOT EXISTS (SELECT 1 FROM ImagenesProductos WHERE ProductoId = @productoId)
    INSERT INTO ImagenesProductos (ProductoId, Url, Titulo, Descripcion, Orden, NombreArchivo, TamanoBytes, FechaSubida) VALUES
    (@productoId, N'/img/producto/01-escaneo-en-vivo.jpg', N'Escaneo en vivo', N'La manga transmite el pulso y la conductancia de la piel en tiempo real mientras dura la lectura.', 1, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/02-tu-aura-del-dia.jpg', N'Tu aura del día', N'Las dos señales se cruzan y se traducen en un color con su interpretación y tus valores del momento.', 2, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/03-diario-energetico.jpg', N'Diario energético', N'Historial completo de lecturas con su aura, su pulso y su nivel de estrés.', 3, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/04-analisis-de-tendencias.jpg', N'Análisis de tendencias', N'Evolución del pulso y del estrés por día, semana o mes, con la distribución de auras.', 4, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/05-desafios.jpg', N'Desafíos', N'Misiones que acompañan el hábito de medición y celebran la constancia.', 5, N'', 0, GETUTCDATE()),
    (@productoId, N'/img/producto/06-perfil-y-ajustes.jpg', N'Perfil y ajustes', N'Resumen personal, aura dominante y configuración del dispositivo.', 6, N'', 0, GETUTCDATE());

-- Caracteristicas destacadas de la ficha comercial. Se reescriben en cada arranque
-- (igual que la receta) para que una base creada con la ficha anterior quede
-- alineada con el seed de EF; si solo se insertaran cuando la tabla esta vacia,
-- los textos viejos se quedarian ahi para siempre.
DELETE FROM CaracteristicasProductos WHERE ProductoId = @productoId;
INSERT INTO CaracteristicasProductos (ProductoId, Texto, Icono, Orden) VALUES
    (@productoId, N'Sensor MCU-6701 de respuesta galvánica de la piel', N'activity', 1),
    (@productoId, N'Sensor MAX30102 de ritmo cardíaco', N'heart-pulse', 2),
    (@productoId, N'Generación de aura en tiempo real', N'circle-half', 3),
    (@productoId, N'Aplicación móvil para iOS y Android', N'phone', 4),
    (@productoId, N'Conexión Wi-Fi mediante ESP32', N'wifi', 5),
    (@productoId, N'Batería recargable de 9V y 500 mAh, con indicador de nivel de carga XW228DKFR4', N'battery-full', 6),
    (@productoId, N'Regulador de voltaje LM2596 que estabiliza la alimentación de los sensores', N'lightning-charge', 7),
    (@productoId, N'Carcasa impresa en 3D en filamento PLA', N'box', 8),
    (@productoId, N'Compartir el aura en vivo', N'share', 9);

-- Ficha tecnica agrupada por bloque. Filas que no existen a proposito: Bluetooth
-- (transmite por Wi-Fi), Resistencia al agua y Carga inalambrica (la manga no las
-- tiene), Correa (ya no lleva) y Autonomia (con la bateria de 9V no hay una cifra
-- medida; se agrega cuando la haya).
DELETE FROM EspecificacionesProductos WHERE ProductoId = @productoId;
INSERT INTO EspecificacionesProductos (ProductoId, Grupo, Campo, Valor, Orden) VALUES
    (@productoId, N'Físicas', N'Dimensiones', N'14 × 13.5 × 8 cm', 1),
    (@productoId, N'Físicas', N'Peso', N'240 g', 2),
    (@productoId, N'Físicas', N'Carcasa', N'Filamento PLA', 3),
    (@productoId, N'Sensores', N'Conductancia', N'MCU-6701 (GSR)', 6),
    (@productoId, N'Sensores', N'Pulso', N'MAX30102', 7),
    (@productoId, N'Sensores', N'Rango de pulso', N'30 – 220 BPM', 9),
    (@productoId, N'Sistema', N'Procesador', N'ESP32 con Wi-Fi', 10),
    (@productoId, N'Sistema', N'Batería', N'Recargable de 9V · 500 mAh', 11),
    (@productoId, N'Sistema', N'Regulador de voltaje', N'LM2596', 13),
    (@productoId, N'Sistema', N'Indicador de carga', N'XW228DKFR4', 15),
    (@productoId, N'Sistema', N'Compatibilidad', N'iOS 14+ · Android 11+', 17);

-- Lote inicial fabricado y compra de demostracion para el cliente de prueba.
--
-- Van juntos a proposito: sin producto terminado en almacen no se puede registrar
-- ninguna venta, asi que una base recien creada nacia con una venta de demostracion
-- que el propio sistema habria rechazado. Se siembra el lote y la venta descuenta
-- de el, igual que cualquier otra: 25 fabricadas - 1 vendida = 24 disponibles.
--
-- Todo el bloque corre una sola vez; despues manda la produccion real.
DECLARE @clienteId INT = (SELECT TOP 1 Id FROM Usuarios WHERE Username = 'cliente');
IF @clienteId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ComprasClientes WHERE UsuarioId = @clienteId)
BEGIN
    UPDATE Productos SET Stock = Stock + 25 WHERE Id = @productoId;

    INSERT INTO ComprasClientes (UsuarioId, ProductoId, Folio, Cantidad, Monto, Estado, NumeroSerie, Resenado, FechaCompra)
    VALUES (@clienteId, @productoId, 'VTA-2026-0001', 1, 2044.05, N'Procesando', 'CS-2026-000418', 0, DATEADD(day, -25, GETUTCDATE()));

    UPDATE Productos SET Stock = Stock - 1 WHERE Id = @productoId;
END
";
    }
}
