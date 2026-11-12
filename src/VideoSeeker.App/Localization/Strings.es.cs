// Textos en español.

namespace VideoSeeker.App.Localization;

public sealed class SpanishStrings : Strings
{
    // ---- Teclas (para componer atajos) ----

    public override string KeyCtrl => "Ctrl";
    public override string KeyShift => "Shift";
    public override string KeyAlt => "Alt";
    public override string KeyDelete => "Supr";
    public override string KeyEsc => "Esc";

    // ---- Ventana principal ----

    public override string EmptyHint => "Arrastra un vídeo aquí o pulsa Ctrl + O";
    public override string CloseZoomTip => "Cerrar zoom (Esc)";
    public override string RangeLoopTip(string? region) => $"Bucle {ZoneOf(region)} (Shift + R)";
    public override string RangeLoopOffTip(string? region) => $"Quitar el bucle {ZoneOf(region)} (Shift + R)";
    public override string MarkLastTip(string? region) => $"Asignar el frame en pantalla al fin {ZoneOf(region)} (O)";
    public override string MarkFirstTip(string? region) => $"Asignar el frame en pantalla al inicio {ZoneOf(region)} (I)";
    public override string VideoLoopTip => "Bucle del vídeo (R)";
    public override string VideoLoopOffTip => "Quitar el bucle del vídeo (R)";
    public override string PreviousFrameTip => "Frame anterior (,) · rueda: frame a frame";
    public override string NextFrameTip => "Frame siguiente (.) · rueda: frame a frame";
    public override string PlayTip => "Reproducir / pausar (Espacio). J / K / L: atrás / pausa / adelante · rueda: frame a frame";
    public override string SpeedTip => "Velocidad · rueda: ±0,1 · botón central: 1×";
    public override string OpenVideoTip => "Abrir vídeo (Ctrl + O)";
    public override string RangePositionTip => "Respecto al tramo ampliado";
    public override string CurrentBookmarkTip => "Bookmark en el que se está ([ / ] anterior / siguiente, 1–9 y 0 para ir a los diez primeros)";
    public override string BookmarksButtonTip => "Panel de bookmarks (Ctrl + B) · añadir: B";
    public override string FullscreenTip => "Pantalla completa (F)";
    public override string SettingsTip => "Ajustes (Ctrl + ,)";
    public override string TracksTip => "Audio y subtítulos (A / S)";
    public override string VolumeTip => "Volumen";
    public override string MuteTip => "Silenciar (M)";
    public override string CloseBookmarkPanelTip => "Cerrar (Ctrl + B)";
    public override string ExportVideoBookmarksTip => "Exportar los bookmarks de este vídeo, para compartirlos";
    public override string ExportCollectionTip => "Exportar la colección entera, para compartirla";
    public override string BookmarksTitle => "Bookmarks";
    public override string CollectionTip => "Colección de bookmarks: cada una tiene los suyos de cada vídeo. Cambiar, crear, renombrar o borrar.";
    public override string SearchPlaceholder => "Buscar (Ctrl + F)";
    public override string ScopeThisVideo => "Este vídeo";
    public override string ScopeAllVideos => "Todos los vídeos";
    public override string PanelHint => "B crea una región con el tramo ampliado o, sin él, de un frame (O fija su fin). Clic o Enter: ir y ampliarla. Ctrl / Shift + clic: varios. F2 renombra, Supr borra, Ctrl + Z deshace.";
    public override string BookmarkKeyTip => "Tecla para ir a este bookmark";
    public override string NameHint => "Enter guarda · Esc deja el nombre como estaba";
    public override string NameHintWithLabels => "Enter guarda · Esc cancela · Alt + número: etiqueta";
    public override string GeneratingThumbnail => "Generando miniatura…";
    public override string Close => "Cerrar";
    public override string ShowInFolder => "Mostrar en la carpeta";
    public override string Open => "Abrir";
    public override string ZoneOf(string? region) => region is null ? "del tramo" : $"de «{region}»";
    public override string SaveZoneTip => "Guardar el tramo ampliado como bookmark (B)";
    public override string NewLabelTitle => "Nueva etiqueta";
    public override string NewLabelBoxTip => "Enter la crea y la pone · Esc vuelve";
    public override string ZoneNameTip(string name) => $"La zona es la región «{name}»: I / O, ⇤ ⇥ y las banderitas la cambian";
    public override string CloseZoomDeselectTip(string name) => $"Cerrar zoom y deseleccionar «{name}» (Esc)";
    public override string Loading => "Cargando…";

    // ---- Ajustes ----

    public override string SettingsTitle => "Ajustes — VideoSeeker";
    public override string ResetDefaults => "Restablecer valores por defecto";
    public override string SupportTip => "VideoSeeker es gratis. Si te resulta útil, puedes invitar al autor a un café.";
    public override string Support => "Apoyar";
    public override string SourceCode => "Código fuente";
    public override string SourceCodeTip => "Software libre, con licencia GPL v3.";
    public override string Version(string version) => $"Versión {version}";
    public override string SectionBars => "BARRAS";
    public override string MoveRegionsTip => "Arrastrar el centro del tramo ampliado o de una región de bookmark la desplaza entera. Desactivado, mueve el cursor de la barra.";
    public override string MoveRegions => "Mover regiones enteras arrastrando su centro";
    public override string EdgeFrameTip => "Al arrastrar un extremo del tramo ampliado o de un bookmark. Desactivado, la imagen no cambia.";
    public override string EdgeFrame => "Ver el frame del extremo que se arrastra";
    public override string CachedLineTip => "Marca bajo las barras lo que ya está en la caché: ahí se salta al instante.";
    public override string CachedLine => "Línea verde de la caché de frames";
    public override string MarksTip => "Atajo: Ctrl + Shift + B.";
    public override string Marks => "Marcas de los bookmarks";
    public override string PulseMarksTip => "Con las marcas ocultas, al crear un bookmark o ir a uno su botón (abajo a la derecha) parpadea unas veces. No sale ningún mensaje.";
    public override string PulseMarks => "Hacer parpadear su botón si están ocultas";
    public override string SectionNavigation => "NAVEGACIÓN";
    public override string SecondsUnit => "s";
    public override string SeekTip => "Segundos que salta cada flecha, y la rueda sobre la barra principal: sola, con Shift o con Alt.";
    public override string Seek => "Saltos con ← →";
    public override string ScrubTip => "Píxeles que hay que arrastrar sobre la imagen, con el botón pulsado, para avanzar un frame.";
    public override string Scrub => "Sensibilidad del scrubbing";
    public override string PixelsPerFrame(int pixels) => $"{pixels} px por frame";
    public override string WheelSpeed => "Velocidad";
    public override string WheelVolume => "Volumen";
    public override string WheelTip => "Velocidad: ±0,1× (botón central: 1×). Volumen: ±5 %.";
    public override string Wheel => "Rueda del ratón sobre el vídeo";
    public override string Resume => "Reanudar cada vídeo donde se dejó";
    public override string SectionTimeAndBookmarks => "TIEMPO Y BOOKMARKS";
    public override string TimeFormatTip => "En 00:01:23:11, el último número es el frame dentro de cada segundo.";
    public override string TimeFormat => "Formato del tiempo";
    public override string NewColorTip => "Sin etiqueta, mientras no se elija otro (o siempre, si no se repite lo último).";
    public override string NewColor => "Color de los bookmarks nuevos";
    public override string RepeatLabelTip => "El nuevo lleva la etiqueta o el color que se eligió la última vez; en el cuadro del nombre se cambia.";
    public override string RepeatLabel => "Repetir la última etiqueta o color";
    public override string TimesInPreview => "Vista previa";
    public override string TimesInList => "Lista";
    public override string BookmarkTimesTip => "En la lista se ven todos de un vistazo. En la vista previa, la lista deja más sitio al nombre.";
    public override string BookmarkTimes => "Tiempos de los bookmarks";
    public override string SectionAudioAndSubtitles => "AUDIO Y SUBTÍTULOS";
    public override string AudioLanguageTip => "Se elige esa pista si el vídeo trae varias. Se aplica al abrir el siguiente vídeo.";
    public override string AudioLanguage => "Idioma preferido del audio";
    public override string SubtitleLanguageTip => "Se elige esa pista al activar los subtítulos. Se aplica al abrir el siguiente vídeo.";
    public override string SubtitleLanguage => "Idioma preferido de los subtítulos";
    public override string AppliesOnNextVideo => "Se aplica al abrir el siguiente vídeo.";
    public override string SubtitlesOnOpen => "Activar los subtítulos al abrir un vídeo";
    public override string NoPreference => "Sin preferencia";
    public override string SectionPerformance => "RENDIMIENTO";
    public override string CacheMemory => "Memoria de la caché de frames";
    public override string CacheMemoryTip => "Cuanta más, más tiempo de vídeo se puede recorrer frame a frame al instante. Se aplica al abrir el siguiente vídeo.";
    public override string CacheOfTotal(string used, string total) => $"{used} de {total} GB";
    public override string GigabytesUnit => "GB";
    public override string MegabytesUnit => "MB";
    public override string CacheTooBigTip => "Más del 90 % de la memoria de la tarjeta: puede faltar para otros programas.";
    public override string HardwareDecodingTip => "Reproduce con la tarjeta gráfica. Desactívala solo si un vídeo se ve mal.";
    public override string HardwareDecoding => "Decodificación por hardware";
    public override string SectionClips => "EXPORTAR CLIPS";
    public override string ChangeEllipsis => "Cambiar…";
    public override string BesideVideo => "Junto al vídeo";
    public override string ClipFolderTip => "Dónde se guardan los clips exportados. Si no se puede escribir allí, van a Vídeos\\VideoSeeker.";
    public override string ClipFolder => "Carpeta de destino";
    public override string BesideEachVideo => "Junto a cada vídeo";
    public override string ClipFolderDialogTitle => "Carpeta para los clips exportados";
    public override string ClipClipboardTip => "Como archivo, para pegarlo directamente en un chat, en Jira o en una carpeta.";
    public override string ClipClipboard => "Copiar cada clip al portapapeles";
    public override string LightClipTip => "Megas por cada segundo de clip, con el audio. Nunca más que el vídeo original.";
    public override string LightClip => "Tamaño del clip ligero";
    public override string MegabytesPerSecond(string value) => $"{value} MB/s";
    public override string SectionGeneral => "GENERAL";
    public override string InterfaceLanguage => "Idioma";
    public override string InterfaceLanguageTip => "El idioma de VideoSeeker. Automático: el de Windows. El cambio se aplica al reiniciar.";
    public override string AnimatePanels => "Animar los paneles";
    public override string AnimatePanelsTip => "El panel de bookmarks y la barra del zoom se abren y se cierran deslizándose, y el zoom pasa suavemente de un tramo a otro.";
    public override string AutomaticLanguage(string language) => $"Automático · {language}";
    public override string LanguageChangedRestart => "El nuevo idioma se usará al reiniciar VideoSeeker.";
    public override string LanguageCaption => "Idioma";
    public override string RestartNow => "Reiniciar ahora";
    public override string Later => "Más tarde";
    public override string DefaultFolder => "Por defecto";
    public override string DefaultFolderTip => "La carpeta data, junto al programa.";
    public override string DataFolderTip => "Ajustes, bookmarks y posición de cada vídeo. El cambio se aplica al reiniciar.";
    public override string DataFolder => "Carpeta de datos";
    public override string ChooseInWindows => "Elegir en Windows";
    public override string Unregister => "Quitar";
    public override string DefaultPlayer => "Reproductor de vídeo predeterminado";
    public override string DefaultPlayerTip => "Añade los tipos de vídeo a «Abrir con» y lleva a Aplicaciones predeterminadas de Windows, donde se elige para cuáles.";
    public override string NumberRange => "Entre 0,04 y 3600 segundos";
    public override string PendingFolder(string folder) => $"Al reiniciar se usará {folder}.";
    public override string PendingFolderWithCopy(string folder) => $"Al reiniciar se usará {folder}, con una copia de los datos actuales.";
    public override string DataFolderDialogTitle => "Carpeta para los datos de VideoSeeker";
    public override string DataFolderNested => "La carpeta de datos no puede estar dentro de la actual ni contenerla.";
    public override string DataFolderHasData(string folder) => $"{folder}\n\nya tiene datos de VideoSeeker. Si copias allí los actuales, los que había se guardan en su subcarpeta «anteriores».";
    public override string UseThatData => "Usar esos datos";
    public override string CopyCurrentData => "Copiar los actuales";
    public override string MoveDataQuestion(string folder) => $"¿Llevar tus datos (ajustes, bookmarks, posición de cada vídeo) a\n{folder}?\n\nLa carpeta actual se queda como está.";
    public override string MoveData => "Llevar los datos";
    public override string StartEmpty => "Empezar vacía";
    public override string Cancel => "Cancelar";
    public override string CannotUseFolder(string error) => $"No se puede usar esa carpeta:\n{error}";
    public override string DataFolderRestart => "La nueva carpeta de datos se usará al reiniciar VideoSeeker.";
    public override string OpenWithDoubleClick => "Abrir con doble clic";
    public override string RegisterFailed(string error) => $"No se pudieron registrar los tipos de vídeo:\n{error}";
    public override string UnregisterFailed(string error) => $"No se pudo quitar el registro:\n{error}";
    public override string ResetQuestion => "Se volverán a poner los valores por defecto de todos los ajustes.";
    public override string Reset => "Restablecer";
    public override string ResetTitle => "Restablecer ajustes";
    public override string DefaultFolderPackagedTip => "La carpeta de la app en Windows. Se borra al desinstalarla.";
    public override string HoverPreview => "Miniatura al pasar el ratón por las barras";
    public override string HoverPreviewTip => "Para ver qué hay en un punto del vídeo sin ir a él.";
    public override string DataCopyIncomplete(string file) => $"La copia de {file} no salió entera";

    // ---- Ventana principal: reproducción ----

    public override string VideoFileFilter => "Vídeos|*.mkv;*.mp4;*.m4v;*.avi;*.wmv;*.mov;*.webm;*.flv;*.ts;*.m2ts;*.mts;*.mpg;*.mpeg;*.vob;*.3gp;*.ogv|Todos los archivos|*.*";
    public override string OpenVideoTitle => "Abrir vídeo";
    public override string FileNotFound(string path) => $"No se encuentra el archivo:\n{path}";
    public override string FrameNumber(int frame) => $"frame {frame}";
    public override string FrameNumberApprox(string frame) => $"frame ~{frame}";
    public override string Frames(int count) => count == 1 ? "1 frame" : $"{count} frames";
    public override string FrameOfRange(int frame, int count) => $"frame {frame}/{count}";
    public override string VideoLoopOn => "Bucle del vídeo";
    public override string VideoLoopOff => "Sin bucle del vídeo";
    public override string RangeLoopOn(string? region) => $"Bucle {ZoneOf(region)}";
    public override string RangeLoopOff(string? region) => $"Sin bucle {ZoneOf(region)}";
    public override string ZoomSection => "Ampliar sección (±5 s)";
    public override string MarkFirstFromScreen(int frame, string? region) => $"Asignar el frame en pantalla ({frame}) al inicio {ZoneOf(region)}";
    public override string MarkLastFromScreen(int frame, string? region) => $"Asignar el frame en pantalla ({frame}) al fin {ZoneOf(region)}";
    public override string MarkFirstFromCursor(int frame, string? region) => $"Asignar el frame bajo el cursor ({frame}) al inicio {ZoneOf(region)}";
    public override string MarkLastFromCursor(int frame, string? region) => $"Asignar el frame bajo el cursor ({frame}) al fin {ZoneOf(region)}";
    public override string NormalSpeed => "1× (normal)";
    public override string SpeedToast(string speed) => $"Velocidad {speed}";
    public override string VolumeToast(int volume) => $"Volumen {volume} %";
    public override string VolumeMutedToast(int volume) => $"Volumen {volume} % (silenciado)";
    public override string Audio => "Audio";
    public override string Subtitles => "Subtítulos";
    public override string SubtitlesOff => "Desactivados";
    public override string NoTracks => "Sin pistas";
    public override string Track(long id) => $"Pista {id}";
    public override string ControlsPinned => "Controles fijos";
    public override string ControlsHidden => "Controles ocultos: lleva el ratón abajo para verlos";
    public override string ControlsPinnedTip => "Controles fijos (Tab)";
    public override string PinControlsTip => "Fijar los controles (Tab)";
    public override string VideoGone(string name) => $"Ya no está: {name}";
    public override string HistoryBackTip => "Volver al vídeo que se abrió antes (botón lateral del ratón)";
    public override string HistoryForwardTip => "Ir al vídeo que se abrió después (botón lateral del ratón)";
    public override string HistoryAllBack(int count) => $"Clic derecho: los {count} anteriores";
    public override string HistoryAllForward(int count) => $"Clic derecho: los {count} siguientes";

    // ---- Bookmarks ----

    public override string NewBookmark => "Nuevo bookmark";
    public override string RenameBookmark => "Renombrar bookmark";
    public override string SelectBookmarkFirst => "Selecciona antes un bookmark (clic en su marca o en el panel)";
    public override string BookmarkDeleted(string name) => $"Borrado «{name}» · Ctrl + Z lo recupera";
    public override string BookmarksDeleted(int count) => $"Borrados {count} bookmarks · Ctrl + Z los recupera";
    public override string UndoFailed(string error) => $"No se pudo deshacer: {error}";
    public override string RedoFailed(string error) => $"No se pudo rehacer: {error}";
    public override string NothingToUndo => "Nada que deshacer";
    public override string NothingToRedo => "Nada que rehacer";
    public override string Undone(string step) => $"Deshecho: {step}";
    public override string Redone(string step) => $"Rehecho: {step}";
    public override string InVideo(string video) => $" (en «{video}»)";
    public override string VideoHasNoBookmarks => "Este vídeo no tiene bookmarks (B añade uno)";
    public override string NoBookmarkNumber(int number, int count) => $"No hay bookmark {number} (hay {count})";
    public override string NoMoreBookmarksAhead => "No hay más bookmarks por delante";
    public override string NoMoreBookmarksBehind => "No hay más bookmarks por detrás";
    public override string AddBookmarkHere => "Añadir bookmark aquí";
    public override string BookmarksPanel => "Panel de bookmarks";
    public override string Rename => "Renombrar";
    public override string UseFrameAsThumbnail(int frame, string? region) => region is null ? $"Usar el frame actual ({frame}) como miniatura" : $"Usar el frame actual ({frame}) como miniatura de «{region}»";
    public override string FrameOutsideRegion(int frame, string time, string name, string range) => $"El frame actual ({frame}, {time}) está fuera de la región «{name}» ({range}).\n\n¿Usarlo igualmente como miniatura?";
    public override string UseIt => "Usarlo";
    public override string ResetThumbnail(string? region) => region is null ? "Volver a la miniatura del primer frame" : $"Volver a la miniatura del primer frame de «{region}»";
    public override string Delete => "Borrar";
    public override string DeleteCount(int count) => $"Borrar los {count}";
    public override string BookmarksExported(int count) => count == 1 ? "1 bookmark exportado" : $"{count} bookmarks exportados";
    public override string CannotWriteBesideVideo(string error) => $"No se pudo escribir junto al vídeo:\n{error}";
    public override string MarksShown => "Marcas de bookmarks visibles";
    public override string MarksHidden => "Marcas de bookmarks ocultas (Ctrl + Shift + B las muestra)";
    public override string HideMarksTip => "Ocultar las marcas de bookmarks en las barras (Ctrl + Shift + B). El panel sigue funcionando.";
    public override string ShowMarksTip => "Mostrar las marcas de bookmarks en las barras (Ctrl + Shift + B)";
    public override string CollectionButton(string name) => $"Colección: {name}  ▾";
    public override string CollectionEmpty => "vacía";
    public override string BookmarksInVideos(int bookmarks, int videos) => videos == 1 ? $"{bookmarks} en 1 vídeo" : $"{bookmarks} en {videos} vídeos";
    public override string NewCollection => "Nueva colección…";
    public override string NewCollectionName => "Nombre de la nueva colección";
    public override string CollectionExists(string name) => $"Ya hay una colección «{name}»";
    public override string RenameCollection(string name) => $"Renombrar «{name}»…";
    public override string NewNameFor(string name) => $"Nuevo nombre para «{name}»";
    public override string RenameFailed(string error) => $"No se pudo renombrar: {error}";
    public override string CollectionRenamed(string name) => $"Colección renombrada a «{name}»";
    public override string DeleteCollection(string name) => $"Borrar «{name}»…";
    public override string ImportCollectionOrBookmarks => "Importar colección o bookmarks…";
    public override string ExportCollectionMenu(string name) => $"Exportar «{name}»…";
    public override string CollectionIsEmpty => "Está vacía.";
    public override string CollectionWillBeLost(int bookmarks, int videos) => videos == 1 ? $"Tiene {bookmarks} bookmarks de 1 vídeo y se perderán." : $"Tiene {bookmarks} bookmarks de {videos} vídeos y se perderán.";
    public override string DeleteCollectionQuestion(string name, string detail) => $"¿Borrar la colección «{name}»?\n{detail}";
    public override string DeleteFailed(string error) => $"No se pudo borrar: {error}";
    public override string CollectionToast(string name) => $"Colección «{name}»";
    public override string BookmarkCount(int count) => count == 1 ? "1 bookmark" : $"{count} bookmarks";
    public override string VideoCount(int count) => count == 1 ? "1 vídeo" : $"{count} vídeos";
    public override string BookmarksFromVideos(int bookmarks, int videos) => $"{bookmarks} bookmarks de {videos} vídeos";
    public override string ShowVideoInExplorer => "Mostrar el vídeo en el Explorador";
    public override string ShowVideoInExplorerMissing => "Mostrar el vídeo en el Explorador (no se encuentra)";
    public override string MovedTo(string collection) => $"Movido a «{collection}» · Ctrl + Z lo devuelve";
    public override string MovedManyTo(int count, string collection) => $"Movidos {count} a «{collection}» · Ctrl + Z los devuelve";
    public override string ChangeFailed(string error) => $"No se pudo cambiar: {error}";
    public override string Color => "Color";
    public override string OwnColor => "Propio";
    public override string OtherColor => "Otro color…";
    public override string MoveToCollection => "Mover a la colección";
    public override string MoveToNewCollectionName => "Nombre de la colección a la que moverlos";
    public override string MoveFailed(string error) => $"No se pudieron mover: {error}";
    public override string BookmarkVideoNotFound(string video) => $"No se encuentra el vídeo de este bookmark:\n{video}";
    public override string LocateIt => "Buscarlo…";
    public override string LocateTitle(string name) => $"Buscar «{name}»";
    public override string CannotReadFile(string error) => $"No se pudo leer el archivo:\n{error}";
    public override string NotSameVideo(string chosen, string name) => $"«{chosen}» no es el mismo vídeo que «{name}» (su contenido es distinto: otra copia, otra versión o un vídeo diferente). Los tiempos de los bookmarks podrían no coincidir.\n\n¿Usarlo igualmente? Los bookmarks de ese vídeo (en todas las colecciones) pasarán a este archivo; Ctrl + Z lo deshace.";
    public override string UseItAnyway => "Usarlo igualmente";
    public override string NotAllMoved(string error) => $"No se pudieron pasar todos: {error}";
    public override string VideosCountName(int count) => $"{count} vídeos";
    public override string BookmarkOfZoomedRange => "Bookmark del tramo ampliado";

    // ---- Pasos de deshacer (van detrás de «Deshecho:») ----

    public override string StepAdd(string name) => $"añadir «{name}»";
    public override string StepEdit(string name) => $"editar «{name}»";
    public override string StepRename(string name) => $"renombrar «{name}»";
    public override string StepLabelOf(string name) => $"etiqueta de «{name}»";
    public override string StepLabelOfMany(int count) => $"etiqueta de {count} bookmarks";
    public override string StepColorOf(string name) => $"color de «{name}»";
    public override string StepColorOfMany(int count) => $"color de {count} bookmarks";
    public override string StepDelete(string name) => $"borrar «{name}»";
    public override string StepDeleteMany(int count) => $"borrar {count} bookmarks";
    public override string StepMoveTo(string name, string collection) => $"mover «{name}» a «{collection}»";
    public override string StepMoveManyTo(int count, string collection) => $"mover {count} bookmarks a «{collection}»";
    public override string StepMoveStart(string name) => $"mover el inicio de «{name}»";
    public override string StepMoveEnd(string name) => $"mover el fin de «{name}»";
    public override string StepMove(string name) => $"mover «{name}»";
    public override string StepRelink(string from, string to) => $"pasar los bookmarks de «{from}» a «{to}»";
    public override string StepImportBookmarks => "importar bookmarks";
    public override string StepImportCollection(string name) => $"importar la colección «{name}»";
    public override string DefaultBookmarkName(int number) => $"Bookmark {number}";
    public override string StepAdjust(string name) => $"ajustar «{name}»";
    public override string StepSetStartOf(string name) => $"asignar el inicio de «{name}»";
    public override string StepSetEndOf(string name) => $"asignar el fin de «{name}»";

    // ---- Etiquetas ----

    public override string LabelChipTip(string name) => $"Ver solo los «{name}» (clic en otras para sumarlas). Clic derecho: renombrar, color, borrar…";
    public override string LabelChipShortcut(string shortcut) => $"\n{shortcut} se la pone al bookmark seleccionado.";
    public override string NoLabel => "Sin etiqueta";
    public override string NoLabelChipTip => "Ver solo los que no tienen etiqueta";
    public override string AddLabelChip => "+ Etiqueta";
    public override string AddLabelChipTip => "Nueva etiqueta (por ejemplo «Boss» o «Cinemática»), para saber de un vistazo qué marca cada bookmark";
    public override string NoLabelsInCollection => "Esta colección no tiene etiquetas: «+ Etiqueta» en el panel de bookmarks (Ctrl + B)";
    public override string NoLabelNumber(int number, int count) => $"No hay etiqueta {number} (hay {count})";
    public override string LabelToast(string name) => $"Etiqueta «{name}»";
    public override string BookmarkWithLabel(string name, string label) => $"«{name}»: {label}";
    public override string Labels => "Etiquetas";
    public override string NewLabel => "Nueva etiqueta…";
    public override string NewLabelName => "Nombre de la nueva etiqueta";
    public override string LabelExists(string name) => $"Ya hay una etiqueta «{name}»";
    public override string LabelsChangeFailed(string error) => $"No se pudieron cambiar las etiquetas: {error}";
    public override string NoBookmarks => "sin bookmarks";
    public override string RenameEllipsis => "Renombrar…";
    public override string MoveLeft => "Mover a la izquierda";
    public override string MoveRight => "Mover a la derecha";
    public override string AdoptLabelColor(string color, int count) => $"Ponérsela a los de color {color.ToLower(Loc.Culture)} sin etiqueta ({count})";
    public override string AdoptLabelSameColor(int count) => $"Ponérsela a los de su color sin etiqueta ({count})";
    public override string DeleteEllipsis => "Borrar…";
    public override string DeleteLabelQuestion(string name, int uses) => $"¿Borrar la etiqueta «{name}»?\n" + (uses == 1 ? "La lleva 1 bookmark: se queda sin etiqueta, con su color." : $"La llevan {uses} bookmarks: se quedan sin etiqueta, con su color.");
    public override string StepCreateLabel(string name) => $"crear la etiqueta «{name}»";
    public override string StepRenameLabel(string name) => $"renombrar la etiqueta «{name}»";
    public override string StepLabelColor(string name) => $"color de la etiqueta «{name}»";
    public override string StepMoveLabel(string name) => $"mover la etiqueta «{name}»";
    public override string StepAdoptLabel(string name, int count) => $"poner la etiqueta «{name}» a {count} bookmarks";
    public override string StepDeleteLabel(string name) => $"borrar la etiqueta «{name}»";
    public override string LabelOrColor => "Etiqueta o color";
    public override string AddLabelChipLong => "+ Nueva etiqueta";
    public override string AddLabelChipShort => "+ Nueva";
    public override string AddLabelChipNameTip => "Crear una etiqueta y ponérsela";
    public override string ColorWithoutLabel(string color) => $"{color} (sin etiqueta)";

    // ---- Colores de la paleta ----

    public override string ColorPink => "Rosa";
    public override string ColorRed => "Rojo";
    public override string ColorOrange => "Naranja";
    public override string ColorViolet => "Violeta";
    public override string ColorCyan => "Cian";
    public override string ColorLime => "Lima";
    public override string ColorBrown => "Marrón";
    public override string ColorGray => "Gris";
    public override string ColorWhite => "Blanco";

    // ---- Compartir colecciones y bookmarks ----

    public override string CollectionHasNoBookmarks(string name) => $"La colección «{name}» no tiene bookmarks";
    public override string ExportCollectionTitle => "Exportar la colección";
    public override string CollectionFileFilter => "Colección de bookmarks|*.collection.json";
    public override string CollectionExported(string name) => $"Colección «{name}» exportada";
    public override string CollectionSaveFailed(string error) => $"No se pudo guardar la colección:\n{error}";
    public override string ImportTitle => "Importar una colección o los bookmarks de un vídeo";
    public override string ImportFileFilter => "Colecciones y bookmarks|*.collection.json;*.bookmarks.json|Todos los JSON|*.json";
    public override string NotBookmarksFile(string file) => $"«{file}» no es un archivo de bookmarks";
    public override string CannotReadNamed(string file, string error) => $"No se pudo leer «{file}»: {error}";
    public override string FileHasNoBookmarks(string file) => $"«{file}» no tiene bookmarks";
    public override string UnknownVideo => "No se sabe de qué vídeo son: abre el vídeo y vuelve a arrastrarlos";
    public override string ImportFailed(string error) => $"No se pudieron importar: {error}";
    public override string AlreadyHadBookmarks => "Ya tenías estos bookmarks";
    public override string BookmarksAddedToThisVideo(int count) => count == 1 ? "1 bookmark añadido a este vídeo" : $"{count} bookmarks añadidos a este vídeo";
    public override string BookmarksAddedTo(int count, string video, string collection) => (count == 1 ? "1 bookmark añadido" : $"{count} bookmarks añadidos") + $" a «{video}» en la colección «{collection}»";
    public override string ImportedCollectionName => "Importada";
    public override string ImportCollectionFailed(string error) => $"No se pudo importar la colección: {error}";
    public override string CollectionAdded(string name, string videos, string bookmarks) => $"Colección «{name}» añadida: {videos}, {bookmarks}";

    // ---- Exportar clips ----

    public override string ExportClip => "Exportar clip";
    public override string ExportClipOf(string? region) => region is null ? "Exportar clip del tramo ampliado" : $"Exportar clip de «{region}»";
    public override string ExactOriginal => "Original exacto";
    public override string ExactOriginalTip => "Mismo códec y bitrate, cortado en el frame exacto";
    public override string CopyOriginal(string start) => $"Original sin recodificar · {start}";
    public override string CopyOriginalTip => "Copia directa: instantánea y sin pérdida, pero empieza en un keyframe";
    public override string LightClipMenu(string rate) => $"Ligero · {rate} MB/s";
    public override string LightClipMenuTip => "MP4 (H.264 y AAC) para chats y Jira, como mucho 1080p";
    public override string StartsAtPreviousKeyframe => "empieza en el keyframe anterior";
    public override string ExactCut => "corte exacto";
    public override string StartsSecondsEarlier(string seconds) => $"empieza {seconds} s antes";
    public override string StartsFramesEarlier(int frames) => frames == 1 ? "empieza 1 frame antes" : $"empieza {frames} frames antes";
    public override string ExportNeedsRange => "Para exportar un clip, elige una región o marca antes un tramo (I / O, Z o arrastrando con el botón derecho)";
    public override string ExportWhat(string what) => $"Exportar {what}";
    public override string QuotedName(string name) => $"«{name}»";
    public override string ClipQueued => "Clip en cola: se exporta al acabar el anterior";
    public override string ExportingClip => "Exportando el clip…";
    public override string ExportCancelled => "Exportación cancelada";
    public override string ClipSaved => "Clip guardado";
    public override string ClipFailed => "No se pudo exportar el clip";
    public override string InFolder(string folder) => $"En {folder}";
    public override string CopiedForChat => "Copiado al portapapeles: Ctrl + V lo pega en un chat o en Jira";
    public override string CopiedForFolder => "Copiado al portapapeles: Ctrl + V lo pega en un chat o en una carpeta";
    public override string CopyFailed => "No se pudo copiar al portapapeles";
    public override string Copy => "Copiar";
    public override string ExportingPercent(int percent) => $"Exportando {percent} %";
    public override string PreparingClip => "Preparando el clip";
    public override string MoreQueued(int count) => $"\ny {count} más en cola";
    public override string ClickToCancel => "\nClic para cancelar";
    public override string ClipReady => "Clip listo";
    public override string ExportError => "Error al exportar";
    public override string ClickToGo => "\nClic para ir a él";
    public override string ClickToSeeWhy => "Clic para ver por qué";
    public override string CancelClip(string name) => $"Cancelar {name}";
    public override string TheClip => "el clip";
    public override string CancelQueuedToo(int count) => $"Cancelar también los {count} en cola";
    public override string CopyToClipboard => "Copiar al portapapeles";
    public override string ClipCopied => "Clip copiado al portapapeles";
    public override string PreviousClips => "Clips anteriores";
    public override string RemoveFromHere => "Quitar de aquí";
    public override string ExportingOnExit => "Se está exportando un clip. Si sales ahora, se cancela.";
    public override string Quit => "Salir";
    public override string Wait => "Esperar";
    public override string FfmpegMissing => "Falta ffmpeg.exe junto al programa.";
    public override string FfmpegFailed => "ffmpeg no pudo exportar el tramo.";
    public override string FfmpegNotStarted => "No se pudo lanzar ffmpeg.";
    public override string FfmpegExitCode(int code) => $"ffmpeg terminó con el código {code}.";
    public override string LightClipSuffix => " (ligero)";

    // ---- Otro color ----

    public override string OtherColorTitle => "Otro color";
    public override string UseThisColor => "Usar este color";
    public override string ReservedRangeAmber => "ámbar del tramo ampliado";
    public override string ReservedPlayedBlue => "azul de lo reproducido";
    public override string ReservedCacheGreen => "verde de la caché de frames";
    public override string ColorClash(string clash) => $"Este color se parece mucho al {clash} de las barras, y se confundirán.\n\n¿Usarlo igualmente?";

    // ---- Avisos ----

    public override string Ok => "Aceptar";

    // ---- Abrir con doble clic (registro de Windows) ----

    public override string AppDescription => "Reproductor de vídeo";
    public override string VideoFileType(string extension) => $"Vídeo {extension}";

    public override string LanguageName(string code) => code switch
    {
        "ger" => "Alemán",
        "ara" => "Árabe",
        "cat" => "Catalán",
        "chi" => "Chino",
        "kor" => "Coreano",
        "dan" => "Danés",
        "spa" => "Español",
        "baq" => "Euskera",
        "fin" => "Finés",
        "fre" => "Francés",
        "glg" => "Gallego",
        "gre" => "Griego",
        "heb" => "Hebreo",
        "hin" => "Hindi",
        "eng" => "Inglés",
        "ita" => "Italiano",
        "jpn" => "Japonés",
        "dut" => "Neerlandés",
        "nor" => "Noruego",
        "pol" => "Polaco",
        "por" => "Portugués",
        "rus" => "Ruso",
        "swe" => "Sueco",
        "tha" => "Tailandés",
        "tur" => "Turco",
        "ukr" => "Ucraniano",
        "vie" => "Vietnamita",
        _ => code,
    };
}
