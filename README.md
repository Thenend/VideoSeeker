# VideoSeeker

Reproductor ligero de vídeos locales para Windows: arranque rápido, soporte para los formatos habituales (MKV, MP4, AVI; H.264, HEVC, AV1; AC3, DTS…) y sistemas ágiles de bookmarks y scrubbing para navegar e inspeccionar cualquier parte del vídeo.

La interfaz está en español, inglés, francés, alemán, italiano y portugués de Brasil: por defecto, en el idioma de Windows; se cambia en Ajustes → General → Idioma.

Es gratis y de código abierto (GPL v3). Si te resulta útil, puedes apoyar su desarrollo con el botón **Apoyar** que hay abajo en Ajustes o con el botón **Sponsor** de esta página.

> *English:* VideoSeeker is a lightweight, free and open source (GPL v3) video player for Windows, built for frame-accurate inspection: fast scrubbing, bookmarks and regions, zoomed ranges and loops. The interface is available in English, Spanish, French, German, Italian and Brazilian Portuguese: it follows the Windows language by default and can be changed in Settings → General → Language. If you find it useful, you can support it with the **Support** button at the bottom of Settings or the **Sponsor** button on this page.

## Descargar

Descarga el zip de la [última versión](https://github.com/Thenend/VideoSeeker/releases/latest), descomprímelo y abre `VideoSeeker.exe`. No se instala ni pide permisos de administrador; funciona en Windows 10/11 de 64 bits. Si Windows avisa al abrirlo («Windows protegió su PC»), pulsa «Más información» > «Ejecutar de todas formas».

*English:* download the zip from the [latest release](https://github.com/Thenend/VideoSeeker/releases/latest), unzip it and run `VideoSeeker.exe` (Windows 10/11 x64, no installation needed).

## Compilar y ejecutar

Requisitos: Windows 10/11 x64 y .NET SDK 10.

```powershell
pwsh scripts/fetch-deps.ps1                                  # descarga libmpv en deps/
dotnet publish src/VideoSeeker.App -c Release -o out/app     # compila con ReadyToRun
out/app/VideoSeeker.exe "ruta\al\video.mkv"
```

Los datos (ajustes, posición de cada vídeo) se guardan en `out/app/data/`; el botón de carpeta de la app la abre.

## Versión portable

```powershell
pwsh scripts/make-portable.ps1     # out/dist/VideoSeeker-<fecha>-<commit>-win-x64.zip
```

Zip autocontenido (incluye .NET, libmpv y FFmpeg) para otro PC con Windows 10/11 x64: se descomprime y se ejecuta, sin instalar ni permisos de administrador. Instrucciones para el usuario en `scripts/portable/LEEME.txt` (van dentro del zip).

## Microsoft Store

```powershell
pwsh scripts/make-msix.ps1         # out/msix/VideoSeeker-<versión>-x64.msix
```

Paquete MSIX para la Store (la Store lo firma). Cómo publicarlo y qué cambia en la app instalada así: [docs/TIENDA.md](docs/TIENDA.md).

## Atajos (fase 1)

| Acción | Tecla / ratón |
| --- | --- |
| Reproducir / pausar | Espacio, K, clic en el vídeo |
| Frame siguiente / anterior | ↑ / ↓, o arrastrar a derecha / izquierda sobre la imagen |
| Saltar 5 s (Shift 1 s, Alt 30 s) | ← / →, rueda sobre la barra principal (con Shift / Alt, igual) |
| Pantalla completa | F, Enter, doble clic en el vídeo (Esc para salir) |
| Silenciar | M |
| Cambiar pista de audio / subtítulos | A / S |
| Abrir vídeo | Ctrl + O, arrastrar a la ventana |

Plan completo en [docs/PLAN.md](docs/PLAN.md).

## Licencia

VideoSeeker es software libre: puedes usarlo, copiarlo, modificarlo y redistribuirlo bajo los términos de la [GNU GPL v3](LICENSE) o posterior. Usa [mpv](https://mpv.io) y [FFmpeg](https://ffmpeg.org), que también son GPL; el detalle de los componentes de terceros está en [scripts/portable/LICENCIAS.txt](scripts/portable/LICENCIAS.txt).

## Colaborar y forks

Las mejoras son bienvenidas: abre un issue o un pull request. Si haces un fork, lee [CONTRIBUTING.md](CONTRIBUTING.md): te pedimos un pequeño favor sobre el botón **Apoyar**.
