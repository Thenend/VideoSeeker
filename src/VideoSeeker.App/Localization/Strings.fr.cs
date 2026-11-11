// Textos en francés.

namespace VideoSeeker.App.Localization;

public sealed class FrenchStrings : Strings
{
    // ---- Teclas (para componer atajos) ----

    public override string KeyCtrl => "Ctrl";
    public override string KeyShift => "Maj";
    public override string KeyDelete => "Suppr";
    public override string KeyEsc => "Échap";

    // ---- Ventana principal ----

    public override string EmptyHint => "Faites glisser une vidéo ici ou appuyez sur Ctrl + O";
    public override string CloseZoomTip => "Fermer le zoom (Échap)";
    public override string RangeLoopTip(string? region) => $"Boucler {(region is null ? "la plage" : $"« {region} »")} (Maj + R)";
    public override string RangeLoopOffTip(string? region) => $"Arrêter la boucle {ZoneOf(region)} (Maj + R)";
    public override string MarkLastTip(string? region) => $"Placer la fin {ZoneOf(region)} sur l'image affichée (O)";
    public override string MarkFirstTip(string? region) => $"Placer le début {ZoneOf(region)} sur l'image affichée (I)";
    public override string VideoLoopTip => "Boucler la vidéo (R)";
    public override string VideoLoopOffTip => "Arrêter la boucle de la vidéo (R)";
    public override string PreviousFrameTip => "Image précédente (,) · molette : image par image";
    public override string NextFrameTip => "Image suivante (.) · molette : image par image";
    public override string PlayTip => "Lecture / pause (Espace). J / K / L : lecture arrière / pause / lecture avant · molette : image par image";
    public override string SpeedTip => "Vitesse · molette : ±0,1 · bouton central : 1×";
    public override string OpenVideoTip => "Ouvrir une vidéo (Ctrl + O)";
    public override string RangePositionTip => "Par rapport à la plage zoomée";
    public override string CurrentBookmarkTip => "Signet actuel ([ / ] précédent / suivant, 1–9 et 0 pour aller aux dix premiers)";
    public override string BookmarksButtonTip => "Panneau des signets (Ctrl + B) · ajouter : B";
    public override string FullscreenTip => "Plein écran (F)";
    public override string SettingsTip => "Paramètres (Ctrl + ,)";
    public override string TracksTip => "Audio et sous-titres (A / S)";
    public override string VolumeTip => "Volume";
    public override string MuteTip => "Couper le son (M)";
    public override string CloseBookmarkPanelTip => "Fermer (Ctrl + B)";
    public override string ExportVideoBookmarksTip => "Exporter les signets de cette vidéo, pour les partager";
    public override string ExportCollectionTip => "Exporter toute la collection, pour la partager";
    public override string BookmarksTitle => "Signets";
    public override string CollectionTip => "Collection de signets : chacune a ses propres signets pour chaque vidéo. Changer, créer, renommer ou supprimer.";
    public override string SearchPlaceholder => "Rechercher (Ctrl + F)";
    public override string ScopeThisVideo => "Cette vidéo";
    public override string ScopeAllVideos => "Toutes les vidéos";
    public override string PanelHint => "B crée une région à partir de la plage zoomée ou, s'il n'y en a pas, d'une seule image (O fixe sa fin). Clic ou Entrée : y aller et zoomer. Ctrl / Maj + clic : sélection multiple. F2 renomme, Suppr supprime, Ctrl + Z annule.";
    public override string BookmarkKeyTip => "Touche pour aller à ce signet";
    public override string NameHint => "Entrée enregistre · Échap conserve l'ancien nom";
    public override string NameHintWithLabels => "Entrée enregistre · Échap annule · Alt + chiffre : étiquette";
    public override string GeneratingThumbnail => "Création de la miniature…";
    public override string Close => "Fermer";
    public override string ShowInFolder => "Afficher dans le dossier";
    public override string Open => "Ouvrir";
    public override string ZoneOf(string? region) => region is null ? "de la plage" : $"de « {region} »";
    public override string SaveZoneTip => "Enregistrer la plage zoomée comme signet (B)";
    public override string NewLabelTitle => "Nouvelle étiquette";
    public override string NewLabelBoxTip => "Entrée la crée et l'applique · Échap revient";
    public override string ZoneNameTip(string name) => $"La plage zoomée est la région « {name} » : I / O, ⇤ ⇥ et les drapeaux la modifient";
    public override string CloseZoomDeselectTip(string name) => $"Fermer le zoom et désélectionner « {name} » (Échap)";
    public override string Loading => "Chargement…";

    // ---- Ajustes ----

    public override string SettingsTitle => "Paramètres — VideoSeeker";
    public override string ResetDefaults => "Rétablir les valeurs par défaut";
    public override string SupportTip => "VideoSeeker est gratuit. S'il vous est utile, vous pouvez offrir un café à l'auteur.";
    public override string Support => "Soutenir";
    public override string SourceCode => "Code source";
    public override string SourceCodeTip => "Logiciel libre, sous licence GPL v3.";
    public override string Version(string version) => $"Version {version}";
    public override string SectionBars => "BARRES";
    public override string MoveRegionsTip => "Faire glisser le milieu de la plage zoomée ou d'une région de signet la déplace en entier. Désactivé, cela déplace la tête de lecture.";
    public override string MoveRegions => "Déplacer des régions entières en faisant glisser leur milieu";
    public override string EdgeFrameTip => "En faisant glisser un bord de la plage zoomée ou d'un signet. Désactivé, l'image ne change pas.";
    public override string EdgeFrame => "Afficher l'image du bord déplacé";
    public override string CachedLineTip => "Indique sous les barres ce qui est déjà en cache : on y saute instantanément.";
    public override string CachedLine => "Ligne verte du cache d'images";
    public override string MarksTip => "Raccourci : Ctrl + Maj + B.";
    public override string Marks => "Repères des signets";
    public override string PulseMarksTip => "S'ils sont masqués, créer un signet ou y aller fait clignoter leur bouton (en bas à droite) quelques fois. Aucun message ne s'affiche.";
    public override string PulseMarks => "Faire clignoter leur bouton s'ils sont masqués";
    public override string SectionNavigation => "NAVIGATION";
    public override string SecondsUnit => "s";
    public override string SeekTip => "Secondes que saute chaque flèche : seule, avec Maj ou avec Ctrl.";
    public override string Seek => "Sauts avec ← →";
    public override string ScrubTip => "Pixels à faire glisser sur l'image, bouton enfoncé, pour avancer d'une image.";
    public override string Scrub => "Sensibilité du glissement sur l'image";
    public override string PixelsPerFrame(int pixels) => $"{pixels} px par image";
    public override string WheelSpeed => "Vitesse";
    public override string WheelVolume => "Volume";
    public override string WheelTip => "Vitesse : ±0,1× (bouton central : 1×). Volume : ±5 %.";
    public override string Wheel => "Molette de la souris sur la vidéo";
    public override string Resume => "Reprendre chaque vidéo là où elle s'était arrêtée";
    public override string SectionTimeAndBookmarks => "TEMPS ET SIGNETS";
    public override string TimeFormatTip => "Dans 00:01:23:11, le dernier nombre est l'image dans la seconde.";
    public override string TimeFormat => "Format du temps";
    public override string NewColorTip => "Utilisée sans étiquette tant qu'on ne choisit rien d'autre (ou toujours, si on ne répète pas le dernier choix).";
    public override string NewColor => "Couleur des nouveaux signets";
    public override string RepeatLabelTip => "Un nouveau signet reçoit l'étiquette ou la couleur choisie la dernière fois ; on la change dans la zone du nom.";
    public override string RepeatLabel => "Répéter la dernière étiquette ou couleur";
    public override string TimesInPreview => "Aperçu";
    public override string TimesInList => "Liste";
    public override string BookmarkTimesTip => "Dans la liste, on les voit tous d'un coup d'œil. Dans l'aperçu, la liste laisse plus de place au nom.";
    public override string BookmarkTimes => "Temps des signets";
    public override string SectionAudioAndSubtitles => "AUDIO ET SOUS-TITRES";
    public override string AudioLanguageTip => "Cette piste est choisie si la vidéo en a plusieurs. S'applique à l'ouverture de la vidéo suivante.";
    public override string AudioLanguage => "Langue audio préférée";
    public override string SubtitleLanguageTip => "Cette piste est choisie quand on active les sous-titres. S'applique à l'ouverture de la vidéo suivante.";
    public override string SubtitleLanguage => "Langue de sous-titres préférée";
    public override string AppliesOnNextVideo => "S'applique à l'ouverture de la vidéo suivante.";
    public override string SubtitlesOnOpen => "Activer les sous-titres à l'ouverture d'une vidéo";
    public override string NoPreference => "Aucune préférence";
    public override string SectionPerformance => "PERFORMANCES";
    public override string CacheMemory => "Mémoire du cache d'images";
    public override string CacheMemoryTip => "Plus elle est grande, plus on peut parcourir de vidéo image par image instantanément. S'applique à l'ouverture de la vidéo suivante.";
    public override string CacheOfTotal(string used, string total) => $"{used} sur {total} Go";
    public override string GigabytesUnit => "Go";
    public override string MegabytesUnit => "Mo";
    public override string CacheTooBigTip => "Plus de 90 % de la mémoire de la carte graphique : d'autres programmes pourraient en manquer.";
    public override string HardwareDecodingTip => "Utilise la carte graphique pour la lecture. À désactiver seulement si une vidéo s'affiche mal.";
    public override string HardwareDecoding => "Décodage matériel";
    public override string SectionClips => "EXPORTER DES CLIPS";
    public override string ChangeEllipsis => "Modifier…";
    public override string BesideVideo => "À côté de la vidéo";
    public override string ClipFolderTip => "Emplacement des clips exportés. Si ce dossier n'est pas accessible en écriture, ils vont dans Vidéos\\VideoSeeker.";
    public override string ClipFolder => "Dossier de destination";
    public override string BesideEachVideo => "À côté de chaque vidéo";
    public override string ClipFolderDialogTitle => "Dossier des clips exportés";
    public override string ClipClipboardTip => "Comme fichier, pour le coller directement dans un chat, dans Jira ou dans un dossier.";
    public override string ClipClipboard => "Copier chaque clip dans le presse-papiers";
    public override string LightClipTip => "Mégaoctets par seconde de clip, audio compris. Jamais plus que la vidéo d'origine.";
    public override string LightClip => "Taille du clip léger";
    public override string MegabytesPerSecond(string value) => $"{value} Mo/s";
    public override string SectionGeneral => "GÉNÉRAL";
    public override string InterfaceLanguage => "Langue";
    public override string InterfaceLanguageTip => "La langue de VideoSeeker. Automatique : celle de Windows. Prend effet au redémarrage.";
    public override string AutomaticLanguage(string language) => $"Automatique · {language}";
    public override string LanguageChangedRestart => "La nouvelle langue sera utilisée au redémarrage de VideoSeeker.";
    public override string LanguageCaption => "Langue";
    public override string RestartNow => "Redémarrer maintenant";
    public override string Later => "Plus tard";
    public override string DefaultFolder => "Par défaut";
    public override string DefaultFolderTip => "Le dossier data, à côté du programme.";
    public override string DataFolderTip => "Paramètres, signets et position de lecture de chaque vidéo. Prend effet au redémarrage.";
    public override string DataFolder => "Dossier de données";
    public override string ChooseInWindows => "Choisir dans Windows";
    public override string Unregister => "Retirer";
    public override string DefaultPlayer => "Lecteur vidéo par défaut";
    public override string DefaultPlayerTip => "Ajoute les types de vidéo à « Ouvrir avec » et ouvre Applications par défaut de Windows, où l'on choisit lesquels.";
    public override string NumberRange => "Entre 0,04 et 3600 secondes";
    public override string PendingFolder(string folder) => $"Au redémarrage, {folder} sera utilisé.";
    public override string PendingFolderWithCopy(string folder) => $"Au redémarrage, {folder} sera utilisé, avec une copie des données actuelles.";
    public override string DataFolderDialogTitle => "Dossier des données de VideoSeeker";
    public override string DataFolderNested => "Le dossier de données ne peut pas se trouver dans le dossier actuel ni le contenir.";
    public override string DataFolderHasData(string folder) => $"{folder}\n\ncontient déjà des données de VideoSeeker. Si vous y copiez les données actuelles, les anciennes sont conservées dans son sous-dossier « anteriores ».";
    public override string UseThatData => "Utiliser ces données";
    public override string CopyCurrentData => "Copier les données actuelles";
    public override string MoveDataQuestion(string folder) => $"Transférer vos données (paramètres, signets, position de lecture de chaque vidéo) vers\n{folder} ?\n\nLe dossier actuel reste tel quel.";
    public override string MoveData => "Transférer les données";
    public override string StartEmpty => "Partir de zéro";
    public override string Cancel => "Annuler";
    public override string CannotUseFolder(string error) => $"Impossible d'utiliser ce dossier :\n{error}";
    public override string DataFolderRestart => "Le nouveau dossier de données sera utilisé au redémarrage de VideoSeeker.";
    public override string OpenWithDoubleClick => "Ouvrir d'un double-clic";
    public override string RegisterFailed(string error) => $"Impossible d'enregistrer les types de vidéo :\n{error}";
    public override string UnregisterFailed(string error) => $"Impossible de retirer l'enregistrement :\n{error}";
    public override string ResetQuestion => "Tous les paramètres reprendront leurs valeurs par défaut.";
    public override string Reset => "Rétablir";
    public override string ResetTitle => "Rétablir les paramètres";
    public override string DefaultFolderPackagedTip => "Le dossier de l'application dans Windows. Il est supprimé à la désinstallation.";
    public override string HoverPreview => "Miniature au survol des barres";
    public override string HoverPreviewTip => "Pour voir ce qu'il y a à un endroit de la vidéo sans y aller.";
    public override string DataCopyIncomplete(string file) => $"La copie de {file} est incomplète";

    // ---- Ventana principal: reproducción ----

    public override string VideoFileFilter => "Vidéos|*.mkv;*.mp4;*.m4v;*.avi;*.wmv;*.mov;*.webm;*.flv;*.ts;*.m2ts;*.mts;*.mpg;*.mpeg;*.vob;*.3gp;*.ogv|Tous les fichiers|*.*";
    public override string OpenVideoTitle => "Ouvrir une vidéo";
    public override string FileNotFound(string path) => $"Fichier introuvable :\n{path}";
    public override string FrameNumber(int frame) => $"image {frame}";
    public override string FrameNumberApprox(string frame) => $"image ~{frame}";
    public override string Frames(int count) => count == 1 ? "1 image" : $"{count} images";
    public override string FrameOfRange(int frame, int count) => $"image {frame}/{count}";
    public override string VideoLoopOn => "Boucle de la vidéo activée";
    public override string VideoLoopOff => "Boucle de la vidéo désactivée";
    public override string RangeLoopOn(string? region) => $"Boucle {ZoneOf(region)} activée";
    public override string RangeLoopOff(string? region) => $"Boucle {ZoneOf(region)} désactivée";
    public override string RangeLoopMenu(string? region) => $"Boucle {ZoneOf(region)}";
    public override string ZoomSection => "Zoomer sur la section (±5 s)";
    public override string MarkFirstFromScreen(int frame, string? region) => $"Placer le début {ZoneOf(region)} sur l'image affichée ({frame})";
    public override string MarkLastFromScreen(int frame, string? region) => $"Placer la fin {ZoneOf(region)} sur l'image affichée ({frame})";
    public override string MarkFirstFromCursor(int frame, string? region) => $"Placer le début {ZoneOf(region)} sur l'image sous le curseur ({frame})";
    public override string MarkLastFromCursor(int frame, string? region) => $"Placer la fin {ZoneOf(region)} sur l'image sous le curseur ({frame})";
    public override string CloseZoom => "Fermer le zoom";
    public override string NormalSpeed => "1× (normale)";
    public override string SpeedToast(string speed) => $"Vitesse {speed}";
    public override string VolumeToast(int volume) => $"Volume {volume} %";
    public override string VolumeMutedToast(int volume) => $"Volume {volume} % (son coupé)";
    public override string Audio => "Audio";
    public override string Subtitles => "Sous-titres";
    public override string SubtitlesOff => "Désactivés";
    public override string NoTracks => "Aucune piste";
    public override string Track(long id) => $"Piste {id}";
    public override string ControlsPinned => "Commandes épinglées";
    public override string ControlsHidden => "Commandes masquées : amenez la souris en bas pour les voir";
    public override string ControlsPinnedTip => "Commandes épinglées (Tab)";
    public override string PinControlsTip => "Épingler les commandes (Tab)";
    public override string VideoGone(string name) => $"Introuvable : {name}";
    public override string HistoryBackTip => "Revenir à la vidéo précédente (Alt + ←)";
    public override string HistoryForwardTip => "Aller à la vidéo suivante (Alt + →)";
    public override string CloseZoomDeselect(string name) => $"Fermer le zoom et désélectionner « {name} »";
    public override string HistoryAllBack(int count) => $"Clic droit : les {count} précédentes";
    public override string HistoryAllForward(int count) => $"Clic droit : les {count} suivantes";

    // ---- Bookmarks ----

    public override string NewBookmark => "Nouveau signet";
    public override string RenameBookmark => "Renommer le signet";
    public override string SelectBookmarkFirst => "Sélectionnez d'abord un signet (cliquez dessus sur les barres ou dans le panneau)";
    public override string BookmarkDeleted(string name) => $"« {name} » supprimé · Ctrl + Z le récupère";
    public override string BookmarksDeleted(int count) => $"{count} signets supprimés · Ctrl + Z les récupère";
    public override string UndoFailed(string error) => $"Impossible d'annuler : {error}";
    public override string RedoFailed(string error) => $"Impossible de rétablir : {error}";
    public override string NothingToUndo => "Rien à annuler";
    public override string NothingToRedo => "Rien à rétablir";
    public override string Undone(string step) => $"Annulé : {step}";
    public override string Redone(string step) => $"Rétabli : {step}";
    public override string InVideo(string video) => $" (dans « {video} »)";
    public override string VideoHasNoBookmarks => "Cette vidéo n'a pas de signets (B en ajoute un)";
    public override string NoBookmarkNumber(int number, int count) => $"Il n'y a pas de signet {number} (il y en a {count})";
    public override string NoMoreBookmarksAhead => "Aucun autre signet après";
    public override string NoMoreBookmarksBehind => "Aucun autre signet avant";
    public override string AddBookmarkHere => "Ajouter un signet ici";
    public override string BookmarksPanel => "Panneau des signets";
    public override string Rename => "Renommer";
    public override string UseFrameAsThumbnail(int frame, string? region) => region is null ? $"Utiliser l'image actuelle ({frame}) comme miniature" : $"Utiliser l'image actuelle ({frame}) comme miniature de « {region} »";
    public override string FrameOutsideRegion(int frame, string time, string name, string range) => $"L'image actuelle ({frame}, {time}) est en dehors de la région « {name} » ({range}).\n\nL'utiliser quand même comme miniature ?";
    public override string UseIt => "L'utiliser";
    public override string ResetThumbnail(string? region) => region is null ? "Revenir à la miniature de la première image" : $"Revenir à la miniature de la première image de « {region} »";
    public override string Delete => "Supprimer";
    public override string DeleteCount(int count) => $"Supprimer les {count}";
    public override string BookmarksExported(int count) => count == 1 ? "1 signet exporté" : $"{count} signets exportés";
    public override string CannotWriteBesideVideo(string error) => $"Impossible d'écrire à côté de la vidéo :\n{error}";
    public override string MarksShown => "Repères des signets affichés";
    public override string MarksHidden => "Repères des signets masqués (Ctrl + Maj + B les affiche)";
    public override string HideMarksTip => "Masquer les repères des signets sur les barres (Ctrl + Maj + B). Le panneau continue de fonctionner.";
    public override string ShowMarksTip => "Afficher les repères des signets sur les barres (Ctrl + Maj + B)";
    public override string CollectionButton(string name) => $"Collection : {name}  ▾";
    public override string CollectionEmpty => "vide";
    public override string BookmarksInVideos(int bookmarks, int videos) => videos == 1 ? $"{bookmarks} dans 1 vidéo" : $"{bookmarks} dans {videos} vidéos";
    public override string NewCollection => "Nouvelle collection…";
    public override string NewCollectionName => "Nom de la nouvelle collection";
    public override string CollectionExists(string name) => $"Il existe déjà une collection « {name} »";
    public override string RenameCollection(string name) => $"Renommer « {name} »…";
    public override string NewNameFor(string name) => $"Nouveau nom pour « {name} »";
    public override string RenameFailed(string error) => $"Impossible de renommer : {error}";
    public override string CollectionRenamed(string name) => $"Collection renommée en « {name} »";
    public override string DeleteCollection(string name) => $"Supprimer « {name} »…";
    public override string ImportCollectionOrBookmarks => "Importer une collection ou des signets…";
    public override string ExportCollectionMenu(string name) => $"Exporter « {name} »…";
    public override string CollectionIsEmpty => "Elle est vide.";
    public override string CollectionWillBeLost(int bookmarks, int videos) => videos == 1 ? $"Elle contient {bookmarks} signets d'1 vidéo, qui seront perdus." : $"Elle contient {bookmarks} signets de {videos} vidéos, qui seront perdus.";
    public override string DeleteCollectionQuestion(string name, string detail) => $"Supprimer la collection « {name} » ?\n{detail}";
    public override string DeleteFailed(string error) => $"Impossible de supprimer : {error}";
    public override string CollectionToast(string name) => $"Collection « {name} »";
    public override string BookmarkCount(int count) => count == 1 ? "1 signet" : $"{count} signets";
    public override string VideoCount(int count) => count == 1 ? "1 vidéo" : $"{count} vidéos";
    public override string BookmarksFromVideos(int bookmarks, int videos) => $"{bookmarks} signets de {videos} vidéos";
    public override string ShowVideoInExplorer => "Afficher la vidéo dans l'Explorateur";
    public override string ShowVideoInExplorerMissing => "Afficher la vidéo dans l'Explorateur (introuvable)";
    public override string MovedTo(string collection) => $"Déplacé vers « {collection} » · Ctrl + Z le remet";
    public override string MovedManyTo(int count, string collection) => $"{count} déplacés vers « {collection} » · Ctrl + Z les remet";
    public override string ChangeFailed(string error) => $"Impossible de modifier : {error}";
    public override string Color => "Couleur";
    public override string OwnColor => "Personnalisée";
    public override string OtherColor => "Autre couleur…";
    public override string MoveToCollection => "Déplacer vers la collection";
    public override string MoveToNewCollectionName => "Nom de la collection où les déplacer";
    public override string MoveFailed(string error) => $"Impossible de les déplacer : {error}";
    public override string BookmarkVideoNotFound(string video) => $"La vidéo de ce signet est introuvable :\n{video}";
    public override string LocateIt => "Le chercher…";
    public override string LocateTitle(string name) => $"Chercher « {name} »";
    public override string CannotReadFile(string error) => $"Impossible de lire le fichier :\n{error}";
    public override string NotSameVideo(string chosen, string name) => $"« {chosen} » n'est pas la même vidéo que « {name} » (son contenu est différent : une autre copie, une autre version ou une autre vidéo). Les temps des signets pourraient ne pas correspondre.\n\nL'utiliser quand même ? Les signets de cette vidéo (dans toutes les collections) passeront à ce fichier ; Ctrl + Z l'annule.";
    public override string UseItAnyway => "L'utiliser quand même";
    public override string NotAllMoved(string error) => $"Impossible de tous les transférer : {error}";
    public override string VideosCountName(int count) => $"{count} vidéos";
    public override string BookmarkOfZoomedRange => "Signet de la plage zoomée";

    // ---- Pasos de deshacer (van detrás de «Deshecho:») ----

    public override string StepAdd(string name) => $"ajouter « {name} »";
    public override string StepEdit(string name) => $"modifier « {name} »";
    public override string StepRename(string name) => $"renommer « {name} »";
    public override string StepLabelOf(string name) => $"étiquette de « {name} »";
    public override string StepLabelOfMany(int count) => $"étiquette de {count} signets";
    public override string StepColorOf(string name) => $"couleur de « {name} »";
    public override string StepColorOfMany(int count) => $"couleur de {count} signets";
    public override string StepDelete(string name) => $"supprimer « {name} »";
    public override string StepDeleteMany(int count) => $"supprimer {count} signets";
    public override string StepMoveTo(string name, string collection) => $"déplacer « {name} » vers « {collection} »";
    public override string StepMoveManyTo(int count, string collection) => $"déplacer {count} signets vers « {collection} »";
    public override string StepMoveStart(string name) => $"déplacer le début de « {name} »";
    public override string StepMoveEnd(string name) => $"déplacer la fin de « {name} »";
    public override string StepMove(string name) => $"déplacer « {name} »";
    public override string StepRelink(string from, string to) => $"transférer les signets de « {from} » vers « {to} »";
    public override string StepImportBookmarks => "importer des signets";
    public override string StepImportCollection(string name) => $"importer la collection « {name} »";
    public override string DefaultBookmarkName(int number) => $"Signet {number}";
    public override string StepAdjust(string name) => $"ajuster « {name} »";
    public override string StepSetStartOf(string name) => $"définir le début de « {name} »";
    public override string StepSetEndOf(string name) => $"définir la fin de « {name} »";

    // ---- Etiquetas ----

    public override string LabelChipTip(string name) => $"N'afficher que « {name} » (cliquez sur d'autres pour les ajouter). Clic droit : renommer, couleur, supprimer…";
    public override string LabelChipShortcut(string shortcut) => $"\n{shortcut} l'applique au signet sélectionné.";
    public override string NoLabel => "Sans étiquette";
    public override string NoLabelChipTip => "N'afficher que ceux sans étiquette";
    public override string AddLabelChip => "+ Étiquette";
    public override string AddLabelChipTip => "Nouvelle étiquette (par exemple « Boss » ou « Cinématique »), pour voir d'un coup d'œil ce que marque chaque signet";
    public override string NoLabelsInCollection => "Cette collection n'a pas d'étiquettes : « + Étiquette » dans le panneau des signets (Ctrl + B)";
    public override string NoLabelNumber(int number, int count) => $"Il n'y a pas d'étiquette {number} (il y en a {count})";
    public override string LabelToast(string name) => $"Étiquette « {name} »";
    public override string BookmarkWithLabel(string name, string label) => $"« {name} » : {label}";
    public override string Labels => "Étiquettes";
    public override string NewLabel => "Nouvelle étiquette…";
    public override string NewLabelName => "Nom de la nouvelle étiquette";
    public override string LabelExists(string name) => $"Il existe déjà une étiquette « {name} »";
    public override string LabelsChangeFailed(string error) => $"Impossible de modifier les étiquettes : {error}";
    public override string NoBookmarks => "aucun signet";
    public override string RenameEllipsis => "Renommer…";
    public override string MoveLeft => "Déplacer à gauche";
    public override string MoveRight => "Déplacer à droite";
    public override string AdoptLabelColor(string color, int count) => $"L'appliquer à ceux de couleur {color.ToLower(Loc.Culture)} sans étiquette ({count})";
    public override string AdoptLabelSameColor(int count) => $"L'appliquer à ceux de sa couleur sans étiquette ({count})";
    public override string DeleteEllipsis => "Supprimer…";
    public override string DeleteLabelQuestion(string name, int uses) => $"Supprimer l'étiquette « {name} » ?\n" + (uses == 1 ? "1 signet la porte : il restera sans étiquette, avec sa couleur." : $"{uses} signets la portent : ils resteront sans étiquette, avec leur couleur.");
    public override string StepCreateLabel(string name) => $"créer l'étiquette « {name} »";
    public override string StepRenameLabel(string name) => $"renommer l'étiquette « {name} »";
    public override string StepLabelColor(string name) => $"couleur de l'étiquette « {name} »";
    public override string StepMoveLabel(string name) => $"déplacer l'étiquette « {name} »";
    public override string StepAdoptLabel(string name, int count) => $"appliquer l'étiquette « {name} » à {count} signets";
    public override string StepDeleteLabel(string name) => $"supprimer l'étiquette « {name} »";
    public override string LabelOrColor => "Étiquette ou couleur";
    public override string AddLabelChipLong => "+ Nouvelle étiquette";
    public override string AddLabelChipShort => "+ Nouvelle";
    public override string AddLabelChipNameTip => "Créer une étiquette et l'appliquer";
    public override string ColorWithoutLabel(string color) => $"{color} (sans étiquette)";

    // ---- Colores de la paleta ----

    public override string ColorPink => "Rose";
    public override string ColorRed => "Rouge";
    public override string ColorOrange => "Orange";
    public override string ColorViolet => "Violet";
    public override string ColorCyan => "Cyan";
    public override string ColorLime => "Citron vert";
    public override string ColorBrown => "Marron";
    public override string ColorGray => "Gris";
    public override string ColorWhite => "Blanc";

    // ---- Compartir colecciones y bookmarks ----

    public override string CollectionHasNoBookmarks(string name) => $"La collection « {name} » n'a pas de signets";
    public override string ExportCollectionTitle => "Exporter la collection";
    public override string CollectionFileFilter => "Collection de signets|*.collection.json";
    public override string CollectionExported(string name) => $"Collection « {name} » exportée";
    public override string CollectionSaveFailed(string error) => $"Impossible d'enregistrer la collection :\n{error}";
    public override string ImportTitle => "Importer une collection ou les signets d'une vidéo";
    public override string ImportFileFilter => "Collections et signets|*.collection.json;*.bookmarks.json|Tous les JSON|*.json";
    public override string NotBookmarksFile(string file) => $"« {file} » n'est pas un fichier de signets";
    public override string CannotReadNamed(string file, string error) => $"Impossible de lire « {file} » : {error}";
    public override string FileHasNoBookmarks(string file) => $"« {file} » n'a pas de signets";
    public override string UnknownVideo => "On ne sait pas à quelle vidéo ils appartiennent : ouvrez la vidéo et faites-les glisser à nouveau";
    public override string ImportFailed(string error) => $"Impossible de les importer : {error}";
    public override string AlreadyHadBookmarks => "Vous aviez déjà ces signets";
    public override string BookmarksAddedToThisVideo(int count) => count == 1 ? "1 signet ajouté à cette vidéo" : $"{count} signets ajoutés à cette vidéo";
    public override string BookmarksAddedTo(int count, string video, string collection) => (count == 1 ? "1 signet ajouté" : $"{count} signets ajoutés") + $" à « {video} » dans la collection « {collection} »";
    public override string ImportedCollectionName => "Importée";
    public override string ImportCollectionFailed(string error) => $"Impossible d'importer la collection : {error}";
    public override string CollectionAdded(string name, string videos, string bookmarks) => $"Collection « {name} » ajoutée : {videos}, {bookmarks}";

    // ---- Exportar clips ----

    public override string ExportClip => "Exporter un clip";
    public override string ExactOriginal => "Original exact";
    public override string ExactOriginalTip => "Même codec et même débit, coupé à l'image exacte";
    public override string CopyOriginal(string start) => $"Original sans réencodage · {start}";
    public override string CopyOriginalTip => "Copie directe : instantanée et sans perte, mais elle commence sur une image clé";
    public override string LightClipMenu(string rate) => $"Léger · {rate} Mo/s";
    public override string LightClipMenuTip => "MP4 (H.264 et AAC) pour les chats et Jira, 1080p au maximum";
    public override string StartsAtPreviousKeyframe => "commence à l'image clé précédente";
    public override string ExactCut => "coupe exacte";
    public override string StartsSecondsEarlier(string seconds) => $"commence {seconds} s plus tôt";
    public override string StartsFramesEarlier(int frames) => frames == 1 ? "commence 1 image plus tôt" : $"commence {frames} images plus tôt";
    public override string ExportNeedsRange => "Pour exporter un clip, choisissez une région ou marquez d'abord une plage (I / O, Z ou en faisant glisser avec le bouton droit)";
    public override string ExportWhat(string what) => $"Exporter {what}";
    public override string QuotedName(string name) => $"« {name} »";
    public override string ClipQueued => "Clip en file d'attente : il sera exporté une fois le précédent terminé";
    public override string ExportingClip => "Exportation du clip…";
    public override string ExportCancelled => "Exportation annulée";
    public override string ClipSaved => "Clip enregistré";
    public override string ClipFailed => "Impossible d'exporter le clip";
    public override string InFolder(string folder) => $"Dans {folder}";
    public override string CopiedForChat => "Copié dans le presse-papiers : Ctrl + V le colle dans un chat ou dans Jira";
    public override string CopiedForFolder => "Copié dans le presse-papiers : Ctrl + V le colle dans un chat ou dans un dossier";
    public override string CopyFailed => "Impossible de copier dans le presse-papiers";
    public override string Copy => "Copier";
    public override string ExportingPercent(int percent) => $"Exportation {percent} %";
    public override string PreparingClip => "Préparation du clip";
    public override string MoreQueued(int count) => $"\net {count} de plus en attente";
    public override string ClickToCancel => "\nCliquez pour annuler";
    public override string ClipReady => "Clip prêt";
    public override string ExportError => "Erreur d'exportation";
    public override string ClickToGo => "\nCliquez pour l'afficher ou l'ouvrir";
    public override string ClickToSeeWhy => "Cliquez pour voir pourquoi";
    public override string CancelClip(string name) => $"Annuler {name}";
    public override string TheClip => "le clip";
    public override string CancelQueuedToo(int count) => $"Annuler aussi les {count} en attente";
    public override string CopyToClipboard => "Copier dans le presse-papiers";
    public override string ClipCopied => "Clip copié dans le presse-papiers";
    public override string PreviousClips => "Clips précédents";
    public override string RemoveFromHere => "Retirer d'ici";
    public override string ExportingOnExit => "Un clip est en cours d'exportation. Si vous quittez maintenant, il sera annulé.";
    public override string Quit => "Quitter";
    public override string Wait => "Attendre";
    public override string FfmpegMissing => "ffmpeg.exe est introuvable dans le dossier du programme.";
    public override string FfmpegFailed => "ffmpeg n'a pas pu exporter la plage.";
    public override string FfmpegNotStarted => "Impossible de lancer ffmpeg.";
    public override string FfmpegExitCode(int code) => $"ffmpeg s'est terminé avec le code {code}.";
    public override string LightClipSuffix => " (léger)";

    // ---- Otro color ----

    public override string OtherColorTitle => "Autre couleur";
    public override string UseThisColor => "Utiliser cette couleur";
    public override string ReservedRangeAmber => "ambre de la plage zoomée";
    public override string ReservedPlayedBlue => "bleu de la partie lue";
    public override string ReservedCacheGreen => "vert du cache d'images";
    public override string ColorClash(string clash) => $"Cette couleur ressemble beaucoup à une couleur des barres ({clash}), et on les confondra.\n\nL'utiliser quand même ?";

    // ---- Avisos ----

    public override string Ok => "OK";

    // ---- Abrir con doble clic (registro de Windows) ----

    public override string AppDescription => "Lecteur vidéo";
    public override string VideoFileType(string extension) => $"Vidéo {extension}";

    public override string LanguageName(string code) => code switch
    {
        "ger" => "Allemand",
        "ara" => "Arabe",
        "cat" => "Catalan",
        "chi" => "Chinois",
        "kor" => "Coréen",
        "dan" => "Danois",
        "spa" => "Espagnol",
        "baq" => "Basque",
        "fin" => "Finnois",
        "fre" => "Français",
        "glg" => "Galicien",
        "gre" => "Grec",
        "heb" => "Hébreu",
        "hin" => "Hindi",
        "eng" => "Anglais",
        "ita" => "Italien",
        "jpn" => "Japonais",
        "dut" => "Néerlandais",
        "nor" => "Norvégien",
        "pol" => "Polonais",
        "por" => "Portugais",
        "rus" => "Russe",
        "swe" => "Suédois",
        "tha" => "Thaï",
        "tur" => "Turc",
        "ukr" => "Ukrainien",
        "vie" => "Vietnamien",
        _ => code,
    };
}
