# Descarga las dependencias nativas con versiones fijas en deps/ (no se suben al repo).
# Uso: pwsh scripts/fetch-deps.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$deps = Join-Path $root 'deps'
New-Item -ItemType Directory -Force $deps | Out-Null

# libmpv (build de shinchiro/mpv-winbuild-cmake, x86_64 genérico)
$mpvTag = '20261021'
$mpvAsset = 'mpv-dev-x86_64-20261021-git-e471496c56.7z'
$mpvDir = Join-Path $deps 'mpv'

if (-not (Test-Path (Join-Path $mpvDir 'libmpv-2.dll'))) {
    $archive = Join-Path $deps $mpvAsset
    if (-not (Test-Path $archive)) {
        $url = "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/$mpvTag/$mpvAsset"
        Write-Host "Descargando $url"
        Invoke-WebRequest -Uri $url -OutFile $archive
    }
    New-Item -ItemType Directory -Force $mpvDir | Out-Null
    # El tar de Windows 11 (libarchive) sabe extraer .7z
    & "$env:SystemRoot\System32\tar.exe" -xf $archive -C $mpvDir
    if ($LASTEXITCODE -ne 0) { throw "No se pudo extraer $archive" }
    Remove-Item $archive
}

Write-Host "libmpv listo en $mpvDir"

# FFmpeg en DLL compartidas (build "full_build-shared" de gyan.dev), para el motor de inspección, y ffmpeg.exe
# (usa esas mismas DLL) para exportar clips. La versión debe coincidir con la del paquete FFmpeg.AutoGen de
# VideoSeeker.Inspect.
$ffVersion = '9.0.2'
$ffDir = Join-Path $deps 'ffmpeg'

if (-not (Test-Path (Join-Path $ffDir 'ffmpeg.exe'))) {
    $asset = "ffmpeg-$ffVersion-full_build-shared.zip"
    $archive = Join-Path $deps $asset
    if (-not (Test-Path $archive)) {
        $url = "https://github.com/GyanD/codexffmpeg/releases/download/$ffVersion/$asset"
        Write-Host "Descargando $url"
        Invoke-WebRequest -Uri $url -OutFile $archive
    }
    $extract = Join-Path $deps 'ffmpeg-extract'
    Expand-Archive -Path $archive -DestinationPath $extract -Force
    New-Item -ItemType Directory -Force $ffDir | Out-Null
    Get-ChildItem $extract -Recurse -Filter '*.dll' | Copy-Item -Destination $ffDir
    Get-ChildItem $extract -Recurse -Filter 'ffmpeg.exe' | Copy-Item -Destination $ffDir
    Remove-Item $extract -Recurse -Force
    Remove-Item $archive
}

Write-Host "FFmpeg listo en $ffDir"
