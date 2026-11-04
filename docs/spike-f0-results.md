# Fase 0: mediciones de mpv

Generado por `tools/VideoSeeker.Bench` el 2026-10-31 17:56. WPF + libmpv en HwndHost, `vo=gpu-next`, `gpu-api=d3d11`, `hwdec=auto-safe`, caché de demuxer de 512 MiB adelante y atrás.

## Conclusiones

- **Decisión de la fase 0: el motor de inspección entra en la fase 2.** Con mpv solo, cada paso atrás tarda 137–178 ms en el clip tipo grabación de partida (H.264 1080p60, keyframe cada 250 frames) y unos 420 ms en HEVC 4K 10 bits. Ambos superan el umbral de 100 ms; mantener ↓ iría a 2–7 frames por segundo.
- Con GOP cortos (1–3 s, 720p o menos) mpv ya da 7–25 ms por paso atrás, así que para vídeos ligeros la fase 1 será usable sin el motor.
- El paso adelante con `frame-step 1 seek` cuesta lo mismo que `frame-back-step`: ambos decodifican desde el keyframe anterior. No es una alternativa.
- El paso adelante normal de mpv tarda lo que dura un frame (unos 40 ms a 24 fps) en la primera ejecución, con la ventana visible. En las ejecuciones posteriores dio timeout casi siempre; sospecho que la ventana no estaba en primer plano y mpv no presentaba frames, pero no está comprobado. Queda pendiente para la fase 1.
- Arranque: la ventana aparece en 756 ms y mpv inicializa en 15 ms con la DLL ya en caché de disco (474 ms en frío). El primer vídeo tarda unos 460 ms en mostrar su primer frame (inicialización de D3D11 y audio); los siguientes, 23–110 ms. Para bajar de 1 s en frío, la fase 1 debe inicializar mpv en paralelo con la ventana.
- Decodificación por hardware (d3d11va) activa en todo menos MPEG-4 ASP y WMV antiguos, que van por CPU sin problema.

## Datos

- Ventana visible desde el arranque del proceso: 756 ms
- Inicialización de mpv: 15 ms

Tiempos en ms. Paso atrás y adelante: mediana / máximo de 8 pasos seguidos empezando al final de un GOP (peor caso para ir atrás).

| Archivo | Vídeo | GOP (s) | hwdec | Primer frame | Seek al final del GOP | Paso atrás | Paso adelante (ver conclusiones) | Paso adelante (modo seek) | Seek exacto aleatorio (med / máx) | Seek a keyframe (med) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ejemplo mkv - surfing.mkv | h264 1280x720 yuv420p 23,976 fps | 2,1 | d3d11va | 462 | 12 | 13 / 15 | timeout / timeout | 14 / 16 | 9 / 10 | 7 |
| ejemplo pelicula - Gangs of New York.avi | mpeg4 720x306 yuv420p 23,976 fps | 2,5 | no | 325 | 12 | 7 / 16 | timeout / timeout | 7 / 7 | 11 / 21 | 3 |
| ejemplo video corto - zelda.mp4 | h264 716x880 yuv420p 25 fps | 3,0 | d3d11va | 31 | 23 | 15 / 21 | 40 / 41 | 34 / 41 | 21 / 28 | 4 |
| ejemplo video largo - Metroid Prime 3.mp4 | h264 1280x720 yuv420p 30 fps | 6,0 | d3d11va | 44 | 13 | 46 / 48 | 33 / 34 | 43 / 46 | 6 / 25 | 4 |
| ejemplo video viejo - anuncio.wmv | msmpeg4v3 384x288 yuv420p 25 fps | 1,0 | no | 54 | 11 | 11 / 13 | timeout / timeout | 7 / 10 | 6 / 6 | 6 |
| av1_aac.mp4 | av1 1280x720 yuv420p 23,976 fps | 10,4 | d3d11va | 58 | 97 | 86 / 106 | timeout / timeout | 95 / 110 | 9 / 72 | 7 |
| av1-10bit_opus.mkv | av1 1280x720 yuv420p10le 23,976 fps | 6,7 | d3d11va | 52 | 56 | 57 / 60 | timeout / timeout | 70 / 78 | 40 / 44 | 6 |
| h264_truehd-5.1.mkv | h264 1280x720 yuv420p 23,976 fps | 2,9 | d3d11va | 62 | 46 | 20 / 22 | timeout / timeout | 27 / 28 | 15 / 38 | 6 |
| h264-1080p60-gop250_aac.mp4 | h264 1920x1080 yuv420p 60 fps | 4,2 | d3d11va | 102 | 108 | 139 / 178 | timeout / timeout | 149 / 190 | 31 / 74 | 11 |
| hevc_ac3-5.1.mp4 | hevc 1280x720 yuv420p 23,976 fps | 10,4 | d3d11va | 74 | 33 | 68 / 70 | timeout / timeout | 83 / 95 | 6 / 35 | 5 |
| hevc-10bit_dts-5.1.mkv | hevc 1280x720 yuv420p10le 23,976 fps | 1,5 | d3d11va | 32 | 15 | 11 / 12 | timeout / timeout | 14 / 16 | 13 / 16 | 7 |
| hevc-4k-10bit_eac3.mkv | hevc 3840x2160 yuv420p10le 23,976 fps | 10,4 | d3d11va | 107 | 248 | 417 / 432 | timeout / timeout | 421 / 554 | 46 / 189 | 26 |
| multi-audio_subs_chapters.mkv | h264 1280x720 yuv420p 23,976 fps | 2,9 | d3d11va | 43 | 11 | 18 / 37 | timeout / timeout | 26 / 27 | 11 / 34 | 8 |
| vp9_opus.webm | vp9 1280x720 yuv420p 23,976 fps | 0,0 | d3d11va | 23 | 18 | 19 / 24 | timeout / timeout | 20 / 22 | 14 / 40 | 7 |

Pasos atrás individuales (ms):

- ejemplo mkv - surfing.mkv: 13, 14, 12, 12, 15, 13, 12, 9
- ejemplo pelicula - Gangs of New York.avi: 8, 7, 7, 16, 7, 7, 6, 6
- ejemplo video corto - zelda.mp4: 21, 15, 15, 20, 13, 15, 14, 16
- ejemplo video largo - Metroid Prime 3.mp4: 46, 46, 47, 46, 46, 48, 46, 44
- ejemplo video viejo - anuncio.wmv: 13, 11, 11, 13, 10, 9, 10, 10
- av1_aac.mp4: 106, 101, 93, 86, 80, 77, 79, 72
- av1-10bit_opus.mkv: 59, 57, 59, 60, 57, 56, 56, 54
- h264_truehd-5.1.mkv: 22, 20, 22, 22, 20, 19, 19, 19
- h264-1080p60-gop250_aac.mp4: 178, 148, 137, 147, 139, 137, 134, 135
- hevc_ac3-5.1.mp4: 70, 68, 67, 68, 65, 69, 66, 65
- hevc-10bit_dts-5.1.mkv: 11, 12, 10, 11, 10, 11, 8, 10
- hevc-4k-10bit_eac3.mkv: 432, 420, 419, 417, 416, 413, 412, 411
- multi-audio_subs_chapters.mkv: 19, 37, 17, 15, 18, 18, 16, 18
- vp9_opus.webm: 19, 19, 20, 18, 24, 17, 18, 15
