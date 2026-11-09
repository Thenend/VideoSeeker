// Textos en inglés.

namespace VideoSeeker.App.Localization;

public sealed class EnglishStrings : Strings
{
    // ---- Teclas (para componer atajos) ----

    public override string KeyCtrl => "Ctrl";
    public override string KeyShift => "Shift";
    public override string KeyDelete => "Del";
    public override string KeyEsc => "Esc";

    // ---- Ventana principal ----

    public override string EmptyHint => "Drag a video here or press Ctrl + O";
    public override string CloseZoomTip => "Close zoom (Esc)";
    public override string RangeLoopTip(string? region) => $"Loop {ZoneOf(region)} (Shift + R)";
    public override string RangeLoopOffTip(string? region) => $"Stop looping {ZoneOf(region)} (Shift + R)";
    public override string MarkLastTip(string? region) => $"Set the end of {ZoneOf(region)} to the current frame (O)";
    public override string MarkFirstTip(string? region) => $"Set the start of {ZoneOf(region)} to the current frame (I)";
    public override string VideoLoopTip => "Loop video (R)";
    public override string VideoLoopOffTip => "Stop looping the video (R)";
    public override string PreviousFrameTip => "Previous frame (,) · wheel: frame by frame";
    public override string NextFrameTip => "Next frame (.) · wheel: frame by frame";
    public override string PlayTip => "Play / pause (Space). J / K / L: reverse / pause / forward · wheel: frame by frame";
    public override string SpeedTip => "Speed · wheel: ±0.1 · middle button: 1×";
    public override string OpenVideoTip => "Open video (Ctrl + O)";
    public override string RangePositionTip => "Relative to the zoomed range";
    public override string CurrentBookmarkTip => "Current bookmark ([ / ] previous / next, 1–9 and 0 jump to the first ten)";
    public override string BookmarksButtonTip => "Bookmarks panel (Ctrl + B) · add: B";
    public override string FullscreenTip => "Full screen (F)";
    public override string SettingsTip => "Settings (Ctrl + ,)";
    public override string TracksTip => "Audio and subtitles (A / S)";
    public override string VolumeTip => "Volume";
    public override string MuteTip => "Mute (M)";
    public override string CloseBookmarkPanelTip => "Close (Ctrl + B)";
    public override string ExportVideoBookmarksTip => "Export this video's bookmarks to share them";
    public override string ExportCollectionTip => "Export the whole collection to share it";
    public override string BookmarksTitle => "Bookmarks";
    public override string CollectionTip => "Bookmark collection: each one keeps its own bookmarks for every video. Switch, create, rename or delete.";
    public override string SearchPlaceholder => "Search (Ctrl + F)";
    public override string ScopeThisVideo => "This video";
    public override string ScopeAllVideos => "All videos";
    public override string PanelHint => "B creates a region from the zoomed range, or a one-frame region if there isn't one (O sets its end). Click or Enter: go to it and zoom in. Ctrl / Shift + click: select several. F2 renames, Del deletes, Ctrl + Z undoes.";
    public override string BookmarkKeyTip => "Key to go to this bookmark";
    public override string NameHint => "Enter saves · Esc keeps the old name";
    public override string NameHintWithLabels => "Enter saves · Esc cancels · Alt + number: label";
    public override string GeneratingThumbnail => "Generating thumbnail…";
    public override string Close => "Close";
    public override string ShowInFolder => "Show in folder";
    public override string Open => "Open";
    public override string ZoneOf(string? region) => region is null ? "the range" : $"“{region}”";
    public override string SaveZoneTip => "Save the zoomed range as a bookmark (B)";
    public override string NewLabelTitle => "New label";
    public override string NewLabelBoxTip => "Enter creates and applies it · Esc goes back";
    public override string ZoneNameTip(string name) => $"The zoomed range is the region “{name}”: I / O, ⇤ ⇥ and the flags edit it";
    public override string CloseZoomDeselectTip(string name) => $"Close zoom and deselect “{name}” (Esc)";
    public override string Loading => "Loading…";

    // ---- Ajustes ----

    public override string SettingsTitle => "Settings — VideoSeeker";
    public override string ResetDefaults => "Reset to defaults";
    public override string SupportTip => "VideoSeeker is free. If you find it useful, you can buy the author a coffee.";
    public override string Support => "Support";
    public override string SourceCode => "Source code";
    public override string SourceCodeTip => "Free software, licensed under GPL v3.";
    public override string Version(string version) => $"Version {version}";
    public override string SectionBars => "BARS";
    public override string MoveRegionsTip => "Drag the middle of the zoomed range or of a bookmark region to move it whole. When off, dragging there moves the playhead.";
    public override string MoveRegions => "Move whole regions by dragging their middle";
    public override string EdgeFrameTip => "When dragging an edge of the zoomed range or of a bookmark. When off, the image stays as it is.";
    public override string EdgeFrame => "Show the frame at the edge being dragged";
    public override string CachedLineTip => "Shows under the bars what's already cached: jumping there is instant.";
    public override string CachedLine => "Green line for cached frames";
    public override string MarksTip => "Shortcut: Ctrl + Shift + B.";
    public override string Marks => "Bookmarks on the bars";
    public override string SectionNavigation => "NAVIGATION";
    public override string SecondsUnit => "s";
    public override string SeekTip => "Seconds jumped by each arrow key press: alone, with Shift or with Ctrl.";
    public override string Seek => "Jump length (← →)";
    public override string ScrubTip => "How many pixels to drag across the image, holding the button, to move one frame.";
    public override string Scrub => "Scrubbing sensitivity";
    public override string PixelsPerFrame(int pixels) => $"{pixels} px per frame";
    public override string WheelSpeed => "Speed";
    public override string WheelVolume => "Volume";
    public override string WheelTip => "Speed: ±0.1× (middle button: 1×). Volume: ±5%.";
    public override string Wheel => "Mouse wheel over the video";
    public override string Resume => "Resume each video where you left off";
    public override string SectionTimeAndBookmarks => "TIME AND BOOKMARKS";
    public override string TimeFormatTip => "In 00:01:23:11, the last number is the frame within the second.";
    public override string TimeFormat => "Time format";
    public override string NewColorTip => "Used with no label until you pick something else (or always, if the last choice isn't repeated).";
    public override string NewColor => "Color of new bookmarks";
    public override string RepeatLabelTip => "A new bookmark gets the label or color picked last time; you can change it in the name box.";
    public override string RepeatLabel => "Repeat the last label or color";
    public override string TimesInPreview => "Preview";
    public override string TimesInList => "List";
    public override string BookmarkTimesTip => "The list shows them all at a glance. With the preview, the list leaves more room for the name.";
    public override string BookmarkTimes => "Bookmark times";
    public override string SectionAudioAndSubtitles => "AUDIO AND SUBTITLES";
    public override string AudioLanguageTip => "Picks that track when a video has several. Takes effect with the next video you open.";
    public override string AudioLanguage => "Preferred audio language";
    public override string SubtitleLanguageTip => "Picks that track when subtitles are turned on. Takes effect with the next video you open.";
    public override string SubtitleLanguage => "Preferred subtitle language";
    public override string AppliesOnNextVideo => "Takes effect with the next video you open.";
    public override string SubtitlesOnOpen => "Turn on subtitles when opening a video";
    public override string NoPreference => "No preference";
    public override string SectionPerformance => "PERFORMANCE";
    public override string CacheMemory => "Frame cache memory";
    public override string CacheMemoryTip => "More memory lets you step frame by frame instantly through a longer stretch of video. Takes effect with the next video you open.";
    public override string CacheOfTotal(string used, string total) => $"{used} of {total} GB";
    public override string GigabytesUnit => "GB";
    public override string MegabytesUnit => "MB";
    public override string CacheTooBigTip => "Over 90% of the graphics card's memory: other programs may run short.";
    public override string HardwareDecodingTip => "Plays using the graphics card. Turn it off only if a video looks wrong.";
    public override string HardwareDecoding => "Hardware decoding";
    public override string SectionClips => "EXPORT CLIPS";
    public override string ChangeEllipsis => "Change…";
    public override string BesideVideo => "Next to the video";
    public override string ClipFolderTip => "Where exported clips are saved. If that folder can't be written to, they go to Videos\\VideoSeeker.";
    public override string ClipFolder => "Destination folder";
    public override string BesideEachVideo => "Next to each video";
    public override string ClipFolderDialogTitle => "Folder for exported clips";
    public override string ClipClipboardTip => "As a file, to paste it straight into a chat, Jira or a folder.";
    public override string ClipClipboard => "Copy each clip to the clipboard";
    public override string LightClipTip => "Megabytes per second of clip, audio included. Never more than the original video.";
    public override string LightClip => "Light clip size";
    public override string MegabytesPerSecond(string value) => $"{value} MB/s";
    public override string SectionGeneral => "GENERAL";
    public override string InterfaceLanguage => "Language";
    public override string InterfaceLanguageTip => "VideoSeeker's language. Automatic follows Windows. Takes effect after a restart.";
    public override string AutomaticLanguage(string language) => $"Automatic · {language}";
    public override string LanguageChangedRestart => "The new language will be used when VideoSeeker restarts.";
    public override string LanguageCaption => "Language";
    public override string RestartNow => "Restart now";
    public override string Later => "Later";
    public override string DefaultFolder => "Default";
    public override string DefaultFolderTip => "The data folder, next to the program.";
    public override string DataFolderTip => "Settings, bookmarks and where you left off in each video. Takes effect after a restart.";
    public override string DataFolder => "Data folder";
    public override string ChooseInWindows => "Choose in Windows";
    public override string Unregister => "Remove";
    public override string DefaultPlayer => "Default video player";
    public override string DefaultPlayerTip => "Adds the video types to “Open with” and opens Windows Default apps, where you choose which ones.";
    public override string NumberRange => "Between 0.04 and 3600 seconds";
    public override string PendingFolder(string folder) => $"{folder} will be used after a restart.";
    public override string PendingFolderWithCopy(string folder) => $"{folder} will be used after a restart, with a copy of the current data.";
    public override string DataFolderDialogTitle => "Folder for VideoSeeker's data";
    public override string DataFolderNested => "The data folder can't be inside the current one or contain it.";
    public override string DataFolderHasData(string folder) => $"{folder}\n\nalready has VideoSeeker data. If you copy the current data there, the existing data is kept in its “anteriores” subfolder.";
    public override string UseThatData => "Use that data";
    public override string CopyCurrentData => "Copy the current data";
    public override string MoveDataQuestion(string folder) => $"Move your data (settings, bookmarks, where you left off in each video) to\n{folder}?\n\nThe current folder stays as it is.";
    public override string MoveData => "Move the data";
    public override string StartEmpty => "Start empty";
    public override string Cancel => "Cancel";
    public override string CannotUseFolder(string error) => $"That folder can't be used:\n{error}";
    public override string DataFolderRestart => "The new data folder will be used when VideoSeeker restarts.";
    public override string OpenWithDoubleClick => "Open with a double click";
    public override string RegisterFailed(string error) => $"The video types couldn't be registered:\n{error}";
    public override string UnregisterFailed(string error) => $"The registration couldn't be removed:\n{error}";
    public override string ResetQuestion => "All settings will go back to their default values.";
    public override string Reset => "Reset";
    public override string ResetTitle => "Reset settings";
    public override string DefaultFolderPackagedTip => "The app's folder in Windows. It's deleted when the app is uninstalled.";
    public override string HoverPreview => "Thumbnail when hovering over the bars";
    public override string HoverPreviewTip => "See what's at any point in the video without going there.";
    public override string DataCopyIncomplete(string file) => $"The copy of {file} was incomplete";

    // ---- Ventana principal: reproducción ----

    public override string VideoFileFilter => "Videos|*.mkv;*.mp4;*.m4v;*.avi;*.wmv;*.mov;*.webm;*.flv;*.ts;*.m2ts;*.mts;*.mpg;*.mpeg;*.vob;*.3gp;*.ogv|All files|*.*";
    public override string OpenVideoTitle => "Open video";
    public override string FileNotFound(string path) => $"The file can't be found:\n{path}";
    public override string FrameNumber(int frame) => $"frame {frame}";
    public override string FrameNumberApprox(string frame) => $"frame ~{frame}";
    public override string Frames(int count) => count == 1 ? "1 frame" : $"{count} frames";
    public override string FrameOfRange(int frame, int count) => $"frame {frame}/{count}";
    public override string VideoLoopOn => "Video loop on";
    public override string VideoLoopOff => "Video loop off";
    public override string RangeLoopOn(string? region) => $"Looping {ZoneOf(region)}";
    public override string RangeLoopOff(string? region) => $"Stopped looping {ZoneOf(region)}";
    public override string RangeLoopMenu(string? region) => $"Loop {ZoneOf(region)}";
    public override string ZoomSection => "Zoom into section (±5 s)";
    public override string MarkFirstFromScreen(int frame, string? region) => $"Set the start of {ZoneOf(region)} to the current frame ({frame})";
    public override string MarkLastFromScreen(int frame, string? region) => $"Set the end of {ZoneOf(region)} to the current frame ({frame})";
    public override string MarkFirstFromCursor(int frame, string? region) => $"Set the start of {ZoneOf(region)} to the frame under the cursor ({frame})";
    public override string MarkLastFromCursor(int frame, string? region) => $"Set the end of {ZoneOf(region)} to the frame under the cursor ({frame})";
    public override string CloseZoom => "Close zoom";
    public override string NormalSpeed => "1× (normal)";
    public override string SpeedToast(string speed) => $"Speed {speed}";
    public override string VolumeToast(int volume) => $"Volume {volume}%";
    public override string VolumeMutedToast(int volume) => $"Volume {volume}% (muted)";
    public override string Audio => "Audio";
    public override string Subtitles => "Subtitles";
    public override string SubtitlesOff => "Off";
    public override string NoTracks => "No tracks";
    public override string Track(long id) => $"Track {id}";
    public override string ControlsPinned => "Controls pinned";
    public override string ControlsHidden => "Controls hidden: move the mouse to the bottom to see them";
    public override string ControlsPinnedTip => "Controls pinned (Tab)";
    public override string PinControlsTip => "Pin the controls (Tab)";
    public override string VideoGone(string name) => $"Not found: {name}";
    public override string HistoryBackTip => "Back to the previous video (Alt + ←)";
    public override string HistoryForwardTip => "Forward to the next video (Alt + →)";
    public override string CloseZoomDeselect(string name) => $"Close zoom and deselect “{name}”";
    public override string HistoryAllBack(int count) => $"Right-click: the previous {count}";
    public override string HistoryAllForward(int count) => $"Right-click: the next {count}";

    // ---- Bookmarks ----

    public override string NewBookmark => "New bookmark";
    public override string RenameBookmark => "Rename bookmark";
    public override string SelectBookmarkFirst => "Select a bookmark first (click it on the bars or in the panel)";
    public override string BookmarkDeleted(string name) => $"Deleted “{name}” · Ctrl + Z brings it back";
    public override string BookmarksDeleted(int count) => $"Deleted {count} bookmarks · Ctrl + Z brings them back";
    public override string UndoFailed(string error) => $"Couldn't undo: {error}";
    public override string RedoFailed(string error) => $"Couldn't redo: {error}";
    public override string NothingToUndo => "Nothing to undo";
    public override string NothingToRedo => "Nothing to redo";
    public override string Undone(string step) => $"Undone: {step}";
    public override string Redone(string step) => $"Redone: {step}";
    public override string InVideo(string video) => $" (in “{video}”)";
    public override string VideoHasNoBookmarks => "This video has no bookmarks (B adds one)";
    public override string NoBookmarkNumber(int number, int count) => $"There's no bookmark {number} (there are only {count})";
    public override string NoMoreBookmarksAhead => "No more bookmarks ahead";
    public override string NoMoreBookmarksBehind => "No more bookmarks behind";
    public override string AddBookmarkHere => "Add bookmark here";
    public override string BookmarksPanel => "Bookmarks panel";
    public override string Rename => "Rename";
    public override string UseFrameAsThumbnail(int frame, string? region) => region is null ? $"Use the current frame ({frame}) as the thumbnail" : $"Use the current frame ({frame}) as the thumbnail for “{region}”";
    public override string FrameOutsideRegion(int frame, string time, string name, string range) => $"The current frame ({frame}, {time}) is outside the region “{name}” ({range}).\n\nUse it as the thumbnail anyway?";
    public override string UseIt => "Use it";
    public override string ResetThumbnail(string? region) => region is null ? "Reset the thumbnail to the first frame" : $"Reset the thumbnail of “{region}” to its first frame";
    public override string Delete => "Delete";
    public override string DeleteCount(int count) => $"Delete all {count}";
    public override string BookmarksExported(int count) => count == 1 ? "1 bookmark exported" : $"{count} bookmarks exported";
    public override string CannotWriteBesideVideo(string error) => $"Couldn't write next to the video:\n{error}";
    public override string MarksShown => "Bookmarks shown on the bars";
    public override string MarksHidden => "Bookmarks hidden from the bars (Ctrl + Shift + B shows them)";
    public override string HideMarksTip => "Hide bookmarks on the bars (Ctrl + Shift + B). The panel still works.";
    public override string ShowMarksTip => "Show bookmarks on the bars (Ctrl + Shift + B)";
    public override string CollectionButton(string name) => $"Collection: {name}  ▾";
    public override string CollectionEmpty => "empty";
    public override string BookmarksInVideos(int bookmarks, int videos) => videos == 1 ? $"{bookmarks} in 1 video" : $"{bookmarks} in {videos} videos";
    public override string NewCollection => "New collection…";
    public override string NewCollectionName => "Name of the new collection";
    public override string CollectionExists(string name) => $"There's already a collection “{name}”";
    public override string RenameCollection(string name) => $"Rename “{name}”…";
    public override string NewNameFor(string name) => $"New name for “{name}”";
    public override string RenameFailed(string error) => $"Couldn't rename: {error}";
    public override string CollectionRenamed(string name) => $"Collection renamed to “{name}”";
    public override string DeleteCollection(string name) => $"Delete “{name}”…";
    public override string ImportCollectionOrBookmarks => "Import collection or bookmarks…";
    public override string ExportCollectionMenu(string name) => $"Export “{name}”…";
    public override string CollectionIsEmpty => "It's empty.";
    public override string CollectionWillBeLost(int bookmarks, int videos) => videos == 1 ? $"It has {bookmarks} bookmarks from 1 video and they will be lost." : $"It has {bookmarks} bookmarks from {videos} videos and they will be lost.";
    public override string DeleteCollectionQuestion(string name, string detail) => $"Delete the collection “{name}”?\n{detail}";
    public override string DeleteFailed(string error) => $"Couldn't delete: {error}";
    public override string CollectionToast(string name) => $"Collection “{name}”";
    public override string BookmarkCount(int count) => count == 1 ? "1 bookmark" : $"{count} bookmarks";
    public override string VideoCount(int count) => count == 1 ? "1 video" : $"{count} videos";
    public override string BookmarksFromVideos(int bookmarks, int videos) => $"{bookmarks} bookmarks from {videos} videos";
    public override string ShowVideoInExplorer => "Show the video in Explorer";
    public override string ShowVideoInExplorerMissing => "Show the video in Explorer (not found)";
    public override string MovedTo(string collection) => $"Moved to “{collection}” · Ctrl + Z moves it back";
    public override string MovedManyTo(int count, string collection) => $"Moved {count} to “{collection}” · Ctrl + Z moves them back";
    public override string ChangeFailed(string error) => $"Couldn't change it: {error}";
    public override string Color => "Color";
    public override string OwnColor => "Custom";
    public override string OtherColor => "Other color…";
    public override string MoveToCollection => "Move to collection";
    public override string MoveToNewCollectionName => "Name of the collection to move them to";
    public override string MoveFailed(string error) => $"Couldn't move them: {error}";
    public override string BookmarkVideoNotFound(string video) => $"This bookmark's video can't be found:\n{video}";
    public override string LocateIt => "Locate it…";
    public override string LocateTitle(string name) => $"Locate “{name}”";
    public override string CannotReadFile(string error) => $"Couldn't read the file:\n{error}";
    public override string NotSameVideo(string chosen, string name) => $"“{chosen}” is not the same video as “{name}” (its content is different: another copy, another version or a different video). The bookmark times might not match.\n\nUse it anyway? That video's bookmarks (in every collection) will move to this file; Ctrl + Z undoes it.";
    public override string UseItAnyway => "Use it anyway";
    public override string NotAllMoved(string error) => $"Not all of them could be moved: {error}";
    public override string VideosCountName(int count) => $"{count} videos";
    public override string BookmarkOfZoomedRange => "Bookmark the zoomed range";

    // ---- Pasos de deshacer (van detrás de «Deshecho:») ----

    public override string StepAdd(string name) => $"add “{name}”";
    public override string StepEdit(string name) => $"edit “{name}”";
    public override string StepRename(string name) => $"rename “{name}”";
    public override string StepLabelOf(string name) => $"label of “{name}”";
    public override string StepLabelOfMany(int count) => $"label of {count} bookmarks";
    public override string StepColorOf(string name) => $"color of “{name}”";
    public override string StepColorOfMany(int count) => $"color of {count} bookmarks";
    public override string StepDelete(string name) => $"delete “{name}”";
    public override string StepDeleteMany(int count) => $"delete {count} bookmarks";
    public override string StepMoveTo(string name, string collection) => $"move “{name}” to “{collection}”";
    public override string StepMoveManyTo(int count, string collection) => $"move {count} bookmarks to “{collection}”";
    public override string StepMoveStart(string name) => $"move the start of “{name}”";
    public override string StepMoveEnd(string name) => $"move the end of “{name}”";
    public override string StepMove(string name) => $"move “{name}”";
    public override string StepRelink(string from, string to) => $"move the bookmarks from “{from}” to “{to}”";
    public override string StepImportBookmarks => "import bookmarks";
    public override string StepImportCollection(string name) => $"import the collection “{name}”";
    public override string DefaultBookmarkName(int number) => $"Bookmark {number}";
    public override string StepAdjust(string name) => $"adjust “{name}”";
    public override string StepSetStartOf(string name) => $"set the start of “{name}”";
    public override string StepSetEndOf(string name) => $"set the end of “{name}”";

    // ---- Etiquetas ----

    public override string LabelChipTip(string name) => $"Show only “{name}” (click others to add them). Right-click: rename, color, delete…";
    public override string LabelChipShortcut(string shortcut) => $"\n{shortcut} applies it to the selected bookmark.";
    public override string NoLabel => "No label";
    public override string NoLabelChipTip => "Show only those without a label";
    public override string AddLabelChip => "+ Label";
    public override string AddLabelChipTip => "New label (for example “Boss” or “Cutscene”) to see at a glance what each bookmark marks";
    public override string NoLabelsInCollection => "This collection has no labels: use “+ Label” in the bookmarks panel (Ctrl + B)";
    public override string NoLabelNumber(int number, int count) => $"There's no label {number} (there are only {count})";
    public override string LabelToast(string name) => $"Label “{name}”";
    public override string BookmarkWithLabel(string name, string label) => $"“{name}”: {label}";
    public override string Labels => "Labels";
    public override string NewLabel => "New label…";
    public override string NewLabelName => "Name of the new label";
    public override string LabelExists(string name) => $"There's already a label “{name}”";
    public override string LabelsChangeFailed(string error) => $"The labels couldn't be changed: {error}";
    public override string NoBookmarks => "no bookmarks";
    public override string RenameEllipsis => "Rename…";
    public override string MoveLeft => "Move left";
    public override string MoveRight => "Move right";
    public override string AdoptLabelColor(string color, int count) => $"Apply to unlabeled {color.ToLower(Loc.Culture)} bookmarks ({count})";
    public override string AdoptLabelSameColor(int count) => $"Apply to unlabeled bookmarks of its color ({count})";
    public override string DeleteEllipsis => "Delete…";
    public override string DeleteLabelQuestion(string name, int uses) => $"Delete the label “{name}”?\n" + (uses == 1 ? "1 bookmark uses it: it will keep its color, without a label." : $"{uses} bookmarks use it: they will keep their color, without a label.");
    public override string StepCreateLabel(string name) => $"create the label “{name}”";
    public override string StepRenameLabel(string name) => $"rename the label “{name}”";
    public override string StepLabelColor(string name) => $"color of the label “{name}”";
    public override string StepMoveLabel(string name) => $"move the label “{name}”";
    public override string StepAdoptLabel(string name, int count) => $"put the label “{name}” on {count} bookmarks";
    public override string StepDeleteLabel(string name) => $"delete the label “{name}”";
    public override string LabelOrColor => "Label or color";
    public override string AddLabelChipLong => "+ New label";
    public override string AddLabelChipShort => "+ New";
    public override string AddLabelChipNameTip => "Create a label and apply it";
    public override string ColorWithoutLabel(string color) => $"{color} (no label)";

    // ---- Colores de la paleta ----

    public override string ColorPink => "Pink";
    public override string ColorRed => "Red";
    public override string ColorOrange => "Orange";
    public override string ColorViolet => "Violet";
    public override string ColorCyan => "Cyan";
    public override string ColorLime => "Lime";
    public override string ColorBrown => "Brown";
    public override string ColorGray => "Gray";
    public override string ColorWhite => "White";

    // ---- Compartir colecciones y bookmarks ----

    public override string CollectionHasNoBookmarks(string name) => $"The collection “{name}” has no bookmarks";
    public override string ExportCollectionTitle => "Export the collection";
    public override string CollectionFileFilter => "Bookmark collection|*.collection.json";
    public override string CollectionExported(string name) => $"Collection “{name}” exported";
    public override string CollectionSaveFailed(string error) => $"The collection couldn't be saved:\n{error}";
    public override string ImportTitle => "Import a collection or a video's bookmarks";
    public override string ImportFileFilter => "Collections and bookmarks|*.collection.json;*.bookmarks.json|All JSON files|*.json";
    public override string NotBookmarksFile(string file) => $"“{file}” is not a bookmarks file";
    public override string CannotReadNamed(string file, string error) => $"Couldn't read “{file}”: {error}";
    public override string FileHasNoBookmarks(string file) => $"“{file}” has no bookmarks";
    public override string UnknownVideo => "Can't tell which video they belong to: open the video and drag them in again";
    public override string ImportFailed(string error) => $"Couldn't import them: {error}";
    public override string AlreadyHadBookmarks => "You already had these bookmarks";
    public override string BookmarksAddedToThisVideo(int count) => count == 1 ? "1 bookmark added to this video" : $"{count} bookmarks added to this video";
    public override string BookmarksAddedTo(int count, string video, string collection) => (count == 1 ? "1 bookmark added" : $"{count} bookmarks added") + $" to “{video}” in the collection “{collection}”";
    public override string ImportedCollectionName => "Imported";
    public override string ImportCollectionFailed(string error) => $"The collection couldn't be imported: {error}";
    public override string CollectionAdded(string name, string videos, string bookmarks) => $"Collection “{name}” added: {videos}, {bookmarks}";

    // ---- Exportar clips ----

    public override string ExportClip => "Export clip";
    public override string ExactOriginal => "Exact original";
    public override string ExactOriginalTip => "Same codec and bitrate, cut at the exact frame";
    public override string CopyOriginal(string start) => $"Original, no re-encoding · {start}";
    public override string CopyOriginalTip => "Direct copy: instant and lossless, but it starts at a keyframe";
    public override string LightClipMenu(string rate) => $"Light · {rate} MB/s";
    public override string LightClipMenuTip => "MP4 (H.264 and AAC) for chats and Jira, 1080p at most";
    public override string StartsAtPreviousKeyframe => "starts at the previous keyframe";
    public override string ExactCut => "exact cut";
    public override string StartsSecondsEarlier(string seconds) => $"starts {seconds} s earlier";
    public override string StartsFramesEarlier(int frames) => frames == 1 ? "starts 1 frame earlier" : $"starts {frames} frames earlier";
    public override string ExportNeedsRange => "To export a clip, select a region or mark a range first (I / O, Z or right-drag)";
    public override string ExportWhat(string what) => $"Export {what}";
    public override string QuotedName(string name) => $"“{name}”";
    public override string ClipQueued => "Clip queued: it will be exported when the current one finishes";
    public override string ExportingClip => "Exporting the clip…";
    public override string ExportCancelled => "Export canceled";
    public override string ClipSaved => "Clip saved";
    public override string ClipFailed => "The clip couldn't be exported";
    public override string InFolder(string folder) => $"In {folder}";
    public override string CopiedForChat => "Copied to the clipboard: Ctrl + V pastes it into a chat or Jira";
    public override string CopiedForFolder => "Copied to the clipboard: Ctrl + V pastes it into a chat or a folder";
    public override string CopyFailed => "Couldn't copy to the clipboard";
    public override string Copy => "Copy";
    public override string ExportingPercent(int percent) => $"Exporting {percent}%";
    public override string PreparingClip => "Preparing the clip";
    public override string MoreQueued(int count) => $"\nand {count} more queued";
    public override string ClickToCancel => "\nClick to cancel";
    public override string ClipReady => "Clip ready";
    public override string ExportError => "Export error";
    public override string ClickToGo => "\nClick to show or open it";
    public override string ClickToSeeWhy => "Click to see why";
    public override string CancelClip(string name) => $"Cancel {name}";
    public override string TheClip => "the clip";
    public override string CancelQueuedToo(int count) => $"Also cancel the {count} queued";
    public override string CopyToClipboard => "Copy to clipboard";
    public override string ClipCopied => "Clip copied to the clipboard";
    public override string PreviousClips => "Previous clips";
    public override string RemoveFromHere => "Remove from here";
    public override string ExportingOnExit => "A clip is being exported. If you quit now, it will be canceled.";
    public override string Quit => "Quit";
    public override string Wait => "Wait";
    public override string FfmpegMissing => "ffmpeg.exe is missing from the program folder.";
    public override string FfmpegFailed => "ffmpeg couldn't export the range.";
    public override string FfmpegNotStarted => "ffmpeg couldn't be started.";
    public override string FfmpegExitCode(int code) => $"ffmpeg ended with code {code}.";
    public override string LightClipSuffix => " (light)";

    // ---- Otro color ----

    public override string OtherColorTitle => "Other color";
    public override string UseThisColor => "Use this color";
    public override string ReservedRangeAmber => "amber of the zoomed range";
    public override string ReservedPlayedBlue => "blue of the played part";
    public override string ReservedCacheGreen => "green of the frame cache";
    public override string ColorClash(string clash) => $"This color looks a lot like the {clash} on the bars, so they'd be easy to mix up.\n\nUse it anyway?";

    // ---- Avisos ----

    public override string Ok => "OK";

    // ---- Abrir con doble clic (registro de Windows) ----

    public override string AppDescription => "Video player";
    public override string VideoFileType(string extension) => $"{extension} video";

    public override string LanguageName(string code) => code switch
    {
        "ger" => "German",
        "ara" => "Arabic",
        "cat" => "Catalan",
        "chi" => "Chinese",
        "kor" => "Korean",
        "dan" => "Danish",
        "spa" => "Spanish",
        "baq" => "Basque",
        "fin" => "Finnish",
        "fre" => "French",
        "glg" => "Galician",
        "gre" => "Greek",
        "heb" => "Hebrew",
        "hin" => "Hindi",
        "eng" => "English",
        "ita" => "Italian",
        "jpn" => "Japanese",
        "dut" => "Dutch",
        "nor" => "Norwegian",
        "pol" => "Polish",
        "por" => "Portuguese",
        "rus" => "Russian",
        "swe" => "Swedish",
        "tha" => "Thai",
        "tur" => "Turkish",
        "ukr" => "Ukrainian",
        "vie" => "Vietnamese",
        _ => code,
    };
}
