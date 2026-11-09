# Publicar VideoSeeker en la Microsoft Store

Preparado el 9 de noviembre de 2026. Lo que hay en el repo, las decisiones y los pasos que tiene que hacer Mario con su cuenta.

## Cómo se publica

- **Paquete MSIX, no instalador.** La Store admite las dos cosas, pero con MSIX la Store firma el paquete (no hace falta un certificado de firma de código, que cuesta cientos de euros al año), instala y actualiza sola, y desinstala sin dejar nada. Con un instalador EXE/MSI habría que firmarlo con un certificado de pago, alojarlo en una web propia y gestionar las actualizaciones.
- **Cuenta de desarrollador individual: gratis** desde septiembre de 2025 (antes costaba unos 19 $). Se crea con una cuenta Microsoft y pide verificar la identidad (documento y selfie), que puede tardar unos días. La de empresa cuesta unos 99 $ y solo hace falta si se publica a nombre de una empresa.
- **Licencia GPL v3:** la Store no lo impide (hay muchas apps GPL publicadas). Se cumple igual que con el zip: el código está en GitHub y el enlace «Código fuente» está en Ajustes. La app es gratis.
- **Códecs:** VideoSeeker lleva su propio libmpv y FFmpeg, así que no depende de las extensiones de códecs de Windows (la de HEVC es de pago) y reproduce lo mismo que el zip. La Store no revisa patentes. El único riesgo real es HEVC (sus patentes se licencian en consorcios), y para una app gratuita de un particular en la UE es muy bajo; AC3, MP3 y el núcleo de DTS ya no tienen patentes vigentes. Si algún día la app fuera de pago o de una empresa, conviene revisarlo.
- **Botón «Apoyar»:** la norma 10.8.2 pide que las donaciones usen un sistema de pago seguro (PayPal lo es) y que quien dona no reciba nada a cambio en la app. Se declara en las notas para la certificación. Si lo rechazaran, se oculta en la versión de la Store (`AppLinks.SupportUrl` vacío) y se vuelve a enviar.
- **Política de privacidad:** la Store la exige para cualquier app de escritorio, aunque no recoja nada. Está en `docs/privacidad/` y se publica con GitHub Pages en https://thenend.github.io/VideoSeeker/privacidad/.
- **Idiomas:** el paquete declara los seis de la interfaz (español, inglés, francés, alemán, italiano y portugués de Brasil), así que Partner Center abre una ficha para cada uno. La de español es la principal; los textos de las otras cinco están en [TIENDA-fichas.md](TIENDA-fichas.md). Se puede ofrecer en todos los países.

## Qué cambia en la app instalada desde la Store

La app detecta que corre como paquete (`Packaged.cs`) y en ese caso:

- **Datos** (ajustes, bookmarks, posición de cada vídeo): en la carpeta propia de la app, `%LOCALAPPDATA%\Packages\<paquete>\LocalState\data`, porque la carpeta del programa es de solo lectura y Windows desvía a otro sitio lo que se escribe en `%LOCALAPPDATA%\VideoSeeker` (el Explorador no lo vería al pulsar «Abrir»). Windows borra esa carpeta al desinstalar la app; quien quiera conservarlos puede elegir otra carpeta en Ajustes. Los bookmarks exportados junto al vídeo no se tocan.
- **Tipos de vídeo:** los 18 tipos van en el manifiesto; Windows los registra al instalar y los quita al desinstalar. «Elegir en Windows» lleva directamente a la página de VideoSeeker en Aplicaciones predeterminadas y el botón «Quitar» no aparece. El registro en `HKCU` no se usa.
- Todo lo demás funciona igual que el zip (exportar clips con ffmpeg, caché de frames, instancia única…).

## Qué hay en el repo

| Archivo | Para qué |
| --- | --- |
| `scripts/make-msix.ps1` | Compila y genera `out\msix\VideoSeeker-<versión>-x64.msix`. Descarga una vez MakeAppx y MakePri (paquete NuGet del SDK de Windows), no hace falta Visual Studio. `-Instalar` lo instala en el PC para probarlo; `-Quitar` lo desinstala. |
| `scripts/msix/AppxManifest.xml` | Manifiesto: identidad, iconos, tipos de vídeo, `runFullTrust` (app de escritorio normal). |
| `scripts/msix/identidad.json` | Nombre del paquete y editor, copiados de Partner Center (editor «Dreams2Bits», Store ID 9N1XLG37QHWQ). |
| `scripts/msix/Assets/` | Iconos del paquete (65 PNG). Los tamaños del `.ico` se copian de él; el resto se dibuja desde `docs/icono` con la misma regla (16 px pixelado, el de la barra de 24 a 44 px, el principal en el resto). Se rehacen con `scripts/msix/generar-assets.py`. |
| `scripts/msix/tienda/` | Logo para la ficha: 300 × 300 transparente y 1080 × 1080 con el fondo de la app. |
| `docs/privacidad/` | Política de privacidad (español e inglés). |

Versión del paquete: `año.mesdía.horaminuto.0` (por ejemplo `2026.1109.2015.0`): siempre crece, y la Store pide que la última parte sea 0. En Ajustes se sigue viendo la versión con fecha y commit, como en el zip.

## Pasos de Mario

1. **Crear la cuenta.** En https://storedeveloper.microsoft.com, «Empezar» como desarrollador individual, con tu cuenta Microsoft. Verifica la identidad cuando lo pida.
2. **Reservar el nombre.** En Partner Center: Aplicaciones y juegos > Nuevo producto > «Aplicación MSIX o PWA» > nombre «VideoSeeker». Si está cogido, prueba «VideoSeeker Player» o similar (y avísame para cambiar el nombre que se ve).
3. **Copiar la identidad** (hecho el 9 de noviembre). En la app reservada: Administración de productos > Identidad del producto. Copia `Package/Identity/Name`, `Package/Identity/Publisher` y `Package/Properties/PublisherDisplayName` en `scripts/msix/identidad.json` (o pásamelos y lo hago yo).
4. **Generar el paquete.** `pwsh scripts/make-msix.ps1` → `out\msix\VideoSeeker-<versión>-x64.msix`.
5. **Probarlo antes (opcional).** Activa el Modo de desarrollador (Configuración > Sistema > Para programadores), cierra el VideoSeeker del zip (si está abierto, el instalado le pasaría los vídeos a él) y ejecuta `pwsh scripts/make-msix.ps1 -Instalar`. Sale en Inicio como VideoSeeker. Quítalo con `-Quitar` antes de instalar el de la Store.
6. **Crear el envío** («Iniciar el envío») y rellenar:
   - **Precios y disponibilidad:** Gratis; todos los mercados; visible para todos.
   - **Propiedades:** categoría «Foto y vídeo»; URL de privacidad https://thenend.github.io/VideoSeeker/privacidad/; sitio web https://github.com/Thenend/VideoSeeker; contacto de soporte: https://github.com/Thenend/VideoSeeker/issues. Requisitos de hardware recomendados: tarjeta gráfica con DirectX 11.
   - **Clasificación por edades:** el cuestionario (IARC). Es una herramienta: sin contenido propio, sin chat, sin compras, sin compartir ubicación. Saldrá «Para todos» / PEGI 3.
   - **Paquetes:** sube el `.msix`. Partner Center pedirá justificar `runFullTrust`: usa el texto de abajo.
   - **Fichas de la Store:** una por idioma. En la de español, el texto de abajo, al menos una captura (1366 × 768 o más grande, sin bordes) y el logo de `scripts/msix/tienda/`; en las otras cinco, los textos de [TIENDA-fichas.md](TIENDA-fichas.md) y una captura con la app en ese idioma (Ajustes > General > Idioma) o, si no, la misma.
   - **Opciones de envío > Notas para la certificación:** el texto de abajo.
7. **Enviar a certificación.** Suele tardar entre unas horas y 3 días laborables. Si la rechazan, el informe dice por qué: pásamelo.
8. **Actualizaciones:** `make-msix.ps1` otra vez, nuevo envío en Partner Center (copia el anterior), sube el nuevo `.msix`. Los usuarios lo reciben solos.

## Textos para Partner Center

**Descripción corta:**
Reproductor de vídeo para revisar frame a frame, con bookmarks, zoom en la línea de tiempo y bucles.

**Descripción:**
VideoSeeker es un reproductor de vídeo para Windows pensado para inspeccionar vídeos con precisión: animaciones, partidas, rodajes o cualquier vídeo que haya que mirar con lupa.

Arranca rápido y reproduce los formatos habituales (MKV, MP4, AVI, MOV, WebM; H.264, HEVC, AV1, VP9; AAC, AC3, DTS…) sin instalar códecs aparte.

- Frame a frame al instante, hacia delante y hacia atrás, gracias a una caché de frames en la tarjeta gráfica.
- Scrubbing arrastrando sobre la imagen o sobre la barra, viendo siempre el frame exacto.
- Línea de tiempo con zoom para trabajar un tramo concreto, con marcas de cada frame.
- Bookmarks de regiones con nombre, etiquetas y colores, en un panel para saltar entre ellos.
- Bucle del vídeo o de un tramo, velocidad variable y marcha atrás real (J/K/L).
- Exporta clips de un tramo con un clic.
- Miniatura del frame al pasar el ratón por la barra.

Gratis y de código abierto (GPL v3): https://github.com/Thenend/VideoSeeker. Sin anuncios ni recogida de datos.

**Características (lista corta de la ficha):**
Frame a frame instantáneo, adelante y atrás · Bookmarks de regiones con etiquetas y colores · Línea de tiempo con zoom · Bucles y marcha atrás real · Exportar clips · Sin códecs aparte: MKV, MP4, HEVC, AV1, AC3, DTS…

**Palabras clave:** reproductor de vídeo, frame a frame, bookmarks, scrubbing, animación, línea de tiempo, MKV, HEVC

**Justificación de `runFullTrust`:**
VideoSeeker es una aplicación de escritorio (WPF) empaquetada en MSIX. Necesita confianza total para reproducir con su motor de vídeo propio (libmpv y FFmpeg incluidos en el paquete), usar Direct3D 11 para su caché de frames, leer los vídeos locales que abre el usuario y guardar junto a ellos los clips y bookmarks que exporta.

**Notas para la certificación:**
Aplicación gratuita y de código abierto (GPL v3, https://github.com/Thenend/VideoSeeker). Para probarla, abra cualquier vídeo local (Ctrl+O o arrastrándolo a la ventana). No necesita cuenta ni conexión. La interfaz sigue el idioma de Windows (español, inglés, francés, alemán, italiano o portugués de Brasil; se cambia en Ajustes > General). El botón «Apoyar» («Support» en inglés) de Ajustes solo abre en el navegador una página con un enlace de donación voluntaria por PayPal; quien dona no recibe nada a cambio en la app. El botón «Código fuente» («Source code») abre el repositorio.

## Ficha en los seis idiomas (importación)

Los textos de la ficha (descripción, características, palabras clave y pies de las capturas) están en `scripts/msix/tienda/textos.json`, en los seis idiomas; sustituyen a los de arriba y a los de [TIENDA-fichas.md](TIENDA-fichas.md). Las capturas salen de la autoprueba `capturas` (Sprite Fright, CC BY) en `out\capturas-tienda\<idioma>\`.

Para subirlo todo de una vez: en Partner Center, añadir los seis idiomas, «Export listings», y con ese CSV `python scripts/msix/tienda/rellenar-ficha.py <exportado.csv> out\capturas-tienda out\ficha-store`. Luego «Import listings» eligiendo la carpeta entera `ficha-store`. El CSV tiene que ser de una exportación del mismo envío (uno hecho a mano o de otro envío se rechaza entero) y las rutas de imagen empiezan por el nombre de la carpeta (`ficha-store/es/1.png`). `scripts/msix/tienda/listingData.csv` es el del primer envío ya relleno.
