# Compila VideoSeeker en out\selftest y lanza autopruebas, cada una con datos de prueba propios (out\selftest\data,
# nunca los de Mario) y aunque la app de Mario esté abierta. Al final, un resumen: OK o los fallos de cada una.
#
# Uso: pwsh scripts/selftest.ps1                    -> la rápida (rapida, unos segundos): textos, tooltips, menús, ajustes
#      pwsh scripts/selftest.ps1 bookmarks zona     -> las de la parte que se ha tocado
#      pwsh scripts/selftest.ps1 -Full              -> todas (varios minutos)
#      -Video <ruta>   otro vídeo (por defecto samples\generated\h264-1080p60-gop250_aac.mp4)
#      -NoBuild        sin compilar (la copia de out\selftest tal cual)
#      -Idioma <código> la interfaz en otro idioma (en, fr, de, it, pt-BR; por defecto es). Solo tiene sentido con la
#                      rápida, para ver si los textos caben: las demás buscan textos en español.
#      Registro y capturas de cada una: out\selftest\data\resultados\<prueba>\
#      -TimeoutSec     tiempo máximo de cada una (por defecto 300 s; pasado, se cierra y cuenta como fallo)
#
# Cuándo usar cada una: CLAUDE.md, apartado «Autopruebas».
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)] [string[]]$Mode,
    [switch]$Full,
    [string]$Video,
    [switch]$NoBuild,
    [string]$Idioma,
    [int]$TimeoutSec = 300
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'out\selftest'
$data = Join-Path $app 'data'
$exe = Join-Path $app 'VideoSeeker.exe'

# Todas, en un orden en que las más rápidas avisan antes. «arranque» (solo mide tiempos) va aparte.
$all = 'rapida', 'inicio', '1', 'resume', 'drag', 'reverse', 'loop', 'longdrag', 'zoom', 'settings', 'bookmarks', 'zona',
    'saltar', 'vistazo', 'historial', 'clips'
$known = $all + 'arranque'
$modes = if ($Full) { $all } elseif ($Mode) { $Mode } else { @('rapida') }
foreach ($m in $modes) {
    if ($m -notin $known) { throw "No hay autoprueba «$m». Las que hay: $($known -join ', ')" }
}

if (-not $Video) {
    $Video = Join-Path $root 'samples\generated\h264-1080p60-gop250_aac.mp4'
    if (-not (Test-Path $Video)) {
        $Video = Get-ChildItem (Join-Path $root 'samples') -Recurse -File -Include *.mp4, *.mkv -ErrorAction SilentlyContinue |
            Sort-Object Length | Select-Object -First 1 -ExpandProperty FullName
    }
}
if (-not $Video -or -not (Test-Path $Video)) { throw 'No hay vídeo de prueba: pasa -Video o crea samples\generated con scripts/generate-samples.ps1' }
$Video = (Resolve-Path $Video).Path

if (-not $NoBuild) {
    # Sin ReadyToRun: compila bastante antes y para las pruebas da igual.
    $build = [Diagnostics.Stopwatch]::StartNew()
    dotnet publish (Join-Path $root 'src\VideoSeeker.App') -c Release -o $app -p:PublishReadyToRun=false -nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ha fallado' }
    Write-Host ("Compilado en {0:0} s" -f $build.Elapsed.TotalSeconds)
}
if (-not (Test-Path $exe)) { throw "No está $exe (quita -NoBuild)" }

# Los datos de prueba siempre en out\selftest\data, aunque Mario haya elegido otra carpeta (eso se apunta en
# %LOCALAPPDATA%, y la copia lo leería).
New-Item -ItemType Directory -Force $data | Out-Null
@{ folder = $data } | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $app 'datalocation.json')

$results = @()
foreach ($m in $modes) {
    Get-ChildItem $data -Filter 'selftest*' -File -ErrorAction SilentlyContinue | Remove-Item -Force
    $env:VIDEOSEEKER_SELFTEST = $m
    if ($Idioma) { $env:VIDEOSEEKER_UILANG = $Idioma }
    $arguments = if ($m -eq 'inicio') { @() } else { @("`"$Video`"") }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $process = if ($arguments) { Start-Process $exe -ArgumentList $arguments -PassThru } else { Start-Process $exe -PassThru }
    $finished = $process.WaitForExit($TimeoutSec * 1000)
    if (-not $finished) { $process.Kill(); $process.WaitForExit() }
    $seconds = $clock.Elapsed.TotalSeconds
    Remove-Item Env:VIDEOSEEKER_SELFTEST
    Remove-Item Env:VIDEOSEEKER_UILANG -ErrorAction SilentlyContinue

    # Cada una escribe selftest.log (clips e historial, el suyo): las líneas «FALLO» son los fallos.
    $logs = @(Get-ChildItem $data -Filter 'selftest*.log' -File -ErrorAction SilentlyContinue)
    $lines = @($logs | ForEach-Object { Get-Content $_.FullName -Encoding utf8 })
    $failed = @($lines | Where-Object { $_ -match '\bFALLO\b' })
    $problem = if (-not $finished) { "no terminó en $TimeoutSec s" }
        elseif (-not $logs) { "cerró sin escribir el registro (código $($process.ExitCode); ¿se ha colgado o cerrado de golpe?)" }
        elseif ($failed) { "$($failed.Count) fallos" }
    # Registro y capturas de cada una en resultados\<prueba>, para mirarlos después (la siguiente borra los suyos).
    $keep = Join-Path $data ("resultados\$m" + $(if ($Idioma) { "-$Idioma" }))
    if (Test-Path $keep) { Remove-Item $keep -Recurse -Force }
    New-Item -ItemType Directory -Force $keep | Out-Null
    Get-ChildItem $data -Filter 'selftest*' -File -ErrorAction SilentlyContinue | Copy-Item -Destination $keep
    $results += [pscustomobject]@{ Prueba = $m; Resultado = if ($problem) { $problem } else { 'OK' }; Segundos = [math]::Round($seconds) }
    if ($problem) {
        Write-Host "✗ $m ($([math]::Round($seconds)) s): $problem" -ForegroundColor Red
        $failed | ForEach-Object { Write-Host "    $_" }
    }
    else {
        Write-Host "✓ $m ($([math]::Round($seconds)) s)" -ForegroundColor Green
    }
}

Write-Host ''
$results | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "Registros y capturas de cada una en $(Join-Path $data 'resultados')\<prueba>"
if ($results | Where-Object Resultado -ne 'OK') { exit 1 }
