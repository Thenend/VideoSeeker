# VideoSeeker — Plan de producto y técnico

31 de octubre de 2026. Copia del documento vivo, que incluye el boceto de la ventana y el diagrama de fases: https://claude.ai/artifact/9cekRNrPgLCXcjjYaNUjPk

## Resumen

Mantenemos C# + WPF + libmpv como base, y le añadimos un motor propio de inspección (FFmpeg + caché de frames decodificados en la GPU) para que ir frame a frame hacia atrás sea instantáneo. mpv por sí solo reproduce todo muy bien, pero retroceder un frame le obliga a decodificar desde el keyframe anterior, y en nuestros clips los keyframes están a 3–11 s (el HEVC 4K, cada 10,4 s): hasta unos 250 frames por cada paso atrás.

Lo construimos en 6 fases. La fase 0 es un experimento corto que mide esa latencia con los clips de `samples/` y decide si el motor de inspección hace falta ya o puede esperar.

Decisiones tomadas:

- [x] Selección de tramo: arrastrar con botón derecho; clic derecho sin arrastrar abre el menú.
- [x] ← → saltan 5 s y ↑ ↓ van frame a frame. El volumen se maneja con un control en pantalla con botón de silencio.
- [x] Caché de frames: 3 GB de VRAM por defecto, ampliable en ajustes.
- [x] Datos en archivos JSON en una carpeta `data\` junto al exe, con un botón para abrirla.
- [x] Descargas de libmpv y de las DLL de FFmpeg aprobadas.

## Evaluación del stack

WPF + libmpv es la mejor base para reproducir; lo que no resuelve es el paso atrás instantáneo, y eso lo cubrimos con un motor de inspección propio en vez de cambiar de stack. mpv ya cubre formatos, audio AC3/DTS/TrueHD, subtítulos, HDR y decodificación por hardware (NVDEC/D3D11VA).

| Opción | A favor | En contra | Veredicto |
| --- | --- | --- | --- |
| WPF + libmpv + motor de inspección FFmpeg | mpv reproduce todo; el motor propio da frames exactos al instante | Dos rutas de render que deben verse igual; más piezas | Recomendado |
| WPF + libmpv solo | Lo más simple; `frame-back-step` y `hr-seek` dan precisión de frame | Cada paso atrás decodifica desde el keyframe: lento en GOP largos y 4K | Punto de partida; la fase 0 mide si basta |
| Reproductor propio sobre FFmpeg + Direct3D 11 | Control total | Sincronía A/V, audio, subtítulos y HDR desde cero | Descartado |
| WinUI 3 + libmpv | UI más moderna | mpv no renderiza en un swapchain ajeno; Windows App SDK más frágil | Sin ventaja real |
| C++/Qt o Rust | Arranque más rápido | Sin MSVC ni Rust instalados; desarrollo más lento | Descartado |

- El vídeo de mpv va en un `HwndHost` (ventana hija con render `gpu-next` en D3D11). Las barras van debajo del vídeo, así que el problema de "airspace" solo afecta a los controles en pantalla completa.
- WPF no admite Native AOT; con ReadyToRun la ventana aparece en unos 300–500 ms. Objetivo: primer frame en menos de 1 s.
- Se compila con `dotnet` y VS Code; no hace falta Visual Studio.

## Arquitectura

| Proyecto | Responsabilidad |
| --- | --- |
| `VideoSeeker.App` | WPF: ventana, barra principal, barra de zoom, paneles de bookmarks y favoritos, atajos |
| `VideoSeeker.Playback` | Envoltorio de libmpv por P/Invoke: comandos, propiedades observadas, hilo de eventos |
| `VideoSeeker.Inspect` | Índice de frames, decodificador FFmpeg con NVDEC, caché de frames en VRAM, render D3D11 de frames cacheados |
| `VideoSeeker.Library` | Archivos JSON de datos, huella de archivos, servicio de miniaturas y portadas |
| `VideoSeeker.Core` | Modelos, timecodes, ajustes, sin dependencias |

1. Reproduciendo, la imagen la pinta mpv.
2. Al pausar, el motor de inspección decodifica ±5 s alrededor del frame actual y los guarda como texturas en la GPU.
3. Al ir frame a frame, si el frame está en caché se muestra al instante desde nuestro render; si no, se pide a mpv con un seek exacto.
4. Al reanudar, mpv continúa desde el frame mostrado y la caché se libera poco a poco.

Dependencias: libmpv-2.dll, DLL compartidas de FFmpeg con bindings tipo FFmpeg.AutoGen, Vortice.Windows para Direct3D 11 y System.Text.Json.

## Scrubbing frame a frame y precarga

Objetivo: paso de un frame en menos de 30 ms dentro de la zona precargada y en menos de 300 ms fuera.

- **Índice de frames:** un hilo lee los paquetes sin decodificar y anota el instante de cada frame y los keyframes. Se guarda en disco.
- **Precarga en pausa:** ±5 s por orden de cercanía (GOP actual, anterior, siguiente…), con dos decodificadores. Si hay región de zoom y cabe, se precarga entera. La ventana se desplaza hacia donde te mueves.
- **Memoria:** 3 GB de VRAM por defecto, ampliable. 720p ≈ ±45 s, 1080p ≈ ±20 s, 4K 10 bits ≈ ±2,5 s a 24 fps.
- **Arrastrando la barra principal:** seeks a keyframe y seek exacto al soltar. En la barra de zoom todo es exacto.
- **Moverse:** ↑ ↓ un frame (mantener para repetir), ← → 5 s, J/K/L, rueda sobre vídeo o barra de zoom un frame por muesca.

## Barra de zoom

Aparece bajo la barra principal y amplía un tramo con precisión de frame; el tramo se ve resaltado en la barra principal con dos asas.

- Se abre arrastrando con botón derecho, con "Ampliar sección" / Z (±5 s), con Ctrl + rueda, o seleccionando un bookmark de región (+10 % de margen).
- Clic o arrastre va al frame exacto; marcas por frame y timecodes; más adelante, tira de miniaturas.
- Rueda: un frame. Ctrl + rueda: zoom. Shift + rueda: desplaza el tramo. Arrastrar la banda o sus asas en la barra principal.
- Bucle en la región (R). Esc cierra.

## Bookmarks

Nombre, inicio y fin opcional (región). Miniatura de 320 px generada en segundo plano.

- Añadir: clic derecho → "Añadir bookmark aquí" con campo de nombre en línea; B en el frame actual; Shift + B o "Bookmark de esta región".
- En las barras: marcas para frames, bandas de color para regiones; tooltip con miniatura; [ y ] saltan.
- Editar: asas de inicio y fin en ambas barras; arrastrar el fin convierte en región; F2, Supr, Ctrl + Z.
- Librería: panel lateral (Ctrl + B) con buscador instantáneo sin tildes ni mayúsculas, "este vídeo" / "todos", Enter salta (abre otro vídeo si hace falta).

## Favoritos

Cuadrícula de tarjetas con portada, nombre, duración, número de bookmarks y progreso. ★ / Ctrl + D.

- Cambio rápido: Ctrl + P con buscador enfocado; abre donde lo dejaste.
- Portada automática: ~12 keyframes entre el 10 % y el 90 %, descartando negros/uniformes, eligiendo el más nítido y variado.
- Portada manual: "Usar este frame como portada".
- Archivos movidos: identificación por huella; si falta, "Buscar archivo".

## Almacenamiento de datos

Archivos JSON legibles en `data\` junto al ejecutable; desde los ajustes (Ctrl + ,) se abre la carpeta o se lleva a otra. Si no se puede escribir ahí (Program Files), se usa `%LOCALAPPDATA%\VideoSeeker`.

| Dato | Dónde | Contenido |
| --- | --- | --- |
| Bookmarks | `data\bookmarks\`, un archivo por vídeo | Nombre y huella del vídeo; por bookmark: nombre, inicio y fin en segundos con milisegundos, color, fechas |
| Vídeos conocidos y favoritos | `data\library.json` | Huella, ruta, duración, resolución, última posición, favorito y orden, portada |
| Ajustes | `data\settings.json` | Ventana de ajustes (Ctrl + ,): barras, saltos, scrubbing, reanudar, formato de tiempo, color de los bookmarks nuevos, idiomas, caché, decodificación por hardware; volumen y colección activa |
| Miniaturas y portadas | `data\cache\thumbs\` | JPEG de 320 px, regenerables |
| Índices de frames | `data\cache\index\` | Binario por huella |

- **Compartir:** un `<vídeo>.bookmarks.json` junto al vídeo se carga y mezcla automáticamente; "Exportar bookmarks junto al vídeo" lo crea.
- **Fiabilidad:** escritura a temporal + renombrado, copia `.bak`.
- **Huella:** xxHash64 del tamaño y del primer y último MB. Tiempos como instantes, no números de frame.

## Atajos

| Acción | Teclado | Ratón |
| --- | --- | --- |
| Reproducir / pausar | Espacio, K | Clic en el vídeo |
| Frame anterior / siguiente | ↓ / ↑, mantener para repetir | Rueda sobre vídeo o barra de zoom |
| Saltar 5 s (Shift 1 s, Alt 30 s; cambiables en ajustes) | ← / → | Rueda sobre la barra principal (con Shift / Alt, igual) |
| Atrás / adelante, más rápido | J / L | — |
| Ampliar ±5 s | Z | Clic derecho → Ampliar sección |
| Seleccionar tramo | — | Arrastrar con botón derecho |
| Zoom en la línea de tiempo | — | Ctrl + rueda sobre cualquiera de las dos barras |
| Bucle en la región | Shift + R | — |
| Bucle del vídeo | R | — |
| Primer / último frame del tramo = frame actual | I / O | Botones ⇤ ⇥, clic derecho; botón derecho mientras se arrastra |
| Ir al primer / último frame del tramo | Shift + I / Shift + O | — |
| Quitar el tramo | Alt + X | Botón × |
| Frame anterior / siguiente (alternativa) | , / . | — |
| Principio / final del vídeo | Inicio / Fin | — |
| Cerrar zoom o panel | Esc | Botón × |
| Añadir bookmark / de región | B / Shift + B | Clic derecho → Añadir bookmark |
| Bookmark anterior / siguiente | [ / ], Re Pág / Av Pág | Clic en su marca |
| Ir a los bookmarks 1–10 | 1–9, 0 | — |
| Panel de bookmarks / buscar | Ctrl + B / Ctrl + F | — |
| Mostrar / ocultar las marcas de bookmarks | Ctrl + Shift + B | Botón del ojo |
| Renombrar / borrar / deshacer / rehacer | F2 / Supr / Ctrl + Z / Ctrl + Y | Clic derecho en su marca |
| Favoritos / cambio rápido | Ctrl + D / Ctrl + P | Botón ★ |
| Abrir archivo | Ctrl + O | Arrastrar a la ventana |
| Pantalla completa | F, Alt + Enter, Enter sin bookmark seleccionado | Doble clic |
| Volumen | M silencia | Control en pantalla |
| Pista de audio / subtítulos | A / S | — |
| Ajustes | Ctrl + , | Botón del engranaje |

Con el foco en una lista o buscador, las flechas mueven la lista; Esc o clic en el vídeo las devuelven al vídeo.

## Fases

| Fase | Contenido | Listo cuando |
| --- | --- | --- |
| 0 · Experimento | WPF + libmpv en HwndHost con todos los clips; medir arranque, primer frame y paso atrás | Decisión: si el paso atrás con mpv supera 100 ms, el motor de inspección entra en la fase 2 |
| 1 · Reproductor base | Abrir (arrastre, argumento, Abrir con), instancia única, pistas, barra principal, pantalla completa, reanudar | Todos los clips se reproducen; primer frame < 1 s |
| 2 · Scrubbing preciso | Índice de frames, timecode, J/K/L, precarga ±5 s en VRAM, indicador | Paso atrás < 30 ms en caché, también en HEVC 4K |
| 3 · Barra de zoom | Selección, ±5 s, Ctrl + rueda, banda con asas, marcas por frame, bucle | Cualquier frame de 10 s a un clic |
| 4 · Bookmarks | JSON, marcas y regiones, edición con asas, miniaturas, panel, compartir | Crear un bookmark con nombre < 2 s |
| 5 · Favoritos y pulido | Cuadrícula, portadas, Ctrl + P, archivos movidos, ajustes (hechos), ReadyToRun | — |

## Riesgos

| Riesgo | Mitigación |
| --- | --- |
| Paso atrás lento en GOP largos y 4K | Medir en fase 0; caché en VRAM |
| Nuestro render no se ve igual que mpv (HDR) | Misma conversión de color; tone mapping equivalente o libplacebo |
| Controles sobre el vídeo en pantalla completa | Ventana transparente propia |
| AVI/WMV sin instantes fiables | Reconstruir por orden de frame; si no, navegar por tiempo |
| Vídeos muy largos (10,6 h) | Barra de zoom y Ctrl + rueda |
| Versiones de libmpv/FFmpeg | Fijar versiones en un script de descarga |
| Licencias GPL de libmpv | Sin problema para uso interno; revisar si se distribuye |
