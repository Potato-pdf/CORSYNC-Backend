<#
.SYNOPSIS
    Sube en bloque las imágenes de un producto a la galería de CORSYNC.

.DESCRIPTION
    Recorre una carpeta, ordena los archivos por nombre y los sube uno a uno al
    endpoint de galería. El orden alfabético del nombre determina el orden del
    carrusel, así que conviene numerarlos: 01-..., 02-..., etc.

    El título de cada imagen se toma del nombre del archivo, quitando el número
    inicial y la extensión, y reemplazando guiones por espacios.

.EXAMPLE
    pwsh Tools/subir-imagenes-producto.ps1 -Carpeta C:\fotos\corsync

.EXAMPLE
    pwsh Tools/subir-imagenes-producto.ps1 -Carpeta .\fotos -ProductoId 1 -Api http://corsync.runasp.net
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Carpeta,

    [int]$ProductoId = 1,

    [string]$Api = 'http://localhost:5213',

    [string]$Usuario = 'admin',

    [string]$Password = 'admin123',

    # Borra las imágenes que ya tenga el producto antes de subir las nuevas.
    [switch]$Reemplazar
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Carpeta)) {
    throw "No existe la carpeta '$Carpeta'."
}

$extensiones = @('.jpg', '.jpeg', '.png', '.webp', '.gif')
$archivos = Get-ChildItem -Path $Carpeta -File |
    Where-Object { $extensiones -contains $_.Extension.ToLower() } |
    Sort-Object Name

if ($archivos.Count -eq 0) {
    throw "No se encontró ninguna imagen (jpg, png, webp, gif) en '$Carpeta'."
}

Write-Host "Se subirán $($archivos.Count) imágenes al producto $ProductoId." -ForegroundColor Cyan

# --- Autenticación ---
$login = Invoke-RestMethod -Uri "$Api/api/auth/login" -Method Post -ContentType 'application/json' `
    -Body (@{ username = $Usuario; password = $Password } | ConvertTo-Json)
$cabeceras = @{ Authorization = "Bearer $($login.token)" }
Write-Host "Sesión iniciada como $Usuario." -ForegroundColor DarkGray

# --- Limpieza opcional ---
if ($Reemplazar) {
    $existentes = Invoke-RestMethod -Uri "$Api/api/producto/$ProductoId/imagenes" -Headers $cabeceras
    foreach ($img in $existentes) {
        Invoke-RestMethod -Uri "$Api/api/producto/imagenes/$($img.id)" -Method Delete -Headers $cabeceras | Out-Null
    }
    if ($existentes.Count -gt 0) {
        Write-Host "Se eliminaron $($existentes.Count) imágenes anteriores." -ForegroundColor DarkYellow
    }
}

# --- Subida ---
$subidas = 0
foreach ($archivo in $archivos) {
    # "01-tu-aura-de-hoy.png" -> "tu aura de hoy"
    $titulo = [IO.Path]::GetFileNameWithoutExtension($archivo.Name) -replace '^\d+[-_\s]*', ''
    $titulo = ($titulo -replace '[-_]', ' ').Trim()
    if ($titulo) {
        $titulo = $titulo.Substring(0, 1).ToUpper() + $titulo.Substring(1)
    }

    try {
        $formulario = @{
            archivo = Get-Item $archivo.FullName
            titulo  = $titulo
        }
        $respuesta = Invoke-RestMethod -Uri "$Api/api/producto/$ProductoId/imagenes" -Method Post `
            -Headers $cabeceras -Form $formulario

        $subidas++
        Write-Host ("  [{0}/{1}] {2}  ->  {3}" -f $subidas, $archivos.Count, $archivo.Name, $respuesta.url) -ForegroundColor Green
    }
    catch {
        Write-Host ("  ERROR con {0}: {1}" -f $archivo.Name, $_.Exception.Message) -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Listo: $subidas de $($archivos.Count) imágenes subidas." -ForegroundColor Cyan
Write-Host "Revísalas en el panel: /admin/producto (pestaña Galería)." -ForegroundColor DarkGray
Write-Host "Y en la página pública: /producto" -ForegroundColor DarkGray
