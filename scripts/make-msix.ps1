# Genera el paquete MSIX de VideoSeeker para subirlo a la Microsoft Store.
# Lleva dentro .NET (self-contained), libmpv y FFmpeg, igual que el portable. No hace falta Visual Studio ni el SDK de
# Windows: MakeAppx y MakePri se descargan una vez del paquete NuGet Microsoft.Windows.SDK.BuildTools.
#
# Uso: pwsh scripts/make-msix.ps1             -> out\msix\VideoSeeker-<versión>-x64.msix (sin firmar: la Store lo firma)
#      pwsh scripts/make-msix.ps1 -Instalar   -> además lo instala en este PC desde la carpeta, para probarlo
#                                                 (necesita el Modo de desarrollador de Windows)
#      pwsh scripts/make-msix.ps1 -Quitar     -> desinstala la copia de prueba
#
# La identidad del paquete (nombre, editor) está en scripts/msix/identidad.json: hay que copiarla de Partner Center
# antes de subirlo. El manifiesto es scripts/msix/AppxManifest.xml y los iconos scripts/msix/Assets
# (scripts/msix/generar-assets.py los rehace desde docs/icono).
param([switch]$Instalar, [switch]$Quitar)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$msix = Join-Path $PSScriptRoot 'msix'
$out = Join-Path $root 'out\msix'
$stage = Join-Path $out 'VideoSeeker'
$identity = Get-Content (Join-Path $msix 'identidad.json') -Raw | ConvertFrom-Json

if ($Quitar) {
    Get-AppxPackage -Name $identity.name | Remove-AppxPackage
    Write-Host "Quitado $($identity.name)"
    return
}

foreach ($dep in 'deps\mpv\libmpv-2.dll', 'deps\ffmpeg\ffmpeg.exe') {
    if (-not (Test-Path (Join-Path $root $dep))) { throw "Falta ${dep}: ejecuta antes scripts/fetch-deps.ps1" }
}

# Versión: año.mesdía.horaminuto.0 (la Store pide que la cuarta parte sea 0 y que cada envío tenga una mayor).
$now = Get-Date
$version = '{0}.{1}.{2}.0' -f $now.Year, ($now.Month * 100 + $now.Day), ($now.Hour * 100 + $now.Minute)
$commit = (git -C $root rev-parse --short HEAD).Trim()
$dirty = if (git -C $root status --porcelain) { '-mod' } else { '' }
$shown = $now.ToString('yyyy.MM.dd') + "-$commit$dirty"

# MakeAppx y MakePri, del paquete NuGet del SDK de Windows (una sola vez).
$toolsVersion = '10.0.28000.2957'
$tools = Join-Path $root "out\herramientas\sdk-buildtools-$toolsVersion"
if (-not (Test-Path $tools)) {
    New-Item -ItemType Directory -Force $tools | Out-Null
    $zip = Join-Path $tools 'paquete.zip'
    Write-Host "Descargando las herramientas de empaquetado ($toolsVersion)..."
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$toolsVersion/microsoft.windows.sdk.buildtools.$toolsVersion.nupkg" -OutFile $zip
    Expand-Archive $zip -DestinationPath $tools
    Remove-Item $zip
}
$bin = Get-ChildItem (Join-Path $tools 'bin') -Directory | Sort-Object Name | Select-Object -Last 1
$makeappx = Join-Path $bin.FullName 'x64\makeappx.exe'
$makepri = Join-Path $bin.FullName 'x64\makepri.exe'

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

# Como el portable, pero en carpeta (sin "un solo archivo"): el paquete ya va comprimido y así no se extrae nada a
# %TEMP% al arrancar.
dotnet publish (Join-Path $root 'src\VideoSeeker.App') -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -p:DebugType=none -p:DebugSymbols=false `
    -p:SatelliteResourceLanguages='es%3Ben' -p:InformationalVersion=$shown `
    -o $stage
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ha fallado' }
foreach ($file in 'VideoSeeker.exe', 'mpv\libmpv-2.dll', 'ffmpeg\ffmpeg.exe', 'ffmpeg\avcodec-63.dll') {
    if (-not (Test-Path (Join-Path $stage $file))) { throw "Falta $file en la carpeta publicada" }
}
foreach ($leftover in 'data', 'datalocation.json') {
    $path = Join-Path $stage $leftover
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}
Copy-Item (Join-Path $PSScriptRoot 'portable\LICENCIAS.txt') $stage
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE.txt')

# Manifiesto con la identidad y la versión, e iconos.
$manifest = (Get-Content (Join-Path $msix 'AppxManifest.xml') -Raw -Encoding utf8).
    Replace('$Name$', $identity.name).Replace('$Publisher$', [Security.SecurityElement]::Escape($identity.publisher)).
    Replace('$PublisherDisplayName$', [Security.SecurityElement]::Escape($identity.publisherDisplayName)).
    Replace('$Version$', $version)
[IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest, [Text.UTF8Encoding]::new($false))
Copy-Item (Join-Path $msix 'Assets') (Join-Path $stage 'Assets') -Recurse

# resources.pri: el índice con el que Windows elige el icono de cada tamaño (scale-*, targetsize-*). Se genera en una
# carpeta aparte con solo los iconos, para que no indexe todas las DLL.
$pri = Join-Path $out 'pri'
if (Test-Path $pri) { Remove-Item $pri -Recurse -Force }
New-Item -ItemType Directory -Force $pri | Out-Null
Copy-Item (Join-Path $msix 'Assets') (Join-Path $pri 'Assets') -Recurse
& $makepri createconfig /cf (Join-Path $out 'priconfig.xml') /dq es-ES /pv 10.0.0 /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri createconfig ha fallado' }
& $makepri new /pr $pri /cf (Join-Path $out 'priconfig.xml') /mn (Join-Path $stage 'AppxManifest.xml') /of (Join-Path $stage 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri new ha fallado' }
Remove-Item $pri -Recurse -Force

$package = Join-Path $out "VideoSeeker-$version-x64.msix"
Get-ChildItem $out -Filter 'VideoSeeker-*.msix' | Remove-Item -Force
& $makeappx pack /d $stage /p $package /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack ha fallado' }
Write-Host ("Paquete listo: {0} ({1:N0} MB), versión {2}, identidad {3}" -f $package, ((Get-Item $package).Length / 1MB), $version, $identity.name)

if ($Instalar) {
    # Desde la carpeta, sin firmar: Windows solo lo permite con el Modo de desarrollador.
    Add-AppxPackage -Register (Join-Path $stage 'AppxManifest.xml') -ForceApplicationShutdown
    Write-Host "Instalado para probar. Sale en Inicio como VideoSeeker; se quita con: pwsh scripts/make-msix.ps1 -Quitar"
}
