// Textos en portugués de Brasil.

namespace VideoSeeker.App.Localization;

public sealed class PortugueseBrazilStrings : Strings
{
    // ---- Teclas (para componer atajos) ----

    public override string KeyCtrl => "Ctrl";
    public override string KeyShift => "Shift";
    public override string KeyAlt => "Alt";
    public override string KeyDelete => "Delete";
    public override string KeyEsc => "Esc";

    // ---- Ventana principal ----

    public override string EmptyHint => "Arraste um vídeo para cá ou pressione Ctrl + O";
    public override string CloseZoomTip => "Fechar o zoom (Esc)";
    public override string RangeLoopTip(string? region) => $"Loop {ZoneOf(region)} (Shift + R)";
    public override string RangeLoopOffTip(string? region) => $"Desativar o loop {ZoneOf(region)} (Shift + R)";
    public override string MarkLastTip(string? region) => $"Definir o fim {ZoneOf(region)} no quadro atual (O)";
    public override string MarkFirstTip(string? region) => $"Definir o início {ZoneOf(region)} no quadro atual (I)";
    public override string VideoLoopTip => "Loop do vídeo (R)";
    public override string VideoLoopOffTip => "Desativar o loop do vídeo (R)";
    public override string PreviousFrameTip => "Quadro anterior (,) · roda: quadro a quadro";
    public override string NextFrameTip => "Próximo quadro (.) · roda: quadro a quadro";
    public override string PlayTip => "Reproduzir / pausar (Espaço). J / K / L: para trás / pausa / para a frente · roda: quadro a quadro";
    public override string SpeedTip => "Velocidade · roda: ±0,1 · botão do meio: 1×";
    public override string OpenVideoTip => "Abrir vídeo (Ctrl + O)";
    public override string RangePositionTip => "Em relação ao trecho ampliado";
    public override string CurrentBookmarkTip => "Marcador atual ([ / ] anterior / próximo, 1–9 e 0 para ir aos dez primeiros)";
    public override string BookmarksButtonTip => "Painel de marcadores (Ctrl + B) · adicionar: B";
    public override string FullscreenTip => "Tela cheia (F)";
    public override string SettingsTip => "Configurações (Ctrl + ,)";
    public override string TracksTip => "Áudio e legendas (A / S)";
    public override string VolumeTip => "Volume";
    public override string MuteTip => "Silenciar (M)";
    public override string CloseBookmarkPanelTip => "Fechar (Ctrl + B)";
    public override string ExportVideoBookmarksTip => "Exportar os marcadores deste vídeo, para compartilhá-los";
    public override string ExportCollectionTip => "Exportar a coleção inteira, para compartilhá-la";
    public override string BookmarksTitle => "Marcadores";
    public override string CollectionTip => "Coleção de marcadores: cada uma tem os seus de cada vídeo. Trocar, criar, renomear ou excluir.";
    public override string SearchPlaceholder => "Pesquisar (Ctrl + F)";
    public override string ScopeThisVideo => "Este vídeo";
    public override string ScopeAllVideos => "Todos os vídeos";
    public override string PanelHint => "B cria uma região com o trecho ampliado ou, se não houver, de um só quadro (O define o fim). Clique ou Enter: ir até ela e ampliar. Ctrl / Shift + clique: vários. F2 renomeia, Delete exclui, Ctrl + Z desfaz.";
    public override string BookmarkKeyTip => "Tecla para ir a este marcador";
    public override string NameHint => "Enter salva · Esc mantém o nome como estava";
    public override string NameHintWithLabels => "Enter salva · Esc cancela · Alt + número: etiqueta";
    public override string GeneratingThumbnail => "Gerando miniatura…";
    public override string Close => "Fechar";
    public override string ShowInFolder => "Mostrar na pasta";
    public override string Open => "Abrir";
    public override string ZoneOf(string? region) => region is null ? "do trecho" : $"de “{region}”";
    public override string SaveZoneTip => "Salvar o trecho ampliado como marcador (B)";
    public override string NewLabelTitle => "Nova etiqueta";
    public override string NewLabelBoxTip => "Enter cria e aplica · Esc volta";
    public override string ZoneNameTip(string name) => $"O trecho ampliado é a região “{name}”: I / O, ⇤ ⇥ e as bandeirinhas o alteram";
    public override string CloseZoomDeselectTip(string name) => $"Fechar o zoom e desmarcar “{name}” (Esc)";
    public override string Loading => "Carregando…";

    // ---- Ajustes ----

    public override string SettingsTitle => "Configurações — VideoSeeker";
    public override string ResetDefaults => "Restaurar os valores padrão";
    public override string SupportTip => "O VideoSeeker é gratuito. Se ele for útil para você, que tal pagar um café para o autor?";
    public override string Support => "Apoiar";
    public override string SourceCode => "Código-fonte";
    public override string SourceCodeTip => "Software livre, com licença GPL v3.";
    public override string Version(string version) => $"Versão {version}";
    public override string SectionBars => "BARRAS";
    public override string MoveRegionsTip => "Arrastar o centro do trecho ampliado ou de uma região de marcador a move por inteiro. Desativado, move o cursor de reprodução.";
    public override string MoveRegions => "Mover regiões inteiras arrastando o centro";
    public override string EdgeFrameTip => "Ao arrastar uma extremidade do trecho ampliado ou de um marcador. Desativado, a imagem não muda.";
    public override string EdgeFrame => "Ver o quadro da extremidade arrastada";
    public override string CachedLineTip => "Marca sob as barras o que já está no cache: ali os saltos são instantâneos.";
    public override string CachedLine => "Linha verde do cache de quadros";
    public override string MarksTip => "Atalho: Ctrl + Shift + B.";
    public override string Marks => "Marcadores nas barras";
    public override string PulseMarksTip => "Com eles ocultos, ao criar um marcador ou ir para um, o botão deles (no canto inferior direito) pisca algumas vezes. Nenhuma mensagem aparece.";
    public override string PulseMarks => "Piscar o botão quando estiverem ocultos";
    public override string SectionNavigation => "NAVEGAÇÃO";
    public override string SecondsUnit => "s";
    public override string SeekTip => "Segundos que cada seta pula, e a roda do mouse sobre a barra principal: sozinha, com Shift ou com Alt.";
    public override string Seek => "Saltos com ← →";
    public override string ScrubTip => "Pixels que é preciso arrastar sobre a imagem, com o botão pressionado, para avançar um quadro.";
    public override string Scrub => "Sensibilidade do scrubbing";
    public override string PixelsPerFrame(int pixels) => $"{pixels} px por quadro";
    public override string WheelSpeed => "Velocidade";
    public override string WheelVolume => "Volume";
    public override string WheelTip => "Velocidade: ±0,1× (botão do meio: 1×). Volume: ±5%.";
    public override string Wheel => "Roda do mouse sobre o vídeo";
    public override string Resume => "Retomar cada vídeo de onde parou";
    public override string SectionTimeAndBookmarks => "TEMPO E MARCADORES";
    public override string TimeFormatTip => "Em 00:01:23:11, o último número é o quadro dentro de cada segundo.";
    public override string TimeFormat => "Formato do tempo";
    public override string NewColorTip => "Sem etiqueta, até você escolher outra coisa (ou sempre, se a última escolha não for repetida).";
    public override string NewColor => "Cor dos novos marcadores";
    public override string RepeatLabelTip => "O novo marcador recebe a etiqueta ou a cor escolhida da última vez; dá para trocar na caixa do nome.";
    public override string RepeatLabel => "Repetir a última etiqueta ou cor";
    public override string TimesInPreview => "Pré-visualização";
    public override string TimesInList => "Lista";
    public override string BookmarkTimesTip => "Na lista dá para ver todos de uma vez. Na pré-visualização, a lista deixa mais espaço para o nome.";
    public override string BookmarkTimes => "Tempos dos marcadores";
    public override string SectionAudioAndSubtitles => "ÁUDIO E LEGENDAS";
    public override string AudioLanguageTip => "Essa faixa é escolhida se o vídeo tiver várias. Vale ao abrir o próximo vídeo.";
    public override string AudioLanguage => "Idioma preferido do áudio";
    public override string SubtitleLanguageTip => "Essa faixa é escolhida ao ativar as legendas. Vale ao abrir o próximo vídeo.";
    public override string SubtitleLanguage => "Idioma preferido das legendas";
    public override string AppliesOnNextVideo => "Vale ao abrir o próximo vídeo.";
    public override string SubtitlesOnOpen => "Ativar as legendas ao abrir um vídeo";
    public override string NoPreference => "Sem preferência";
    public override string SectionPerformance => "DESEMPENHO";
    public override string CacheMemory => "Memória do cache de quadros";
    public override string CacheMemoryTip => "Quanto mais, mais tempo de vídeo dá para percorrer quadro a quadro instantaneamente. Vale ao abrir o próximo vídeo.";
    public override string CacheOfTotal(string used, string total) => $"{used} de {total} GB";
    public override string GigabytesUnit => "GB";
    public override string MegabytesUnit => "MB";
    public override string CacheTooBigTip => "Mais de 90% da memória da placa de vídeo: pode faltar para outros programas.";
    public override string HardwareDecodingTip => "Reproduz com a placa de vídeo. Desative só se algum vídeo aparecer com defeito.";
    public override string HardwareDecoding => "Decodificação por hardware";
    public override string SectionClips => "EXPORTAR CLIPES";
    public override string ChangeEllipsis => "Alterar…";
    public override string BesideVideo => "Junto ao vídeo";
    public override string ClipFolderTip => "Onde os clipes exportados são salvos. Se não der para gravar ali, vão para Vídeos\\VideoSeeker.";
    public override string ClipFolder => "Pasta de destino";
    public override string BesideEachVideo => "Junto a cada vídeo";
    public override string ClipFolderDialogTitle => "Pasta para os clipes exportados";
    public override string ClipClipboardTip => "Como arquivo, para colar direto em um chat, no Jira ou em uma pasta.";
    public override string ClipClipboard => "Copiar cada clipe para a área de transferência";
    public override string LightClipTip => "Megabytes por segundo de clipe, áudio incluído. Nunca mais que o vídeo original.";
    public override string LightClip => "Tamanho do clipe leve";
    public override string MegabytesPerSecond(string value) => $"{value} MB/s";
    public override string SectionGeneral => "GERAL";
    public override string InterfaceLanguage => "Idioma";
    public override string InterfaceLanguageTip => "O idioma do VideoSeeker. Automático: o do Windows. A mudança vale ao reiniciar.";
    public override string AnimatePanels => "Animar os painéis";
    public override string AnimatePanelsTip => "O painel de marcadores e a barra do zoom abrem e fecham deslizando, e o zoom passa suavemente de um trecho para outro.";
    public override string AutomaticLanguage(string language) => $"Automático · {language}";
    public override string LanguageChangedRestart => "O novo idioma será usado ao reiniciar o VideoSeeker.";
    public override string LanguageCaption => "Idioma";
    public override string RestartNow => "Reiniciar agora";
    public override string Later => "Mais tarde";
    public override string DefaultFolder => "Padrão";
    public override string DefaultFolderTip => "A pasta data, junto ao programa.";
    public override string DataFolderTip => "Configurações, marcadores e posição de cada vídeo. A mudança vale ao reiniciar.";
    public override string DataFolder => "Pasta de dados";
    public override string ChooseInWindows => "Escolher no Windows";
    public override string Unregister => "Remover";
    public override string DefaultPlayer => "Player de vídeo padrão";
    public override string DefaultPlayerTip => "Adiciona os tipos de vídeo a “Abrir com” e leva a Aplicativos padrão do Windows, onde se escolhe para quais.";
    public override string NumberRange => "Entre 0,04 e 3600 segundos";
    public override string PendingFolder(string folder) => $"Ao reiniciar, será usada {folder}.";
    public override string PendingFolderWithCopy(string folder) => $"Ao reiniciar, será usada {folder}, com uma cópia dos dados atuais.";
    public override string DataFolderDialogTitle => "Pasta para os dados do VideoSeeker";
    public override string DataFolderNested => "A pasta de dados não pode ficar dentro da atual nem contê-la.";
    public override string DataFolderHasData(string folder) => $"{folder}\n\njá tem dados do VideoSeeker. Se você copiar os atuais para lá, os que havia ficam guardados na subpasta “anteriores”.";
    public override string UseThatData => "Usar esses dados";
    public override string CopyCurrentData => "Copiar os atuais";
    public override string MoveDataQuestion(string folder) => $"Levar seus dados (configurações, marcadores, posição de cada vídeo) para\n{folder}?\n\nA pasta atual fica como está.";
    public override string MoveData => "Levar os dados";
    public override string StartEmpty => "Começar do zero";
    public override string Cancel => "Cancelar";
    public override string CannotUseFolder(string error) => $"Não é possível usar essa pasta:\n{error}";
    public override string DataFolderRestart => "A nova pasta de dados será usada ao reiniciar o VideoSeeker.";
    public override string OpenWithDoubleClick => "Abrir com clique duplo";
    public override string RegisterFailed(string error) => $"Não foi possível registrar os tipos de vídeo:\n{error}";
    public override string UnregisterFailed(string error) => $"Não foi possível remover o registro:\n{error}";
    public override string ResetQuestion => "Todas as configurações voltarão aos valores padrão.";
    public override string Reset => "Restaurar";
    public override string ResetTitle => "Restaurar configurações";
    public override string DefaultFolderPackagedTip => "A pasta do app no Windows. É apagada ao desinstalar o app.";
    public override string HoverPreview => "Miniatura ao passar o mouse pelas barras";
    public override string HoverPreviewTip => "Para ver o que há em um ponto do vídeo sem ir até ele.";
    public override string DataCopyIncomplete(string file) => $"A cópia de {file} ficou incompleta";

    // ---- Ventana principal: reproducción ----

    public override string VideoFileFilter => "Vídeos|*.mkv;*.mp4;*.m4v;*.avi;*.wmv;*.mov;*.webm;*.flv;*.ts;*.m2ts;*.mts;*.mpg;*.mpeg;*.vob;*.3gp;*.ogv|Todos os arquivos|*.*";
    public override string OpenVideoTitle => "Abrir vídeo";
    public override string FileNotFound(string path) => $"O arquivo não foi encontrado:\n{path}";
    public override string FrameNumber(int frame) => $"quadro {frame}";
    public override string FrameNumberApprox(string frame) => $"quadro ~{frame}";
    public override string Frames(int count) => count == 1 ? "1 quadro" : $"{count} quadros";
    public override string FrameOfRange(int frame, int count) => $"quadro {frame}/{count}";
    public override string VideoLoopOn => "Loop do vídeo ativado";
    public override string VideoLoopOff => "Loop do vídeo desativado";
    public override string RangeLoopOn(string? region) => $"Loop {ZoneOf(region)} ativado";
    public override string RangeLoopOff(string? region) => $"Loop {ZoneOf(region)} desativado";
    public override string ZoomSection => "Ampliar seção (±5 s)";
    public override string MarkFirstFromScreen(int frame, string? region) => $"Definir o início {ZoneOf(region)} no quadro atual ({frame})";
    public override string MarkLastFromScreen(int frame, string? region) => $"Definir o fim {ZoneOf(region)} no quadro atual ({frame})";
    public override string MarkFirstFromCursor(int frame, string? region) => $"Definir o início {ZoneOf(region)} no quadro sob o cursor ({frame})";
    public override string MarkLastFromCursor(int frame, string? region) => $"Definir o fim {ZoneOf(region)} no quadro sob o cursor ({frame})";
    public override string NormalSpeed => "1× (normal)";
    public override string SpeedToast(string speed) => $"Velocidade {speed}";
    public override string VolumeToast(int volume) => $"Volume {volume}%";
    public override string VolumeMutedToast(int volume) => $"Volume {volume}% (silenciado)";
    public override string Audio => "Áudio";
    public override string Subtitles => "Legendas";
    public override string SubtitlesOff => "Desativadas";
    public override string NoTracks => "Sem faixas";
    public override string Track(long id) => $"Faixa {id}";
    public override string ControlsPinned => "Controles fixos";
    public override string ControlsHidden => "Controles ocultos: leve o mouse para baixo para vê-los";
    public override string ControlsPinnedTip => "Controles fixos (Tab)";
    public override string PinControlsTip => "Fixar os controles (Tab)";
    public override string VideoGone(string name) => $"Não encontrado: {name}";
    public override string HistoryBackTip => "Voltar ao vídeo anterior (botão lateral do mouse)";
    public override string HistoryForwardTip => "Avançar para o próximo vídeo (botão lateral do mouse)";
    public override string HistoryAllBack(int count) => $"Clique direito: os {count} anteriores";
    public override string HistoryAllForward(int count) => $"Clique direito: os {count} seguintes";

    // ---- Bookmarks ----

    public override string NewBookmark => "Novo marcador";
    public override string RenameBookmark => "Renomear marcador";
    public override string SelectBookmarkFirst => "Selecione um marcador primeiro (clique nele nas barras ou no painel)";
    public override string BookmarkDeleted(string name) => $"“{name}” excluído · Ctrl + Z o recupera";
    public override string BookmarksDeleted(int count) => $"{count} marcadores excluídos · Ctrl + Z os recupera";
    public override string UndoFailed(string error) => $"Não foi possível desfazer: {error}";
    public override string RedoFailed(string error) => $"Não foi possível refazer: {error}";
    public override string NothingToUndo => "Nada para desfazer";
    public override string NothingToRedo => "Nada para refazer";
    public override string Undone(string step) => $"Desfeito: {step}";
    public override string Redone(string step) => $"Refeito: {step}";
    public override string InVideo(string video) => $" (em “{video}”)";
    public override string VideoHasNoBookmarks => "Este vídeo não tem marcadores (B adiciona um)";
    public override string NoBookmarkNumber(int number, int count) => $"Não há marcador {number} (há {count})";
    public override string NoMoreBookmarksAhead => "Não há mais marcadores à frente";
    public override string NoMoreBookmarksBehind => "Não há mais marcadores para trás";
    public override string AddBookmarkHere => "Adicionar marcador aqui";
    public override string BookmarksPanel => "Painel de marcadores";
    public override string Rename => "Renomear";
    public override string UseFrameAsThumbnail(int frame, string? region) => region is null ? $"Usar o quadro atual ({frame}) como miniatura" : $"Usar o quadro atual ({frame}) como miniatura de “{region}”";
    public override string FrameOutsideRegion(int frame, string time, string name, string range) => $"O quadro atual ({frame}, {time}) está fora da região “{name}” ({range}).\n\nUsá-lo mesmo assim como miniatura?";
    public override string UseIt => "Usar";
    public override string ResetThumbnail(string? region) => region is null ? "Voltar à miniatura do primeiro quadro" : $"Voltar à miniatura do primeiro quadro de “{region}”";
    public override string Delete => "Excluir";
    public override string DeleteCount(int count) => $"Excluir os {count}";
    public override string BookmarksExported(int count) => count == 1 ? "1 marcador exportado" : $"{count} marcadores exportados";
    public override string CannotWriteBesideVideo(string error) => $"Não foi possível gravar junto ao vídeo:\n{error}";
    public override string MarksShown => "Marcadores visíveis nas barras";
    public override string MarksHidden => "Marcadores ocultos nas barras (Ctrl + Shift + B os mostra)";
    public override string HideMarksTip => "Ocultar os marcadores nas barras (Ctrl + Shift + B). O painel continua funcionando.";
    public override string ShowMarksTip => "Mostrar os marcadores nas barras (Ctrl + Shift + B)";
    public override string CollectionButton(string name) => $"Coleção: {name}  ▾";
    public override string CollectionEmpty => "vazia";
    public override string BookmarksInVideos(int bookmarks, int videos) => videos == 1 ? $"{bookmarks} em 1 vídeo" : $"{bookmarks} em {videos} vídeos";
    public override string NewCollection => "Nova coleção…";
    public override string NewCollectionName => "Nome da nova coleção";
    public override string CollectionExists(string name) => $"Já existe uma coleção “{name}”";
    public override string RenameCollection(string name) => $"Renomear “{name}”…";
    public override string NewNameFor(string name) => $"Novo nome para “{name}”";
    public override string RenameFailed(string error) => $"Não foi possível renomear: {error}";
    public override string CollectionRenamed(string name) => $"Coleção renomeada para “{name}”";
    public override string DeleteCollection(string name) => $"Excluir “{name}”…";
    public override string ImportCollectionOrBookmarks => "Importar coleção ou marcadores…";
    public override string ExportCollectionMenu(string name) => $"Exportar “{name}”…";
    public override string CollectionIsEmpty => "Está vazia.";
    public override string CollectionWillBeLost(int bookmarks, int videos) => videos == 1 ? $"Ela tem {bookmarks} marcadores de 1 vídeo, que serão perdidos." : $"Ela tem {bookmarks} marcadores de {videos} vídeos, que serão perdidos.";
    public override string DeleteCollectionQuestion(string name, string detail) => $"Excluir a coleção “{name}”?\n{detail}";
    public override string DeleteFailed(string error) => $"Não foi possível excluir: {error}";
    public override string CollectionToast(string name) => $"Coleção “{name}”";
    public override string BookmarkCount(int count) => count == 1 ? "1 marcador" : $"{count} marcadores";
    public override string VideoCount(int count) => count == 1 ? "1 vídeo" : $"{count} vídeos";
    public override string BookmarksFromVideos(int bookmarks, int videos) => $"{bookmarks} marcadores de {videos} vídeos";
    public override string ShowVideoInExplorer => "Mostrar o vídeo no Explorador";
    public override string ShowVideoInExplorerMissing => "Mostrar o vídeo no Explorador (não encontrado)";
    public override string MovedTo(string collection) => $"Movido para “{collection}” · Ctrl + Z o devolve";
    public override string MovedManyTo(int count, string collection) => $"{count} movidos para “{collection}” · Ctrl + Z os devolve";
    public override string ChangeFailed(string error) => $"Não foi possível alterar: {error}";
    public override string Color => "Cor";
    public override string OwnColor => "Personalizada";
    public override string OtherColor => "Outra cor…";
    public override string MoveToCollection => "Mover para a coleção";
    public override string MoveToNewCollectionName => "Nome da coleção para onde movê-los";
    public override string MoveFailed(string error) => $"Não foi possível movê-los: {error}";
    public override string BookmarkVideoNotFound(string video) => $"O vídeo deste marcador não foi encontrado:\n{video}";
    public override string LocateIt => "Procurar…";
    public override string LocateTitle(string name) => $"Procurar “{name}”";
    public override string CannotReadFile(string error) => $"Não foi possível ler o arquivo:\n{error}";
    public override string NotSameVideo(string chosen, string name) => $"“{chosen}” não é o mesmo vídeo que “{name}” (o conteúdo é diferente: outra cópia, outra versão ou um vídeo diferente). Os tempos dos marcadores podem não coincidir.\n\nUsá-lo mesmo assim? Os marcadores desse vídeo (em todas as coleções) passarão para este arquivo; Ctrl + Z desfaz.";
    public override string UseItAnyway => "Usar mesmo assim";
    public override string NotAllMoved(string error) => $"Não foi possível transferir todos: {error}";
    public override string VideosCountName(int count) => $"{count} vídeos";
    public override string BookmarkOfZoomedRange => "Marcador do trecho ampliado";

    // ---- Pasos de deshacer (van detrás de «Deshecho:») ----

    public override string StepAdd(string name) => $"adicionar “{name}”";
    public override string StepEdit(string name) => $"editar “{name}”";
    public override string StepRename(string name) => $"renomear “{name}”";
    public override string StepLabelOf(string name) => $"etiqueta de “{name}”";
    public override string StepLabelOfMany(int count) => $"etiqueta de {count} marcadores";
    public override string StepColorOf(string name) => $"cor de “{name}”";
    public override string StepColorOfMany(int count) => $"cor de {count} marcadores";
    public override string StepDelete(string name) => $"excluir “{name}”";
    public override string StepDeleteMany(int count) => $"excluir {count} marcadores";
    public override string StepMoveTo(string name, string collection) => $"mover “{name}” para “{collection}”";
    public override string StepMoveManyTo(int count, string collection) => $"mover {count} marcadores para “{collection}”";
    public override string StepMoveStart(string name) => $"mover o início de “{name}”";
    public override string StepMoveEnd(string name) => $"mover o fim de “{name}”";
    public override string StepMove(string name) => $"mover “{name}”";
    public override string StepRelink(string from, string to) => $"passar os marcadores de “{from}” para “{to}”";
    public override string StepImportBookmarks => "importar marcadores";
    public override string StepImportCollection(string name) => $"importar a coleção “{name}”";
    public override string DefaultBookmarkName(int number) => $"Marcador {number}";
    public override string StepAdjust(string name) => $"ajustar “{name}”";
    public override string StepSetStartOf(string name) => $"definir o início de “{name}”";
    public override string StepSetEndOf(string name) => $"definir o fim de “{name}”";

    // ---- Etiquetas ----

    public override string LabelChipTip(string name) => $"Ver só os “{name}” (clique em outras para adicioná-las). Clique direito: renomear, cor, excluir…";
    public override string LabelChipShortcut(string shortcut) => $"\n{shortcut} a aplica ao marcador selecionado.";
    public override string NoLabel => "Sem etiqueta";
    public override string NoLabelChipTip => "Ver só os que não têm etiqueta";
    public override string AddLabelChip => "+ Etiqueta";
    public override string AddLabelChipTip => "Nova etiqueta (por exemplo “Chefe” ou “Cinemática”), para saber de relance o que cada marcador indica";
    public override string NoLabelsInCollection => "Esta coleção não tem etiquetas: “+ Etiqueta” no painel de marcadores (Ctrl + B)";
    public override string NoLabelNumber(int number, int count) => $"Não há etiqueta {number} (há {count})";
    public override string LabelToast(string name) => $"Etiqueta “{name}”";
    public override string BookmarkWithLabel(string name, string label) => $"“{name}”: {label}";
    public override string Labels => "Etiquetas";
    public override string NewLabel => "Nova etiqueta…";
    public override string NewLabelName => "Nome da nova etiqueta";
    public override string LabelExists(string name) => $"Já existe uma etiqueta “{name}”";
    public override string LabelsChangeFailed(string error) => $"Não foi possível alterar as etiquetas: {error}";
    public override string NoBookmarks => "sem marcadores";
    public override string RenameEllipsis => "Renomear…";
    public override string MoveLeft => "Mover para a esquerda";
    public override string MoveRight => "Mover para a direita";
    public override string AdoptLabelColor(string color, int count) => $"Aplicar aos marcadores sem etiqueta na cor {color.ToLower(Loc.Culture)} ({count})";
    public override string AdoptLabelSameColor(int count) => $"Aplicar aos marcadores sem etiqueta da mesma cor ({count})";
    public override string DeleteEllipsis => "Excluir…";
    public override string DeleteLabelQuestion(string name, int uses) => $"Excluir a etiqueta “{name}”?\n" + (uses == 1 ? "1 marcador a usa: ele fica sem etiqueta, com a sua cor." : $"{uses} marcadores a usam: eles ficam sem etiqueta, com a sua cor.");
    public override string StepCreateLabel(string name) => $"criar a etiqueta “{name}”";
    public override string StepRenameLabel(string name) => $"renomear a etiqueta “{name}”";
    public override string StepLabelColor(string name) => $"cor da etiqueta “{name}”";
    public override string StepMoveLabel(string name) => $"mover a etiqueta “{name}”";
    public override string StepAdoptLabel(string name, int count) => $"aplicar a etiqueta “{name}” a {count} marcadores";
    public override string StepDeleteLabel(string name) => $"excluir a etiqueta “{name}”";
    public override string LabelOrColor => "Etiqueta ou cor";
    public override string AddLabelChipLong => "+ Nova etiqueta";
    public override string AddLabelChipShort => "+ Nova";
    public override string AddLabelChipNameTip => "Criar uma etiqueta e aplicá-la";
    public override string ColorWithoutLabel(string color) => $"{color} (sem etiqueta)";

    // ---- Colores de la paleta ----

    public override string ColorPink => "Rosa";
    public override string ColorRed => "Vermelho";
    public override string ColorOrange => "Laranja";
    public override string ColorViolet => "Violeta";
    public override string ColorCyan => "Ciano";
    public override string ColorLime => "Lima";
    public override string ColorBrown => "Marrom";
    public override string ColorGray => "Cinza";
    public override string ColorWhite => "Branco";

    // ---- Compartir colecciones y bookmarks ----

    public override string CollectionHasNoBookmarks(string name) => $"A coleção “{name}” não tem marcadores";
    public override string ExportCollectionTitle => "Exportar a coleção";
    public override string CollectionFileFilter => "Coleção de marcadores|*.collection.json";
    public override string CollectionExported(string name) => $"Coleção “{name}” exportada";
    public override string CollectionSaveFailed(string error) => $"Não foi possível salvar a coleção:\n{error}";
    public override string ImportTitle => "Importar uma coleção ou os marcadores de um vídeo";
    public override string ImportFileFilter => "Coleções e marcadores|*.collection.json;*.bookmarks.json|Todos os JSON|*.json";
    public override string NotBookmarksFile(string file) => $"“{file}” não é um arquivo de marcadores";
    public override string CannotReadNamed(string file, string error) => $"Não foi possível ler “{file}”: {error}";
    public override string FileHasNoBookmarks(string file) => $"“{file}” não tem marcadores";
    public override string UnknownVideo => "Não se sabe de qual vídeo são: abra o vídeo e arraste-os de novo";
    public override string ImportFailed(string error) => $"Não foi possível importá-los: {error}";
    public override string AlreadyHadBookmarks => "Você já tinha estes marcadores";
    public override string BookmarksAddedToThisVideo(int count) => count == 1 ? "1 marcador adicionado a este vídeo" : $"{count} marcadores adicionados a este vídeo";
    public override string BookmarksAddedTo(int count, string video, string collection) => (count == 1 ? "1 marcador adicionado" : $"{count} marcadores adicionados") + $" a “{video}” na coleção “{collection}”";
    public override string ImportedCollectionName => "Importada";
    public override string ImportCollectionFailed(string error) => $"Não foi possível importar a coleção: {error}";
    public override string CollectionAdded(string name, string videos, string bookmarks) => $"Coleção “{name}” adicionada: {videos}, {bookmarks}";

    // ---- Exportar clips ----

    public override string ExportClip => "Exportar clipe";
    public override string ExportClipOf(string? region) => region is null ? "Exportar clipe do trecho ampliado" : $"Exportar clipe de “{region}”";
    public override string ExactOriginal => "Original exato";
    public override string ExactOriginalTip => "Mesmo codec e bitrate, cortado no quadro exato";
    public override string CopyOriginal(string start) => $"Original sem recodificar · {start}";
    public override string CopyOriginalTip => "Cópia direta: instantânea e sem perda, mas começa em um quadro-chave";
    public override string LightClipMenu(string rate) => $"Leve · {rate} MB/s";
    public override string LightClipMenuTip => "MP4 (H.264 e AAC) para chats e Jira, no máximo 1080p";
    public override string StartsAtPreviousKeyframe => "começa no quadro-chave anterior";
    public override string ExactCut => "corte exato";
    public override string StartsSecondsEarlier(string seconds) => $"começa {seconds} s antes";
    public override string StartsFramesEarlier(int frames) => frames == 1 ? "começa 1 quadro antes" : $"começa {frames} quadros antes";
    public override string ExportNeedsRange => "Para exportar um clipe, escolha uma região ou marque antes um trecho (I / O, Z ou arrastando com o botão direito)";
    public override string ExportWhat(string what) => $"Exportar {what}";
    public override string QuotedName(string name) => $"“{name}”";
    public override string ClipQueued => "Clipe na fila: será exportado quando o anterior terminar";
    public override string ExportingClip => "Exportando o clipe…";
    public override string ExportCancelled => "Exportação cancelada";
    public override string ClipSaved => "Clipe salvo";
    public override string ClipFailed => "Não foi possível exportar o clipe";
    public override string InFolder(string folder) => $"Em {folder}";
    public override string CopiedForChat => "Copiado para a área de transferência: Ctrl + V cola em um chat ou no Jira";
    public override string CopiedForFolder => "Copiado para a área de transferência: Ctrl + V cola em um chat ou em uma pasta";
    public override string CopyFailed => "Não foi possível copiar para a área de transferência";
    public override string Copy => "Copiar";
    public override string ExportingPercent(int percent) => $"Exportando {percent}%";
    public override string PreparingClip => "Preparando o clipe";
    public override string MoreQueued(int count) => $"\ne mais {count} na fila";
    public override string ClickToCancel => "\nClique para cancelar";
    public override string ClipReady => "Clipe pronto";
    public override string ExportError => "Erro ao exportar";
    public override string ClickToGo => "\nClique para mostrá-lo ou abri-lo";
    public override string ClickToSeeWhy => "Clique para ver por quê";
    public override string CancelClip(string name) => $"Cancelar {name}";
    public override string TheClip => "o clipe";
    public override string CancelQueuedToo(int count) => $"Cancelar também os {count} na fila";
    public override string CopyToClipboard => "Copiar para a área de transferência";
    public override string ClipCopied => "Clipe copiado para a área de transferência";
    public override string PreviousClips => "Clipes anteriores";
    public override string RemoveFromHere => "Remover daqui";
    public override string ExportingOnExit => "Um clipe está sendo exportado. Se você sair agora, ele será cancelado.";
    public override string Quit => "Sair";
    public override string Wait => "Esperar";
    public override string FfmpegMissing => "Falta o ffmpeg.exe na pasta do programa.";
    public override string FfmpegFailed => "O ffmpeg não conseguiu exportar o trecho.";
    public override string FfmpegNotStarted => "Não foi possível iniciar o ffmpeg.";
    public override string FfmpegExitCode(int code) => $"O ffmpeg terminou com o código {code}.";
    public override string LightClipSuffix => " (leve)";

    // ---- Otro color ----

    public override string OtherColorTitle => "Outra cor";
    public override string UseThisColor => "Usar esta cor";
    public override string ReservedRangeAmber => "âmbar do trecho ampliado";
    public override string ReservedPlayedBlue => "azul do que já foi reproduzido";
    public override string ReservedCacheGreen => "verde do cache de quadros";
    public override string ColorClash(string clash) => $"Esta cor se parece muito com o {clash} das barras, e elas vão se confundir.\n\nUsá-la mesmo assim?";

    // ---- Avisos ----

    public override string Ok => "OK";

    // ---- Abrir con doble clic (registro de Windows) ----

    public override string AppDescription => "Player de vídeo";
    public override string VideoFileType(string extension) => $"Vídeo {extension}";

    public override string LanguageName(string code) => code switch
    {
        "ger" => "Alemão",
        "ara" => "Árabe",
        "cat" => "Catalão",
        "chi" => "Chinês",
        "kor" => "Coreano",
        "dan" => "Dinamarquês",
        "spa" => "Espanhol",
        "baq" => "Basco",
        "fin" => "Finlandês",
        "fre" => "Francês",
        "glg" => "Galego",
        "gre" => "Grego",
        "heb" => "Hebraico",
        "hin" => "Hindi",
        "eng" => "Inglês",
        "ita" => "Italiano",
        "jpn" => "Japonês",
        "dut" => "Holandês",
        "nor" => "Norueguês",
        "pol" => "Polonês",
        "por" => "Português",
        "rus" => "Russo",
        "swe" => "Sueco",
        "tha" => "Tailandês",
        "tur" => "Turco",
        "ukr" => "Ucraniano",
        "vie" => "Vietnamita",
        _ => code,
    };
}
