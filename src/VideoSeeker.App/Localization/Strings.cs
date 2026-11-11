// Cada texto de la interfaz se declara aquí y cada idioma le da valor en su Strings.<idioma>.cs: si falta en
// alguno, no compila. Para añadir uno: declararlo aquí (con su versión en español en el comentario) y escribirlo
// en los seis archivos. Para añadir un idioma: copiar Strings.en.cs, traducirlo y apuntarlo en Loc.All.

namespace VideoSeeker.App.Localization;

/// <summary>Los textos de la interfaz. El comentario de cada uno es su versión en español.</summary>
public abstract class Strings
{
    // ---- Teclas (para componer atajos) ----

    /// <summary>Ctrl</summary>
    public abstract string KeyCtrl { get; }
    /// <summary>Shift</summary>
    public abstract string KeyShift { get; }
    /// <summary>Supr</summary>
    public abstract string KeyDelete { get; }
    /// <summary>Esc</summary>
    public abstract string KeyEsc { get; }

    // ---- Ventana principal ----

    /// <summary>Arrastra un vídeo aquí o pulsa Ctrl + O</summary>
    public abstract string EmptyHint { get; }
    /// <summary>Cerrar zoom (Esc)</summary>
    public abstract string CloseZoomTip { get; }
    /// <summary>Bucle del tramo (Shift + R)</summary>
    public abstract string RangeLoopTip(string? region);
    /// <summary>Quitar el bucle del tramo (Shift + R)</summary>
    public abstract string RangeLoopOffTip(string? region);
    /// <summary>Asignar el frame en pantalla al fin del tramo (O)</summary>
    public abstract string MarkLastTip(string? region);
    /// <summary>Asignar el frame en pantalla al inicio del tramo (I)</summary>
    public abstract string MarkFirstTip(string? region);
    /// <summary>Bucle del vídeo (R)</summary>
    public abstract string VideoLoopTip { get; }
    /// <summary>Quitar el bucle del vídeo (R)</summary>
    public abstract string VideoLoopOffTip { get; }
    /// <summary>Frame anterior (,) · rueda: frame a frame</summary>
    public abstract string PreviousFrameTip { get; }
    /// <summary>Frame siguiente (.) · rueda: frame a frame</summary>
    public abstract string NextFrameTip { get; }
    /// <summary>Reproducir / pausar (Espacio). J / K / L: atrás / pausa / adelante · rueda: frame a frame</summary>
    public abstract string PlayTip { get; }
    /// <summary>Velocidad · rueda: ±0,1 · botón central: 1×</summary>
    public abstract string SpeedTip { get; }
    /// <summary>Abrir vídeo (Ctrl + O)</summary>
    public abstract string OpenVideoTip { get; }
    /// <summary>Respecto al tramo ampliado</summary>
    public abstract string RangePositionTip { get; }
    /// <summary>Bookmark en el que se está ([ / ] anterior / siguiente, 1–9 y 0 para ir a los diez primeros)</summary>
    public abstract string CurrentBookmarkTip { get; }
    /// <summary>Panel de bookmarks (Ctrl + B) · añadir: B</summary>
    public abstract string BookmarksButtonTip { get; }
    /// <summary>Pantalla completa (F)</summary>
    public abstract string FullscreenTip { get; }
    /// <summary>Ajustes (Ctrl + ,)</summary>
    public abstract string SettingsTip { get; }
    /// <summary>Audio y subtítulos (A / S)</summary>
    public abstract string TracksTip { get; }
    /// <summary>Volumen</summary>
    public abstract string VolumeTip { get; }
    /// <summary>Silenciar (M)</summary>
    public abstract string MuteTip { get; }
    /// <summary>Cerrar (Ctrl + B)</summary>
    public abstract string CloseBookmarkPanelTip { get; }
    /// <summary>Exportar los bookmarks de este vídeo, para compartirlos</summary>
    public abstract string ExportVideoBookmarksTip { get; }
    /// <summary>Exportar la colección entera, para compartirla</summary>
    public abstract string ExportCollectionTip { get; }
    /// <summary>Bookmarks</summary>
    public abstract string BookmarksTitle { get; }
    /// <summary>Colección de bookmarks: cada una tiene los suyos de cada vídeo. Cambiar, crear, renombrar o borrar.</summary>
    public abstract string CollectionTip { get; }
    /// <summary>Buscar (Ctrl + F)</summary>
    public abstract string SearchPlaceholder { get; }
    /// <summary>Este vídeo</summary>
    public abstract string ScopeThisVideo { get; }
    /// <summary>Todos los vídeos</summary>
    public abstract string ScopeAllVideos { get; }
    /// <summary>B crea una región con el tramo ampliado o, sin él, de un frame (O fija su fin). Clic o Enter: ir y ampliarla. Ctrl / Shift + clic: varios. F2 renombra, Supr borra, Ctrl + Z deshace.</summary>
    public abstract string PanelHint { get; }
    /// <summary>Tecla para ir a este bookmark</summary>
    public abstract string BookmarkKeyTip { get; }
    /// <summary>Enter guarda · Esc deja el nombre como estaba</summary>
    public abstract string NameHint { get; }
    /// <summary>Enter guarda · Esc cancela · Alt + número: etiqueta</summary>
    public abstract string NameHintWithLabels { get; }
    /// <summary>Generando miniatura…</summary>
    public abstract string GeneratingThumbnail { get; }
    /// <summary>Cerrar</summary>
    public abstract string Close { get; }
    /// <summary>Mostrar en la carpeta</summary>
    public abstract string ShowInFolder { get; }
    /// <summary>Abrir</summary>
    public abstract string Open { get; }
    /// <summary>«del tramo» o «de «región»»: para los textos que hablan del tramo ampliado, que puede ser una región seleccionada</summary>
    public abstract string ZoneOf(string? region);
    /// <summary>Guardar el tramo ampliado como bookmark (B)</summary>
    public abstract string SaveZoneTip { get; }
    /// <summary>Nueva etiqueta</summary>
    public abstract string NewLabelTitle { get; }
    /// <summary>Enter la crea y la pone · Esc vuelve</summary>
    public abstract string NewLabelBoxTip { get; }
    /// <summary>La zona es la región «{name}»: I / O, ⇤ ⇥ y las banderitas la cambian</summary>
    public abstract string ZoneNameTip(string name);
    /// <summary>Cerrar zoom y deseleccionar «{name}» (Esc)</summary>
    public abstract string CloseZoomDeselectTip(string name);
    /// <summary>Cargando…</summary>
    public abstract string Loading { get; }

    // ---- Ajustes ----

    /// <summary>Ajustes — VideoSeeker</summary>
    public abstract string SettingsTitle { get; }
    /// <summary>Restablecer valores por defecto</summary>
    public abstract string ResetDefaults { get; }
    /// <summary>VideoSeeker es gratis. Si te resulta útil, puedes invitar al autor a un café.</summary>
    public abstract string SupportTip { get; }
    /// <summary>Apoyar</summary>
    public abstract string Support { get; }
    /// <summary>Código fuente</summary>
    public abstract string SourceCode { get; }
    /// <summary>Software libre, con licencia GPL v3.</summary>
    public abstract string SourceCodeTip { get; }
    /// <summary>Versión {version}</summary>
    public abstract string Version(string version);
    /// <summary>BARRAS</summary>
    public abstract string SectionBars { get; }
    /// <summary>Arrastrar el centro del tramo ampliado o de una región de bookmark la desplaza entera. Desactivado, mueve el cursor de la barra.</summary>
    public abstract string MoveRegionsTip { get; }
    /// <summary>Mover regiones enteras arrastrando su centro</summary>
    public abstract string MoveRegions { get; }
    /// <summary>Al arrastrar un extremo del tramo ampliado o de un bookmark. Desactivado, la imagen no cambia.</summary>
    public abstract string EdgeFrameTip { get; }
    /// <summary>Ver el frame del extremo que se arrastra</summary>
    public abstract string EdgeFrame { get; }
    /// <summary>Marca bajo las barras lo que ya está en la caché: ahí se salta al instante.</summary>
    public abstract string CachedLineTip { get; }
    /// <summary>Línea verde de la caché de frames</summary>
    public abstract string CachedLine { get; }
    /// <summary>Atajo: Ctrl + Shift + B.</summary>
    public abstract string MarksTip { get; }
    /// <summary>Marcas de los bookmarks</summary>
    public abstract string Marks { get; }
    /// <summary>Con las marcas ocultas, al crear un bookmark o ir a uno su botón (abajo a la derecha) parpadea unas veces. No sale ningún mensaje.</summary>
    public abstract string PulseMarksTip { get; }
    /// <summary>Hacer parpadear su botón si están ocultas</summary>
    public abstract string PulseMarks { get; }
    /// <summary>NAVEGACIÓN</summary>
    public abstract string SectionNavigation { get; }
    /// <summary>s</summary>
    public abstract string SecondsUnit { get; }
    /// <summary>Segundos que salta cada flecha: sola, con Shift o con Ctrl.</summary>
    public abstract string SeekTip { get; }
    /// <summary>Saltos con ← →</summary>
    public abstract string Seek { get; }
    /// <summary>Píxeles que hay que arrastrar sobre la imagen, con el botón pulsado, para avanzar un frame.</summary>
    public abstract string ScrubTip { get; }
    /// <summary>Sensibilidad del scrubbing</summary>
    public abstract string Scrub { get; }
    /// <summary>{pixels} px por frame</summary>
    public abstract string PixelsPerFrame(int pixels);
    /// <summary>Velocidad</summary>
    public abstract string WheelSpeed { get; }
    /// <summary>Volumen</summary>
    public abstract string WheelVolume { get; }
    /// <summary>Velocidad: ±0,1× (botón central: 1×). Volumen: ±5 %.</summary>
    public abstract string WheelTip { get; }
    /// <summary>Rueda del ratón sobre el vídeo</summary>
    public abstract string Wheel { get; }
    /// <summary>Reanudar cada vídeo donde se dejó</summary>
    public abstract string Resume { get; }
    /// <summary>TIEMPO Y BOOKMARKS</summary>
    public abstract string SectionTimeAndBookmarks { get; }
    /// <summary>En 00:01:23:11, el último número es el frame dentro de cada segundo.</summary>
    public abstract string TimeFormatTip { get; }
    /// <summary>Formato del tiempo</summary>
    public abstract string TimeFormat { get; }
    /// <summary>Sin etiqueta, mientras no se elija otro (o siempre, si no se repite lo último).</summary>
    public abstract string NewColorTip { get; }
    /// <summary>Color de los bookmarks nuevos</summary>
    public abstract string NewColor { get; }
    /// <summary>El nuevo lleva la etiqueta o el color que se eligió la última vez; en el cuadro del nombre se cambia.</summary>
    public abstract string RepeatLabelTip { get; }
    /// <summary>Repetir la última etiqueta o color</summary>
    public abstract string RepeatLabel { get; }
    /// <summary>Vista previa</summary>
    public abstract string TimesInPreview { get; }
    /// <summary>Lista</summary>
    public abstract string TimesInList { get; }
    /// <summary>En la lista se ven todos de un vistazo. En la vista previa, la lista deja más sitio al nombre.</summary>
    public abstract string BookmarkTimesTip { get; }
    /// <summary>Tiempos de los bookmarks</summary>
    public abstract string BookmarkTimes { get; }
    /// <summary>AUDIO Y SUBTÍTULOS</summary>
    public abstract string SectionAudioAndSubtitles { get; }
    /// <summary>Se elige esa pista si el vídeo trae varias. Se aplica al abrir el siguiente vídeo.</summary>
    public abstract string AudioLanguageTip { get; }
    /// <summary>Idioma preferido del audio</summary>
    public abstract string AudioLanguage { get; }
    /// <summary>Se elige esa pista al activar los subtítulos. Se aplica al abrir el siguiente vídeo.</summary>
    public abstract string SubtitleLanguageTip { get; }
    /// <summary>Idioma preferido de los subtítulos</summary>
    public abstract string SubtitleLanguage { get; }
    /// <summary>Se aplica al abrir el siguiente vídeo.</summary>
    public abstract string AppliesOnNextVideo { get; }
    /// <summary>Activar los subtítulos al abrir un vídeo</summary>
    public abstract string SubtitlesOnOpen { get; }
    /// <summary>Sin preferencia</summary>
    public abstract string NoPreference { get; }
    /// <summary>RENDIMIENTO</summary>
    public abstract string SectionPerformance { get; }
    /// <summary>Memoria de la caché de frames</summary>
    public abstract string CacheMemory { get; }
    /// <summary>Cuanta más, más tiempo de vídeo se puede recorrer frame a frame al instante. Se aplica al abrir el siguiente vídeo.</summary>
    public abstract string CacheMemoryTip { get; }
    /// <summary>{used} de {total} GB</summary>
    public abstract string CacheOfTotal(string used, string total);
    /// <summary>GB</summary>
    public abstract string GigabytesUnit { get; }
    /// <summary>MB</summary>
    public abstract string MegabytesUnit { get; }
    /// <summary>Más del 90 % de la memoria de la tarjeta: puede faltar para otros programas.</summary>
    public abstract string CacheTooBigTip { get; }
    /// <summary>Reproduce con la tarjeta gráfica. Desactívala solo si un vídeo se ve mal.</summary>
    public abstract string HardwareDecodingTip { get; }
    /// <summary>Decodificación por hardware</summary>
    public abstract string HardwareDecoding { get; }
    /// <summary>EXPORTAR CLIPS</summary>
    public abstract string SectionClips { get; }
    /// <summary>Cambiar…</summary>
    public abstract string ChangeEllipsis { get; }
    /// <summary>Junto al vídeo</summary>
    public abstract string BesideVideo { get; }
    /// <summary>Dónde se guardan los clips exportados. Si no se puede escribir allí, van a Vídeos\\VideoSeeker.</summary>
    public abstract string ClipFolderTip { get; }
    /// <summary>Carpeta de destino</summary>
    public abstract string ClipFolder { get; }
    /// <summary>Junto a cada vídeo</summary>
    public abstract string BesideEachVideo { get; }
    /// <summary>Carpeta para los clips exportados</summary>
    public abstract string ClipFolderDialogTitle { get; }
    /// <summary>Como archivo, para pegarlo directamente en un chat, en Jira o en una carpeta.</summary>
    public abstract string ClipClipboardTip { get; }
    /// <summary>Copiar cada clip al portapapeles</summary>
    public abstract string ClipClipboard { get; }
    /// <summary>Megas por cada segundo de clip, con el audio. Nunca más que el vídeo original.</summary>
    public abstract string LightClipTip { get; }
    /// <summary>Tamaño del clip ligero</summary>
    public abstract string LightClip { get; }
    /// <summary>{value} MB/s</summary>
    public abstract string MegabytesPerSecond(string value);
    /// <summary>GENERAL</summary>
    public abstract string SectionGeneral { get; }
    /// <summary>Idioma</summary>
    public abstract string InterfaceLanguage { get; }
    /// <summary>El idioma de VideoSeeker. Automático: el de Windows. El cambio se aplica al reiniciar.</summary>
    public abstract string InterfaceLanguageTip { get; }
    /// <summary>Animar los paneles</summary>
    public abstract string AnimatePanels { get; }
    /// <summary>El panel de bookmarks y la barra del zoom se abren y se cierran deslizándose, y el zoom pasa suavemente de un tramo a otro.</summary>
    public abstract string AnimatePanelsTip { get; }
    /// <summary>Automático · {language}</summary>
    public abstract string AutomaticLanguage(string language);
    /// <summary>El nuevo idioma se usará al reiniciar VideoSeeker.</summary>
    public abstract string LanguageChangedRestart { get; }
    /// <summary>Idioma</summary>
    public abstract string LanguageCaption { get; }
    /// <summary>Reiniciar ahora</summary>
    public abstract string RestartNow { get; }
    /// <summary>Más tarde</summary>
    public abstract string Later { get; }
    /// <summary>Por defecto</summary>
    public abstract string DefaultFolder { get; }
    /// <summary>La carpeta data, junto al programa.</summary>
    public abstract string DefaultFolderTip { get; }
    /// <summary>Ajustes, bookmarks y posición de cada vídeo. El cambio se aplica al reiniciar.</summary>
    public abstract string DataFolderTip { get; }
    /// <summary>Carpeta de datos</summary>
    public abstract string DataFolder { get; }
    /// <summary>Elegir en Windows</summary>
    public abstract string ChooseInWindows { get; }
    /// <summary>Quitar</summary>
    public abstract string Unregister { get; }
    /// <summary>Reproductor de vídeo predeterminado</summary>
    public abstract string DefaultPlayer { get; }
    /// <summary>Añade los tipos de vídeo a «Abrir con» y lleva a Aplicaciones predeterminadas de Windows, donde se elige para cuáles.</summary>
    public abstract string DefaultPlayerTip { get; }
    /// <summary>Entre 0,04 y 3600 segundos</summary>
    public abstract string NumberRange { get; }
    /// <summary>Al reiniciar se usará {folder}.</summary>
    public abstract string PendingFolder(string folder);
    /// <summary>Al reiniciar se usará {folder}, con una copia de los datos actuales.</summary>
    public abstract string PendingFolderWithCopy(string folder);
    /// <summary>Carpeta para los datos de VideoSeeker</summary>
    public abstract string DataFolderDialogTitle { get; }
    /// <summary>La carpeta de datos no puede estar dentro de la actual ni contenerla.</summary>
    public abstract string DataFolderNested { get; }
    /// <summary>{folder}\n\nya tiene datos de VideoSeeker. Si copias allí los actuales, los que había se guardan en su subcarpeta «anteriores».</summary>
    public abstract string DataFolderHasData(string folder);
    /// <summary>Usar esos datos</summary>
    public abstract string UseThatData { get; }
    /// <summary>Copiar los actuales</summary>
    public abstract string CopyCurrentData { get; }
    /// <summary>¿Llevar tus datos (ajustes, bookmarks, posición de cada vídeo) a\n{folder}?\n\nLa carpeta actual se queda como está.</summary>
    public abstract string MoveDataQuestion(string folder);
    /// <summary>Llevar los datos</summary>
    public abstract string MoveData { get; }
    /// <summary>Empezar vacía</summary>
    public abstract string StartEmpty { get; }
    /// <summary>Cancelar</summary>
    public abstract string Cancel { get; }
    /// <summary>No se puede usar esa carpeta:\n{error}</summary>
    public abstract string CannotUseFolder(string error);
    /// <summary>La nueva carpeta de datos se usará al reiniciar VideoSeeker.</summary>
    public abstract string DataFolderRestart { get; }
    /// <summary>Abrir con doble clic</summary>
    public abstract string OpenWithDoubleClick { get; }
    /// <summary>No se pudieron registrar los tipos de vídeo:\n{error}</summary>
    public abstract string RegisterFailed(string error);
    /// <summary>No se pudo quitar el registro:\n{error}</summary>
    public abstract string UnregisterFailed(string error);
    /// <summary>Se volverán a poner los valores por defecto de todos los ajustes.</summary>
    public abstract string ResetQuestion { get; }
    /// <summary>Restablecer</summary>
    public abstract string Reset { get; }
    /// <summary>Restablecer ajustes</summary>
    public abstract string ResetTitle { get; }
    /// <summary>La carpeta de la app en Windows. Se borra al desinstalarla.</summary>
    public abstract string DefaultFolderPackagedTip { get; }
    /// <summary>Miniatura al pasar el ratón por las barras</summary>
    public abstract string HoverPreview { get; }
    /// <summary>Para ver qué hay en un punto del vídeo sin ir a él.</summary>
    public abstract string HoverPreviewTip { get; }
    /// <summary>La copia de {file} no salió entera</summary>
    public abstract string DataCopyIncomplete(string file);

    // ---- Ventana principal: reproducción ----

    /// <summary>Vídeos|*.mkv;*.mp4;*.m4v;*.avi;*.wmv;*.mov;*.webm;*.flv;*.ts;*.m2ts;*.mts;*.mpg;*.mpeg;*.vob;*.3gp;*.ogv|Todos los archivos|*.*</summary>
    public abstract string VideoFileFilter { get; }
    /// <summary>Abrir vídeo</summary>
    public abstract string OpenVideoTitle { get; }
    /// <summary>No se encuentra el archivo:\n{path}</summary>
    public abstract string FileNotFound(string path);
    /// <summary>frame {frame}</summary>
    public abstract string FrameNumber(int frame);
    /// <summary>frame ~{frame}</summary>
    public abstract string FrameNumberApprox(string frame);
    /// <summary>= count == 1 ? "1 frame" : $"{count} frames"</summary>
    public abstract string Frames(int count);
    /// <summary>frame {frame}/{count}</summary>
    public abstract string FrameOfRange(int frame, int count);
    /// <summary>Bucle del vídeo</summary>
    public abstract string VideoLoopOn { get; }
    /// <summary>Sin bucle del vídeo</summary>
    public abstract string VideoLoopOff { get; }
    /// <summary>Bucle del tramo</summary>
    public abstract string RangeLoopOn(string? region);
    /// <summary>Sin bucle del tramo</summary>
    public abstract string RangeLoopOff(string? region);
    /// <summary>Ampliar sección (±5 s)</summary>
    public abstract string ZoomSection { get; }
    /// <summary>Asignar el frame en pantalla (N) al inicio del tramo</summary>
    public abstract string MarkFirstFromScreen(int frame, string? region);
    /// <summary>Asignar el frame en pantalla (N) al fin del tramo</summary>
    public abstract string MarkLastFromScreen(int frame, string? region);
    /// <summary>Asignar el frame bajo el cursor (N) al inicio del tramo</summary>
    public abstract string MarkFirstFromCursor(int frame, string? region);
    /// <summary>Asignar el frame bajo el cursor (N) al fin del tramo</summary>
    public abstract string MarkLastFromCursor(int frame, string? region);
    /// <summary>1× (normal)</summary>
    public abstract string NormalSpeed { get; }
    /// <summary>Velocidad {speed}</summary>
    public abstract string SpeedToast(string speed);
    /// <summary>Volumen {volume} %</summary>
    public abstract string VolumeToast(int volume);
    /// <summary>Volumen {volume} % (silenciado)</summary>
    public abstract string VolumeMutedToast(int volume);
    /// <summary>Audio</summary>
    public abstract string Audio { get; }
    /// <summary>Subtítulos</summary>
    public abstract string Subtitles { get; }
    /// <summary>Desactivados</summary>
    public abstract string SubtitlesOff { get; }
    /// <summary>Sin pistas</summary>
    public abstract string NoTracks { get; }
    /// <summary>Pista {id}</summary>
    public abstract string Track(long id);
    /// <summary>Controles fijos</summary>
    public abstract string ControlsPinned { get; }
    /// <summary>Controles ocultos: lleva el ratón abajo para verlos</summary>
    public abstract string ControlsHidden { get; }
    /// <summary>Controles fijos (Tab)</summary>
    public abstract string ControlsPinnedTip { get; }
    /// <summary>Fijar los controles (Tab)</summary>
    public abstract string PinControlsTip { get; }
    /// <summary>Ya no está: {name}</summary>
    public abstract string VideoGone(string name);
    /// <summary>Volver al vídeo que se abrió antes (Alt + ←)</summary>
    public abstract string HistoryBackTip { get; }
    /// <summary>Ir al vídeo que se abrió después (Alt + →)</summary>
    public abstract string HistoryForwardTip { get; }
    /// <summary>Clic derecho: los {count} anteriores</summary>
    public abstract string HistoryAllBack(int count);
    /// <summary>Clic derecho: los {count} siguientes</summary>
    public abstract string HistoryAllForward(int count);

    // ---- Bookmarks ----

    /// <summary>Nuevo bookmark</summary>
    public abstract string NewBookmark { get; }
    /// <summary>Renombrar bookmark</summary>
    public abstract string RenameBookmark { get; }
    /// <summary>Selecciona antes un bookmark (clic en su marca o en el panel)</summary>
    public abstract string SelectBookmarkFirst { get; }
    /// <summary>Borrado «{name}» · Ctrl + Z lo recupera</summary>
    public abstract string BookmarkDeleted(string name);
    /// <summary>Borrados {count} bookmarks · Ctrl + Z los recupera</summary>
    public abstract string BookmarksDeleted(int count);
    /// <summary>No se pudo deshacer: {error}</summary>
    public abstract string UndoFailed(string error);
    /// <summary>No se pudo rehacer: {error}</summary>
    public abstract string RedoFailed(string error);
    /// <summary>Nada que deshacer</summary>
    public abstract string NothingToUndo { get; }
    /// <summary>Nada que rehacer</summary>
    public abstract string NothingToRedo { get; }
    /// <summary>Deshecho: {step}</summary>
    public abstract string Undone(string step);
    /// <summary>Rehecho: {step}</summary>
    public abstract string Redone(string step);
    /// <summary> (en «{video}»)</summary>
    public abstract string InVideo(string video);
    /// <summary>Este vídeo no tiene bookmarks (B añade uno)</summary>
    public abstract string VideoHasNoBookmarks { get; }
    /// <summary>No hay bookmark {number} (hay {count})</summary>
    public abstract string NoBookmarkNumber(int number, int count);
    /// <summary>No hay más bookmarks por delante</summary>
    public abstract string NoMoreBookmarksAhead { get; }
    /// <summary>No hay más bookmarks por detrás</summary>
    public abstract string NoMoreBookmarksBehind { get; }
    /// <summary>Añadir bookmark aquí</summary>
    public abstract string AddBookmarkHere { get; }
    /// <summary>Panel de bookmarks</summary>
    public abstract string BookmarksPanel { get; }
    /// <summary>Renombrar</summary>
    public abstract string Rename { get; }
    /// <summary>Usar el frame actual (N) como miniatura [de «región»]</summary>
    public abstract string UseFrameAsThumbnail(int frame, string? region);
    /// <summary>El frame actual ({frame}, {time}) está fuera de la región «{name}» ({range}).\n\n¿Usarlo igualmente como miniatura?</summary>
    public abstract string FrameOutsideRegion(int frame, string time, string name, string range);
    /// <summary>Usarlo</summary>
    public abstract string UseIt { get; }
    /// <summary>Volver a la miniatura del primer frame [de «región»]</summary>
    public abstract string ResetThumbnail(string? region);
    /// <summary>Borrar</summary>
    public abstract string Delete { get; }
    /// <summary>Borrar los {count}</summary>
    public abstract string DeleteCount(int count);
    /// <summary>= count == 1 ? "1 bookmark exportado" : $"{count} bookmarks exportados"</summary>
    public abstract string BookmarksExported(int count);
    /// <summary>No se pudo escribir junto al vídeo:\n{error}</summary>
    public abstract string CannotWriteBesideVideo(string error);
    /// <summary>Marcas de bookmarks visibles</summary>
    public abstract string MarksShown { get; }
    /// <summary>Marcas de bookmarks ocultas (Ctrl + Shift + B las muestra)</summary>
    public abstract string MarksHidden { get; }
    /// <summary>Ocultar las marcas de bookmarks en las barras (Ctrl + Shift + B). El panel sigue funcionando.</summary>
    public abstract string HideMarksTip { get; }
    /// <summary>Mostrar las marcas de bookmarks en las barras (Ctrl + Shift + B)</summary>
    public abstract string ShowMarksTip { get; }
    /// <summary>Colección: {name}  ▾</summary>
    public abstract string CollectionButton(string name);
    /// <summary>vacía</summary>
    public abstract string CollectionEmpty { get; }
    /// <summary>= videos == 1 ? $"{bookmarks} en 1 vídeo" : $"{bookmarks} en {videos} vídeos"</summary>
    public abstract string BookmarksInVideos(int bookmarks, int videos);
    /// <summary>Nueva colección…</summary>
    public abstract string NewCollection { get; }
    /// <summary>Nombre de la nueva colección</summary>
    public abstract string NewCollectionName { get; }
    /// <summary>Ya hay una colección «{name}»</summary>
    public abstract string CollectionExists(string name);
    /// <summary>Renombrar «{name}»…</summary>
    public abstract string RenameCollection(string name);
    /// <summary>Nuevo nombre para «{name}»</summary>
    public abstract string NewNameFor(string name);
    /// <summary>No se pudo renombrar: {error}</summary>
    public abstract string RenameFailed(string error);
    /// <summary>Colección renombrada a «{name}»</summary>
    public abstract string CollectionRenamed(string name);
    /// <summary>Borrar «{name}»…</summary>
    public abstract string DeleteCollection(string name);
    /// <summary>Importar colección o bookmarks…</summary>
    public abstract string ImportCollectionOrBookmarks { get; }
    /// <summary>Exportar «{name}»…</summary>
    public abstract string ExportCollectionMenu(string name);
    /// <summary>Está vacía.</summary>
    public abstract string CollectionIsEmpty { get; }
    /// <summary>= videos == 1 ? $"Tiene {bookmarks} bookmarks de 1 vídeo y se perderán." : $"Tiene {bookmarks} bookmarks de {videos} vídeos y se perderán."</summary>
    public abstract string CollectionWillBeLost(int bookmarks, int videos);
    /// <summary>¿Borrar la colección «{name}»?\n{detail}</summary>
    public abstract string DeleteCollectionQuestion(string name, string detail);
    /// <summary>No se pudo borrar: {error}</summary>
    public abstract string DeleteFailed(string error);
    /// <summary>Colección «{name}»</summary>
    public abstract string CollectionToast(string name);
    /// <summary>= count == 1 ? "1 bookmark" : $"{count} bookmarks"</summary>
    public abstract string BookmarkCount(int count);
    /// <summary>= count == 1 ? "1 vídeo" : $"{count} vídeos"</summary>
    public abstract string VideoCount(int count);
    /// <summary>{bookmarks} bookmarks de {videos} vídeos</summary>
    public abstract string BookmarksFromVideos(int bookmarks, int videos);
    /// <summary>Mostrar el vídeo en el Explorador</summary>
    public abstract string ShowVideoInExplorer { get; }
    /// <summary>Mostrar el vídeo en el Explorador (no se encuentra)</summary>
    public abstract string ShowVideoInExplorerMissing { get; }
    /// <summary>Movido a «{collection}» · Ctrl + Z lo devuelve</summary>
    public abstract string MovedTo(string collection);
    /// <summary>Movidos {count} a «{collection}» · Ctrl + Z los devuelve</summary>
    public abstract string MovedManyTo(int count, string collection);
    /// <summary>No se pudo cambiar: {error}</summary>
    public abstract string ChangeFailed(string error);
    /// <summary>Color</summary>
    public abstract string Color { get; }
    /// <summary>Propio</summary>
    public abstract string OwnColor { get; }
    /// <summary>Otro color…</summary>
    public abstract string OtherColor { get; }
    /// <summary>Mover a la colección</summary>
    public abstract string MoveToCollection { get; }
    /// <summary>Nombre de la colección a la que moverlos</summary>
    public abstract string MoveToNewCollectionName { get; }
    /// <summary>No se pudieron mover: {error}</summary>
    public abstract string MoveFailed(string error);
    /// <summary>No se encuentra el vídeo de este bookmark:\n{video}</summary>
    public abstract string BookmarkVideoNotFound(string video);
    /// <summary>Buscarlo…</summary>
    public abstract string LocateIt { get; }
    /// <summary>Buscar «{name}»</summary>
    public abstract string LocateTitle(string name);
    /// <summary>No se pudo leer el archivo:\n{error}</summary>
    public abstract string CannotReadFile(string error);
    /// <summary>«{chosen}» no es el mismo vídeo que «{name}» (su contenido es distinto: otra copia, otra versión o un vídeo diferente). Los tiempos de los bookmarks podrían no coincidir.\n\n¿Usarlo igualmente? Los bookmarks de ese vídeo (en todas las colecciones) pasarán a este archivo; Ctrl + Z lo deshace.</summary>
    public abstract string NotSameVideo(string chosen, string name);
    /// <summary>Usarlo igualmente</summary>
    public abstract string UseItAnyway { get; }
    /// <summary>No se pudieron pasar todos: {error}</summary>
    public abstract string NotAllMoved(string error);
    /// <summary>{count} vídeos</summary>
    public abstract string VideosCountName(int count);
    /// <summary>Bookmark del tramo ampliado</summary>
    public abstract string BookmarkOfZoomedRange { get; }

    // ---- Pasos de deshacer (van detrás de «Deshecho:») ----

    /// <summary>añadir «{name}»</summary>
    public abstract string StepAdd(string name);
    /// <summary>editar «{name}»</summary>
    public abstract string StepEdit(string name);
    /// <summary>renombrar «{name}»</summary>
    public abstract string StepRename(string name);
    /// <summary>etiqueta de «{name}»</summary>
    public abstract string StepLabelOf(string name);
    /// <summary>etiqueta de {count} bookmarks</summary>
    public abstract string StepLabelOfMany(int count);
    /// <summary>color de «{name}»</summary>
    public abstract string StepColorOf(string name);
    /// <summary>color de {count} bookmarks</summary>
    public abstract string StepColorOfMany(int count);
    /// <summary>borrar «{name}»</summary>
    public abstract string StepDelete(string name);
    /// <summary>borrar {count} bookmarks</summary>
    public abstract string StepDeleteMany(int count);
    /// <summary>mover «{name}» a «{collection}»</summary>
    public abstract string StepMoveTo(string name, string collection);
    /// <summary>mover {count} bookmarks a «{collection}»</summary>
    public abstract string StepMoveManyTo(int count, string collection);
    /// <summary>mover el inicio de «{name}»</summary>
    public abstract string StepMoveStart(string name);
    /// <summary>mover el fin de «{name}»</summary>
    public abstract string StepMoveEnd(string name);
    /// <summary>mover «{name}»</summary>
    public abstract string StepMove(string name);
    /// <summary>pasar los bookmarks de «{from}» a «{to}»</summary>
    public abstract string StepRelink(string from, string to);
    /// <summary>importar bookmarks</summary>
    public abstract string StepImportBookmarks { get; }
    /// <summary>importar la colección «{name}»</summary>
    public abstract string StepImportCollection(string name);
    /// <summary>Bookmark {number}</summary>
    public abstract string DefaultBookmarkName(int number);
    /// <summary>ajustar «{name}»</summary>
    public abstract string StepAdjust(string name);
    /// <summary>asignar el inicio de «{name}»</summary>
    public abstract string StepSetStartOf(string name);
    /// <summary>asignar el fin de «{name}»</summary>
    public abstract string StepSetEndOf(string name);

    // ---- Etiquetas ----

    /// <summary>Ver solo los «{name}» (clic en otras para sumarlas). Clic derecho: renombrar, color, borrar…</summary>
    public abstract string LabelChipTip(string name);
    /// <summary>\n{shortcut} se la pone al bookmark seleccionado.</summary>
    public abstract string LabelChipShortcut(string shortcut);
    /// <summary>Sin etiqueta</summary>
    public abstract string NoLabel { get; }
    /// <summary>Ver solo los que no tienen etiqueta</summary>
    public abstract string NoLabelChipTip { get; }
    /// <summary>+ Etiqueta</summary>
    public abstract string AddLabelChip { get; }
    /// <summary>Nueva etiqueta (por ejemplo «Boss» o «Cinemática»), para saber de un vistazo qué marca cada bookmark</summary>
    public abstract string AddLabelChipTip { get; }
    /// <summary>Esta colección no tiene etiquetas: «+ Etiqueta» en el panel de bookmarks (Ctrl + B)</summary>
    public abstract string NoLabelsInCollection { get; }
    /// <summary>No hay etiqueta {number} (hay {count})</summary>
    public abstract string NoLabelNumber(int number, int count);
    /// <summary>Etiqueta «{name}»</summary>
    public abstract string LabelToast(string name);
    /// <summary>«{name}»: {label}</summary>
    public abstract string BookmarkWithLabel(string name, string label);
    /// <summary>Etiquetas</summary>
    public abstract string Labels { get; }
    /// <summary>Nueva etiqueta…</summary>
    public abstract string NewLabel { get; }
    /// <summary>Nombre de la nueva etiqueta</summary>
    public abstract string NewLabelName { get; }
    /// <summary>Ya hay una etiqueta «{name}»</summary>
    public abstract string LabelExists(string name);
    /// <summary>No se pudieron cambiar las etiquetas: {error}</summary>
    public abstract string LabelsChangeFailed(string error);
    /// <summary>sin bookmarks</summary>
    public abstract string NoBookmarks { get; }
    /// <summary>Renombrar…</summary>
    public abstract string RenameEllipsis { get; }
    /// <summary>Mover a la izquierda</summary>
    public abstract string MoveLeft { get; }
    /// <summary>Mover a la derecha</summary>
    public abstract string MoveRight { get; }
    /// <summary>Ponérsela a los de color {color.ToLower(Loc.Culture)} sin etiqueta ({count})</summary>
    public abstract string AdoptLabelColor(string color, int count);
    /// <summary>Ponérsela a los de su color sin etiqueta ({count})</summary>
    public abstract string AdoptLabelSameColor(int count);
    /// <summary>Borrar…</summary>
    public abstract string DeleteEllipsis { get; }
    /// <summary>= $"¿Borrar la etiqueta «{name}»?\n" + (uses == 1 ? "La lleva 1 bookmark: se queda sin etiqueta, con su color." : $"La llevan {uses} bookmarks: se quedan sin etiqueta, con su color.")</summary>
    public abstract string DeleteLabelQuestion(string name, int uses);
    /// <summary>crear la etiqueta «{name}»</summary>
    public abstract string StepCreateLabel(string name);
    /// <summary>renombrar la etiqueta «{name}»</summary>
    public abstract string StepRenameLabel(string name);
    /// <summary>color de la etiqueta «{name}»</summary>
    public abstract string StepLabelColor(string name);
    /// <summary>mover la etiqueta «{name}»</summary>
    public abstract string StepMoveLabel(string name);
    /// <summary>poner la etiqueta «{name}» a {count} bookmarks</summary>
    public abstract string StepAdoptLabel(string name, int count);
    /// <summary>borrar la etiqueta «{name}»</summary>
    public abstract string StepDeleteLabel(string name);
    /// <summary>Etiqueta o color</summary>
    public abstract string LabelOrColor { get; }
    /// <summary>+ Nueva etiqueta</summary>
    public abstract string AddLabelChipLong { get; }
    /// <summary>+ Nueva</summary>
    public abstract string AddLabelChipShort { get; }
    /// <summary>Crear una etiqueta y ponérsela</summary>
    public abstract string AddLabelChipNameTip { get; }
    /// <summary>{color} (sin etiqueta)</summary>
    public abstract string ColorWithoutLabel(string color);

    // ---- Colores de la paleta ----

    /// <summary>Rosa</summary>
    public abstract string ColorPink { get; }
    /// <summary>Rojo</summary>
    public abstract string ColorRed { get; }
    /// <summary>Naranja</summary>
    public abstract string ColorOrange { get; }
    /// <summary>Violeta</summary>
    public abstract string ColorViolet { get; }
    /// <summary>Cian</summary>
    public abstract string ColorCyan { get; }
    /// <summary>Lima</summary>
    public abstract string ColorLime { get; }
    /// <summary>Marrón</summary>
    public abstract string ColorBrown { get; }
    /// <summary>Gris</summary>
    public abstract string ColorGray { get; }
    /// <summary>Blanco</summary>
    public abstract string ColorWhite { get; }

    // ---- Compartir colecciones y bookmarks ----

    /// <summary>La colección «{name}» no tiene bookmarks</summary>
    public abstract string CollectionHasNoBookmarks(string name);
    /// <summary>Exportar la colección</summary>
    public abstract string ExportCollectionTitle { get; }
    /// <summary>Colección de bookmarks|*.collection.json</summary>
    public abstract string CollectionFileFilter { get; }
    /// <summary>Colección «{name}» exportada</summary>
    public abstract string CollectionExported(string name);
    /// <summary>No se pudo guardar la colección:\n{error}</summary>
    public abstract string CollectionSaveFailed(string error);
    /// <summary>Importar una colección o los bookmarks de un vídeo</summary>
    public abstract string ImportTitle { get; }
    /// <summary>Colecciones y bookmarks|*.collection.json;*.bookmarks.json|Todos los JSON|*.json</summary>
    public abstract string ImportFileFilter { get; }
    /// <summary>«{file}» no es un archivo de bookmarks</summary>
    public abstract string NotBookmarksFile(string file);
    /// <summary>No se pudo leer «{file}»: {error}</summary>
    public abstract string CannotReadNamed(string file, string error);
    /// <summary>«{file}» no tiene bookmarks</summary>
    public abstract string FileHasNoBookmarks(string file);
    /// <summary>No se sabe de qué vídeo son: abre el vídeo y vuelve a arrastrarlos</summary>
    public abstract string UnknownVideo { get; }
    /// <summary>No se pudieron importar: {error}</summary>
    public abstract string ImportFailed(string error);
    /// <summary>Ya tenías estos bookmarks</summary>
    public abstract string AlreadyHadBookmarks { get; }
    /// <summary>= count == 1 ? "1 bookmark añadido a este vídeo" : $"{count} bookmarks añadidos a este vídeo"</summary>
    public abstract string BookmarksAddedToThisVideo(int count);
    /// <summary>= (count == 1 ? "1 bookmark añadido" : $"{count} bookmarks añadidos") + $" a «{video}» en la colección «{collection}»"</summary>
    public abstract string BookmarksAddedTo(int count, string video, string collection);
    /// <summary>Importada</summary>
    public abstract string ImportedCollectionName { get; }
    /// <summary>No se pudo importar la colección: {error}</summary>
    public abstract string ImportCollectionFailed(string error);
    /// <summary>Colección «{name}» añadida: {videos}, {bookmarks}</summary>
    public abstract string CollectionAdded(string name, string videos, string bookmarks);

    // ---- Exportar clips ----

    /// <summary>Exportar clip</summary>
    public abstract string ExportClip { get; }
    /// <summary>Exportar clip de «{region}» / del tramo ampliado (menú de la imagen)</summary>
    public abstract string ExportClipOf(string? region);
    /// <summary>Original exacto</summary>
    public abstract string ExactOriginal { get; }
    /// <summary>Mismo códec y bitrate, cortado en el frame exacto</summary>
    public abstract string ExactOriginalTip { get; }
    /// <summary>Original sin recodificar · {start}</summary>
    public abstract string CopyOriginal(string start);
    /// <summary>Copia directa: instantánea y sin pérdida, pero empieza en un keyframe</summary>
    public abstract string CopyOriginalTip { get; }
    /// <summary>Ligero · {rate} MB/s</summary>
    public abstract string LightClipMenu(string rate);
    /// <summary>MP4 (H.264 y AAC) para chats y Jira, como mucho 1080p</summary>
    public abstract string LightClipMenuTip { get; }
    /// <summary>empieza en el keyframe anterior</summary>
    public abstract string StartsAtPreviousKeyframe { get; }
    /// <summary>corte exacto</summary>
    public abstract string ExactCut { get; }
    /// <summary>empieza {seconds} s antes</summary>
    public abstract string StartsSecondsEarlier(string seconds);
    /// <summary>= frames == 1 ? "empieza 1 frame antes" : $"empieza {frames} frames antes"</summary>
    public abstract string StartsFramesEarlier(int frames);
    /// <summary>Para exportar un clip, elige una región o marca antes un tramo (I / O, Z o arrastrando con el botón derecho)</summary>
    public abstract string ExportNeedsRange { get; }
    /// <summary>Exportar {what}</summary>
    public abstract string ExportWhat(string what);
    /// <summary>«{name}»</summary>
    public abstract string QuotedName(string name);
    /// <summary>Clip en cola: se exporta al acabar el anterior</summary>
    public abstract string ClipQueued { get; }
    /// <summary>Exportando el clip…</summary>
    public abstract string ExportingClip { get; }
    /// <summary>Exportación cancelada</summary>
    public abstract string ExportCancelled { get; }
    /// <summary>Clip guardado</summary>
    public abstract string ClipSaved { get; }
    /// <summary>No se pudo exportar el clip</summary>
    public abstract string ClipFailed { get; }
    /// <summary>En {folder}</summary>
    public abstract string InFolder(string folder);
    /// <summary>Copiado al portapapeles: Ctrl + V lo pega en un chat o en Jira</summary>
    public abstract string CopiedForChat { get; }
    /// <summary>Copiado al portapapeles: Ctrl + V lo pega en un chat o en una carpeta</summary>
    public abstract string CopiedForFolder { get; }
    /// <summary>No se pudo copiar al portapapeles</summary>
    public abstract string CopyFailed { get; }
    /// <summary>Copiar</summary>
    public abstract string Copy { get; }
    /// <summary>Exportando {percent} %</summary>
    public abstract string ExportingPercent(int percent);
    /// <summary>Preparando el clip</summary>
    public abstract string PreparingClip { get; }
    /// <summary>\ny {count} más en cola</summary>
    public abstract string MoreQueued(int count);
    /// <summary>\nClic para cancelar</summary>
    public abstract string ClickToCancel { get; }
    /// <summary>Clip listo</summary>
    public abstract string ClipReady { get; }
    /// <summary>Error al exportar</summary>
    public abstract string ExportError { get; }
    /// <summary>\nClic para ir a él</summary>
    public abstract string ClickToGo { get; }
    /// <summary>Clic para ver por qué</summary>
    public abstract string ClickToSeeWhy { get; }
    /// <summary>Cancelar {name}</summary>
    public abstract string CancelClip(string name);
    /// <summary>el clip</summary>
    public abstract string TheClip { get; }
    /// <summary>Cancelar también los {count} en cola</summary>
    public abstract string CancelQueuedToo(int count);
    /// <summary>Copiar al portapapeles</summary>
    public abstract string CopyToClipboard { get; }
    /// <summary>Clip copiado al portapapeles</summary>
    public abstract string ClipCopied { get; }
    /// <summary>Clips anteriores</summary>
    public abstract string PreviousClips { get; }
    /// <summary>Quitar de aquí</summary>
    public abstract string RemoveFromHere { get; }
    /// <summary>Se está exportando un clip. Si sales ahora, se cancela.</summary>
    public abstract string ExportingOnExit { get; }
    /// <summary>Salir</summary>
    public abstract string Quit { get; }
    /// <summary>Esperar</summary>
    public abstract string Wait { get; }
    /// <summary>Falta ffmpeg.exe junto al programa.</summary>
    public abstract string FfmpegMissing { get; }
    /// <summary>ffmpeg no pudo exportar el tramo.</summary>
    public abstract string FfmpegFailed { get; }
    /// <summary>No se pudo lanzar ffmpeg.</summary>
    public abstract string FfmpegNotStarted { get; }
    /// <summary>ffmpeg terminó con el código {code}.</summary>
    public abstract string FfmpegExitCode(int code);
    /// <summary> (ligero)</summary>
    public abstract string LightClipSuffix { get; }

    // ---- Otro color ----

    /// <summary>Otro color</summary>
    public abstract string OtherColorTitle { get; }
    /// <summary>Usar este color</summary>
    public abstract string UseThisColor { get; }
    /// <summary>ámbar del tramo ampliado</summary>
    public abstract string ReservedRangeAmber { get; }
    /// <summary>azul de lo reproducido</summary>
    public abstract string ReservedPlayedBlue { get; }
    /// <summary>verde de la caché de frames</summary>
    public abstract string ReservedCacheGreen { get; }
    /// <summary>Este color se parece mucho al {clash} de las barras, y se confundirán.\n\n¿Usarlo igualmente?</summary>
    public abstract string ColorClash(string clash);

    // ---- Avisos ----

    /// <summary>Aceptar</summary>
    public abstract string Ok { get; }

    // ---- Abrir con doble clic (registro de Windows) ----

    /// <summary>Reproductor de vídeo</summary>
    public abstract string AppDescription { get; }
    /// <summary>Vídeo {extension}</summary>
    public abstract string VideoFileType(string extension);

    /// <summary>Nombre de un idioma de audio o subtítulos (por su código de Data.Languages).</summary>
    public abstract string LanguageName(string code);
}
