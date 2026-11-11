// Textos en alemán.

namespace VideoSeeker.App.Localization;

public sealed class GermanStrings : Strings
{
    // ---- Teclas (para componer atajos) ----

    public override string KeyCtrl => "Strg";
    public override string KeyShift => "Umschalt";
    public override string KeyDelete => "Entf";
    public override string KeyEsc => "Esc";

    // ---- Ventana principal ----

    public override string EmptyHint => "Video hierher ziehen oder Strg + O drücken";
    public override string CloseZoomTip => "Zoom schließen (Esc)";
    public override string RangeLoopTip(string? region) => $"Wiederholung {ZoneOf(region)} (Umschalt + R)";
    public override string RangeLoopOffTip(string? region) => $"Wiederholung {ZoneOf(region)} beenden (Umschalt + R)";
    public override string MarkLastTip(string? region) => $"Angezeigten Frame als Ende {ZoneOf(region)} setzen (O)";
    public override string MarkFirstTip(string? region) => $"Angezeigten Frame als Anfang {ZoneOf(region)} setzen (I)";
    public override string VideoLoopTip => "Video wiederholen (R)";
    public override string VideoLoopOffTip => "Video nicht mehr wiederholen (R)";
    public override string PreviousFrameTip => "Vorheriger Frame (,) · Mausrad: Frame für Frame";
    public override string NextFrameTip => "Nächster Frame (.) · Mausrad: Frame für Frame";
    public override string PlayTip => "Wiedergabe / Pause (Leertaste). J / K / L: rückwärts / Pause / vorwärts · Mausrad: Frame für Frame";
    public override string SpeedTip => "Geschwindigkeit · Mausrad: ±0,1 · mittlere Taste: 1×";
    public override string OpenVideoTip => "Video öffnen (Strg + O)";
    public override string RangePositionTip => "Bezogen auf den gezoomten Abschnitt";
    public override string CurrentBookmarkTip => "Aktuelles Lesezeichen ([ / ] vorheriges / nächstes, 1–9 und 0 springen zu den ersten zehn)";
    public override string BookmarksButtonTip => "Lesezeichenbereich (Strg + B) · hinzufügen: B";
    public override string FullscreenTip => "Vollbild (F)";
    public override string SettingsTip => "Einstellungen (Strg + ,)";
    public override string TracksTip => "Audio und Untertitel (A / S)";
    public override string VolumeTip => "Lautstärke";
    public override string MuteTip => "Stummschalten (M)";
    public override string CloseBookmarkPanelTip => "Schließen (Strg + B)";
    public override string ExportVideoBookmarksTip => "Lesezeichen dieses Videos exportieren, um sie zu teilen";
    public override string ExportCollectionTip => "Die ganze Sammlung exportieren, um sie zu teilen";
    public override string BookmarksTitle => "Lesezeichen";
    public override string CollectionTip => "Lesezeichensammlung: Jede hat für jedes Video ihre eigenen Lesezeichen. Wechseln, erstellen, umbenennen oder löschen.";
    public override string SearchPlaceholder => "Suchen (Strg + F)";
    public override string ScopeThisVideo => "Dieses Video";
    public override string ScopeAllVideos => "Alle Videos";
    public override string PanelHint => "B erstellt eine Region aus dem gezoomten Abschnitt oder, falls keiner da ist, eine mit einem Frame (O setzt ihr Ende). Klick oder Eingabe: hinspringen und zoomen. Strg / Umschalt + Klick: mehrere auswählen. F2 benennt um, Entf löscht, Strg + Z macht rückgängig.";
    public override string BookmarkKeyTip => "Taste, um zu diesem Lesezeichen zu springen";
    public override string NameHint => "Eingabe speichert · Esc behält den bisherigen Namen";
    public override string NameHintWithLabels => "Eingabe speichert · Esc bricht ab · Alt + Zahl: Label";
    public override string GeneratingThumbnail => "Vorschaubild wird erstellt…";
    public override string Close => "Schließen";
    public override string ShowInFolder => "Im Ordner anzeigen";
    public override string Open => "Öffnen";
    public override string ZoneOf(string? region) => region is null ? "des Abschnitts" : $"von „{region}“";
    public override string SaveZoneTip => "Gezoomten Abschnitt als Lesezeichen speichern (B)";
    public override string NewLabelTitle => "Neues Label";
    public override string NewLabelBoxTip => "Eingabe erstellt und weist es zu · Esc geht zurück";
    public override string ZoneNameTip(string name) => $"Der gezoomte Abschnitt ist die Region „{name}“: I / O, ⇤ ⇥ und die Fähnchen ändern ihn";
    public override string CloseZoomDeselectTip(string name) => $"Zoom schließen und „{name}“ abwählen (Esc)";
    public override string Loading => "Wird geladen…";

    // ---- Ajustes ----

    public override string SettingsTitle => "Einstellungen — VideoSeeker";
    public override string ResetDefaults => "Standardwerte wiederherstellen";
    public override string SupportTip => "VideoSeeker ist kostenlos. Wenn es dir nützt, kannst du dem Autor einen Kaffee spendieren.";
    public override string Support => "Unterstützen";
    public override string SourceCode => "Quellcode";
    public override string SourceCodeTip => "Freie Software unter der Lizenz GPL v3.";
    public override string Version(string version) => $"Version {version}";
    public override string SectionBars => "LEISTEN";
    public override string MoveRegionsTip => "Zieht man die Mitte des gezoomten Abschnitts oder einer Lesezeichen-Region, wird sie als Ganzes verschoben. Ausgeschaltet bewegt das die Abspielposition.";
    public override string MoveRegions => "Ganze Regionen durch Ziehen ihrer Mitte verschieben";
    public override string EdgeFrameTip => "Beim Ziehen eines Randes des gezoomten Abschnitts oder eines Lesezeichens. Ausgeschaltet bleibt das Bild unverändert.";
    public override string EdgeFrame => "Frame am gezogenen Rand anzeigen";
    public override string CachedLineTip => "Markiert unter den Leisten, was schon im Cache ist: Dorthin springt man sofort.";
    public override string CachedLine => "Grüne Linie des Frame-Caches";
    public override string MarksTip => "Tastenkürzel: Strg + Umschalt + B.";
    public override string Marks => "Lesezeichen-Markierungen";
    public override string PulseMarksTip => "Sind sie ausgeblendet, blinkt ihre Schaltfläche (unten rechts) ein paar Mal, wenn du ein Lesezeichen erstellst oder zu einem springst. Sonst erscheint kein Hinweis.";
    public override string PulseMarks => "Schaltfläche blinken lassen, wenn ausgeblendet";
    public override string SectionNavigation => "NAVIGATION";
    public override string SecondsUnit => "s";
    public override string SeekTip => "Sekunden pro Pfeiltastendruck: allein, mit Umschalt oder mit Strg.";
    public override string Seek => "Sprünge mit ← →";
    public override string ScrubTip => "Pixel, die man mit gedrückter Taste über das Bild ziehen muss, um einen Frame weiterzugehen.";
    public override string Scrub => "Scrubbing-Empfindlichkeit";
    public override string PixelsPerFrame(int pixels) => $"{pixels} px pro Frame";
    public override string WheelSpeed => "Geschwindigkeit";
    public override string WheelVolume => "Lautstärke";
    public override string WheelTip => "Geschwindigkeit: ±0,1× (mittlere Taste: 1×). Lautstärke: ±5 %.";
    public override string Wheel => "Mausrad über dem Video";
    public override string Resume => "Jedes Video dort fortsetzen, wo man aufgehört hat";
    public override string SectionTimeAndBookmarks => "ZEIT UND LESEZEICHEN";
    public override string TimeFormatTip => "In 00:01:23:11 ist die letzte Zahl der Frame innerhalb der Sekunde.";
    public override string TimeFormat => "Zeitformat";
    public override string NewColorTip => "Ohne Label, bis du etwas anderes wählst (oder immer, wenn die letzte Wahl nicht wiederholt wird).";
    public override string NewColor => "Farbe neuer Lesezeichen";
    public override string RepeatLabelTip => "Ein neues Lesezeichen bekommt das zuletzt gewählte Label bzw. die zuletzt gewählte Farbe; im Namensfeld lässt sich das ändern.";
    public override string RepeatLabel => "Letztes Label oder letzte Farbe wiederholen";
    public override string TimesInPreview => "Vorschau";
    public override string TimesInList => "Liste";
    public override string BookmarkTimesTip => "In der Liste sieht man alle auf einen Blick. In der Vorschau lässt die Liste mehr Platz für den Namen.";
    public override string BookmarkTimes => "Zeiten der Lesezeichen";
    public override string SectionAudioAndSubtitles => "AUDIO UND UNTERTITEL";
    public override string AudioLanguageTip => "Diese Spur wird gewählt, wenn das Video mehrere hat. Gilt ab dem nächsten geöffneten Video.";
    public override string AudioLanguage => "Bevorzugte Audiosprache";
    public override string SubtitleLanguageTip => "Diese Spur wird gewählt, wenn man Untertitel einschaltet. Gilt ab dem nächsten geöffneten Video.";
    public override string SubtitleLanguage => "Bevorzugte Untertitelsprache";
    public override string AppliesOnNextVideo => "Gilt ab dem nächsten geöffneten Video.";
    public override string SubtitlesOnOpen => "Untertitel beim Öffnen eines Videos einschalten";
    public override string NoPreference => "Keine Vorgabe";
    public override string SectionPerformance => "LEISTUNG";
    public override string CacheMemory => "Speicher für den Frame-Cache";
    public override string CacheMemoryTip => "Je mehr, desto mehr Video lässt sich sofort Frame für Frame durchgehen. Gilt ab dem nächsten geöffneten Video.";
    public override string CacheOfTotal(string used, string total) => $"{used} von {total} GB";
    public override string GigabytesUnit => "GB";
    public override string MegabytesUnit => "MB";
    public override string CacheTooBigTip => "Mehr als 90 % des Grafikspeichers: Für andere Programme kann es knapp werden.";
    public override string HardwareDecodingTip => "Spielt über die Grafikkarte ab. Nur ausschalten, wenn ein Video fehlerhaft aussieht.";
    public override string HardwareDecoding => "Hardware-Decodierung";
    public override string SectionClips => "CLIPS EXPORTIEREN";
    public override string ChangeEllipsis => "Ändern…";
    public override string BesideVideo => "Neben dem Video";
    public override string ClipFolderTip => "Wo exportierte Clips gespeichert werden. Wenn dort nicht geschrieben werden kann, landen sie in Videos\\VideoSeeker.";
    public override string ClipFolder => "Zielordner";
    public override string BesideEachVideo => "Neben jedem Video";
    public override string ClipFolderDialogTitle => "Ordner für exportierte Clips";
    public override string ClipClipboardTip => "Als Datei, um sie direkt in einen Chat, in Jira oder in einen Ordner einzufügen.";
    public override string ClipClipboard => "Jeden Clip in die Zwischenablage kopieren";
    public override string LightClipTip => "Megabyte pro Clipsekunde, inklusive Audio. Nie mehr als das Originalvideo.";
    public override string LightClip => "Größe des kompakten Clips";
    public override string MegabytesPerSecond(string value) => $"{value} MB/s";
    public override string SectionGeneral => "ALLGEMEIN";
    public override string InterfaceLanguage => "Sprache";
    public override string InterfaceLanguageTip => "Die Sprache von VideoSeeker. Automatisch: die von Windows. Die Änderung gilt nach einem Neustart.";
    public override string AnimatePanels => "Bereiche animieren";
    public override string AnimatePanelsTip => "Der Lesezeichenbereich und die Zoomleiste gleiten auf und zu.";
    public override string AutomaticLanguage(string language) => $"Automatisch · {language}";
    public override string LanguageChangedRestart => "Die neue Sprache wird nach dem Neustart von VideoSeeker verwendet.";
    public override string LanguageCaption => "Sprache";
    public override string RestartNow => "Jetzt neu starten";
    public override string Later => "Später";
    public override string DefaultFolder => "Standard";
    public override string DefaultFolderTip => "Der Ordner data neben dem Programm.";
    public override string DataFolderTip => "Einstellungen, Lesezeichen und Wiedergabeposition jedes Videos. Die Änderung gilt nach einem Neustart.";
    public override string DataFolder => "Datenordner";
    public override string ChooseInWindows => "In Windows auswählen";
    public override string Unregister => "Entfernen";
    public override string DefaultPlayer => "Standard-Videoplayer";
    public override string DefaultPlayerTip => "Fügt die Videotypen zu „Öffnen mit“ hinzu und öffnet Standard-Apps von Windows, wo man auswählt, für welche.";
    public override string NumberRange => "Zwischen 0,04 und 3600 Sekunden";
    public override string PendingFolder(string folder) => $"Nach dem Neustart wird {folder} verwendet.";
    public override string PendingFolderWithCopy(string folder) => $"Nach dem Neustart wird {folder} verwendet, mit einer Kopie der aktuellen Daten.";
    public override string DataFolderDialogTitle => "Ordner für die Daten von VideoSeeker";
    public override string DataFolderNested => "Der Datenordner darf nicht im aktuellen liegen und ihn auch nicht enthalten.";
    public override string DataFolderHasData(string folder) => $"{folder}\n\nenthält schon Daten von VideoSeeker. Wenn du die aktuellen dorthin kopierst, werden die vorhandenen im Unterordner „anteriores“ aufbewahrt.";
    public override string UseThatData => "Diese Daten verwenden";
    public override string CopyCurrentData => "Aktuelle Daten kopieren";
    public override string MoveDataQuestion(string folder) => $"Deine Daten (Einstellungen, Lesezeichen, Wiedergabeposition jedes Videos) nach\n{folder} mitnehmen?\n\nDer aktuelle Ordner bleibt, wie er ist.";
    public override string MoveData => "Daten mitnehmen";
    public override string StartEmpty => "Ohne Daten beginnen";
    public override string Cancel => "Abbrechen";
    public override string CannotUseFolder(string error) => $"Dieser Ordner kann nicht verwendet werden:\n{error}";
    public override string DataFolderRestart => "Der neue Datenordner wird nach dem Neustart von VideoSeeker verwendet.";
    public override string OpenWithDoubleClick => "Mit Doppelklick öffnen";
    public override string RegisterFailed(string error) => $"Die Videotypen konnten nicht registriert werden:\n{error}";
    public override string UnregisterFailed(string error) => $"Die Registrierung konnte nicht entfernt werden:\n{error}";
    public override string ResetQuestion => "Alle Einstellungen werden auf ihre Standardwerte zurückgesetzt.";
    public override string Reset => "Zurücksetzen";
    public override string ResetTitle => "Einstellungen zurücksetzen";
    public override string DefaultFolderPackagedTip => "Der Ordner der App in Windows. Er wird beim Deinstallieren gelöscht.";
    public override string HoverPreview => "Vorschaubild beim Überfahren der Leisten";
    public override string HoverPreviewTip => "Um zu sehen, was an einer Stelle des Videos ist, ohne hinzuspringen.";
    public override string DataCopyIncomplete(string file) => $"Die Kopie von {file} ist unvollständig";

    // ---- Ventana principal: reproducción ----

    public override string VideoFileFilter => "Videos|*.mkv;*.mp4;*.m4v;*.avi;*.wmv;*.mov;*.webm;*.flv;*.ts;*.m2ts;*.mts;*.mpg;*.mpeg;*.vob;*.3gp;*.ogv|Alle Dateien|*.*";
    public override string OpenVideoTitle => "Video öffnen";
    public override string FileNotFound(string path) => $"Die Datei wurde nicht gefunden:\n{path}";
    public override string FrameNumber(int frame) => $"Frame {frame}";
    public override string FrameNumberApprox(string frame) => $"Frame ~{frame}";
    public override string Frames(int count) => count == 1 ? "1 Frame" : $"{count} Frames";
    public override string FrameOfRange(int frame, int count) => $"Frame {frame}/{count}";
    public override string VideoLoopOn => "Video wird wiederholt";
    public override string VideoLoopOff => "Video wird nicht mehr wiederholt";
    public override string RangeLoopOn(string? region) => $"Wiederholung {ZoneOf(region)} an";
    public override string RangeLoopOff(string? region) => $"Wiederholung {ZoneOf(region)} aus";
    public override string ZoomSection => "In Abschnitt zoomen (±5 s)";
    public override string MarkFirstFromScreen(int frame, string? region) => $"Angezeigten Frame ({frame}) als Anfang {ZoneOf(region)} setzen";
    public override string MarkLastFromScreen(int frame, string? region) => $"Angezeigten Frame ({frame}) als Ende {ZoneOf(region)} setzen";
    public override string MarkFirstFromCursor(int frame, string? region) => $"Frame unter dem Cursor ({frame}) als Anfang {ZoneOf(region)} setzen";
    public override string MarkLastFromCursor(int frame, string? region) => $"Frame unter dem Cursor ({frame}) als Ende {ZoneOf(region)} setzen";
    public override string NormalSpeed => "1× (normal)";
    public override string SpeedToast(string speed) => $"Geschwindigkeit {speed}";
    public override string VolumeToast(int volume) => $"Lautstärke {volume} %";
    public override string VolumeMutedToast(int volume) => $"Lautstärke {volume} % (stumm)";
    public override string Audio => "Audio";
    public override string Subtitles => "Untertitel";
    public override string SubtitlesOff => "Aus";
    public override string NoTracks => "Keine Spuren";
    public override string Track(long id) => $"Spur {id}";
    public override string ControlsPinned => "Steuerelemente fixiert";
    public override string ControlsHidden => "Steuerelemente ausgeblendet: Maus nach unten bewegen, um sie zu sehen";
    public override string ControlsPinnedTip => "Steuerelemente fixiert (Tab)";
    public override string PinControlsTip => "Steuerelemente fixieren (Tab)";
    public override string VideoGone(string name) => $"Nicht mehr vorhanden: {name}";
    public override string HistoryBackTip => "Zurück zum davor geöffneten Video (Alt + ←)";
    public override string HistoryForwardTip => "Vor zum danach geöffneten Video (Alt + →)";
    public override string HistoryAllBack(int count) => $"Rechtsklick: die {count} vorherigen";
    public override string HistoryAllForward(int count) => $"Rechtsklick: die {count} nächsten";

    // ---- Bookmarks ----

    public override string NewBookmark => "Neues Lesezeichen";
    public override string RenameBookmark => "Lesezeichen umbenennen";
    public override string SelectBookmarkFirst => "Zuerst ein Lesezeichen auswählen (auf den Leisten oder im Bereich anklicken)";
    public override string BookmarkDeleted(string name) => $"„{name}“ gelöscht · Strg + Z holt es zurück";
    public override string BookmarksDeleted(int count) => $"{count} Lesezeichen gelöscht · Strg + Z holt sie zurück";
    public override string UndoFailed(string error) => $"Rückgängigmachen nicht möglich: {error}";
    public override string RedoFailed(string error) => $"Wiederholen nicht möglich: {error}";
    public override string NothingToUndo => "Nichts rückgängig zu machen";
    public override string NothingToRedo => "Nichts zu wiederholen";
    public override string Undone(string step) => $"Rückgängig gemacht: {step}";
    public override string Redone(string step) => $"Wiederholt: {step}";
    public override string InVideo(string video) => $" (in „{video}“)";
    public override string VideoHasNoBookmarks => "Dieses Video hat keine Lesezeichen (B fügt eines hinzu)";
    public override string NoBookmarkNumber(int number, int count) => $"Es gibt kein Lesezeichen {number} (es gibt {count})";
    public override string NoMoreBookmarksAhead => "Keine weiteren Lesezeichen danach";
    public override string NoMoreBookmarksBehind => "Keine weiteren Lesezeichen davor";
    public override string AddBookmarkHere => "Lesezeichen hier hinzufügen";
    public override string BookmarksPanel => "Lesezeichenbereich";
    public override string Rename => "Umbenennen";
    public override string UseFrameAsThumbnail(int frame, string? region) => region is null ? $"Aktuellen Frame ({frame}) als Vorschaubild verwenden" : $"Aktuellen Frame ({frame}) als Vorschaubild von „{region}“ verwenden";
    public override string FrameOutsideRegion(int frame, string time, string name, string range) => $"Der aktuelle Frame ({frame}, {time}) liegt außerhalb der Region „{name}“ ({range}).\n\nTrotzdem als Vorschaubild verwenden?";
    public override string UseIt => "Verwenden";
    public override string ResetThumbnail(string? region) => region is null ? "Wieder das Vorschaubild des ersten Frames verwenden" : $"Wieder das Vorschaubild des ersten Frames von „{region}“ verwenden";
    public override string Delete => "Löschen";
    public override string DeleteCount(int count) => $"Alle {count} löschen";
    public override string BookmarksExported(int count) => count == 1 ? "1 Lesezeichen exportiert" : $"{count} Lesezeichen exportiert";
    public override string CannotWriteBesideVideo(string error) => $"Neben dem Video konnte nicht geschrieben werden:\n{error}";
    public override string MarksShown => "Lesezeichen-Markierungen sichtbar";
    public override string MarksHidden => "Lesezeichen-Markierungen ausgeblendet (Strg + Umschalt + B zeigt sie)";
    public override string HideMarksTip => "Lesezeichen-Markierungen auf den Leisten ausblenden (Strg + Umschalt + B). Der Bereich funktioniert weiter.";
    public override string ShowMarksTip => "Lesezeichen-Markierungen auf den Leisten anzeigen (Strg + Umschalt + B)";
    public override string CollectionButton(string name) => $"Sammlung: {name}  ▾";
    public override string CollectionEmpty => "leer";
    public override string BookmarksInVideos(int bookmarks, int videos) => videos == 1 ? $"{bookmarks} in 1 Video" : $"{bookmarks} in {videos} Videos";
    public override string NewCollection => "Neue Sammlung…";
    public override string NewCollectionName => "Name der neuen Sammlung";
    public override string CollectionExists(string name) => $"Es gibt schon eine Sammlung „{name}“";
    public override string RenameCollection(string name) => $"„{name}“ umbenennen…";
    public override string NewNameFor(string name) => $"Neuer Name für „{name}“";
    public override string RenameFailed(string error) => $"Umbenennen nicht möglich: {error}";
    public override string CollectionRenamed(string name) => $"Sammlung in „{name}“ umbenannt";
    public override string DeleteCollection(string name) => $"„{name}“ löschen…";
    public override string ImportCollectionOrBookmarks => "Sammlung oder Lesezeichen importieren…";
    public override string ExportCollectionMenu(string name) => $"„{name}“ exportieren…";
    public override string CollectionIsEmpty => "Sie ist leer.";
    public override string CollectionWillBeLost(int bookmarks, int videos) => videos == 1 ? $"Sie enthält {bookmarks} Lesezeichen aus 1 Video, die verloren gehen." : $"Sie enthält {bookmarks} Lesezeichen aus {videos} Videos, die verloren gehen.";
    public override string DeleteCollectionQuestion(string name, string detail) => $"Sammlung „{name}“ löschen?\n{detail}";
    public override string DeleteFailed(string error) => $"Löschen nicht möglich: {error}";
    public override string CollectionToast(string name) => $"Sammlung „{name}“";
    public override string BookmarkCount(int count) => count == 1 ? "1 Lesezeichen" : $"{count} Lesezeichen";
    public override string VideoCount(int count) => count == 1 ? "1 Video" : $"{count} Videos";
    public override string BookmarksFromVideos(int bookmarks, int videos) => $"{bookmarks} Lesezeichen aus {videos} Videos";
    public override string ShowVideoInExplorer => "Video im Explorer anzeigen";
    public override string ShowVideoInExplorerMissing => "Video im Explorer anzeigen (nicht gefunden)";
    public override string MovedTo(string collection) => $"Nach „{collection}“ verschoben · Strg + Z holt es zurück";
    public override string MovedManyTo(int count, string collection) => $"{count} nach „{collection}“ verschoben · Strg + Z holt sie zurück";
    public override string ChangeFailed(string error) => $"Ändern nicht möglich: {error}";
    public override string Color => "Farbe";
    public override string OwnColor => "Eigene";
    public override string OtherColor => "Andere Farbe…";
    public override string MoveToCollection => "In Sammlung verschieben";
    public override string MoveToNewCollectionName => "Name der Sammlung, in die sie verschoben werden";
    public override string MoveFailed(string error) => $"Verschieben nicht möglich: {error}";
    public override string BookmarkVideoNotFound(string video) => $"Das Video dieses Lesezeichens wurde nicht gefunden:\n{video}";
    public override string LocateIt => "Suchen…";
    public override string LocateTitle(string name) => $"„{name}“ suchen";
    public override string CannotReadFile(string error) => $"Die Datei konnte nicht gelesen werden:\n{error}";
    public override string NotSameVideo(string chosen, string name) => $"„{chosen}“ ist nicht dasselbe Video wie „{name}“ (der Inhalt ist anders: eine andere Kopie, eine andere Version oder ein anderes Video). Die Zeiten der Lesezeichen stimmen vielleicht nicht.\n\nTrotzdem verwenden? Die Lesezeichen dieses Videos (in allen Sammlungen) gehen auf diese Datei über; Strg + Z macht es rückgängig.";
    public override string UseItAnyway => "Trotzdem verwenden";
    public override string NotAllMoved(string error) => $"Nicht alle konnten übertragen werden: {error}";
    public override string VideosCountName(int count) => $"{count} Videos";
    public override string BookmarkOfZoomedRange => "Lesezeichen aus dem gezoomten Abschnitt";

    // ---- Pasos de deshacer (van detrás de «Deshecho:») ----

    public override string StepAdd(string name) => $"„{name}“ hinzufügen";
    public override string StepEdit(string name) => $"„{name}“ bearbeiten";
    public override string StepRename(string name) => $"„{name}“ umbenennen";
    public override string StepLabelOf(string name) => $"Label von „{name}“";
    public override string StepLabelOfMany(int count) => $"Label von {count} Lesezeichen";
    public override string StepColorOf(string name) => $"Farbe von „{name}“";
    public override string StepColorOfMany(int count) => $"Farbe von {count} Lesezeichen";
    public override string StepDelete(string name) => $"„{name}“ löschen";
    public override string StepDeleteMany(int count) => $"{count} Lesezeichen löschen";
    public override string StepMoveTo(string name, string collection) => $"„{name}“ nach „{collection}“ verschieben";
    public override string StepMoveManyTo(int count, string collection) => $"{count} Lesezeichen nach „{collection}“ verschieben";
    public override string StepMoveStart(string name) => $"Anfang von „{name}“ verschieben";
    public override string StepMoveEnd(string name) => $"Ende von „{name}“ verschieben";
    public override string StepMove(string name) => $"„{name}“ verschieben";
    public override string StepRelink(string from, string to) => $"Lesezeichen von „{from}“ auf „{to}“ übertragen";
    public override string StepImportBookmarks => "Lesezeichen importieren";
    public override string StepImportCollection(string name) => $"Sammlung „{name}“ importieren";
    public override string DefaultBookmarkName(int number) => $"Lesezeichen {number}";
    public override string StepAdjust(string name) => $"„{name}“ anpassen";
    public override string StepSetStartOf(string name) => $"Anfang von „{name}“ setzen";
    public override string StepSetEndOf(string name) => $"Ende von „{name}“ setzen";

    // ---- Etiquetas ----

    public override string LabelChipTip(string name) => $"Nur „{name}“ anzeigen (auf andere klicken, um sie hinzuzunehmen). Rechtsklick: umbenennen, Farbe, löschen…";
    public override string LabelChipShortcut(string shortcut) => $"\n{shortcut} weist es dem ausgewählten Lesezeichen zu.";
    public override string NoLabel => "Ohne Label";
    public override string NoLabelChipTip => "Nur die ohne Label anzeigen";
    public override string AddLabelChip => "+ Label";
    public override string AddLabelChipTip => "Neues Label (zum Beispiel „Boss“ oder „Zwischensequenz“), um auf einen Blick zu sehen, was jedes Lesezeichen markiert";
    public override string NoLabelsInCollection => "Diese Sammlung hat keine Labels: „+ Label“ im Lesezeichenbereich (Strg + B)";
    public override string NoLabelNumber(int number, int count) => $"Es gibt kein Label {number} (es gibt {count})";
    public override string LabelToast(string name) => $"Label „{name}“";
    public override string BookmarkWithLabel(string name, string label) => $"„{name}“: {label}";
    public override string Labels => "Labels";
    public override string NewLabel => "Neues Label…";
    public override string NewLabelName => "Name des neuen Labels";
    public override string LabelExists(string name) => $"Es gibt schon ein Label „{name}“";
    public override string LabelsChangeFailed(string error) => $"Die Labels konnten nicht geändert werden: {error}";
    public override string NoBookmarks => "keine Lesezeichen";
    public override string RenameEllipsis => "Umbenennen…";
    public override string MoveLeft => "Nach links verschieben";
    public override string MoveRight => "Nach rechts verschieben";
    public override string AdoptLabelColor(string color, int count) => $"Allen Lesezeichen in {color} ohne Label zuweisen ({count})";
    public override string AdoptLabelSameColor(int count) => $"Allen Lesezeichen in seiner Farbe ohne Label zuweisen ({count})";
    public override string DeleteEllipsis => "Löschen…";
    public override string DeleteLabelQuestion(string name, int uses) => $"Label „{name}“ löschen?\n" + (uses == 1 ? "1 Lesezeichen hat es: Es bleibt ohne Label und behält seine Farbe." : $"{uses} Lesezeichen haben es: Sie bleiben ohne Label und behalten ihre Farbe.");
    public override string StepCreateLabel(string name) => $"Label „{name}“ erstellen";
    public override string StepRenameLabel(string name) => $"Label „{name}“ umbenennen";
    public override string StepLabelColor(string name) => $"Farbe des Labels „{name}“";
    public override string StepMoveLabel(string name) => $"Label „{name}“ verschieben";
    public override string StepAdoptLabel(string name, int count) => $"{count} Lesezeichen das Label „{name}“ geben";
    public override string StepDeleteLabel(string name) => $"Label „{name}“ löschen";
    public override string LabelOrColor => "Label oder Farbe";
    public override string AddLabelChipLong => "+ Neues Label";
    public override string AddLabelChipShort => "+ Neu";
    public override string AddLabelChipNameTip => "Label erstellen und zuweisen";
    public override string ColorWithoutLabel(string color) => $"{color} (ohne Label)";

    // ---- Colores de la paleta ----

    public override string ColorPink => "Rosa";
    public override string ColorRed => "Rot";
    public override string ColorOrange => "Orange";
    public override string ColorViolet => "Violett";
    public override string ColorCyan => "Cyan";
    public override string ColorLime => "Limette";
    public override string ColorBrown => "Braun";
    public override string ColorGray => "Grau";
    public override string ColorWhite => "Weiß";

    // ---- Compartir colecciones y bookmarks ----

    public override string CollectionHasNoBookmarks(string name) => $"Die Sammlung „{name}“ hat keine Lesezeichen";
    public override string ExportCollectionTitle => "Sammlung exportieren";
    public override string CollectionFileFilter => "Lesezeichensammlung|*.collection.json";
    public override string CollectionExported(string name) => $"Sammlung „{name}“ exportiert";
    public override string CollectionSaveFailed(string error) => $"Die Sammlung konnte nicht gespeichert werden:\n{error}";
    public override string ImportTitle => "Eine Sammlung oder die Lesezeichen eines Videos importieren";
    public override string ImportFileFilter => "Sammlungen und Lesezeichen|*.collection.json;*.bookmarks.json|Alle JSON-Dateien|*.json";
    public override string NotBookmarksFile(string file) => $"„{file}“ ist keine Lesezeichendatei";
    public override string CannotReadNamed(string file, string error) => $"„{file}“ konnte nicht gelesen werden: {error}";
    public override string FileHasNoBookmarks(string file) => $"„{file}“ hat keine Lesezeichen";
    public override string UnknownVideo => "Unbekannt, zu welchem Video sie gehören: Video öffnen und sie erneut hineinziehen";
    public override string ImportFailed(string error) => $"Import nicht möglich: {error}";
    public override string AlreadyHadBookmarks => "Diese Lesezeichen hattest du schon";
    public override string BookmarksAddedToThisVideo(int count) => count == 1 ? "1 Lesezeichen zu diesem Video hinzugefügt" : $"{count} Lesezeichen zu diesem Video hinzugefügt";
    public override string BookmarksAddedTo(int count, string video, string collection) => $"{count} Lesezeichen zu „{video}“ in der Sammlung „{collection}“ hinzugefügt";
    public override string ImportedCollectionName => "Importiert";
    public override string ImportCollectionFailed(string error) => $"Die Sammlung konnte nicht importiert werden: {error}";
    public override string CollectionAdded(string name, string videos, string bookmarks) => $"Sammlung „{name}“ hinzugefügt: {videos}, {bookmarks}";

    // ---- Exportar clips ----

    public override string ExportClip => "Clip exportieren";
    public override string ExportClipOf(string? region) => region is null ? "Clip aus dem gezoomten Abschnitt exportieren" : $"Clip von „{region}“ exportieren";
    public override string ExactOriginal => "Exaktes Original";
    public override string ExactOriginalTip => "Gleicher Codec und gleiche Bitrate, am exakten Frame geschnitten";
    public override string CopyOriginal(string start) => $"Original ohne Neukodierung · {start}";
    public override string CopyOriginalTip => "Direkte Kopie: sofort und verlustfrei, beginnt aber an einem Keyframe";
    public override string LightClipMenu(string rate) => $"Kompakt · {rate} MB/s";
    public override string LightClipMenuTip => "MP4 (H.264 und AAC) für Chats und Jira, höchstens 1080p";
    public override string StartsAtPreviousKeyframe => "beginnt am vorherigen Keyframe";
    public override string ExactCut => "exakter Schnitt";
    public override string StartsSecondsEarlier(string seconds) => $"beginnt {seconds} s früher";
    public override string StartsFramesEarlier(int frames) => frames == 1 ? "beginnt 1 Frame früher" : $"beginnt {frames} Frames früher";
    public override string ExportNeedsRange => "Um einen Clip zu exportieren, eine Region wählen oder zuerst einen Abschnitt markieren (I / O, Z oder mit der rechten Taste ziehen)";
    public override string ExportWhat(string what) => $"{what} exportieren";
    public override string QuotedName(string name) => $"„{name}“";
    public override string ClipQueued => "Clip in der Warteschlange: Er wird exportiert, wenn der vorherige fertig ist";
    public override string ExportingClip => "Clip wird exportiert…";
    public override string ExportCancelled => "Export abgebrochen";
    public override string ClipSaved => "Clip gespeichert";
    public override string ClipFailed => "Der Clip konnte nicht exportiert werden";
    public override string InFolder(string folder) => $"In {folder}";
    public override string CopiedForChat => "In die Zwischenablage kopiert: Strg + V fügt es in einen Chat oder in Jira ein";
    public override string CopiedForFolder => "In die Zwischenablage kopiert: Strg + V fügt es in einen Chat oder in einen Ordner ein";
    public override string CopyFailed => "Kopieren in die Zwischenablage nicht möglich";
    public override string Copy => "Kopieren";
    public override string ExportingPercent(int percent) => $"Export {percent} %";
    public override string PreparingClip => "Clip wird vorbereitet";
    public override string MoreQueued(int count) => $"\nund {count} weitere in der Warteschlange";
    public override string ClickToCancel => "\nKlicken zum Abbrechen";
    public override string ClipReady => "Clip fertig";
    public override string ExportError => "Exportfehler";
    public override string ClickToGo => "\nKlicken zum Anzeigen oder Öffnen";
    public override string ClickToSeeWhy => "Klicken, um den Grund zu sehen";
    public override string CancelClip(string name) => $"{name} abbrechen";
    public override string TheClip => "den Clip";
    public override string CancelQueuedToo(int count) => $"Auch die {count} in der Warteschlange abbrechen";
    public override string CopyToClipboard => "In die Zwischenablage kopieren";
    public override string ClipCopied => "Clip in die Zwischenablage kopiert";
    public override string PreviousClips => "Frühere Clips";
    public override string RemoveFromHere => "Hier entfernen";
    public override string ExportingOnExit => "Gerade wird ein Clip exportiert. Wenn du jetzt beendest, wird er abgebrochen.";
    public override string Quit => "Beenden";
    public override string Wait => "Warten";
    public override string FfmpegMissing => "ffmpeg.exe fehlt im Programmordner.";
    public override string FfmpegFailed => "ffmpeg konnte den Abschnitt nicht exportieren.";
    public override string FfmpegNotStarted => "ffmpeg konnte nicht gestartet werden.";
    public override string FfmpegExitCode(int code) => $"ffmpeg wurde mit Code {code} beendet.";
    public override string LightClipSuffix => " (kompakt)";

    // ---- Otro color ----

    public override string OtherColorTitle => "Andere Farbe";
    public override string UseThisColor => "Diese Farbe verwenden";
    public override string ReservedRangeAmber => "Bernsteingelb des gezoomten Abschnitts";
    public override string ReservedPlayedBlue => "Blau des abgespielten Teils";
    public override string ReservedCacheGreen => "Grün des Frame-Caches";
    public override string ColorClash(string clash) => $"Diese Farbe ähnelt stark dem {clash} auf den Leisten und ist leicht damit zu verwechseln.\n\nTrotzdem verwenden?";

    // ---- Avisos ----

    public override string Ok => "OK";

    // ---- Abrir con doble clic (registro de Windows) ----

    public override string AppDescription => "Videoplayer";
    public override string VideoFileType(string extension) => $"{extension}-Video";

    public override string LanguageName(string code) => code switch
    {
        "ger" => "Deutsch",
        "ara" => "Arabisch",
        "cat" => "Katalanisch",
        "chi" => "Chinesisch",
        "kor" => "Koreanisch",
        "dan" => "Dänisch",
        "spa" => "Spanisch",
        "baq" => "Baskisch",
        "fin" => "Finnisch",
        "fre" => "Französisch",
        "glg" => "Galicisch",
        "gre" => "Griechisch",
        "heb" => "Hebräisch",
        "hin" => "Hindi",
        "eng" => "Englisch",
        "ita" => "Italienisch",
        "jpn" => "Japanisch",
        "dut" => "Niederländisch",
        "nor" => "Norwegisch",
        "pol" => "Polnisch",
        "por" => "Portugiesisch",
        "rus" => "Russisch",
        "swe" => "Schwedisch",
        "tha" => "Thailändisch",
        "tur" => "Türkisch",
        "ukr" => "Ukrainisch",
        "vie" => "Vietnamesisch",
        _ => code,
    };
}
