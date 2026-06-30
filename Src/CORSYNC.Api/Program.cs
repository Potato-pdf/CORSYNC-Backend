using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CORSYNC.Api.Hubs;
using CORSYNC.Api.Services;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Auth;
using CORSYNC.Infrastructure.Database;
using CORSYNC.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Add DbContexts - Fall back to InMemory for demonstration or local run without SQL Server instance
var adminConn = builder.Configuration.GetConnectionString("AdminConnection");
if (!string.IsNullOrEmpty(adminConn))
{
    builder.Services.AddDbContext<AdminDbContext>(options =>
        options.UseSqlServer(adminConn));
}
else
{
    builder.Services.AddDbContext<AdminDbContext>(options =>
        options.UseInMemoryDatabase("CORSYNC_Admin"));
}

var telemetryConn = builder.Configuration.GetConnectionString("TelemetryConnection");
if (!string.IsNullOrEmpty(telemetryConn))
{
    builder.Services.AddDbContext<TelemetryDbContext>(options =>
        options.UseSqlServer(telemetryConn));
}
else
{
    builder.Services.AddDbContext<TelemetryDbContext>(options =>
        options.UseInMemoryDatabase("CORSYNC_Telemetry"));
}

// Add CORSYNC telemetry services
builder.Services.AddSingleton<ITelemetryProcessor, TelemetryProcessor>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Register Hosted Services (Workers)
builder.Services.AddHostedService<TelemetryDbFlushWorker>();

// Add API Controllers and SignalR WebSockets
builder.Services.AddControllers();
builder.Services.AddSignalR();

// JWT Authentication Configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["TokenConfiguration:Issuer"] ?? "CORSYNCServer",
            ValidAudience = builder.Configuration["TokenConfiguration:Audience"] ?? "CORSYNCClients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration["TokenConfiguration:SecretKey"] ?? "LLAVE_SECRETA_SUPER_LARGA_Y_COMPLEJA_PARA_JWT_2026"))
        };

        // Permitir pasar el JWT Token por Query String para SignalR WebSockets
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/telemetryHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure database seed/schema creation on startup
using (var scope = app.Services.CreateScope())
{
    var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    adminDb.Database.EnsureCreated();

    var telemetryDb = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    telemetryDb.Database.EnsureCreated();

    if (adminDb.Database.IsRelational())
    {
        adminDb.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios') AND name = 'Email')
            BEGIN
                ALTER TABLE Usuarios ADD Email NVARCHAR(100) NOT NULL DEFAULT 'temp@corsync.com';
            END
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios') AND name = 'NombreCompleto')
            BEGIN
                ALTER TABLE Usuarios ADD NombreCompleto NVARCHAR(100) NOT NULL DEFAULT '';
            END
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios') AND name = 'FechaRegistro')
            BEGIN
                ALTER TABLE Usuarios ADD FechaRegistro DATETIME2 NOT NULL DEFAULT GETUTCDATE();
            END
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios') AND name = 'Activo')
            BEGIN
                ALTER TABLE Usuarios ADD Activo BIT NOT NULL DEFAULT 1;
            END

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Usuarios_Username' AND object_id = OBJECT_ID('Usuarios'))
            BEGIN
                CREATE UNIQUE INDEX IX_Usuarios_Username ON Usuarios(Username);
            END
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Usuarios_Email' AND object_id = OBJECT_ID('Usuarios'))
            BEGIN
                CREATE UNIQUE INDEX IX_Usuarios_Email ON Usuarios(Email);
            END

            UPDATE Usuarios 
            SET PasswordHash = '$2a$11$UZ8mNYO7Ss0T41oYzfqHt.ILCFlrmVxEUZr6/i1cdBZ1qAxBhrBj.'
            WHERE Username = 'admin' AND PasswordHash = 'admin123';

            UPDATE Usuarios 
            SET PasswordHash = '$2a$11$fOK8ihp4BxXTrxjzGqw8Gu6Zdv1ZFFmA4XMX5KD26UjdsyLaovOfO'
            WHERE Username = 'cliente' AND PasswordHash = 'cliente123';
        ");
    }

    if (telemetryDb.Database.IsRelational())
    {
        telemetryDb.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LecturasCorazon' AND xtype='U')
            BEGIN
                CREATE TABLE LecturasCorazon (
                    Id              INT IDENTITY(1,1) PRIMARY KEY,
                    DispositivoId   NVARCHAR(50) NOT NULL,
                    IR              BIGINT NOT NULL,
                    BPM             DECIMAL(5,1) NOT NULL,
                    BPMPromedio     INT NOT NULL,
                    GsrRaw          INT NOT NULL DEFAULT 0,
                    GsrVoltaje      DECIMAL(5,3) NOT NULL DEFAULT 0.0,
                    Aura            NVARCHAR(50) NULL,
                    FechaHora       DATETIME2 NOT NULL
                )
            END
            ELSE
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LecturasCorazon') AND name = 'GsrRaw')
                BEGIN
                    ALTER TABLE LecturasCorazon ADD GsrRaw INT NOT NULL DEFAULT 0;
                END
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LecturasCorazon') AND name = 'GsrVoltaje')
                BEGIN
                    ALTER TABLE LecturasCorazon ADD GsrVoltaje DECIMAL(5,3) NOT NULL DEFAULT 0.0;
                END
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LecturasCorazon') AND name = 'Aura')
                BEGIN
                    ALTER TABLE LecturasCorazon ADD Aura NVARCHAR(50) NULL;
                END
            END
        ");
    }
}

//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TelemetryHub>("/telemetryHub");

app.Run();
