using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CORSYNC.Api.Hubs;
using CORSYNC.Api.Services;
using CORSYNC.Core.Interfaces;
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
builder.Services.AddSingleton<TelemetryChannel>();
builder.Services.AddSingleton<ITelemetryProcessor, TelemetryProcessor>();

// Register Hosted Services (Workers)
builder.Services.AddHostedService<MqttTelemetryWorker>();
builder.Services.AddHostedService<SignalRBroadcastWorker>();

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
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TelemetryHub>("/telemetryHub");

app.Run();
