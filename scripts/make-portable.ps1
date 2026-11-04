# Genera un zip portable de VideoSeeker para llevar a otro PC con Windows 10/11 x64:
# se descomprime en cualquier carpeta y se ejecuta, sin instalar nada ni permisos de administrador.
# Lleva dentro .NET (self-contained), libmpv y FFmpeg; no depende del entorno de desarrollo.
#
# Uso: pwsh scripts/make-portable.ps1            -> out\dist\VideoSeeker-<fecha>-win-x64.zip
#      pwsh scripts/make-portable.ps1 -NoZip     -> solo la carpeta out\dist\VideoSeeker
param([switch]$NoZip)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'out\dist'
$stage = Join-Path $dist 'VideoSeeker'

foreach ($dep in 'deps\mpv\libmpv-2.dll', 'deps\ffmpeg\ffmpeg.exe') {
    if (-not (Test-Path (Join-Path $root $dep))) { throw "Falta ${dep}: ejecuta antes scripts/fetch-deps.ps1" }
}

$commit = (git -C $root rev-parse --short HEAD).Trim()
$dirty = if (git -C $root status --porcelain) { '-mod' } else { '' }
$version = (Get-Date -Format 'yyyy.MM.dd') + "-$commit$dirty"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $dist | Out-Null

# Self-contained: el runtime de .NET va en la carpeta (instalar .NET pediría administrador).
# Sin .pdb y solo los recursos de WPF en español e inglés.
dotnet publish (Join-Path $root 'src\VideoSeeker.App') -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -p:DebugType=none -p:DebugSymbols=false `
    -p:SatelliteResourceLanguages='es%3Ben' -p:InformationalVersion=$version `
    -o $stage
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ha fallado' }

foreach ($file in 'VideoSeeker.exe', 'libmpv-2.dll', 'ffmpeg\ffmpeg.exe', 'ffmpeg\avcodec-63.dll', 'coreclr.dll', 'PresentationFramework.dll') {
    if (-not (Test-Path (Join-Path $stage $file))) { throw "Falta $file en la carpeta publicada" }
}
# Por si alguna vez se cuela: los datos (ajustes, bookmarks, caché) se crean en el primer arranque.
foreach ($leftover in 'data', 'datalocation.json') {
    $path = Join-Path $stage $leftover
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}

Copy-Item (Join-Path $PSScriptRoot 'portable\LEEME.txt') $stage
Copy-Item (Join-Path $PSScriptRoot 'portable\LICENCIAS.txt') $stage
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE.txt')
Set-Content (Join-Path $stage 'VERSION.txt') $version -Encoding utf8

$size = (Get-ChildItem $stage -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Carpeta lista: {0} ({1:N0} MB)" -f $stage, $size)

if (-not $NoZip) {
    $zip = Join-Path $dist "VideoSeeker-$version-win-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    # Zip con la carpeta VideoSeeker\ dentro, para que al descomprimir no se desparramen los archivos.
    & "$env:SystemRoot\System32\tar.exe" -a -c -f $zip -C $dist VideoSeeker
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el zip' }
    Write-Host ("Zip listo: {0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
}
