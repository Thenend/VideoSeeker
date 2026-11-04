# Notas para agentes

App de Windows (C# WPF + libmpv + motor de inspección propio con FFmpeg). Estado y decisiones: `docs/STATUS.md`; plan: `docs/PLAN.md`. Compilar: `README.md`. La interfaz está en seis idiomas (es, en, fr, de, it, pt-BR) y no nombra nunca otros programas. Ningún texto de la interfaz va escrito en el código ni en el XAML: se declara en `src/VideoSeeker.App/Localization/Strings.cs` y se escribe en los seis `Strings.<idioma>.cs` (en el código `S.Nombre`, en XAML `{l:Tr Nombre}`; ver `CONTRIBUTING.md`).

## Autopruebas

Se lanzan con `pwsh scripts/selftest.ps1`: compila en `out\selftest` (sin ReadyToRun), usa datos de prueba propios (`out\selftest\data`, nunca los de Mario), funciona aunque Mario tenga la app abierta y acaba con un resumen (OK o las líneas `FALLO`). Registro y capturas de cada prueba en `out\selftest\data\resultados\<prueba>\`. Abren una ventana en el escritorio de Mario mientras duran.

Elige la más corta que cubra el cambio:

| Cambio | Prueba |
| --- | --- |
| Solo documentación o comentarios | Ninguna |
| Textos, cadenas, tooltips, nombres de menús, colores, márgenes, estilos, orden o disposición de la ventana de ajustes, avisos | **Rápida**: `pwsh scripts/selftest.ps1` (unos segundos más la compilación) |
| Cambia cómo funciona algo de una parte | `pwsh scripts/selftest.ps1 rapida <las de esa parte>` (tabla de abajo) |
| Código compartido (`MainWindow.xaml.cs`, `Navigator`, `TimelineBar`, `DataStore` y el formato de los datos, `MpvPlayer`, motor de inspección), antes de publicar una versión, o si no está claro qué afecta | **Completa**: `pwsh scripts/selftest.ps1 -Full` (varios minutos) |

La rápida (`rapida`, `QuickSelfTest.cs`) abre el vídeo, la ventana de ajustes, los menús del vídeo, de un bookmark, de velocidad y de colección y el cuadro del nombre; falla si algún texto no cabe (cortado o con «…» donde no toca), si alguno nombra otro programa o si hay errores de enlace de datos. Deja capturas (`selftest-rapida-*.png`, míralas tras cambiar algo visible) y todos los textos y tooltips en `selftest-rapida-textos.txt` (busca ahí la cadena cambiada para confirmar que llega a la interfaz).

Pruebas por parte:

| Parte | Pruebas |
| --- | --- |
| Ajustes (uno nuevo o lo que hace) | `settings` |
| Bookmarks, etiquetas, colores, panel, cuadro del nombre, compartir | `bookmarks` |
| Zona ampliada, región seleccionada, arrastrar extremos | `zona`, `drag`, `zoom` |
| Ir a un bookmark (panel, `[` `]`, números) | `saltar` |
| Miniatura al pasar por las barras | `vistazo` |
| Exportar clips | `clips` |
| Historial de vídeos (atrás / adelante) | `historial` |
| Pasos de frame, caché, marcha atrás, bucles | `1`, `resume`, `reverse`, `loop`, `longdrag` |
| Arranque, ventana sin vídeo | `inicio`, `arranque` |

Textos nuevos o cambiados: además de la rápida en español, la rápida en los otros idiomas, `pwsh scripts/selftest.ps1 -Idioma de` (y `en`, `fr`, `it`, `pt-BR`; el alemán y el francés son los más largos), para ver que caben. Las demás pruebas van siempre en español.

Otro vídeo: `-Video <ruta>` (por defecto `samples\generated\h264-1080p60-gop250_aac.mp4`; para códecs o resoluciones, repite con el que toque). Antes de culpar a tu cambio de un fallo, mira si también falla sin él: `settings` tiene fallos que ya estaban.
