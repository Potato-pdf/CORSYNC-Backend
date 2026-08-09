# Levanta la API en local con base de datos en memoria (no toca la BD desplegada).
# Util para desarrollar el frontend sin depender del servidor de produccion.
#
#   powershell -File Tools/run-local-api.ps1
#
# La API queda escuchando en http://localhost:5213 y siembra el catalogo completo:
# producto CORSYNC, materia prima, receta, proveedores, FAQ y valoraciones de ejemplo.

$ErrorActionPreference = 'Stop'

$env:ConnectionStrings__AdminConnection = ''
$env:ConnectionStrings__TelemetryConnection = ''
$env:ASPNETCORE_URLS = 'http://localhost:5213'
$env:ASPNETCORE_ENVIRONMENT = 'Development'

Write-Host 'API local en http://localhost:5213 (Swagger en /swagger). Ctrl+C para detener.' -ForegroundColor Cyan

dotnet run --project (Join-Path $PSScriptRoot '..\Src\CORSYNC.Api\CORSYNC.Api.csproj') --no-build -c Debug
