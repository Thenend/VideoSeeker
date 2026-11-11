// Textos en italiano.

namespace VideoSeeker.App.Localization;

public sealed class ItalianStrings : Strings
{
    // ---- Teclas (para componer atajos) ----

    public override string KeyCtrl => "Ctrl";
    public override string KeyShift => "Maiusc";
    public override string KeyDelete => "Canc";
    public override string KeyEsc => "Esc";

    // ---- Ventana principal ----

    public override string EmptyHint => "Trascina qui un video o premi Ctrl + O";
    public override string CloseZoomTip => "Chiudi lo zoom (Esc)";
    public override string RangeLoopTip(string? region) => $"Loop {ZoneOf(region)} (Maiusc + R)";
    public override string RangeLoopOffTip(string? region) => $"Togli il loop {ZoneOf(region)} (Maiusc + R)";
    public override string MarkLastTip(string? region) => $"Imposta la fine {ZoneOf(region)} sul fotogramma visualizzato (O)";
    public override string MarkFirstTip(string? region) => $"Imposta l'inizio {ZoneOf(region)} sul fotogramma visualizzato (I)";
    public override string VideoLoopTip => "Loop del video (R)";
    public override string VideoLoopOffTip => "Togli il loop del video (R)";
    public override string PreviousFrameTip => "Fotogramma precedente (,) · rotellina: fotogramma per fotogramma";
    public override string NextFrameTip => "Fotogramma successivo (.) · rotellina: fotogramma per fotogramma";
    public override string PlayTip => "Riproduci / pausa (Spazio). J / K / L: indietro / pausa / avanti · rotellina: fotogramma per fotogramma";
    public override string SpeedTip => "Velocità · rotellina: ±0,1 · tasto centrale: 1×";
    public override string OpenVideoTip => "Apri video (Ctrl + O)";
    public override string RangePositionTip => "Rispetto all'intervallo ingrandito";
    public override string CurrentBookmarkTip => "Segnalibro attuale ([ / ] precedente / successivo, 1–9 e 0 per andare ai primi dieci)";
    public override string BookmarksButtonTip => "Pannello dei segnalibri (Ctrl + B) · aggiungi: B";
    public override string FullscreenTip => "Schermo intero (F)";
    public override string SettingsTip => "Impostazioni (Ctrl + ,)";
    public override string TracksTip => "Audio e sottotitoli (A / S)";
    public override string VolumeTip => "Volume";
    public override string MuteTip => "Disattiva l'audio (M)";
    public override string CloseBookmarkPanelTip => "Chiudi (Ctrl + B)";
    public override string ExportVideoBookmarksTip => "Esporta i segnalibri di questo video, per condividerli";
    public override string ExportCollectionTip => "Esporta l'intera raccolta, per condividerla";
    public override string BookmarksTitle => "Segnalibri";
    public override string CollectionTip => "Raccolta di segnalibri: ognuna ha i propri per ogni video. Cambia, crea, rinomina o elimina.";
    public override string SearchPlaceholder => "Cerca (Ctrl + F)";
    public override string ScopeThisVideo => "Questo video";
    public override string ScopeAllVideos => "Tutti i video";
    public override string PanelHint => "B crea una regione dall'intervallo ingrandito o, se non c'è, di un solo fotogramma (O ne fissa la fine). Clic o Invio: vai e ingrandisci. Ctrl / Maiusc + clic: selezione multipla. F2 rinomina, Canc elimina, Ctrl + Z annulla.";
    public override string BookmarkKeyTip => "Tasto per andare a questo segnalibro";
    public override string NameHint => "Invio salva · Esc lascia il nome com'era";
    public override string NameHintWithLabels => "Invio salva · Esc annulla · Alt + numero: etichetta";
    public override string GeneratingThumbnail => "Generazione della miniatura…";
    public override string Close => "Chiudi";
    public override string ShowInFolder => "Mostra nella cartella";
    public override string Open => "Apri";
    public override string ZoneOf(string? region) => region is null ? "dell'intervallo" : $"di «{region}»";
    public override string SaveZoneTip => "Salva l'intervallo ingrandito come segnalibro (B)";
    public override string NewLabelTitle => "Nuova etichetta";
    public override string NewLabelBoxTip => "Invio la crea e la applica · Esc torna indietro";
    public override string ZoneNameTip(string name) => $"L'intervallo ingrandito è la regione «{name}»: I / O, ⇤ ⇥ e le bandierine lo modificano";
    public override string CloseZoomDeselectTip(string name) => $"Chiudi lo zoom e deseleziona «{name}» (Esc)";
    public override string Loading => "Caricamento…";

    // ---- Ajustes ----

    public override string SettingsTitle => "Impostazioni — VideoSeeker";
    public override string ResetDefaults => "Ripristina i valori predefiniti";
    public override string SupportTip => "VideoSeeker è gratuito. Se ti è utile, puoi offrire un caffè all'autore.";
    public override string Support => "Sostieni";
    public override string SourceCode => "Codice sorgente";
    public override string SourceCodeTip => "Software libero, con licenza GPL v3.";
    public override string Version(string version) => $"Versione {version}";
    public override string SectionBars => "BARRE";
    public override string MoveRegionsTip => "Trascinando il centro dell'intervallo ingrandito o di una regione di segnalibro la si sposta tutta. Se disattivato, si sposta l'indicatore di riproduzione.";
    public override string MoveRegions => "Sposta regioni intere trascinandone il centro";
    public override string EdgeFrameTip => "Trascinando un estremo dell'intervallo ingrandito o di un segnalibro. Se disattivato, l'immagine non cambia.";
    public override string EdgeFrame => "Mostra il fotogramma dell'estremo trascinato";
    public override string CachedLineTip => "Indica sotto le barre ciò che è già nella cache: lì i salti sono istantanei.";
    public override string CachedLine => "Linea verde della cache dei fotogrammi";
    public override string MarksTip => "Scorciatoia: Ctrl + Maiusc + B.";
    public override string Marks => "Indicatori dei segnalibri";
    public override string PulseMarksTip => "Se sono nascosti, quando crei un segnalibro o ci vai il loro pulsante (in basso a destra) lampeggia qualche volta. Non compare nessun messaggio.";
    public override string PulseMarks => "Far lampeggiare il pulsante se sono nascosti";
    public override string SectionNavigation => "NAVIGAZIONE";
    public override string SecondsUnit => "s";
    public override string SeekTip => "Secondi che salta ogni freccia: da sola, con Maiusc o con Ctrl.";
    public override string Seek => "Salti con ← →";
    public override string ScrubTip => "Pixel da trascinare sull'immagine, tenendo premuto il tasto, per avanzare di un fotogramma.";
    public override string Scrub => "Sensibilità dello scrubbing";
    public override string PixelsPerFrame(int pixels) => $"{pixels} px per fotogramma";
    public override string WheelSpeed => "Velocità";
    public override string WheelVolume => "Volume";
    public override string WheelTip => "Velocità: ±0,1× (tasto centrale: 1×). Volume: ±5%.";
    public override string Wheel => "Rotellina del mouse sul video";
    public override string Resume => "Riprendi ogni video da dove si era interrotto";
    public override string SectionTimeAndBookmarks => "TEMPO E SEGNALIBRI";
    public override string TimeFormatTip => "In 00:01:23:11, l'ultimo numero è il fotogramma all'interno di ogni secondo.";
    public override string TimeFormat => "Formato del tempo";
    public override string NewColorTip => "Senza etichetta, finché non si sceglie altro (o sempre, se non si ripete l'ultima scelta).";
    public override string NewColor => "Colore dei nuovi segnalibri";
    public override string RepeatLabelTip => "Il nuovo segnalibro prende l'etichetta o il colore scelti l'ultima volta; si cambia nella casella del nome.";
    public override string RepeatLabel => "Ripeti l'ultima etichetta o colore";
    public override string TimesInPreview => "Anteprima";
    public override string TimesInList => "Elenco";
    public override string BookmarkTimesTip => "Nell'elenco si vedono tutti a colpo d'occhio. Nell'anteprima, l'elenco lascia più spazio al nome.";
    public override string BookmarkTimes => "Tempi dei segnalibri";
    public override string SectionAudioAndSubtitles => "AUDIO E SOTTOTITOLI";
    public override string AudioLanguageTip => "Si sceglie quella traccia se il video ne ha diverse. Si applica all'apertura del video successivo.";
    public override string AudioLanguage => "Lingua audio preferita";
    public override string SubtitleLanguageTip => "Si sceglie quella traccia quando si attivano i sottotitoli. Si applica all'apertura del video successivo.";
    public override string SubtitleLanguage => "Lingua preferita per i sottotitoli";
    public override string AppliesOnNextVideo => "Si applica all'apertura del video successivo.";
    public override string SubtitlesOnOpen => "Attiva i sottotitoli all'apertura di un video";
    public override string NoPreference => "Nessuna preferenza";
    public override string SectionPerformance => "PRESTAZIONI";
    public override string CacheMemory => "Memoria della cache dei fotogrammi";
    public override string CacheMemoryTip => "Più è grande, più video si può percorrere fotogramma per fotogramma all'istante. Si applica all'apertura del video successivo.";
    public override string CacheOfTotal(string used, string total) => $"{used} di {total} GB";
    public override string GigabytesUnit => "GB";
    public override string MegabytesUnit => "MB";
    public override string CacheTooBigTip => "Oltre il 90% della memoria della scheda video: potrebbe non bastare per altri programmi.";
    public override string HardwareDecodingTip => "Riproduce con la scheda grafica. Disattivala solo se un video si vede male.";
    public override string HardwareDecoding => "Decodifica hardware";
    public override string SectionClips => "ESPORTA CLIP";
    public override string ChangeEllipsis => "Cambia…";
    public override string BesideVideo => "Accanto al video";
    public override string ClipFolderTip => "Dove si salvano le clip esportate. Se lì non si può scrivere, vanno in Video\\VideoSeeker.";
    public override string ClipFolder => "Cartella di destinazione";
    public override string BesideEachVideo => "Accanto a ogni video";
    public override string ClipFolderDialogTitle => "Cartella per le clip esportate";
    public override string ClipClipboardTip => "Come file, per incollarlo direttamente in una chat, in Jira o in una cartella.";
    public override string ClipClipboard => "Copia ogni clip negli appunti";
    public override string LightClipTip => "Megabyte per ogni secondo di clip, audio compreso. Mai più del video originale.";
    public override string LightClip => "Dimensione della clip leggera";
    public override string MegabytesPerSecond(string value) => $"{value} MB/s";
    public override string SectionGeneral => "GENERALE";
    public override string InterfaceLanguage => "Lingua";
    public override string InterfaceLanguageTip => "La lingua di VideoSeeker. Automatica: quella di Windows. La modifica si applica al riavvio.";
    public override string AutomaticLanguage(string language) => $"Automatica · {language}";
    public override string LanguageChangedRestart => "La nuova lingua verrà usata al riavvio di VideoSeeker.";
    public override string LanguageCaption => "Lingua";
    public override string RestartNow => "Riavvia ora";
    public override string Later => "Più tardi";
    public override string DefaultFolder => "Predefinita";
    public override string DefaultFolderTip => "La cartella data, accanto al programma.";
    public override string DataFolderTip => "Impostazioni, segnalibri e punto di riproduzione di ogni video. La modifica si applica al riavvio.";
    public override string DataFolder => "Cartella dei dati";
    public override string ChooseInWindows => "Scegli in Windows";
    public override string Unregister => "Rimuovi";
    public override string DefaultPlayer => "Lettore video predefinito";
    public override string DefaultPlayerTip => "Aggiunge i tipi di video ad «Apri con» e porta ad App predefinite di Windows, dove si sceglie per quali.";
    public override string NumberRange => "Tra 0,04 e 3600 secondi";
    public override string PendingFolder(string folder) => $"Al riavvio verrà usata {folder}.";
    public override string PendingFolderWithCopy(string folder) => $"Al riavvio verrà usata {folder}, con una copia dei dati attuali.";
    public override string DataFolderDialogTitle => "Cartella per i dati di VideoSeeker";
    public override string DataFolderNested => "La cartella dei dati non può stare dentro quella attuale né contenerla.";
    public override string DataFolderHasData(string folder) => $"{folder}\n\ncontiene già dati di VideoSeeker. Se vi copi quelli attuali, quelli che c'erano vengono conservati nella sottocartella «anteriores».";
    public override string UseThatData => "Usa quei dati";
    public override string CopyCurrentData => "Copia quelli attuali";
    public override string MoveDataQuestion(string folder) => $"Portare i tuoi dati (impostazioni, segnalibri, punto di riproduzione di ogni video) in\n{folder}?\n\nLa cartella attuale resta com'è.";
    public override string MoveData => "Porta i dati";
    public override string StartEmpty => "Inizia da zero";
    public override string Cancel => "Annulla";
    public override string CannotUseFolder(string error) => $"Non si può usare quella cartella:\n{error}";
    public override string DataFolderRestart => "La nuova cartella dei dati verrà usata al riavvio di VideoSeeker.";
    public override string OpenWithDoubleClick => "Apri con doppio clic";
    public override string RegisterFailed(string error) => $"Non è stato possibile registrare i tipi di video:\n{error}";
    public override string UnregisterFailed(string error) => $"Non è stato possibile rimuovere la registrazione:\n{error}";
    public override string ResetQuestion => "Tutte le impostazioni torneranno ai valori predefiniti.";
    public override string Reset => "Ripristina";
    public override string ResetTitle => "Ripristina le impostazioni";
    public override string DefaultFolderPackagedTip => "La cartella dell'app in Windows. Viene eliminata quando si disinstalla l'app.";
    public override string HoverPreview => "Miniatura al passaggio del mouse sulle barre";
    public override string HoverPreviewTip => "Per vedere cosa c'è in un punto del video senza andarci.";
    public override string DataCopyIncomplete(string file) => $"La copia di {file} non è completa";

    // ---- Ventana principal: reproducción ----

    public override string VideoFileFilter => "Video|*.mkv;*.mp4;*.m4v;*.avi;*.wmv;*.mov;*.webm;*.flv;*.ts;*.m2ts;*.mts;*.mpg;*.mpeg;*.vob;*.3gp;*.ogv|Tutti i file|*.*";
    public override string OpenVideoTitle => "Apri video";
    public override string FileNotFound(string path) => $"Impossibile trovare il file:\n{path}";
    public override string FrameNumber(int frame) => $"fotogramma {frame}";
    public override string FrameNumberApprox(string frame) => $"fotogramma ~{frame}";
    public override string Frames(int count) => count == 1 ? "1 fotogramma" : $"{count} fotogrammi";
    public override string FrameOfRange(int frame, int count) => $"fotogramma {frame}/{count}";
    public override string VideoLoopOn => "Loop del video attivo";
    public override string VideoLoopOff => "Loop del video disattivato";
    public override string RangeLoopOn(string? region) => $"Loop {ZoneOf(region)} attivo";
    public override string RangeLoopOff(string? region) => $"Loop {ZoneOf(region)} disattivato";
    public override string ZoomSection => "Ingrandisci la sezione (±5 s)";
    public override string MarkFirstFromScreen(int frame, string? region) => $"Imposta l'inizio {ZoneOf(region)} sul fotogramma visualizzato ({frame})";
    public override string MarkLastFromScreen(int frame, string? region) => $"Imposta la fine {ZoneOf(region)} sul fotogramma visualizzato ({frame})";
    public override string MarkFirstFromCursor(int frame, string? region) => $"Imposta l'inizio {ZoneOf(region)} sul fotogramma sotto il cursore ({frame})";
    public override string MarkLastFromCursor(int frame, string? region) => $"Imposta la fine {ZoneOf(region)} sul fotogramma sotto il cursore ({frame})";
    public override string NormalSpeed => "1× (normale)";
    public override string SpeedToast(string speed) => $"Velocità {speed}";
    public override string VolumeToast(int volume) => $"Volume {volume}%";
    public override string VolumeMutedToast(int volume) => $"Volume {volume}% (audio disattivato)";
    public override string Audio => "Audio";
    public override string Subtitles => "Sottotitoli";
    public override string SubtitlesOff => "Disattivati";
    public override string NoTracks => "Nessuna traccia";
    public override string Track(long id) => $"Traccia {id}";
    public override string ControlsPinned => "Controlli fissi";
    public override string ControlsHidden => "Controlli nascosti: porta il mouse in basso per vederli";
    public override string ControlsPinnedTip => "Controlli fissi (Tab)";
    public override string PinControlsTip => "Fissa i controlli (Tab)";
    public override string VideoGone(string name) => $"Non c'è più: {name}";
    public override string HistoryBackTip => "Torna al video precedente (Alt + ←)";
    public override string HistoryForwardTip => "Vai al video successivo (Alt + →)";
    public override string HistoryAllBack(int count) => $"Clic destro: i {count} precedenti";
    public override string HistoryAllForward(int count) => $"Clic destro: i {count} successivi";

    // ---- Bookmarks ----

    public override string NewBookmark => "Nuovo segnalibro";
    public override string RenameBookmark => "Rinomina segnalibro";
    public override string SelectBookmarkFirst => "Prima seleziona un segnalibro (cliccalo sulle barre o nel pannello)";
    public override string BookmarkDeleted(string name) => $"Eliminato «{name}» · Ctrl + Z lo recupera";
    public override string BookmarksDeleted(int count) => $"Eliminati {count} segnalibri · Ctrl + Z li recupera";
    public override string UndoFailed(string error) => $"Impossibile annullare: {error}";
    public override string RedoFailed(string error) => $"Impossibile ripetere: {error}";
    public override string NothingToUndo => "Niente da annullare";
    public override string NothingToRedo => "Niente da ripetere";
    public override string Undone(string step) => $"Annullato: {step}";
    public override string Redone(string step) => $"Ripetuto: {step}";
    public override string InVideo(string video) => $" (in «{video}»)";
    public override string VideoHasNoBookmarks => "Questo video non ha segnalibri (B ne aggiunge uno)";
    public override string NoBookmarkNumber(int number, int count) => $"Non c'è il segnalibro {number} (ce ne sono {count})";
    public override string NoMoreBookmarksAhead => "Non ci sono altri segnalibri più avanti";
    public override string NoMoreBookmarksBehind => "Non ci sono altri segnalibri più indietro";
    public override string AddBookmarkHere => "Aggiungi segnalibro qui";
    public override string BookmarksPanel => "Pannello dei segnalibri";
    public override string Rename => "Rinomina";
    public override string UseFrameAsThumbnail(int frame, string? region) => region is null ? $"Usa il fotogramma attuale ({frame}) come miniatura" : $"Usa il fotogramma attuale ({frame}) come miniatura di «{region}»";
    public override string FrameOutsideRegion(int frame, string time, string name, string range) => $"Il fotogramma attuale ({frame}, {time}) è fuori dalla regione «{name}» ({range}).\n\nUsarlo comunque come miniatura?";
    public override string UseIt => "Usalo";
    public override string ResetThumbnail(string? region) => region is null ? "Torna alla miniatura del primo fotogramma" : $"Torna alla miniatura del primo fotogramma di «{region}»";
    public override string Delete => "Elimina";
    public override string DeleteCount(int count) => $"Elimina tutti e {count}";
    public override string BookmarksExported(int count) => count == 1 ? "1 segnalibro esportato" : $"{count} segnalibri esportati";
    public override string CannotWriteBesideVideo(string error) => $"Impossibile scrivere accanto al video:\n{error}";
    public override string MarksShown => "Indicatori dei segnalibri visibili";
    public override string MarksHidden => "Indicatori dei segnalibri nascosti (Ctrl + Maiusc + B li mostra)";
    public override string HideMarksTip => "Nascondi gli indicatori dei segnalibri sulle barre (Ctrl + Maiusc + B). Il pannello continua a funzionare.";
    public override string ShowMarksTip => "Mostra gli indicatori dei segnalibri sulle barre (Ctrl + Maiusc + B)";
    public override string CollectionButton(string name) => $"Raccolta: {name}  ▾";
    public override string CollectionEmpty => "vuota";
    public override string BookmarksInVideos(int bookmarks, int videos) => videos == 1 ? $"{bookmarks} in 1 video" : $"{bookmarks} in {videos} video";
    public override string NewCollection => "Nuova raccolta…";
    public override string NewCollectionName => "Nome della nuova raccolta";
    public override string CollectionExists(string name) => $"Esiste già una raccolta «{name}»";
    public override string RenameCollection(string name) => $"Rinomina «{name}»…";
    public override string NewNameFor(string name) => $"Nuovo nome per «{name}»";
    public override string RenameFailed(string error) => $"Impossibile rinominare: {error}";
    public override string CollectionRenamed(string name) => $"Raccolta rinominata in «{name}»";
    public override string DeleteCollection(string name) => $"Elimina «{name}»…";
    public override string ImportCollectionOrBookmarks => "Importa raccolta o segnalibri…";
    public override string ExportCollectionMenu(string name) => $"Esporta «{name}»…";
    public override string CollectionIsEmpty => "È vuota.";
    public override string CollectionWillBeLost(int bookmarks, int videos) => videos == 1 ? $"Contiene {bookmarks} segnalibri di 1 video, che andranno persi." : $"Contiene {bookmarks} segnalibri di {videos} video, che andranno persi.";
    public override string DeleteCollectionQuestion(string name, string detail) => $"Eliminare la raccolta «{name}»?\n{detail}";
    public override string DeleteFailed(string error) => $"Impossibile eliminare: {error}";
    public override string CollectionToast(string name) => $"Raccolta «{name}»";
    public override string BookmarkCount(int count) => count == 1 ? "1 segnalibro" : $"{count} segnalibri";
    public override string VideoCount(int count) => count == 1 ? "1 video" : $"{count} video";
    public override string BookmarksFromVideos(int bookmarks, int videos) => $"{bookmarks} segnalibri di {videos} video";
    public override string ShowVideoInExplorer => "Mostra il video in Esplora file";
    public override string ShowVideoInExplorerMissing => "Mostra il video in Esplora file (non trovato)";
    public override string MovedTo(string collection) => $"Spostato in «{collection}» · Ctrl + Z lo riporta";
    public override string MovedManyTo(int count, string collection) => $"Spostati {count} in «{collection}» · Ctrl + Z li riporta";
    public override string ChangeFailed(string error) => $"Impossibile modificare: {error}";
    public override string Color => "Colore";
    public override string OwnColor => "Personalizzato";
    public override string OtherColor => "Altro colore…";
    public override string MoveToCollection => "Sposta nella raccolta";
    public override string MoveToNewCollectionName => "Nome della raccolta in cui spostarli";
    public override string MoveFailed(string error) => $"Impossibile spostarli: {error}";
    public override string BookmarkVideoNotFound(string video) => $"Impossibile trovare il video di questo segnalibro:\n{video}";
    public override string LocateIt => "Cercalo…";
    public override string LocateTitle(string name) => $"Cerca «{name}»";
    public override string CannotReadFile(string error) => $"Impossibile leggere il file:\n{error}";
    public override string NotSameVideo(string chosen, string name) => $"«{chosen}» non è lo stesso video di «{name}» (il contenuto è diverso: un'altra copia, un'altra versione o un video diverso). I tempi dei segnalibri potrebbero non coincidere.\n\nUsarlo comunque? I segnalibri di quel video (in tutte le raccolte) passeranno a questo file; Ctrl + Z annulla.";
    public override string UseItAnyway => "Usalo comunque";
    public override string NotAllMoved(string error) => $"Non è stato possibile spostarli tutti: {error}";
    public override string VideosCountName(int count) => $"{count} video";
    public override string BookmarkOfZoomedRange => "Segnalibro dell'intervallo ingrandito";

    // ---- Pasos de deshacer (van detrás de «Deshecho:») ----

    public override string StepAdd(string name) => $"aggiungere «{name}»";
    public override string StepEdit(string name) => $"modificare «{name}»";
    public override string StepRename(string name) => $"rinominare «{name}»";
    public override string StepLabelOf(string name) => $"etichetta di «{name}»";
    public override string StepLabelOfMany(int count) => $"etichetta di {count} segnalibri";
    public override string StepColorOf(string name) => $"colore di «{name}»";
    public override string StepColorOfMany(int count) => $"colore di {count} segnalibri";
    public override string StepDelete(string name) => $"eliminare «{name}»";
    public override string StepDeleteMany(int count) => $"eliminare {count} segnalibri";
    public override string StepMoveTo(string name, string collection) => $"spostare «{name}» in «{collection}»";
    public override string StepMoveManyTo(int count, string collection) => $"spostare {count} segnalibri in «{collection}»";
    public override string StepMoveStart(string name) => $"spostare l'inizio di «{name}»";
    public override string StepMoveEnd(string name) => $"spostare la fine di «{name}»";
    public override string StepMove(string name) => $"spostare «{name}»";
    public override string StepRelink(string from, string to) => $"passare i segnalibri da «{from}» a «{to}»";
    public override string StepImportBookmarks => "importare segnalibri";
    public override string StepImportCollection(string name) => $"importare la raccolta «{name}»";
    public override string DefaultBookmarkName(int number) => $"Segnalibro {number}";
    public override string StepAdjust(string name) => $"regolare «{name}»";
    public override string StepSetStartOf(string name) => $"impostare l'inizio di «{name}»";
    public override string StepSetEndOf(string name) => $"impostare la fine di «{name}»";

    // ---- Etiquetas ----

    public override string LabelChipTip(string name) => $"Mostra solo «{name}» (clic su altre per aggiungerle). Clic destro: rinomina, colore, elimina…";
    public override string LabelChipShortcut(string shortcut) => $"\n{shortcut} la applica al segnalibro selezionato.";
    public override string NoLabel => "Senza etichetta";
    public override string NoLabelChipTip => "Mostra solo quelli senza etichetta";
    public override string AddLabelChip => "+ Etichetta";
    public override string AddLabelChipTip => "Nuova etichetta (per esempio «Boss» o «Filmato»), per vedere a colpo d'occhio cosa segna ogni segnalibro";
    public override string NoLabelsInCollection => "Questa raccolta non ha etichette: «+ Etichetta» nel pannello dei segnalibri (Ctrl + B)";
    public override string NoLabelNumber(int number, int count) => $"Non c'è l'etichetta {number} (ce ne sono {count})";
    public override string LabelToast(string name) => $"Etichetta «{name}»";
    public override string BookmarkWithLabel(string name, string label) => $"«{name}»: {label}";
    public override string Labels => "Etichette";
    public override string NewLabel => "Nuova etichetta…";
    public override string NewLabelName => "Nome della nuova etichetta";
    public override string LabelExists(string name) => $"Esiste già un'etichetta «{name}»";
    public override string LabelsChangeFailed(string error) => $"Impossibile modificare le etichette: {error}";
    public override string NoBookmarks => "nessun segnalibro";
    public override string RenameEllipsis => "Rinomina…";
    public override string MoveLeft => "Sposta a sinistra";
    public override string MoveRight => "Sposta a destra";
    public override string AdoptLabelColor(string color, int count) => $"Applicala a quelli di colore {color.ToLower(Loc.Culture)} senza etichetta ({count})";
    public override string AdoptLabelSameColor(int count) => $"Applicala a quelli del suo colore senza etichetta ({count})";
    public override string DeleteEllipsis => "Elimina…";
    public override string DeleteLabelQuestion(string name, int uses) => $"Eliminare l'etichetta «{name}»?\n" + (uses == 1 ? "Ce l'ha 1 segnalibro: resterà senza etichetta, con il suo colore." : $"Ce l'hanno {uses} segnalibri: resteranno senza etichetta, con il loro colore.");
    public override string StepCreateLabel(string name) => $"creare l'etichetta «{name}»";
    public override string StepRenameLabel(string name) => $"rinominare l'etichetta «{name}»";
    public override string StepLabelColor(string name) => $"colore dell'etichetta «{name}»";
    public override string StepMoveLabel(string name) => $"spostare l'etichetta «{name}»";
    public override string StepAdoptLabel(string name, int count) => $"applicare l'etichetta «{name}» a {count} segnalibri";
    public override string StepDeleteLabel(string name) => $"eliminare l'etichetta «{name}»";
    public override string LabelOrColor => "Etichetta o colore";
    public override string AddLabelChipLong => "+ Nuova etichetta";
    public override string AddLabelChipShort => "+ Nuova";
    public override string AddLabelChipNameTip => "Crea un'etichetta e applicala";
    public override string ColorWithoutLabel(string color) => $"{color} (senza etichetta)";

    // ---- Colores de la paleta ----

    public override string ColorPink => "Rosa";
    public override string ColorRed => "Rosso";
    public override string ColorOrange => "Arancione";
    public override string ColorViolet => "Viola";
    public override string ColorCyan => "Ciano";
    public override string ColorLime => "Lime";
    public override string ColorBrown => "Marrone";
    public override string ColorGray => "Grigio";
    public override string ColorWhite => "Bianco";

    // ---- Compartir colecciones y bookmarks ----

    public override string CollectionHasNoBookmarks(string name) => $"La raccolta «{name}» non ha segnalibri";
    public override string ExportCollectionTitle => "Esporta la raccolta";
    public override string CollectionFileFilter => "Raccolta di segnalibri|*.collection.json";
    public override string CollectionExported(string name) => $"Raccolta «{name}» esportata";
    public override string CollectionSaveFailed(string error) => $"Impossibile salvare la raccolta:\n{error}";
    public override string ImportTitle => "Importa una raccolta o i segnalibri di un video";
    public override string ImportFileFilter => "Raccolte e segnalibri|*.collection.json;*.bookmarks.json|Tutti i JSON|*.json";
    public override string NotBookmarksFile(string file) => $"«{file}» non è un file di segnalibri";
    public override string CannotReadNamed(string file, string error) => $"Impossibile leggere «{file}»: {error}";
    public override string FileHasNoBookmarks(string file) => $"«{file}» non ha segnalibri";
    public override string UnknownVideo => "Non si sa di quale video siano: apri il video e trascinali di nuovo";
    public override string ImportFailed(string error) => $"Impossibile importarli: {error}";
    public override string AlreadyHadBookmarks => "Avevi già questi segnalibri";
    public override string BookmarksAddedToThisVideo(int count) => count == 1 ? "1 segnalibro aggiunto a questo video" : $"{count} segnalibri aggiunti a questo video";
    public override string BookmarksAddedTo(int count, string video, string collection) => (count == 1 ? "1 segnalibro aggiunto" : $"{count} segnalibri aggiunti") + $" a «{video}» nella raccolta «{collection}»";
    public override string ImportedCollectionName => "Importata";
    public override string ImportCollectionFailed(string error) => $"Impossibile importare la raccolta: {error}";
    public override string CollectionAdded(string name, string videos, string bookmarks) => $"Raccolta «{name}» aggiunta: {videos}, {bookmarks}";

    // ---- Exportar clips ----

    public override string ExportClip => "Esporta clip";
    public override string ExportClipOf(string? region) => region is null ? "Esporta clip dell'intervallo ingrandito" : $"Esporta clip di «{region}»";
    public override string ExactOriginal => "Originale esatto";
    public override string ExactOriginalTip => "Stesso codec e bitrate, tagliato al fotogramma esatto";
    public override string CopyOriginal(string start) => $"Originale senza ricodifica · {start}";
    public override string CopyOriginalTip => "Copia diretta: istantanea e senza perdita, ma inizia su un keyframe";
    public override string LightClipMenu(string rate) => $"Leggera · {rate} MB/s";
    public override string LightClipMenuTip => "MP4 (H.264 e AAC) per chat e Jira, al massimo 1080p";
    public override string StartsAtPreviousKeyframe => "inizia al keyframe precedente";
    public override string ExactCut => "taglio esatto";
    public override string StartsSecondsEarlier(string seconds) => $"inizia {seconds} s prima";
    public override string StartsFramesEarlier(int frames) => frames == 1 ? "inizia 1 fotogramma prima" : $"inizia {frames} fotogrammi prima";
    public override string ExportNeedsRange => "Per esportare una clip, scegli una regione o prima segna un intervallo (I / O, Z o trascinando con il tasto destro)";
    public override string ExportWhat(string what) => $"Esporta {what}";
    public override string QuotedName(string name) => $"«{name}»";
    public override string ClipQueued => "Clip in coda: verrà esportata quando finisce la precedente";
    public override string ExportingClip => "Esportazione della clip…";
    public override string ExportCancelled => "Esportazione annullata";
    public override string ClipSaved => "Clip salvata";
    public override string ClipFailed => "Impossibile esportare la clip";
    public override string InFolder(string folder) => $"In {folder}";
    public override string CopiedForChat => "Copiato negli appunti: Ctrl + V lo incolla in una chat o in Jira";
    public override string CopiedForFolder => "Copiato negli appunti: Ctrl + V lo incolla in una chat o in una cartella";
    public override string CopyFailed => "Impossibile copiare negli appunti";
    public override string Copy => "Copia";
    public override string ExportingPercent(int percent) => $"Esportazione {percent}%";
    public override string PreparingClip => "Preparazione della clip";
    public override string MoreQueued(int count) => $"\ne altre {count} in coda";
    public override string ClickToCancel => "\nClic per annullare";
    public override string ClipReady => "Clip pronta";
    public override string ExportError => "Errore di esportazione";
    public override string ClickToGo => "\nClic per mostrarlo o aprirlo";
    public override string ClickToSeeWhy => "Clic per vedere il motivo";
    public override string CancelClip(string name) => $"Annulla {name}";
    public override string TheClip => "la clip";
    public override string CancelQueuedToo(int count) => $"Annulla anche le {count} in coda";
    public override string CopyToClipboard => "Copia negli appunti";
    public override string ClipCopied => "Clip copiata negli appunti";
    public override string PreviousClips => "Clip precedenti";
    public override string RemoveFromHere => "Togli da qui";
    public override string ExportingOnExit => "Si sta esportando una clip. Se esci ora, verrà annullata.";
    public override string Quit => "Esci";
    public override string Wait => "Attendi";
    public override string FfmpegMissing => "Manca ffmpeg.exe nella cartella del programma.";
    public override string FfmpegFailed => "ffmpeg non è riuscito a esportare l'intervallo.";
    public override string FfmpegNotStarted => "Impossibile avviare ffmpeg.";
    public override string FfmpegExitCode(int code) => $"ffmpeg è terminato con il codice {code}.";
    public override string LightClipSuffix => " (leggera)";

    // ---- Otro color ----

    public override string OtherColorTitle => "Altro colore";
    public override string UseThisColor => "Usa questo colore";
    public override string ReservedRangeAmber => "ambra dell'intervallo ingrandito";
    public override string ReservedPlayedBlue => "blu della parte riprodotta";
    public override string ReservedCacheGreen => "verde della cache dei fotogrammi";
    public override string ColorClash(string clash) => $"Questo colore somiglia molto a un colore delle barre ({clash}), e si confonderanno.\n\nUsarlo comunque?";

    // ---- Avisos ----

    public override string Ok => "OK";

    // ---- Abrir con doble clic (registro de Windows) ----

    public override string AppDescription => "Lettore video";
    public override string VideoFileType(string extension) => $"Video {extension}";

    public override string LanguageName(string code) => code switch
    {
        "ger" => "Tedesco",
        "ara" => "Arabo",
        "cat" => "Catalano",
        "chi" => "Cinese",
        "kor" => "Coreano",
        "dan" => "Danese",
        "spa" => "Spagnolo",
        "baq" => "Basco",
        "fin" => "Finlandese",
        "fre" => "Francese",
        "glg" => "Galiziano",
        "gre" => "Greco",
        "heb" => "Ebraico",
        "hin" => "Hindi",
        "eng" => "Inglese",
        "ita" => "Italiano",
        "jpn" => "Giapponese",
        "dut" => "Olandese",
        "nor" => "Norvegese",
        "pol" => "Polacco",
        "por" => "Portoghese",
        "rus" => "Russo",
        "swe" => "Svedese",
        "tha" => "Thailandese",
        "tur" => "Turco",
        "ukr" => "Ucraino",
        "vie" => "Vietnamita",
        _ => code,
    };
}
