# Genera clips de prueba en samples/generated/ a partir de un vídeo de ejemplo.
# Cubre códecs que no están en los ejemplos originales: HEVC, AV1, VP9, DTS, E-AC3, TrueHD, FLAC, Opus,
# además de MKV con varias pistas de audio, subtítulos y capítulos.
# Uso: powershell -File scripts\generate-samples.ps1 [-Source <vídeo>] [-Seconds 60]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\samples\ejemplo mkv - surfing.mkv'),
    [int]$Seconds = 60
)

$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot '..\samples\generated'
New-Item -ItemType Directory -Force $out | Out-Null

$in = @('-hide_banner', '-loglevel', 'error', '-y', '-ss', '30', '-t', "$Seconds", '-i', $Source)
$surround = @('-af', 'pan=5.1|FL=FL|FR=FR|FC=0.5*FL+0.5*FR|LFE=0.3*FL+0.3*FR|BL=0.7*FL|BR=0.7*FR')

function Make([string]$name, [string[]]$opts) {
    Write-Host "-> $name"
    & ffmpeg @in @opts (Join-Path $out $name)
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg falló generando $name" }
}

# HEVC 10-bit + DTS 5.1 en MKV (típico de descargas)
Make 'hevc-10bit_dts-5.1.mkv' (@('-c:v', 'libx265', '-preset', 'fast', '-crf', '24', '-pix_fmt', 'yuv420p10le', '-x265-params', 'log-level=error') + $surround + @('-c:a', 'dca', '-strict', '-2', '-b:a', '768k'))

# HEVC en MP4 (tag hvc1) + AC3 5.1
Make 'hevc_ac3-5.1.mp4' (@('-c:v', 'hevc_nvenc', '-preset', 'p5', '-cq', '26', '-tag:v', 'hvc1') + $surround + @('-c:a', 'ac3', '-b:a', '448k'))

# HEVC 4K 10-bit (escalado) + E-AC3, para probar rendimiento de decodificación
Make 'hevc-4k-10bit_eac3.mkv' @('-vf', 'scale=3840:2160:flags=lanczos', '-c:v', 'hevc_nvenc', '-preset', 'p5', '-cq', '26', '-profile:v', 'main10', '-pix_fmt', 'p010le', '-c:a', 'eac3', '-b:a', '256k')

# AV1 10-bit + Opus en MKV
Make 'av1-10bit_opus.mkv' @('-c:v', 'libsvtav1', '-preset', '8', '-crf', '32', '-pix_fmt', 'yuv420p10le', '-c:a', 'libopus', '-b:a', '128k')

# AV1 en MP4 + AAC
Make 'av1_aac.mp4' @('-c:v', 'av1_nvenc', '-preset', 'p5', '-cq', '30', '-c:a', 'aac', '-b:a', '160k')

# H.264 1080p a 60 fps con keyframe cada 250 frames, como una grabación de partida con OBS
Make 'h264-1080p60-gop250_aac.mp4' @('-vf', 'scale=1920:1080:flags=lanczos,fps=60', '-c:v', 'h264_nvenc', '-preset', 'p5', '-cq', '23', '-g', '250', '-c:a', 'aac', '-b:a', '160k')

# VP9 + Opus en WebM
Make 'vp9_opus.webm' @('-c:v', 'libvpx-vp9', '-deadline', 'realtime', '-cpu-used', '8', '-crf', '33', '-b:v', '0', '-row-mt', '1', '-c:a', 'libopus', '-b:a', '128k')

# H.264 + TrueHD 5.1 en MKV
Make 'h264_truehd-5.1.mkv' (@('-c:v', 'copy') + $surround + @('-c:a', 'truehd', '-strict', '-2'))

# MKV con 3 pistas de audio (AAC/AC3/FLAC), 2 subtítulos y capítulos cada 15 s
$srt = Join-Path $out 'tmp-subs.srt'
$meta = Join-Path $out 'tmp-chapters.txt'
$subs = for ($i = 0; $i -lt $Seconds / 5; $i++) {
    $a = [TimeSpan]::FromSeconds($i * 5); $b = [TimeSpan]::FromSeconds($i * 5 + 4)
    "$($i + 1)`n{0:hh\:mm\:ss\,fff} --> {1:hh\:mm\:ss\,fff}`nSubtítulo de prueba nº $($i + 1)`n" -f $a, $b
}
Set-Content -Encoding utf8 $srt ($subs -join "`n")
$chapters = @(';FFMETADATA1')
for ($s = 0; $s -lt $Seconds; $s += 15) {
    $chapters += '[CHAPTER]', 'TIMEBASE=1/1000', "START=$($s * 1000)", "END=$([Math]::Min($s + 15, $Seconds) * 1000)", "title=Capítulo $($s / 15 + 1)"
}
Set-Content -Encoding utf8 $meta $chapters
Write-Host '-> multi-audio_subs_chapters.mkv'
& ffmpeg -hide_banner -loglevel error -y -ss 30 -t $Seconds -i $Source -i $srt -i $meta `
    -map 0:v -map 0:a -map 0:a -map 0:a -map 1 -map 1 -map_metadata 2 -map_chapters 2 `
    -c:v copy -c:a:0 aac -b:a:0 160k -c:a:1 ac3 -b:a:1 384k -c:a:2 flac -c:s srt `
    -metadata:s:a:0 language=spa -metadata:s:a:0 title='Español AAC' `
    -metadata:s:a:1 language=eng -metadata:s:a:1 title='English AC3' `
    -metadata:s:a:2 language=jpn -metadata:s:a:2 title='日本語 FLAC' `
    -metadata:s:s:0 language=spa -metadata:s:s:0 title='Español' `
    -metadata:s:s:1 language=eng -metadata:s:s:1 title='English' `
    (Join-Path $out 'multi-audio_subs_chapters.mkv')
if ($LASTEXITCODE -ne 0) { throw 'ffmpeg falló generando multi-audio_subs_chapters.mkv' }
Remove-Item $srt, $meta

Write-Host "Listo: $out"
