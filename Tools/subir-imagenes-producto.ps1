<#
.SYNOPSIS
    Sube en bloque las imágenes de un producto a la galería de CORSYNC.

.DESCRIPTION
    Recorre una carpeta, ordena los archivos por nombre y los sube uno a uno al
    endpoint de galería. El orden alfabético determina el orden del carrusel, así
    que conviene numerarlos: 01-..., 02-..., etc.

    El título de cada imagen sale del nombre del archivo, quitando el número
    inicial y la extensión y cambiando guiones por espacios.

    Compatible con Windows PowerShell 5.1 y con PowerShell 7+: el envío
    multipart se arma con HttpClient porque el parámetro -Form de
    Invoke-RestMethod no existe en 5.1.

.EXAMPLE
    powershell -File Tools\subir-imagenes-producto.ps1 -Carpeta C:\fotos\corsync

.EXAMPLE
    powershell -File Tools\subir-imagenes-producto.ps1 -Carpeta .\fotos -ProductoId 1 -Reemplazar
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

Write-Host "Se subiran $($archivos.Count) imagenes al producto $ProductoId." -ForegroundColor Cyan

Add-Type -AssemblyName System.Net.Http

# --- Autenticación ---
$login = Invoke-RestMethod -Uri "$Api/api/auth/login" -Method Post -ContentType 'application/json' `
    -Body (@{ username = $Usuario; password = $Password } | ConvertTo-Json)
$token = $login.token
Write-Host "Sesion iniciada como $Usuario." -ForegroundColor DarkGray

# --- Limpieza opcional ---
if ($Reemplazar) {
    $cabeceras = @{ Authorization = "Bearer $token" }
    $respuesta = Invoke-RestMethod -Uri "$Api/api/producto/$ProductoId/imagenes" -Headers $cabeceras

    # En PowerShell 5.1 envolver el resultado de Invoke-RestMethod con @() anida
    # el arreglo dentro de otro en lugar de normalizarlo, y al recorrerlo se
    # obtiene la coleccion entera en vez de cada elemento. Por eso se comprueba
    # el tipo a mano.
    $existentes = if ($null -eq $respuesta) { @() }
                  elseif ($respuesta -is [System.Array]) { $respuesta }
                  else { , $respuesta }

    foreach ($img in $existentes) {
        Invoke-RestMethod -Uri "$Api/api/producto/imagenes/$($img.id)" -Method Delete -Headers $cabeceras | Out-Null
    }
    if ($existentes.Count -gt 0) {
        Write-Host "Se eliminaron $($existentes.Count) imagenes anteriores." -ForegroundColor DarkYellow
    }
}

# --- Subida ---
$cliente = New-Object System.Net.Http.HttpClient
$cliente.DefaultRequestHeaders.Authorization =
    New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $token)

$subidas = 0
try {
    foreach ($archivo in $archivos) {
        # "01-tu-aura-de-hoy.png" -> "Tu aura de hoy"
        $titulo = [IO.Path]::GetFileNameWithoutExtension($archivo.Name) -replace '^\d+[-_\s]*', ''
        $titulo = ($titulo -replace '[-_]', ' ').Trim()
        if ($titulo) {
            $titulo = $titulo.Substring(0, 1).ToUpper() + $titulo.Substring(1)
        }

        $contenido = $null
        $bytes = $null
        try {
            $contenido = New-Object System.Net.Http.MultipartFormDataContent

            $bytes = [IO.File]::ReadAllBytes($archivo.FullName)
            $archivoContenido = New-Object System.Net.Http.ByteArrayContent(, $bytes)

            $tipo = switch ($archivo.Extension.ToLower()) {
                '.png'  { 'image/png' }
                '.gif'  { 'image/gif' }
                '.webp' { 'image/webp' }
                default { 'image/jpeg' }
            }
            $archivoContenido.Headers.ContentType =
                [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse($tipo)

            # El nombre del campo debe coincidir con el parámetro del controlador.
            $contenido.Add($archivoContenido, 'archivo', $archivo.Name)
            $contenido.Add((New-Object System.Net.Http.StringContent($titulo)), 'titulo')

            $respuesta = $cliente.PostAsync("$Api/api/producto/$ProductoId/imagenes", $contenido).Result
            $cuerpo = $respuesta.Content.ReadAsStringAsync().Result

            if ($respuesta.IsSuccessStatusCode) {
                $subidas++
                $url = ($cuerpo | ConvertFrom-Json).url
                Write-Host ("  [{0}/{1}] {2}  ->  {3}" -f $subidas, $archivos.Count, $archivo.Name, $url) -ForegroundColor Green
            }
            else {
                Write-Host ("  ERROR con {0}: {1} {2}" -f $archivo.Name, [int]$respuesta.StatusCode, $cuerpo) -ForegroundColor Red
            }
        }
        finally {
            if ($contenido) { $contenido.Dispose() }
        }
    }
}
finally {
    $cliente.Dispose()
}

Write-Host ""
Write-Host "Listo: $subidas de $($archivos.Count) imagenes subidas." -ForegroundColor Cyan
Write-Host "Revisalas en el panel: /admin/producto (pestana Galeria)." -ForegroundColor DarkGray
Write-Host "Y en la pagina publica: /producto" -ForegroundColor DarkGray
