/* ============================================================================
   CORSYNC — Cuentas de base de datos con privilegios acotados
   ----------------------------------------------------------------------------
   Requisito 6b: no operar con la cuenta del administrador del DBMS.

   Este script crea dos cuentas de servicio sobre la base de CORSYNC:

     corsync_app      La que usa la API. Puede leer y escribir los datos de
                      negocio, pero NO puede alterar el esquema, ni crear
                      usuarios, ni tocar las tablas de seguridad del servidor.

     corsync_lectura  Sólo lectura. Pensada para informes, respaldos lógicos o
                      conexiones de BI que no deben poder modificar nada.

   Ejecutar con una cuenta que tenga permisos de administración sobre la
   instancia (sysadmin o db_owner de la base), UNA sola vez.

   Después, cambiar la cadena de conexión de la API para que use corsync_app:

     "AdminConnection": "Server=...; Database=...; User Id=corsync_app;
                         Password=<la que se defina abajo>; Encrypt=True;"

   IMPORTANTE: sustituir las contraseñas de ejemplo antes de ejecutar, y no
   dejarlas escritas en este archivo dentro del repositorio.
   ============================================================================ */

USE master;
GO

/* ---------------------------------------------------------------------------
   1. Inicios de sesión a nivel de servidor
   --------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'corsync_app')
BEGIN
    CREATE LOGIN corsync_app
        WITH PASSWORD = 'CAMBIAR_Esta_Clave_App_2026!',
             CHECK_POLICY = ON,          -- obliga a cumplir la política de contraseñas
             CHECK_EXPIRATION = OFF;     -- una cuenta de servicio no debe caducar sola
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'corsync_lectura')
BEGIN
    CREATE LOGIN corsync_lectura
        WITH PASSWORD = 'CAMBIAR_Esta_Clave_Lectura_2026!',
             CHECK_POLICY = ON,
             CHECK_EXPIRATION = OFF;
END
GO

/* ---------------------------------------------------------------------------
   2. Usuarios dentro de la base de CORSYNC
   --------------------------------------------------------------------------- */
USE db57402;   -- Ajustar al nombre real de la base
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'corsync_app')
    CREATE USER corsync_app FOR LOGIN corsync_app;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'corsync_lectura')
    CREATE USER corsync_lectura FOR LOGIN corsync_lectura;
GO

/* ---------------------------------------------------------------------------
   3. Rol de aplicación: leer y escribir datos, nada más
   ---------------------------------------------------------------------------
   Se usa un rol en lugar de dar permisos sueltos al usuario: si mañana hace
   falta una segunda cuenta de aplicación, basta con añadirla al rol.
   --------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'rol_corsync_app' AND type = 'R')
    CREATE ROLE rol_corsync_app;
GO

-- Permisos de datos sobre el esquema completo.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO rol_corsync_app;

-- Necesario para que EF Core pueda leer los metadatos del modelo al arrancar.
GRANT VIEW DEFINITION ON SCHEMA::dbo TO rol_corsync_app;

/* Lo que este rol NO puede hacer, de forma explícita:
   - Alterar el esquema (CREATE/ALTER/DROP TABLE)
   - Crear o modificar usuarios y permisos
   - Ejecutar comandos de respaldo o restauración                          */
DENY ALTER, CREATE TABLE, CREATE VIEW, CREATE PROCEDURE, CREATE FUNCTION TO rol_corsync_app;
DENY ALTER ANY USER, ALTER ANY ROLE, ALTER ANY SCHEMA TO rol_corsync_app;
GO

ALTER ROLE rol_corsync_app ADD MEMBER corsync_app;
GO

/* ---------------------------------------------------------------------------
   4. Rol de sólo lectura
   --------------------------------------------------------------------------- */
ALTER ROLE db_datareader ADD MEMBER corsync_lectura;
GO

-- Que no pueda escribir nada, aunque alguien le conceda un permiso por error.
DENY INSERT, UPDATE, DELETE ON SCHEMA::dbo TO corsync_lectura;
GO

/* ---------------------------------------------------------------------------
   5. Comprobación
   ---------------------------------------------------------------------------
   Lista los permisos efectivos de cada cuenta para verificar el resultado.
   --------------------------------------------------------------------------- */
SELECT
    pr.name                AS cuenta,
    pr.type_desc           AS tipo,
    pe.permission_name     AS permiso,
    pe.state_desc          AS estado,
    ISNULL(s.name, '(base)') AS ambito
FROM sys.database_principals pr
LEFT JOIN sys.database_permissions pe ON pe.grantee_principal_id = pr.principal_id
LEFT JOIN sys.schemas s ON s.schema_id = pe.major_id AND pe.class = 3
WHERE pr.name IN ('corsync_app', 'corsync_lectura', 'rol_corsync_app')
ORDER BY pr.name, pe.permission_name;
GO

/* ---------------------------------------------------------------------------
   NOTA SOBRE LA MIGRACIÓN DEL ESQUEMA
   ---------------------------------------------------------------------------
   La API ejecuta DDL al arrancar (DatabaseBootstrapper) para crear las tablas
   que falten. Con corsync_app ese DDL fallará por diseño, y el arranque lo
   registrará en el log sin caerse.

   El procedimiento correcto en producción es:
     1. Arrancar UNA vez con una cuenta con permisos de esquema, o ejecutar el
        DDL a mano, para dejar las tablas creadas.
     2. Cambiar la cadena de conexión a corsync_app para la operación normal.
   --------------------------------------------------------------------------- */
