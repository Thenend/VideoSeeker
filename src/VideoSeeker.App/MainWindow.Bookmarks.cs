using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using VideoSeeker.App.Controls;
using VideoSeeker.App.Data;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace VideoSeeker.App;

/// <summary>Bookmarks: crearlos, verlos en las barras, ir a ellos, editarlos y el panel con buscador.</summary>
public partial class MainWindow
{
    private BookmarkSet? _bookmarks;
    // Deshacer / rehacer de toda la app: sirve también para cambios en vídeos que ya no están abiertos.
    private readonly BookmarkHistory _history = new();
    // La región seleccionada, que es también la zona ampliada (MainWindow.xaml.cs); null, ninguna.
    private string? _zoomBookmark;
    private string? _selectedBookmark => _zoomBookmark;
    // Bookmark al que se le está poniendo nombre, y si es uno recién creado (entonces va con su creación al deshacer).
    private (string Id, bool IsNew)? _naming;
    // Otro nombre que se está pidiendo en el mismo cuadro (de una colección), con lo que hacer al aceptarlo.
    private Action<string>? _prompt;
    // Arrastrando una parte de un bookmark en una barra: el instante del extremo arrastrado (se enseña si el ajuste lo pide).
    private double? _bookmarkDragEdge;
    // Sin enseñar el extremo, pulsar un bookmark no mueve la imagen hasta saber que no es un arrastre: a dónde ir al soltar.
    private double? _bookmarkPressGo;
    private string? _hoverThumbKey;
    private readonly ObservableCollection<BookmarkRow> _rows = [];
    // Bookmarks de los demás vídeos para "Todos los vídeos": se leen al abrir el panel o cambiar a esa vista.
    private List<BookmarkFile>? _otherFiles;
    // Al abrir otro vídeo desde el panel: el bookmark que seleccionar (y ampliar) cuando cargue.
    private string? _pendingSelect;
    // Entre abrir un vídeo y saber su duración no se puede ampliar: la región queda pendiente.
    private bool _awaitingDuration;
    // Arrastrando un extremo (o la región entera): cómo era al empezar; el extremo fijo hace de ancla.
    private (double Start, double End)? _dragOrigin;
    // Pulsado (sin soltar) un bookmark que no es la zona: se selecciona solo si se suelta sin arrastrar.
    private string? _pressedOther;
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromMilliseconds(1600) };

    private void InitBookmarks()
    {
        foreach (var bar in new TimelineBar[] { Seek, Zoom })
        {
            var b = bar;
            bar.BookmarkPressed += (bm, part) =>
            {
                // Clic selecciona; arrastrar edita sin seleccionar. Pulsar (sin soltar) uno que no es la zona no lo
                // selecciona ni cambia la zona: si se arrastra, se edita en su sitio; si se suelta sin arrastrar, se va a él.
                if (b.HoldingBookmark && bm.Id != _zoomBookmark)
                {
                    HoverPopup.IsOpen = false;
                    _pressedOther = bm.Id;
                    _bookmarkPressGo = null;
                    return;
                }
                // Pulsando la zona en la propia barra de zoom, no se reencuadra hasta soltar (la escala no cambia bajo el ratón).
                if (b == Zoom && b.HoldingBookmark)
                    _freezeZoomView = true;
                // El triángulo de una región muy corta agarra su fin, pero un clic va a ella como su inicio.
                OnBookmarkPressed(bm, part == BookmarkPart.End && b.IsTiny(bm) ? BookmarkPart.Start : part);
            };
            bar.BookmarkDragged += OnBookmarkDragged;
            bar.BookmarkReleased += OnBookmarkReleased;
            bar.BookmarkMenuRequested += bm => ShowBookmarkMenu(bm, b);
            bar.BookmarkHovered += (bm, x) => OnBookmarkHovered(b, bm, x);
        }
        _thumbs.Ready += OnThumbnailReady;
        try
        {
            BookmarkCollections.Migrate(_store);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
        InitLabels();
        UpdateCollectionText();
        BookmarkList.ItemsSource = _rows;
        BookmarkList.SizeChanged += (_, _) => UpdatePanelHint();
        // También si la lista ya dio el clic por gestionado.
        BookmarkList.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnBookmarkListClick), handledEventsToo: true);
        BookmarkList.AddHandler(MouseRightButtonUpEvent, new MouseButtonEventHandler(OnBookmarkListRightClick), handledEventsToo: true);
        Deactivated += (_, _) => HoverPopup.IsOpen = false;
        BookmarkPanel.PreviewKeyDown += OnPanelKeyDown;
        SetupPanelGrip();
        SetupListPreview();
        SetupFullscreenControls();
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); FadeOutToast(); };
        PreviewTextInput += OnPreviewTextInput;
        _surface.RightClicked += ShowVideoMenu;
        ApplyMarksVisibility();
        _surface.MarkStarted += ReturnFocusToVideo;
        PreviewMouseDown += OnWindowPreviewMouseDown;
    }

    private void LoadBookmarks(string fingerprint, string path)
    {
        CloseNaming(commit: true);
        HoverPopup.IsOpen = false;
        _otherFiles = null;
        _bookmarks = new BookmarkSet(_store, _history, _store.Settings.BookmarkCollection, fingerprint, path, NextFrameStart);
        _bookmarks.Changed += OnBookmarksChanged;
        // Abierto desde el panel: el bookmark queda seleccionado ya (también en la lista, que no salta); su zona se
        // aplica al saber la duración (ApplyPendingBookmark).
        _zoomBookmark = _pendingSelect is { } pending && _bookmarks.Find(pending) != null ? pending : null;
        Seek.SelectedBookmark = Zoom.SelectedBookmark = _zoomBookmark;
        OnBookmarksChanged();
    }

    private IReadOnlyList<Bookmark> BookmarkItems => _bookmarks?.Items ?? [];

    // Fin de un bookmark del vídeo abierto (en memoria siempre lo tiene; "fps" para los de otros vídeos sin él).
    private double EndOf(Bookmark b, double? fps = null) => b.EndOr(1 / ((fps ?? Fps) is > 0 and var f ? f : 30));

    private void OnBookmarksChanged()
    {
        // Copia: el arrastre cambia la lista mientras las barras la recorren al pintar.
        var items = BookmarkItems.ToList();
        Seek.Bookmarks = Zoom.Bookmarks = items;
        // La zona sigue a su región (también al deshacer y rehacer); si se borró, se queda libre con los mismos límites.
        if (_zoomBookmark != null)
        {
            if (ZoneBookmark != null)
                FollowZoneBookmark();
            else
                Select(null);
        }
        UpdateZoneUi();
        // Miniaturas: se piden ya, para que estén al pasar el ratón (no en cada paso de un arrastre).
        if (_bookmarkDragEdge == null && _bookmarks != null)
            foreach (var b in items)
                _thumbs.Get(_bookmarks.Fingerprint, _bookmarks.VideoPath, b.ThumbTime);
        RefreshRows();
        UpdateTimeText();
    }

    /// <summary>
    /// Selecciona un bookmark, que pasa a ser la zona ampliada; null deselecciona (la zona, si la hay, se queda libre
    /// con los mismos límites).
    /// </summary>
    private void Select(string? id)
    {
        _zoomBookmark = id;
        Seek.SelectedBookmark = Zoom.SelectedBookmark = id;
        if (id != null && _rows.FirstOrDefault(r => r.Id == id && r.IsCurrentVideo) is { } row)
        {
            BookmarkList.SelectedItem = row;
            BookmarkList.ScrollIntoView(row);
        }
        FollowZoneBookmark();
        UpdateZoneUi();
    }

    // Texto bajo el vídeo: la región en la que se está (la más interior).
    private string CurrentBookmarkText() => RegionAtPosition() is { } shown ? $"◆ {shown.Name}" : "";

    // ---- Crear ----

    /// <summary>
    /// B (o Shift + B): una región con la zona ampliada si es libre; si no (sin zona, o la zona es otra región), de
    /// un frame en el frame actual. Queda seleccionada (así O fija su fin) y se abre el campo del nombre.
    /// </summary>
    private void AddBookmark()
    {
        if (_zoomRange is { } z && _zoomBookmark == null)
            AddRegion(z.Start, z.End);
        else
            AddBookmarkAt(_nav.CurrentTime);
    }

    /// <summary>"Añadir bookmark aquí" (y B sin zona libre): región de un frame en el frame dado.</summary>
    private void AddBookmarkAt(double time)
    {
        if (_duration <= 0)
            return;
        var start = Zoom.Grid.Snap(Math.Clamp(time, 0, _duration));
        AddRegion(start, NextFrameStart(start));
    }

    // Si ya hay una región igual, no se duplica: se selecciona y se renombra.
    private void AddRegion(double start, double end)
    {
        if (_bookmarks == null || _duration <= 0)
            return;
        if (_bookmarks.Items.FirstOrDefault(b => Math.Abs(b.Start - start) < 1e-4 && Math.Abs(EndOf(b) - end) < 1e-4) is { } existing)
        {
            Select(existing.Id);
            BeginRename(existing, isNew: false);
            PulseMarksButtonIfHidden();
            return;
        }
        var bookmark = _bookmarks.Add(_bookmarks.NextName(), start, end, NewBookmarkColor(), NewBookmarkLabel());
        Select(bookmark.Id);
        BeginRename(bookmark, isNew: true);
        PulseMarksButtonIfHidden();
    }

    // ---- Nombre en línea ----

    private void BeginRename(Bookmark bookmark, bool isNew)
    {
        CloseNaming(commit: true);
        HoverPopup.IsOpen = false;
        _naming = (bookmark.Id, isNew);
        _namingLabel = bookmark.Label;
        _namingColor = bookmark.Color;
        _namingCreatedLabel = false;
        BuildNameChips();
        SetNameCaption(isNew ? S.NewBookmark : S.RenameBookmark, RangeText(bookmark, withFrames: true));
        NameBox.Text = bookmark.Name;
        NamePopup.Placement = PlacementMode.Top;
        NamePopup.VerticalOffset = 0;
        NamePopup.PlacementTarget = Seek;
        NamePopup.PlacementRectangle = new Rect(Math.Clamp(Seek.XOf(bookmark.Start), 0, Seek.ActualWidth), 0, 0, 0);
        NamePopup.HorizontalOffset = -138;
        NamePopup.IsOpen = true;
        Dispatcher.BeginInvoke(() =>
        {
            NameBox.Focus();
            Keyboard.Focus(NameBox);
            NameBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    // Título del cuadro del nombre y, en otra línea más tenue, de qué es (si hay algo que decir).
    private void SetNameCaption(string title, string? detail)
    {
        NameCaption.Text = title;
        NameDetail.Text = detail ?? "";
        NameDetail.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnNameBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleNameLabelKey(e))
            e.Handled = true;
        else if (e.Key == Key.Enter)
        {
            CloseNaming(commit: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseNaming(commit: false);
            e.Handled = true;
        }
    }

    // Aceptar: lo mismo que Intro.
    private void OnNameAcceptClick(object sender, RoutedEventArgs e) => CloseNaming(commit: true);

    // Teclas con el foco en una etiqueta, un color o Aceptar (los cuadros de texto llevan las suyas): Intro guarda, Esc
    // cancela y Alt + número pone una etiqueta. Espacio e Intro sobre una etiqueta o un color los lleva cada uno.
    private void OnNamePopupKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.OriginalSource is TextBox)
            return;
        if (HandleNameLabelKey(e))
            e.Handled = true;
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            CloseNaming(commit: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseNaming(commit: false);
            e.Handled = true;
        }
    }

    // Clic fuera del campo: se guarda lo escrito.
    private void OnNamePopupClosed(object? sender, EventArgs e) => CloseNaming(commit: true);

    /// <summary>
    /// El mismo cuadro, para otros nombres (colecciones, etiquetas, bookmarks de otros vídeos), junto a lo que se pulsó:
    /// bajo "target" (un botón, una fila) o, sin él (desde un menú), bajo el ratón, donde se eligió la opción. WPF lo
    /// mete en la pantalla si no cabe.
    /// </summary>
    private void PromptName(string caption, string initial, UIElement? target, Action<string> done, string? detail = null)
    {
        CloseNaming(commit: false);
        _prompt = done;
        BuildNameChips();
        SetNameCaption(caption, detail);
        NameBox.Text = initial;
        NamePopup.Placement = PlacementMode.Bottom;
        if (target != null)
        {
            NamePopup.PlacementTarget = target;
            NamePopup.PlacementRectangle = Rect.Empty;
        }
        else
        {
            // Un punto fijo en la ventana (no MousePoint): así, si se mueve la ventana, el cuadro va con ella.
            var root = (UIElement)Content;
            var mouse = Mouse.GetPosition(root);
            NamePopup.PlacementTarget = root;
            NamePopup.PlacementRectangle = new Rect(mouse.X, mouse.Y, 0, 0);
        }
        NamePopup.HorizontalOffset = 0;
        NamePopup.VerticalOffset = 4;
        NamePopup.IsOpen = true;
        Dispatcher.BeginInvoke(() =>
        {
            NameBox.Focus();
            Keyboard.Focus(NameBox);
            NameBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void CloseNaming(bool commit)
    {
        if (_prompt is { } prompt)
        {
            _prompt = null;
            NamePopup.IsOpen = false;
            var text = NameBox.Text.Trim();
            ReturnFocusToVideo();
            if (commit && text.Length > 0)
                prompt(text);
            return;
        }
        if (_naming is not { } naming)
            return;
        _naming = null;
        NamePopup.IsOpen = false;
        NewLabelRow.Visibility = Visibility.Collapsed;
        var name = NameBox.Text.Trim();
        var (label, color) = (_namingLabel, _namingColor);
        if (commit && _bookmarks?.Find(naming.Id) is { } bookmark
            && ((name.Length > 0 && name != bookmark.Name) || label != bookmark.Label || (label == null && color != bookmark.Color)))
        {
            if (name.Length == 0)
                name = bookmark.Name;
            var styleChanged = label != bookmark.Label || (label == null && color != bookmark.Color);
            // Sin etiqueta, su color; con etiqueta, el propio se queda como estaba (manda el de la etiqueta).
            void Apply(Bookmark b)
            {
                b.Name = name;
                b.Label = label;
                if (label == null)
                    b.Color = color;
            }
            if (naming.IsNew && !_namingCreatedLabel)
            {
                // Mismo paso de deshacer que su creación.
                _bookmarks.Update(naming.Id, Apply, dragging: true);
                _bookmarks.EndEdit();
            }
            else
            {
                // (Si se creó una etiqueta con el cuadro abierto, el último paso ya es ese: este va aparte.)
                _bookmarks.Update(naming.Id, Apply, label: name != bookmark.Name ? S.StepRename : label != bookmark.Label ? S.StepLabelOf : S.StepColorOf);
            }
            if (styleChanged)
                RememberChoice(label, color);
        }
        ReturnFocusToVideo();
    }

    // Enter con un bookmark seleccionado: ir a él. Devuelve false si no hay ninguno.
    private bool JumpToSelected()
    {
        if (_bookmarks?.Find(_selectedBookmark) is not { } bookmark)
            return false;
        JumpTo(bookmark);
        return true;
    }

    private void RenameSelected()
    {
        if (_bookmarks?.Find(_selectedBookmark) is { } bookmark)
            BeginRename(bookmark, isNew: false);
        else
            ShowToast(S.SelectBookmarkFirst);
    }

    private void DeleteSelected()
    {
        if (_bookmarks?.Find(_selectedBookmark) is { } bookmark)
            DeleteBookmark(bookmark);
    }

    // Si era la zona, la zona se queda libre con los mismos límites (OnBookmarksChanged).
    private void DeleteBookmark(Bookmark bookmark)
    {
        if (_bookmarks == null)
            return;
        _bookmarks.Remove(bookmark.Id);
        ShowToast(S.BookmarkDeleted(bookmark.Name));
    }

    // Ctrl + Z / Ctrl + Y: el historial es de toda la app, así que puede deshacer en otro vídeo (lo dice el aviso).
    private void UndoBookmarks(bool redo)
    {
        BookmarkHistory.Step? step;
        try
        {
            step = redo ? _history.Redo() : _history.Undo();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast(redo ? S.RedoFailed(e.Message) : S.UndoFailed(e.Message));
            return;
        }
        if (step == null)
        {
            ShowToast(redo ? S.NothingToRedo : S.NothingToUndo);
            return;
        }
        AfterFilesChanged(step.Entries.Select(e => e.Path));
        var where = step.Video.Length == 0 || _bookmarks?.VideoName == step.Video ? "" : S.InVideo(step.Video);
        RemoveUndoneImports();
        ShowToast((redo ? S.Redone(step.Label) : S.Undone(step.Label)) + where);
    }

    // Tras cambiar archivos por fuera del vídeo abierto (deshacer, o desde el panel): se releen.
    private void AfterFilesChanged(IEnumerable<string> paths)
    {
        _otherFiles = null;
        LoadLabels();
        if (_bookmarks != null && paths.Any(p => string.Equals(p, _bookmarks.FilePath, StringComparison.OrdinalIgnoreCase)))
            _bookmarks.Reload();
        else
            RefreshRows();
    }

    // ---- Ir a los bookmarks ----

    /// <summary>Va al inicio del bookmark y lo selecciona: su región pasa a ser la zona ampliada.</summary>
    private void JumpTo(Bookmark bookmark)
    {
        if (_awaitingDuration)
            _pendingSelect = bookmark.Id;
        Select(bookmark.Id);
        // Sin cambiar cómo se reproduce: en pausa sigue en pausa; reproduciendo (también hacia atrás), sigue desde él.
        _nav.GoTo(bookmark.Start);
        ShowToast(bookmark.Name);
    }

    // 1–9 y 0: los diez primeros por orden de inicio.
    private void JumpToNumber(int key)
    {
        var index = key == 0 ? 9 : key - 1;
        var items = BookmarkItems;
        if (index < items.Count)
            JumpTo(items[index]);
        else
            ShowToast(items.Count == 0 ? S.VideoHasNoBookmarks : S.NoBookmarkNumber(index + 1, items.Count));
    }

    // [ / ]: el anterior o el siguiente. Reproduciendo, el anterior se salta el que acaba de pasar (como los
    // capítulos de mpv), para que pulsar dos veces vaya más atrás.
    private void JumpAdjacent(int direction)
    {
        var items = BookmarkItems;
        var t = _nav.CurrentTime;
        var margin = _nav.Paused ? 0.5 / Fps : 1.0;
        var target = direction > 0
            ? items.FirstOrDefault(b => b.Start > t + 0.5 / Fps)
            : items.LastOrDefault(b => b.Start < t - margin);
        if (target != null)
            JumpTo(target);
        else
            ShowToast(items.Count == 0 ? S.VideoHasNoBookmarks
                : direction > 0 ? S.NoMoreBookmarksAhead : S.NoMoreBookmarksBehind);
    }

    // [ y ] llegan como texto: en el teclado español se escriben con AltGr y no tienen tecla propia.
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox)
            return;
        switch (e.Text)
        {
            case "[":
                JumpAdjacent(-1);
                e.Handled = true;
                break;
            case "]":
                JumpAdjacent(1);
                e.Handled = true;
                break;
        }
    }

    // ---- En las barras ----

    private void OnBookmarkPressed(Bookmark bookmark, BookmarkPart part)
    {
        HoverPopup.IsOpen = false;
        // Seleccionarlo lo convierte en la zona ampliada.
        Select(bookmark.Id);
        // Como al elegirlo en el panel: su nombre sobre la imagen.
        ShowToast(bookmark.Name);
        _bookmarkPressGo = null;
        // El fin no mueve la imagen hasta arrastrarlo.
        if (part == BookmarkPart.End)
            return;
        if (_store.Settings.ShowDraggedEdgeFrame)
            _nav.GoTo(bookmark.Start);
        else
            _bookmarkPressGo = bookmark.Start;
    }

    /// <summary>
    /// Arrastrando el inicio, el fin o la región entera. Un extremo llevado más allá del otro los intercambia: el
    /// fijo hace de ancla (su frame sigue dentro) y nunca queda menos de un frame. Todo el arrastre es un paso de deshacer.
    /// </summary>
    private void OnBookmarkDragged(Bookmark bookmark, BookmarkPart part, double time)
    {
        if (_bookmarks == null || _duration <= 0)
            return;
        if (_dragOrigin == null)
        {
            _dragOrigin = (bookmark.Start, EndOf(bookmark));
            // Uno que no es la zona se edita sin seleccionarlo: se resalta mientras dura el arrastre.
            if (bookmark.Id != _zoomBookmark)
                Seek.EditingBookmark = Zoom.EditingBookmark = bookmark.Id;
            _bookmarks.BeginEdit(part switch
            {
                BookmarkPart.Start => S.StepMoveStart(bookmark.Name),
                BookmarkPart.End => S.StepMoveEnd(bookmark.Name),
                _ => S.StepMove(bookmark.Name),
            });
        }
        var origin = _dragOrigin.Value;
        double edge;
        if (part == BookmarkPart.Body)
        {
            var length = origin.End - origin.Start;
            var start = Zoom.Grid.Snap(Math.Clamp(time, 0, Math.Max(0, _duration - length)));
            _bookmarks.Update(bookmark.Id, b =>
            {
                // La miniatura elegida se mueve con la región.
                if (b.Thumb is { } thumb)
                    b.Thumb = thumb + (start - b.Start);
                b.Start = start;
                b.End = start + length;
            }, dragging: true);
            edge = start;
        }
        else
        {
            var (start, end, shown) = DragEdge(origin, part, time);
            _bookmarks.Update(bookmark.Id, b => { b.Start = start; b.End = end; }, dragging: true);
            edge = shown;
        }
        _bookmarkDragEdge = edge;
        // Avanzando solo porque el ratón salió de la barra de zoom: la barra se aleja para enseñar la región entera (el
        // otro extremo fijo en su lado), así al soltar no hay salto de escala.
        if (Zoom.AutoExtending && bookmark.Id == _zoomBookmark && _zoomRange is { } zone)
            Zoom.SetRange(zone.Start, zone.End);
        if (_store.Settings.ShowDraggedEdgeFrame)
            _nav.DragTo(edge);
    }

    private void OnBookmarkReleased(Bookmark bookmark, bool moved)
    {
        var go = _bookmarkPressGo;
        _bookmarkPressGo = null;
        var dragged = _dragOrigin != null;
        _dragOrigin = null;
        var other = _pressedOther == bookmark.Id;
        _pressedOther = null;
        Seek.EditingBookmark = Zoom.EditingBookmark = null;
        if (moved && dragged && _bookmarkDragEdge is { } edge)
        {
            _bookmarkDragEdge = null;
            _bookmarks?.EndEdit();
            if (_store.Settings.ShowDraggedEdgeFrame)
                _nav.SeekTo(edge);
        }
        else if (other && _bookmarks?.Find(bookmark.Id) is { } clicked)
        {
            // Un clic en uno que no era la zona: ahora sí, se selecciona (pasa a ser la zona) y se va a él.
            JumpTo(clicked);
        }
        else if (go is { } time)
        {
            // Un clic: ahora sí, ir al bookmark.
            _nav.GoTo(time);
        }
        // Soltado: la barra de zoom ya se puede reencuadrar a la zona.
        if (_freezeZoomView)
        {
            _freezeZoomView = false;
            if (_zoomRange is { } z)
                Zoom.SetRange(z.Start, z.End);
        }
        if (dragged)
            OnBookmarksChanged(); // pide la miniatura del nuevo inicio
    }

    private void OnBookmarkHovered(TimelineBar bar, Bookmark? bookmark, double x)
    {
        if (bookmark == null || !IsActive || _naming != null || _bookmarks == null || _bookmarkDragEdge != null)
        {
            HoverPopup.IsOpen = false;
            _hoverThumbKey = null;
            return;
        }
        SetLabeledName(HoverName, bookmark);
        HoverTime.Text = RangeText(bookmark, withFrames: true, twoLines: true);
        HoverTime.Visibility = Visibility.Visible;
        _hoverThumbKey = Thumbnails.Key(_bookmarks.Fingerprint, bookmark.ThumbTime);
        _hoverFingerprint = _bookmarks.Fingerprint;
        _hoverRow = null;
        _previewTimer.Stop();
        HoverPopup.Child.BeginAnimation(OpacityProperty, null);
        HoverPopup.Child.Opacity = 1;
        SetHoverImage(_thumbs.Get(_bookmarks.Fingerprint, _bookmarks.VideoPath, bookmark.ThumbTime));
        HoverPopup.IsOpen = false; // reabrir para que se recoloque
        HoverPopup.PlacementTarget = bar;
        HoverPopup.Placement = PlacementMode.Top;
        HoverPopup.PlacementRectangle = new Rect(Math.Clamp(x, 0, bar.ActualWidth), 0, 0, 0);
        HoverPopup.HorizontalOffset = -125;
        HoverPopup.VerticalOffset = 0;
        HoverPopup.IsOpen = true;
    }

    // ---- Vista previa grande también en el panel: al pasar el ratón por una fila o al llegar a ella con el teclado ----

    private BookmarkRow? _hoverRow;
    private string? _hoverFingerprint;

    private void SetupListPreview()
    {
        // Las ventanitas flotantes no siguen a la ventana solas: al moverla o cambiarla de tamaño se recolocan. La vista
        // previa de una marca de las barras se quita al cambiar de tamaño (su punto ya no es el mismo).
        LocationChanged += (_, _) => RepositionPopups();
        // WPF abre los popups "siempre encima" de todas las ventanas, también de otras apps y de los ajustes: se les quita.
        foreach (var popup in new[] { HoverPopup, NamePopup, ToastPopup, ControlsPopup, ClipPopup })
            popup.Opened += (_, _) => NotTopmost(popup);
        SizeChanged += (_, _) =>
        {
            if (_hoverRow == null)
                HoverPopup.IsOpen = false;
            RepositionPopups();
        };
        // Manda lo último que se usó: mover el ratón enseña la fila de debajo; las flechas, la elegida. WPF también avisa
        // de "movimientos" cuando la lista se desplaza bajo un ratón quieto: esos no cuentan.
        Point? lastMouse = null;
        BookmarkList.MouseMove += (_, e) =>
        {
            var at = e.GetPosition(this);
            if (at == lastMouse)
                return;
            lastMouse = at;
            if (ListRowFromEvent(e.OriginalSource) is { } row)
                ShowRowPreview(row, fromKeyboard: false);
        };
        BookmarkList.MouseLeave += (_, _) => HideRowPreview();
        // Lo que llega con un botón del ratón pulsado es un clic (elegir el bookmark), no el teclado: ahí no se abre, que
        // al soltar se quitaría y parpadearía.
        BookmarkList.SelectionChanged += (_, _) =>
        {
            if (BookmarkList.IsKeyboardFocusWithin && !AnyMouseButtonDown() && BookmarkList.SelectedItem is BookmarkRow row)
                ShowRowPreview(row, fromKeyboard: true);
        };
        BookmarkList.IsKeyboardFocusWithinChanged += (_, _) =>
        {
            // Al entrar en la lista (↓ desde el buscador) la fila ya estaba elegida antes de tener el foco.
            if (BookmarkList.IsKeyboardFocusWithin && !AnyMouseButtonDown() && BookmarkList.SelectedItem is BookmarkRow row)
                ShowRowPreview(row, fromKeyboard: true);
            else if (!BookmarkList.IsKeyboardFocusWithin && !BookmarkList.IsMouseOver)
                HideRowPreview();
        };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400));
            fade.Completed += (_, _) =>
            {
                if (!_previewTimer.IsEnabled && HoverPopup.Child.Opacity < 0.01)
                    HideRowPreview();
            };
            HoverPopup.Child.BeginAnimation(OpacityProperty, fade);
        };
    }

    // Con el teclado, la vista previa se desvanece a los 3 s sin pulsar nada (con el ratón, se ve mientras está encima).
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    // Bookmark recién elegido (clic o Enter): ya se ve en el vídeo, así que su vista previa no vuelve hasta que el ratón
    // pase a otra fila o se pulsen las flechas.
    private BookmarkRow? _previewDismissed;

    private void DismissRowPreview(BookmarkRow row)
    {
        HideRowPreview();
        _previewDismissed = row;
    }

    private static bool AnyMouseButtonDown() =>
        Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed;

    private static void NotTopmost(Popup popup)
    {
        if (popup.Child != null && PresentationSource.FromVisual(popup.Child) is System.Windows.Interop.HwndSource source)
            SetWindowPos(source.Handle, -2 /* HWND_NOTOPMOST */, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /* NOSIZE | NOMOVE | NOACTIVATE */);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

    private void RepositionPopups()
    {
        foreach (var popup in new[] { HoverPopup, NamePopup, ToastPopup })
        {
            if (!popup.IsOpen)
                continue;
            // Cambiar el desplazamiento obliga a WPF a recalcular la posición.
            var offset = popup.HorizontalOffset;
            popup.HorizontalOffset = offset + 1;
            popup.HorizontalOffset = offset;
        }
    }

    // A la izquierda del panel, a la altura de la fila (sobre la imagen: es una ventana aparte).
    private void ShowRowPreview(BookmarkRow row, bool fromKeyboard)
    {
        if (!IsActive || _naming != null || BookmarkList.ItemContainerGenerator.ContainerFromItem(row) is not FrameworkElement item)
            return;
        if (fromKeyboard)
        {
            _previewTimer.Stop();
            _previewTimer.Start();
        }
        else if (row == _previewDismissed)
        {
            return;
        }
        else
        {
            _previewTimer.Stop();
        }
        _previewDismissed = null;
        HoverPopup.Child.BeginAnimation(OpacityProperty, null);
        HoverPopup.Child.Opacity = 1;
        if (row == _hoverRow && HoverPopup.IsOpen)
            return;
        _hoverRow = row;
        SetLabeledName(HoverName, row.Bookmark);
        // Los tiempos van en la fila o aquí, según el ajuste; el vídeo ya está en la fila.
        HoverTime.Text = RangeText(row.Bookmark, withFrames: row.IsCurrentVideo, row.IsCurrentVideo ? null : FpsOf(row.Fingerprint), twoLines: true);
        HoverTime.Visibility = _store.Settings.BookmarkTimesInList ? Visibility.Collapsed : Visibility.Visible;
        _hoverThumbKey = row.ThumbKey;
        _hoverFingerprint = row.Fingerprint;
        SetHoverImage(_thumbs.Get(row.Fingerprint, row.VideoPath, row.Bookmark.ThumbTime));
        HoverPopup.IsOpen = false;
        HoverPopup.PlacementTarget = item;
        HoverPopup.Placement = PlacementMode.Left;
        HoverPopup.PlacementRectangle = Rect.Empty;
        HoverPopup.HorizontalOffset = -14;
        HoverPopup.VerticalOffset = -8;
        HoverPopup.IsOpen = true;
    }

    private void HideRowPreview()
    {
        _previewTimer.Stop();
        if (_hoverRow == null)
            return;
        _hoverRow = null;
        HoverPopup.Child.BeginAnimation(OpacityProperty, null);
        HoverPopup.Child.Opacity = 1;
        HoverPopup.IsOpen = false;
        _hoverThumbKey = null;
    }

    private void SetHoverImage(ImageSource? image)
    {
        HoverImage.Source = image;
        HoverWait.Visibility = image == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnThumbnailReady(string key)
    {
        if (key == _hoverThumbKey && _hoverFingerprint != null && HoverPopup.IsOpen)
            SetHoverImage(_thumbs.Get(_hoverFingerprint, null, ParseThumbTime(key)));
        foreach (var row in _rows)
            if (row.ThumbKey == key)
                row.Thumb = _thumbs.Get(row.Fingerprint, null, row.Bookmark.ThumbTime);
    }

    private static double ParseThumbTime(string key) =>
        long.TryParse(key[(key.LastIndexOf('_') + 1)..], out var ms) ? ms / 1000.0 : 0;

    // ---- Menús ----

    // "Añadir bookmark aquí": región de un frame en ese punto. Con una zona libre, también un bookmark con ella (B).
    private void AddBookmarkMenuItems(ItemsControl menu, double time)
    {
        var free = _zoomRange is { } && _zoomBookmark == null;
        var add = new MenuItem { Header = S.AddBookmarkHere, InputGestureText = free ? "" : "B" };
        add.Click += (_, _) => AddBookmarkAt(time);
        menu.Items.Add(add);
        if (free && _zoomRange is { } z)
        {
            var region = new MenuItem { Header = S.BookmarkOfZoomedRange, InputGestureText = "B" };
            region.Click += (_, _) => AddRegion(z.Start, z.End);
            menu.Items.Add(region);
        }
    }

    // Clic derecho sobre la imagen sin arrastrar: lo que tiene que ver con el frame que se ve (Mario, 8 de noviembre:
    // ampliar sección no va aquí, está en las barras y en Z). La miniatura es de la región seleccionada o, si no hay,
    // de la región en la que se está (la del texto bajo el vídeo); inicio y fin, como I / O, de la zona.
    private void ShowVideoMenu()
    {
        if (BuildVideoMenu() is { } menu)
            menu.IsOpen = true;
    }

    private ContextMenu? BuildVideoMenu()
    {
        if (_bookmarks == null)
            return null;
        var menu = new ContextMenu { PlacementTarget = VideoArea, Placement = PlacementMode.MousePoint };
        AddBookmarkMenuItems(menu, _nav.CurrentTime);
        var region = ZoneBookmark ?? RegionAtPosition();
        if (region != null || _zoomRange != null)
            menu.Items.Add(new Separator());
        if (region != null)
            AddThumbnailItems(menu, region, region.Name);
        if (_zoomRange != null)
        {
            var current = Zoom.Grid.FrameAt(_nav.CurrentTime);
            var markFirst = new MenuItem { Header = S.MarkFirstFromScreen(current, ZoneRegionName()), InputGestureText = "I" };
            markFirst.Click += (_, _) => MarkFirstFrame();
            menu.Items.Add(markFirst);
            var markLast = new MenuItem { Header = S.MarkLastFromScreen(current, ZoneRegionName()), InputGestureText = "O" };
            markLast.Click += (_, _) => MarkLastFrame();
            menu.Items.Add(markLast);
        }
        menu.Items.Add(new Separator());
        AddRevealItem(menu, _bookmarks.VideoPath);
        menu.Items.Add(new Separator());
        // Como el botón: abre o cierra sin más, con la marca si está abierto (desde el menú el teclado nunca está
        // en el panel, así que el Ctrl + B de TogglePanel nunca lo cerraría).
        var open = BookmarkPanel.Visibility == Visibility.Visible;
        var panel = new MenuItem { Header = S.BookmarksPanel, InputGestureText = $"{S.KeyCtrl}+B", IsCheckable = true, IsChecked = open };
        panel.Click += (_, _) => ToggleOpenPanel();
        menu.Items.Add(panel);
        return menu;
    }

    // Menú de un bookmark (clic derecho en su marca o en el panel): actúa sobre él sin seleccionarlo (no cambia la zona).
    // Ir a él es pinchar en él, y exportar los bookmarks junto al vídeo está en el botón del panel. Orden: lo que lo
    // cambia, lo que hace con el vídeo y, al final, renombrar y borrar.
    private void ShowBookmarkMenu(Bookmark bookmark, UIElement target)
    {
        HoverPopup.IsOpen = false;
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.MousePoint };
        var header = new MenuItem { Header = bookmark.Name, IsEnabled = false };
        menu.Items.Add(header);

        // O etiqueta o color propio (con etiqueta manda su color; elegir un color se la quita).
        AddLabelOrColorMenu(menu, _labels.Find(bookmark.Label)?.Id, bookmark.Color, mixedLabels: false,
            id => SetBookmarkLabel(bookmark, id), hex => SetBookmarkColor(bookmark, hex));
        AddMoveMenu(menu, collection => MoveToCollection([bookmark.Id], collection));
        AddThumbnailItems(menu, bookmark, null);

        menu.Items.Add(new Separator());
        AddExportMenu(menu, _bookmarks?.VideoPath, bookmark.Start, EndOf(bookmark), bookmark.Name);
        if (_bookmarks != null)
            AddRevealItem(menu, _bookmarks.VideoPath);

        menu.Items.Add(new Separator());
        var rename = new MenuItem { Header = S.Rename, InputGestureText = "F2" };
        rename.Click += (_, _) => BeginRename(bookmark, isNew: false);
        menu.Items.Add(rename);
        var delete = new MenuItem { Header = S.Delete, InputGestureText = bookmark.Id == _selectedBookmark ? S.KeyDelete : "" };
        delete.Click += (_, _) => DeleteBookmark(bookmark);
        menu.Items.Add(delete);
        menu.IsOpen = true;
    }

    // La región en la que está el frame actual (la más interior), o null.
    private Bookmark? RegionAtPosition()
    {
        if (_bookmarks == null)
            return null;
        Bookmark? region = null;
        foreach (var b in _bookmarks.Items)
        {
            if (b.Start > _position + 1e-4)
                break;
            if (_position >= b.Start - 1e-4 && _position < EndOf(b) - 1e-4)
                region = b; // la que empieza más tarde: la más interior
        }
        return region;
    }

    // "Usar el frame actual como miniatura" y, si tiene una elegida, volver a la del primer frame. "whose" es el nombre
    // del bookmark donde no se sabe por el menú de cuál es (el de la imagen); si no, null.
    private void AddThumbnailItems(ItemsControl menu, Bookmark bookmark, string? whose)
    {
        var current = Zoom.Grid.Snap(_nav.CurrentTime);
        var useFrame = new MenuItem { Header = S.UseFrameAsThumbnail(Zoom.Grid.FrameAt(current), whose) };
        useFrame.Click += (_, _) =>
        {
            // Un frame de fuera de la región seguramente es un despiste: se pregunta antes.
            if ((current < bookmark.Start - 1e-4 || current >= EndOf(bookmark) - 1e-4)
                && !Dialog.Confirm(this,
                    S.FrameOutsideRegion(Zoom.Grid.FrameAt(current), TimeFormat.Precise(current, Fps), bookmark.Name, RangeText(bookmark, withFrames: true)),
                    S.UseIt))
                return;
            _bookmarks?.Update(bookmark.Id, b => b.Thumb = current);
        };
        menu.Items.Add(useFrame);
        if (bookmark.Thumb != null)
        {
            var resetThumb = new MenuItem { Header = S.ResetThumbnail(whose) };
            resetThumb.Click += (_, _) => _bookmarks?.Update(bookmark.Id, b => b.Thumb = null);
            menu.Items.Add(resetThumb);
        }
    }

    private void ExportBookmarks()
    {
        if (_bookmarks == null)
            return;
        try
        {
            var path = _bookmarks.Export();
            var count = _bookmarks.Items.Count;
            // Como al exportar un clip: dónde está, para ir a la carpeta o copiar el archivo y mandarlo con el vídeo.
            ShowFileSavedPopup(S.BookmarksExported(count), path, "");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Dialog.Inform(this, S.CannotWriteBesideVideo(e.Message));
        }
    }

    // En "Este vídeo" exporta sus bookmarks junto al vídeo; en "Todos los vídeos", la colección entera.
    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (ScopeAll.IsChecked == true)
            ExportCollection();
        else
            ExportBookmarks();
    }

    // ---- Mostrar u ocultar las marcas ----

    // Ctrl + Shift + B o el botón del ojo: quita de las barras las marcas y regiones (y el nombre bajo el vídeo)
    // para una interfaz limpia; el panel, B y los saltos siguen funcionando.
    private void ToggleMarks()
    {
        _store.Settings.ShowBookmarkMarks = !_store.Settings.ShowBookmarkMarks;
        ApplyMarksVisibility();
        ShowToast(_store.Settings.ShowBookmarkMarks ? S.MarksShown : S.MarksHidden);
    }

    private void ApplyMarksVisibility()
    {
        var show = _store.Settings.ShowBookmarkMarks;
        StopMarksPulse();
        Seek.ShowBookmarks = Zoom.ShowBookmarks = show;
        BookmarkText.Visibility = show ? Visibility.Visible : Visibility.Hidden;
        if (!show)
            HoverPopup.IsOpen = false;
        _settingsWindow?.LoadValues();
        MarksIcon.Data = (Geometry)FindResource(show ? "MarksShownIcon" : "MarksHiddenIcon");
        MarksButton.Foreground = (Brush)FindResource(show ? "Fg" : "FgMuted");
        MarksButton.ToolTip = show
            ? S.HideMarksTip
            : S.ShowMarksTip;
    }

    private void OnMarksButtonClick(object sender, RoutedEventArgs e) => ToggleMarks();

    // ---- Aviso de marcas ocultas ----

    // Con las marcas ocultas, crear un bookmark o elegirlo en el panel no deja ver nada en las barras (solo las
    // banderitas de la zona), y parece que algo falla. El ojo late tres veces para que se note: un halo azul se abre y
    // se apaga detrás del botón, que se enciende un momento, y el icono crece un poco y vuelve.
    private const int MarksPulseBeats = 3;
    private static readonly TimeSpan MarksPulseBeat = TimeSpan.FromMilliseconds(800);

    private void PulseMarksButtonIfHidden()
    {
        if (_store.Settings.ShowBookmarkMarks)
            return;
        StopMarksPulse();
        var accent = ((SolidColorBrush)FindResource("Accent")).Color;
        var muted = ((SolidColorBrush)FindResource("FgMuted")).Color;
        var beats = new RepeatBehavior(MarksPulseBeats);
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        KeyTime At(double fraction) =>
            KeyTime.FromTimeSpan(MarksPulseBeat * fraction);

        // Halo: sale del borde del botón, crece y se desvanece.
        var ringScale = (ScaleTransform)MarksPulseRing.RenderTransform;
        var grow = new DoubleAnimation(1, 1.6, MarksPulseBeat) { EasingFunction = easeOut, RepeatBehavior = beats };
        ringScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        ringScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        MarksPulseRing.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.9, 0, MarksPulseBeat) { EasingFunction = easeOut, RepeatBehavior = beats });

        // Icono: se enciende en azul y late (crece y vuelve) al ritmo del halo.
        var foreground = new SolidColorBrush(muted);
        MarksButton.Foreground = foreground;
        var color = new ColorAnimationUsingKeyFrames { RepeatBehavior = beats };
        color.KeyFrames.Add(new LinearColorKeyFrame(muted, At(0)));
        color.KeyFrames.Add(new EasingColorKeyFrame(accent, At(0.15)));
        color.KeyFrames.Add(new LinearColorKeyFrame(accent, At(0.45)));
        color.KeyFrames.Add(new EasingColorKeyFrame(muted, At(1)));
        foreground.BeginAnimation(SolidColorBrush.ColorProperty, color);
        var iconScale = (ScaleTransform)MarksIcon.RenderTransform;
        var beat = new DoubleAnimationUsingKeyFrames { RepeatBehavior = beats };
        beat.KeyFrames.Add(new LinearDoubleKeyFrame(1, At(0)));
        beat.KeyFrames.Add(new EasingDoubleKeyFrame(1.22, At(0.15), easeOut));
        beat.KeyFrames.Add(new EasingDoubleKeyFrame(1, At(0.6),
            new CubicEase { EasingMode = EasingMode.EaseInOut }));
        beat.KeyFrames.Add(new LinearDoubleKeyFrame(1, At(1)));
        iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, beat);
        iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, beat);

        // Fondo del botón: un destello azul suave en cada latido.
        var background = new SolidColorBrush(Color.FromArgb(0, accent.R, accent.G, accent.B));
        MarksButton.Background = background;
        var flash = new ColorAnimationUsingKeyFrames { RepeatBehavior = beats };
        flash.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(0, accent.R, accent.G, accent.B), At(0)));
        flash.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0x48, accent.R, accent.G, accent.B), At(0.15)));
        flash.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0, accent.R, accent.G, accent.B), At(1)));
        background.BeginAnimation(SolidColorBrush.ColorProperty, flash);
    }

    // Quita el latido (también si se enseñan u ocultan las marcas a mitad) y deja el botón como estaba.
    private void StopMarksPulse()
    {
        var ringScale = (ScaleTransform)MarksPulseRing.RenderTransform;
        ringScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ringScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        MarksPulseRing.BeginAnimation(OpacityProperty, null);
        MarksPulseRing.Opacity = 0;
        var iconScale = (ScaleTransform)MarksIcon.RenderTransform;
        iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        MarksButton.ClearValue(BackgroundProperty);
        MarksButton.Foreground = (Brush)FindResource(_store.Settings.ShowBookmarkMarks ? "Fg" : "FgMuted");
    }

    // ---- Colecciones ----

    private void UpdateCollectionText() => CollectionText.Text = S.CollectionButton(_store.Settings.BookmarkCollection);

    private void OnCollectionClick(object sender, RoutedEventArgs e)
    {
        var active = _store.Settings.BookmarkCollection;
        var menu = new ContextMenu { PlacementTarget = CollectionButton, Placement = PlacementMode.Bottom };
        var names = BookmarkCollections.List(_store, active);
        foreach (var name in names)
        {
            var (videos, bookmarks) = BookmarkCollections.Count(_store, name);
            var item = new MenuItem
            {
                Header = name, IsCheckable = true, IsChecked = name == active,
                InputGestureText = bookmarks == 0 ? S.CollectionEmpty : S.BookmarksInVideos(bookmarks, videos),
            };
            var n = name;
            item.Click += (_, _) => SetCollection(n, showAll: true);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var create = new MenuItem { Header = S.NewCollection };
        create.Click += (_, _) => PromptName(S.NewCollectionName, "", null, name =>
        {
            if (BookmarkCollections.Clean(name) is not { } clean)
                return;
            if (BookmarkCollections.Exists(_store, clean))
            {
                ShowToast(S.CollectionExists(clean));
                return;
            }
            BookmarkCollections.Create(_store, clean);
            SetCollection(clean, showAll: true);
        });
        menu.Items.Add(create);
        var rename = new MenuItem { Header = S.RenameCollection(active) };
        rename.Click += (_, _) => PromptName(S.NewNameFor(active), active, null, name =>
        {
            if (BookmarkCollections.Clean(name) is not { } clean || clean == active)
                return;
            if (!string.Equals(clean, active, StringComparison.OrdinalIgnoreCase) && BookmarkCollections.Exists(_store, clean))
            {
                ShowToast(S.CollectionExists(clean));
                return;
            }
            try
            {
                BookmarkCollections.Rename(_store, active, clean);
                // Las rutas del historial dejan de valer.
                _history.Clear();
                _importedCollections.Clear();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowToast(S.RenameFailed(ex.Message));
                return;
            }
            SetCollection(clean, announce: false);
            ShowToast(S.CollectionRenamed(clean));
        });
        menu.Items.Add(rename);
        var delete = new MenuItem { Header = S.DeleteCollection(active), IsEnabled = names.Count > 1 };
        delete.Click += (_, _) => DeleteCollection(active, names);
        menu.Items.Add(delete);
        menu.Items.Add(new Separator());
        AddLabelsManageMenu(menu);
        // Compartir: lo mismo que arrastrar el archivo sobre la app o el botón de exportar en "Todos los vídeos".
        menu.Items.Add(new Separator());
        var import = new MenuItem { Header = S.ImportCollectionOrBookmarks };
        import.Click += (_, _) => ShowImportDialog();
        menu.Items.Add(import);
        var export = new MenuItem { Header = S.ExportCollectionMenu(active) };
        export.Click += (_, _) => ExportCollection();
        menu.Items.Add(export);
        menu.IsOpen = true;
    }

    private void DeleteCollection(string name, List<string> names)
    {
        var (videos, bookmarks) = BookmarkCollections.Count(_store, name);
        var detail = bookmarks == 0 ? S.CollectionIsEmpty : S.CollectionWillBeLost(bookmarks, videos);
        if (!Dialog.Confirm(this, S.DeleteCollectionQuestion(name, detail), S.Delete))
            return;
        try
        {
            BookmarkCollections.Delete(_store, name);
            _history.Clear();
            _importedCollections.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowToast(S.DeleteFailed(ex.Message));
            return;
        }
        SetCollection(names.First(n => n != name), showAll: true);
    }

    /// <summary>
    /// Cambia la colección activa: las barras y el panel pasan a mostrar sus bookmarks. Con "showAll" (al elegirla
    /// en el menú) el panel pasa a "Todos los vídeos": lo normal es querer ir a bookmarks de otros vídeos de ella.
    /// </summary>
    private void SetCollection(string name, bool announce = true, bool showAll = false)
    {
        if (showAll && ScopeAll.IsChecked != true)
            ScopeAll.IsChecked = true;
        CloseNaming(commit: true);
        _store.Settings.BookmarkCollection = name;
        _store.SaveSettings();
        BookmarkCollections.Create(_store, name);
        UpdateCollectionText();
        _labelFilter.Clear();
        LoadLabels();
        _otherFiles = null;
        if (_bookmarks != null)
            LoadBookmarks(_bookmarks.Fingerprint, _bookmarks.VideoPath);
        else
            RefreshRows();
        if (announce)
            ShowToast(S.CollectionToast(name));
    }

    // ---- Panel ----

    // Ctrl + B: si el panel no tiene el teclado (cerrado, o abierto pero usando el vídeo), lo abre con el foco en
    // el buscador; si ya lo tiene, lo cierra. Esc o un clic fuera devuelven el teclado al vídeo.
    private void TogglePanel()
    {
        if (BookmarkPanel.Visibility == Visibility.Visible && BookmarkPanel.IsKeyboardFocusWithin)
            ShowPanel(false);
        else
            OpenSearch();
    }

    private void ShowPanel(bool show)
    {
        BookmarkPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        BookmarksButton.Foreground = (Brush)FindResource(show ? "Accent" : "Fg");
        _otherFiles = null;
        if (show)
            RefreshRows();
        else
            ReturnFocusToVideo();
    }

    // Ancho del panel: arrastrando su borde izquierdo, entre 260 px y lo que deje 320 px al vídeo; se guarda al soltar.
    private const double MinVideoWidth = 320;

    private void SetupPanelGrip()
    {
        BookmarkPanel.Width = _store.Settings.BookmarkPanelWidth;
        SizeChanged += (_, _) => BookmarkPanel.MaxWidth = Math.Max(BookmarkPanel.MinWidth, ActualWidth - MinVideoWidth);
        // Con la posición en la ventana, que no se mueve con el borde.
        double fromX = 0, fromWidth = 0;
        PanelGrip.DragStarted += (_, _) =>
        {
            HoverPopup.IsOpen = false;
            fromX = Mouse.GetPosition(this).X;
            fromWidth = BookmarkPanel.ActualWidth;
        };
        PanelGrip.DragDelta += (_, _) =>
            BookmarkPanel.Width = Math.Clamp(fromWidth + fromX - Mouse.GetPosition(this).X, BookmarkPanel.MinWidth, BookmarkPanel.MaxWidth);
        PanelGrip.DragCompleted += (_, _) => SavePanelWidth();
        PanelGrip.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2)
                return;
            BookmarkPanel.Width = Settings.DefaultPanelWidth;
            SavePanelWidth();
            e.Handled = true;
        };
    }

    private void SavePanelWidth()
    {
        _store.Settings.BookmarkPanelWidth = Math.Round(BookmarkPanel.Width);
        _store.SaveSettings();
        UpdatePanelHint();
    }

    private void OpenSearch()
    {
        ShowPanel(true);
        BookmarkSearch.Focus();
        BookmarkSearch.SelectAll();
    }

    // El botón (y el menú del vídeo) abre y cierra sin más (al pulsarlo, el teclado ya ha vuelto al vídeo).
    private void OnBookmarksButtonClick(object sender, RoutedEventArgs e) => ToggleOpenPanel();

    private void ToggleOpenPanel()
    {
        if (BookmarkPanel.Visibility == Visibility.Visible)
            ShowPanel(false);
        else
            OpenSearch();
    }

    private void OnClosePanelClick(object sender, RoutedEventArgs e) => ShowPanel(false);

    // Tab (o Shift + Tab) dentro del panel alterna "Este vídeo" / "Todos los vídeos", sin sacar el foco del panel.
    private void OnPanelKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab)
            return;
        var inList = BookmarkList.IsKeyboardFocusWithin;
        if (ScopeAll.IsChecked == true)
            ScopeThis.IsChecked = true;
        else
            ScopeAll.IsChecked = true;
        // La lista se rehace: el foco pasa a la fila seleccionada (o la primera) para seguir con las flechas.
        if (inList && _rows.Count > 0)
        {
            if (BookmarkList.SelectedIndex < 0)
                BookmarkList.SelectedIndex = 0;
            BookmarkList.UpdateLayout();
            (BookmarkList.ItemContainerGenerator.ContainerFromIndex(BookmarkList.SelectedIndex) as ListBoxItem)?.Focus();
        }
        else if (inList)
        {
            BookmarkSearch.Focus();
        }
        e.Handled = true;
    }

    private void OnScopeChanged(object sender, RoutedEventArgs e)
    {
        if (ExportBookmarksButton != null && ScopeAll != null)
        {
            var all = ScopeAll.IsChecked == true;
            ExportBookmarksButton.ToolTip = all ? S.ExportCollectionTip : S.ExportVideoBookmarksTip;
            ExportIcon.Data = (Geometry)FindResource(all ? "ExportCollectionIcon" : "ExportBookmarksIcon");
        }
        _otherFiles = null;
        RefreshRows();
    }

    private void OnBookmarkSearchChanged(object sender, TextChangedEventArgs e) => RefreshRows();

    private void RefreshRows()
    {
        if (BookmarkPanel == null || BookmarkPanel.Visibility != Visibility.Visible)
            return;
        var terms = Fold(BookmarkSearch.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var all = ScopeAll.IsChecked == true;
        var rows = new List<BookmarkRow>();
        var currentKey = _bookmarks?.Fingerprint;
        List<BookmarkRow> CurrentRows()
        {
            var list = new List<BookmarkRow>();
            if (_bookmarks == null)
                return list;
            var items = _bookmarks.Items;
            for (var i = 0; i < items.Count; i++)
                list.Add(MakeRow(items[i], _bookmarks.Fingerprint, _bookmarks.VideoPath, all ? Path.GetFileName(_bookmarks.VideoPath) : null,
                    i < 10 ? ((i + 1) % 10).ToString(CultureInfo.InvariantCulture) : "", current: true));
            return list;
        }
        if (all)
        {
            // Por nombre de vídeo, el abierto también en su sitio: al abrir otro desde aquí la lista no se reordena.
            _otherFiles ??= [.. BookmarkSet.LoadAll(_store, _store.Settings.BookmarkCollection).Where(f => f.Fingerprint != currentKey)];
            var groups = _otherFiles.Select(f => (Name: f.Video, Key: f.Fingerprint, File: (BookmarkFile?)f)).ToList();
            if (_bookmarks != null)
                groups.Add((Path.GetFileName(_bookmarks.VideoPath), _bookmarks.Fingerprint, null));
            foreach (var group in groups.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(g => g.Key, StringComparer.Ordinal))
            {
                if (group.File is not { } file)
                {
                    rows.AddRange(CurrentRows());
                    continue;
                }
                var path = _store.Library.Videos.TryGetValue(file.Fingerprint, out var entry) ? entry.Path : null;
                foreach (var b in file.Bookmarks)
                    rows.Add(MakeRow(b, file.Fingerprint, path, file.Video, "", current: false));
            }
        }
        else
        {
            rows.AddRange(CurrentRows());
        }
        if (terms.Length > 0 || _labelFilter.Count > 0)
            rows = rows.Where(r => terms.All(r.SearchText.Contains) && PassesLabelFilter(r.Bookmark)).ToList();

        // Se rehace la lista sin mover el desplazamiento, para no perder de vista lo que se estaba mirando.
        var selected = _selectedBookmark ?? _pendingSelect;
        // Si había varios elegidos, siguen elegidos tras rehacer la lista (p. ej. al cambiarles el color).
        var multi = BookmarkList.SelectedItems.Count > 1
            ? BookmarkList.SelectedItems.OfType<BookmarkRow>().Select(r => r.Id).ToHashSet() : null;
        var scroller = FindScrollViewer(BookmarkList);
        var offset = scroller?.VerticalOffset ?? 0;
        _rows.Clear();
        foreach (var row in rows)
            _rows.Add(row);
        if (scroller != null)
        {
            BookmarkList.UpdateLayout();
            scroller.ScrollToVerticalOffset(offset);
        }
        if (multi != null)
        {
            foreach (var row in _rows.Where(r => multi.Contains(r.Id)))
                BookmarkList.SelectedItems.Add(row);
        }
        else if (_rows.FirstOrDefault(r => r.Id == selected) is { } sel)
        {
            BookmarkList.SelectedItem = sel;
            BookmarkList.ScrollIntoView(sel);
        }
        UpdatePanelHint();
    }

    // Las instrucciones de fondo solo se ven mientras la lista no llega hasta ellas (con pocos bookmarks).
    private void UpdatePanelHint()
    {
        BookmarkList.UpdateLayout();
        var used = 0.0;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (BookmarkList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement item)
            {
                used = double.MaxValue; // no generada: la lista ya no cabe entera
                break;
            }
            used += item.ActualHeight;
        }
        PanelHint.Visibility = used + PanelHint.ActualHeight + 12 <= BookmarkList.ActualHeight || _rows.Count == 0
            ? Visibility.Visible : Visibility.Hidden;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject element)
    {
        if (element is ScrollViewer viewer)
            return viewer;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(element, i)) is { } found)
                return found;
        return null;
    }

    private BookmarkRow MakeRow(Bookmark b, string fingerprint, string? videoPath, string? videoName, string key, bool current)
    {
        // En pleno arrastre no se piden miniaturas de cada posición intermedia.
        var thumb = _thumbs.Get(fingerprint, _bookmarkDragEdge == null ? videoPath : null, b.ThumbTime);
        var label = _labels.Find(b.Label);
        return new BookmarkRow(b, fingerprint, videoPath, current)
        {
            ColorHex = label?.Color ?? b.Color,
            LabelText = label != null ? label.Name + "  " : "",
            TimeText = !_store.Settings.BookmarkTimesInList ? ""
                : current ? ListTimeText(b, withFrames: true, Fps) : ListTimeText(b, withFrames: false, FpsOf(fingerprint)),
            VideoName = videoName ?? "",
            KeyText = key,
            Thumb = thumb,
            SearchText = Fold(b.Name + " " + (videoName ?? "") + " " + label?.Name),
        };
    }

    // Sin tildes ni mayúsculas.
    private static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    // Fps de otro vídeo (apuntado la última vez que se abrió), para sus timecodes; 0 si no se sabe.
    private double FpsOf(string fingerprint) => _store.Library.Videos.TryGetValue(fingerprint, out var entry) ? entry.Fps : 0;

    /// <summary>Tiempo de un bookmark; "fps" es el de su vídeo (por defecto, el abierto).</summary>
    /// <param name="twoLines">El rango en una línea y la duración debajo (para la vista previa, que es estrecha).</param>
    private string RangeText(Bookmark b, bool withFrames, double? fps = null, bool twoLines = false)
    {
        var rate = fps ?? Fps;
        var start = TimeFormat.Precise(b.Start, rate);
        var end = EndOf(b, rate);
        var width = end - b.Start;
        var length = width < 60 ? $"{width:0.00} s" : TimeFormat.Format(width, withMilliseconds: false);
        var text = $"{start} – {TimeFormat.Precise(end, rate)}{(twoLines ? "\n" : " · ")}{length}";
        if (withFrames)
            text += $" · {Zoom.Grid.FrameAt(end) - Zoom.Grid.FrameAt(b.Start)} f";
        return text;
    }

    /// <summary>
    /// Tiempo de un bookmark en la lista del panel, que es estrecha: con décimas y, en una región, la duración y los
    /// frames en una segunda línea. Si aun así no cabe, solo se parte entre los grupos (los espacios de dentro no separan).
    /// </summary>
    private string ListTimeText(Bookmark b, bool withFrames, double fps)
    {
        const string Glue = "\u00A0"; // espacio que no parte la línea
        var start = TimeFormat.Short(b.Start, fps);
        var end = EndOf(b, fps);
        var width = end - b.Start;
        var details = width < 60 ? $"{width:0.0}{Glue}s" : TimeFormat.Format(width, withMilliseconds: false);
        if (withFrames)
            details += $" ·{Glue}{Zoom.Grid.FrameAt(end) - Zoom.Grid.FrameAt(b.Start)}{Glue}f";
        return $"{start}{Glue}–{Glue}{TimeFormat.Short(end, fps)}\n{details}";
    }

    private BookmarkRow? ListRowFromEvent(object? source)
    {
        var element = source as DependencyObject;
        while (element != null && element is not ListBoxItem)
            // El texto de la etiqueta y el del nombre son Runs, que no son Visual: su padre es lógico.
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        return (element as ListBoxItem)?.DataContext as BookmarkRow;
    }

    // Clic (o Enter): al primer frame, seleccionado, y su región pasa a ser la zona ampliada.
    private void OnBookmarkListClick(object sender, MouseButtonEventArgs e)
    {
        // Con Ctrl o Shift se están eligiendo varios: no se va a ninguno.
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            return;
        if (ListRowFromEvent(e.OriginalSource) is { } row)
            OpenRow(row);
    }

    private void OnBookmarkListRightClick(object sender, MouseButtonEventArgs e)
    {
        if (ListRowFromEvent(e.OriginalSource) is not { } row)
            return;
        if (BookmarkList.SelectedItems.Count > 1 && BookmarkList.SelectedItems.Contains(row))
            ShowMultiMenu([.. BookmarkList.SelectedItems.OfType<BookmarkRow>()]);
        else if (row.IsCurrentVideo)
            ShowBookmarkMenu(row.Bookmark, BookmarkList);
        else
            ShowMultiMenu([row]);
        e.Handled = true;
    }

    // Menú para varios elegidos en el panel, o para uno de otro vídeo: lo que se puede hacer sin abrir su vídeo. Mismo
    // orden que el de un bookmark: lo que los cambia, lo que hace con el vídeo (con uno solo) y, al final, renombrar y
    // borrar. Ir a uno (también de otro vídeo) es pinchar en él.
    private void ShowMultiMenu(List<BookmarkRow> rows)
    {
        var menu = new ContextMenu { PlacementTarget = BookmarkList, Placement = PlacementMode.MousePoint };
        var videos = rows.Select(r => r.Fingerprint).Distinct().Count();
        menu.Items.Add(new MenuItem
        {
            Header = rows.Count == 1 ? rows[0].Name : videos == 1 ? S.BookmarkCount(rows.Count) : S.BookmarksFromVideos(rows.Count, videos),
            IsEnabled = false,
        });
        var labels = rows.Select(r => _labels.Find(r.Bookmark.Label)?.Id).Distinct().ToList();
        var colors = rows.Select(r => r.Bookmark.Color).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        AddLabelOrColorMenu(menu, labels[0], colors.Count == 1 ? colors[0] : null, mixedLabels: labels.Count > 1,
            id => SetRowsLabel(rows, id), hex => SetRowsColor(rows, hex));
        AddMoveMenu(menu, collection => MoveRows(rows, collection));
        if (rows.Count == 1)
        {
            menu.Items.Add(new Separator());
            var video = rows[0].VideoPath ?? (rows[0].IsCurrentVideo ? _bookmarks?.VideoPath : null);
            var region = rows[0].Bookmark;
            AddExportMenu(menu, video, region.Start, EndOf(region, rows[0].IsCurrentVideo ? null : FpsOf(rows[0].Fingerprint)), region.Name);
            AddRevealItem(menu, video);
        }
        menu.Items.Add(new Separator());
        if (rows.Count == 1)
        {
            var rename = new MenuItem { Header = S.Rename, InputGestureText = "F2" };
            rename.Click += (_, _) => RenameRow(rows[0], fromMenu: true);
            menu.Items.Add(rename);
        }
        var delete = new MenuItem { Header = rows.Count == 1 ? S.Delete : S.DeleteCount(rows.Count), InputGestureText = S.KeyDelete };
        delete.Click += (_, _) => DeleteRows(rows);
        menu.Items.Add(delete);
        menu.IsOpen = true;
    }

    // Abre el Explorador en la carpeta del vídeo con el archivo seleccionado.
    private void AddRevealItem(ItemsControl menu, string? videoPath)
    {
        var exists = videoPath != null && File.Exists(videoPath);
        var reveal = new MenuItem
        {
            Header = exists ? S.ShowVideoInExplorer : S.ShowVideoInExplorerMissing,
            IsEnabled = exists,
        };
        reveal.Click += (_, _) => System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{videoPath}\"");
        menu.Items.Add(reveal);
    }

    private void DeleteRows(List<BookmarkRow> rows)
    {
        if (rows.Count == 0)
            return;
        EditRows(rows, rows.Count == 1 ? S.StepDelete(rows[0].Name) : S.StepDeleteMany(rows.Count), BookmarkFiles.RemoveIds);
        ShowToast(rows.Count == 1 ? S.BookmarkDeleted(rows[0].Name) : S.BookmarksDeleted(rows.Count));
    }

    private void MoveRows(List<BookmarkRow> rows, string collection)
    {
        if (rows.Count == 0 || collection == _store.Settings.BookmarkCollection)
            return;
        EditRows(rows, rows.Count == 1 ? S.StepMoveTo(rows[0].Name, collection) : S.StepMoveManyTo(rows.Count, collection),
            (file, ids) => file.Bookmarks.RemoveAll(b => ids.Contains(b.Id)), moveTo: collection);
        ShowToast(rows.Count == 1 ? S.MovedTo(collection) : S.MovedManyTo(rows.Count, collection));
    }

    // Desde el menú, el cuadro sale junto al ratón; con F2, bajo la fila.
    private void RenameRow(BookmarkRow row, bool fromMenu = false)
    {
        if (row.IsCurrentVideo && _bookmarks?.Find(row.Id) is { } bookmark)
        {
            BeginRename(bookmark, isNew: false);
            return;
        }
        var target = fromMenu ? null : BookmarkList.ItemContainerGenerator.ContainerFromItem(row) as UIElement ?? BookmarkList;
        PromptName(S.RenameBookmark, row.Name, target, name =>
        {
            if (name != row.Name)
                EditRows([row], S.StepRename(row.Name),
                    (file, ids) => { foreach (var b in file.Bookmarks.Where(b => ids.Contains(b.Id))) { b.Name = name; b.Modified = DateTime.Now; } });
        }, detail: row.VideoName);
    }

    /// <summary>
    /// Cambia bookmarks elegidos en el panel, de este vídeo o de otros, archivo por archivo y en un solo paso de
    /// deshacer. Con "moveTo", antes se copian a esa colección.
    /// </summary>
    private void EditRows(List<BookmarkRow> rows, string label, Action<BookmarkFile, IReadOnlyCollection<string>> change, string? moveTo = null)
    {
        var folder = BookmarkCollections.PathOf(_store, _store.Settings.BookmarkCollection);
        var entries = new List<BookmarkHistory.Entry>();
        try
        {
            // Al moverlos, sus etiquetas van con ellos (o pasan a la de allí con el mismo nombre): primero, todas a la vez.
            var moving = new Dictionary<string, List<Bookmark>>();
            if (moveTo != null)
            {
                foreach (var group in rows.GroupBy(r => r.Fingerprint))
                {
                    var ids = group.Select(r => r.Id).ToHashSet();
                    moving[group.Key] = [.. (DataStore.LoadFile<BookmarkFile>(Path.Combine(folder, group.Key + ".json"))?.Bookmarks ?? []).Where(b => ids.Contains(b.Id))];
                }
                if (BookmarkLabels.CopyTo(_store, _store.Settings.BookmarkCollection, moveTo, [.. moving.Values.SelectMany(m => m)]) is { } labels)
                    entries.Add(labels);
            }
            foreach (var group in rows.GroupBy(r => r.Fingerprint))
            {
                var ids = group.Select(r => r.Id).ToHashSet();
                var video = group.First().IsCurrentVideo && _bookmarks != null ? _bookmarks.VideoName : group.First().VideoName;
                var path = Path.Combine(folder, group.Key + ".json");
                if (moveTo != null)
                {
                    var target = Path.Combine(BookmarkCollections.PathOf(_store, moveTo), group.Key + ".json");
                    var (targetBefore, targetAfter) = BookmarkFiles.Modify(target, group.Key, video, f => BookmarkFiles.AddCopies(f, moving[group.Key]));
                    entries.Add(new(target, targetBefore, targetAfter));
                }
                var (before, after) = BookmarkFiles.Modify(path, group.Key, video, f => change(f, ids));
                entries.Add(new(path, before, after));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast(S.ChangeFailed(e.Message));
        }
        var videos = rows.Select(r => r.Fingerprint).Distinct().Count();
        var name = videos == 1 ? (rows[0].IsCurrentVideo && _bookmarks != null ? _bookmarks.VideoName : rows[0].VideoName) : S.VideosCountName(videos);
        _history.Record(label, name, entries);
        AfterFilesChanged(entries.Select(e => e.Path));
    }

    /// <summary>
    /// Los colores de un submenú "Color": los de la paleta y, con "allowOther" (etiquetas), el suyo si es otro
    /// ("Propio") y "Otro color…", que abre el selector. Los elegidos así no se guardan en ninguna lista.
    /// </summary>
    private void AddColorItems(MenuItem color, string? current, Action<string> apply, bool allowOther)
    {
        bool Is(string hex) => string.Equals(current, hex, StringComparison.OrdinalIgnoreCase);
        void Add(string header, string hex)
        {
            var item = new MenuItem { Header = header, Icon = Swatch(hex), IsCheckable = true, IsChecked = Is(hex) };
            item.Click += (_, _) => apply(hex);
            color.Items.Add(item);
        }
        foreach (var (name, hex) in BookmarkColors.Palette)
            Add(name, hex);
        if (!allowOther)
            return;
        color.Items.Add(new Separator());
        if (current != null && ColorPicker.Parse(current) != null && !BookmarkColors.Palette.Any(p => Is(p.Hex)))
            Add(S.OwnColor, current);
        var other = new MenuItem { Header = S.OtherColor };
        other.Click += (_, _) =>
        {
            if (ColorPicker.Pick(this, current ?? BookmarkColors.Default) is { } hex)
                apply(hex);
        };
        color.Items.Add(other);
    }

    // "Mover a la colección ▸": las demás colecciones, y una nueva.
    private void AddMoveMenu(ItemsControl menu, Action<string> moveTo)
    {
        var active = _store.Settings.BookmarkCollection;
        var move = new MenuItem { Header = S.MoveToCollection };
        foreach (var name in BookmarkCollections.List(_store, active).Where(n => n != active))
        {
            var n = name;
            var item = new MenuItem { Header = name };
            item.Click += (_, _) => moveTo(n);
            move.Items.Add(item);
        }
        if (move.Items.Count > 0)
            move.Items.Add(new Separator());
        var create = new MenuItem { Header = S.NewCollection };
        create.Click += (_, _) => PromptName(S.MoveToNewCollectionName, "", null, name =>
        {
            if (BookmarkCollections.Clean(name) is not { } clean || clean == active)
                return;
            BookmarkCollections.Create(_store, clean);
            moveTo(clean);
        });
        move.Items.Add(create);
        menu.Items.Add(move);
    }

    private void MoveToCollection(List<string> ids, string collection)
    {
        if (_bookmarks == null || ids.Count == 0)
            return;
        try
        {
            _bookmarks.MoveTo(ids, collection);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast(S.MoveFailed(e.Message));
            return;
        }
        _otherFiles = null;
        ShowToast(ids.Count == 1 ? S.MovedTo(collection) : S.MovedManyTo(ids.Count, collection));
    }

    private void OnBookmarkListKeyDown(object sender, KeyEventArgs e)
    {
        var row = BookmarkList.SelectedItem as BookmarkRow;
        switch (e.Key)
        {
            case Key.Enter when row != null:
                OpenRow(row);
                break;
            // Desde el primero, ↑ sube al buscador (igual que ↓ desde el buscador baja a la lista).
            case Key.Up when BookmarkList.SelectedIndex <= 0:
                BookmarkSearch.Focus();
                BookmarkSearch.CaretIndex = BookmarkSearch.Text.Length;
                break;
            case Key.F2 when row != null:
                RenameRow(row);
                break;
            case Key.Delete when BookmarkList.SelectedItems.Count > 0:
                DeleteRows([.. BookmarkList.SelectedItems.OfType<BookmarkRow>()]);
                break;
            case Key.Escape:
                ReturnFocusToVideo();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnBookmarkSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when _rows.Count > 0:
                BookmarkList.SelectedIndex = Math.Max(0, BookmarkList.SelectedIndex);
                (BookmarkList.ItemContainerGenerator.ContainerFromIndex(BookmarkList.SelectedIndex) as ListBoxItem)?.Focus();
                break;
            case Key.Enter when _rows.Count > 0:
                OpenRow(BookmarkList.SelectedItem as BookmarkRow ?? _rows[0]);
                break;
            case Key.Escape when BookmarkSearch.Text.Length > 0:
                BookmarkSearch.Clear();
                break;
            case Key.Escape:
                ReturnFocusToVideo();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // Ir al bookmark de una fila (su región pasa a ser la zona); si es de otro vídeo, se abre ese vídeo en su inicio.
    private void OpenRow(BookmarkRow row)
    {
        DismissRowPreview(row);
        PulseMarksButtonIfHidden();
        if (row.IsCurrentVideo)
        {
            if (_bookmarks?.Find(row.Id) is { } bookmark)
                JumpTo(bookmark);
            return;
        }
        var path = row.VideoPath;
        if (path == null || !File.Exists(path))
        {
            path = LocateVideo(row);
            if (path == null)
                return;
        }
        _pendingSelect = row.Id;
        // Como ir a un bookmark del mismo vídeo: si estaba en pausa, sigue en pausa en su primer frame.
        Open(path, row.Bookmark.Start, keepPause: true);
        ShowToast(row.Bookmark.Name);
    }

    /// <summary>
    /// El vídeo de un bookmark no está donde estaba (movido, renombrado, o colección compartida por otra persona):
    /// ofrece buscarlo. Si el archivo elegido es el mismo vídeo (misma huella) se apunta su nueva ruta; si no, avisa
    /// y, si se acepta, sus bookmarks pasan a ese archivo. Devuelve la ruta a abrir, o null.
    /// </summary>
    private string? LocateVideo(BookmarkRow row)
    {
        var name = row.VideoName.Length > 0 ? row.VideoName : Path.GetFileName(row.VideoPath ?? "");
        if (!Dialog.Confirm(this, S.BookmarkVideoNotFound(row.VideoPath ?? name), S.LocateIt))
            return null;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = VideoFilter, Title = S.LocateTitle(name), FileName = name };
        var folder = Path.GetDirectoryName(row.VideoPath ?? "");
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            dialog.InitialDirectory = folder;
        if (dialog.ShowDialog(this) != true)
            return null;
        var chosen = dialog.FileName;
        string key;
        try
        {
            key = Fingerprint.Compute(chosen);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Dialog.Inform(this, S.CannotReadFile(e.Message));
            return null;
        }
        if (key != row.Fingerprint)
        {
            if (!Dialog.Confirm(this,
                    S.NotSameVideo(Path.GetFileName(chosen), name), S.UseItAnyway))
                return null;
            RekeyBookmarks(row.Fingerprint, key, name, Path.GetFileName(chosen));
            return chosen;
        }
        // El mismo vídeo en otro sitio: se recuerda la ruta nueva.
        if (!_store.Library.Videos.TryGetValue(key, out var entry))
            _store.Library.Videos[key] = entry = new VideoEntry();
        entry.Path = Path.GetFullPath(chosen);
        _store.SaveLibrary();
        _otherFiles = null;
        return chosen;
    }

    // Pasa los bookmarks de un vídeo (todas las colecciones) a otro archivo, en un paso de deshacer.
    private void RekeyBookmarks(string from, string to, string fromName, string toName)
    {
        var entries = new List<BookmarkHistory.Entry>();
        try
        {
            foreach (var collection in BookmarkCollections.List(_store, _store.Settings.BookmarkCollection))
            {
                var folder = BookmarkCollections.PathOf(_store, collection);
                var oldPath = Path.Combine(folder, from + ".json");
                if (DataStore.LoadFile<BookmarkFile>(oldPath) is not { } old)
                    continue;
                var newPath = Path.Combine(folder, to + ".json");
                var (before, after) = BookmarkFiles.Modify(newPath, to, toName, f => BookmarkFiles.AddCopies(f, old.Bookmarks));
                entries.Add(new(newPath, before, after));
                var oldText = BookmarkFiles.ReadText(oldPath);
                BookmarkFiles.WriteText(oldPath, null);
                entries.Add(new(oldPath, oldText, null));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast(S.NotAllMoved(e.Message));
        }
        _history.Record(S.StepRelink(fromName, toName), toName, entries);
        _otherFiles = null;
    }

    // Cuando el vídeo abierto ya sabe su duración: el bookmark pendiente (abierto desde el panel) se selecciona y su
    // región pasa a ser la zona.
    private void ApplyPendingBookmark()
    {
        _awaitingDuration = false;
        if (_pendingSelect is { } id)
        {
            _pendingSelect = null;
            if (_bookmarks?.Find(id) != null)
            {
                Select(id);
                return;
            }
        }
        FollowZoneBookmark();
        // Al abrir el bookmark de otro vídeo la fila del zoom se queda abierta esperando su región (Open): si ya no
        // está, se cierra.
        if (_zoomRange == null)
            ZoomRow.Visibility = Visibility.Collapsed;
    }

    // El teclado vuelve al vídeo: flechas, espacio, etc. dejan de ir a la lista o al buscador. Hay que olvidar
    // también el elemento enfocado de la ventana: si no, WPF se lo devuelve en cuanto la ventana recibe el foco.
    private void ReturnFocusToVideo()
    {
        if (Keyboard.FocusedElement == this && FocusManager.GetFocusedElement(this) == null)
            return;
        FocusManager.SetFocusedElement(this, null);
        Keyboard.ClearFocus();
        Keyboard.Focus(this);
    }

    // Un clic en cualquier sitio fuera del panel (barras, botones, controles) devuelve el teclado al vídeo; los
    // clics en la imagen llegan aparte, por la ventana nativa.
    private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && !IsInside(source, BookmarkPanel) && !IsInside(source, NamePopup.Child))
            ReturnFocusToVideo();
    }

    private static bool IsInside(DependencyObject? element, DependencyObject? container)
    {
        for (; element != null && container != null; element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
            if (element == container)
                return true;
        return false;
    }

    private void ShowToast(string text)
    {
        ToastText.Text = text;
        // Centrado y cerca del borde de abajo de la imagen, donde se mira (como los subtítulos); por encima de los
        // controles si están flotando sobre ella.
        var area = new Size(VideoArea.ActualWidth, VideoArea.ActualHeight - (ControlsPopup.IsOpen ? ControlsHost.ActualHeight : 0));
        ToastText.MaxWidth = Math.Max(200, area.Width - 80);
        var toast = (FrameworkElement)ToastPopup.Child;
        toast.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        ToastPopup.HorizontalOffset = Math.Max(0, (area.Width - toast.DesiredSize.Width) / 2);
        ToastPopup.VerticalOffset = Math.Max(0, area.Height - toast.DesiredSize.Height - Math.Min(60, area.Height * 0.08));
        // Si ya se ve, solo cambia el texto y se recoloca: cerrarlo y reabrirlo lo hacía parpadear.
        toast.BeginAnimation(OpacityProperty, null);
        toast.Opacity = 1;
        ToastPopup.IsOpen = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    // Sin esperar: lo tapa otro aviso más importante.
    private void HideToast()
    {
        _toastTimer.Stop();
        ((FrameworkElement)ToastPopup.Child).BeginAnimation(OpacityProperty, null);
        ToastPopup.IsOpen = false;
    }

    // Al acabar el tiempo se desvanece en vez de desaparecer de golpe.
    private void FadeOutToast()
    {
        var toast = (FrameworkElement)ToastPopup.Child;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400));
        fade.Completed += (_, _) =>
        {
            // Si entretanto salió otro aviso, se queda.
            if (!_toastTimer.IsEnabled)
                ToastPopup.IsOpen = false;
        };
        toast.BeginAnimation(OpacityProperty, fade);
    }
}

/// <summary>Una fila del panel de bookmarks.</summary>
public sealed class BookmarkRow(Bookmark bookmark, string fingerprint, string? videoPath, bool isCurrentVideo) : INotifyPropertyChanged
{
    private ImageSource? _thumb;

    public Bookmark Bookmark { get; } = bookmark;
    public string Fingerprint { get; } = fingerprint;
    public string? VideoPath { get; } = videoPath;
    public bool IsCurrentVideo { get; } = isCurrentVideo;
    public string Id => Bookmark.Id;
    public string Name => Bookmark.Name;
    public string ThumbKey => Thumbnails.Key(Fingerprint, Bookmark.ThumbTime);
    /// <summary>El de su etiqueta, o el suyo.</summary>
    public string ColorHex { get; init; } = BookmarkColors.Default;
    public Brush ColorBrush => new SolidColorBrush(ParseColor(ColorHex));
    /// <summary>Nombre de la etiqueta (con espacio detrás) delante del del bookmark; "" sin etiqueta.</summary>
    public string LabelText { get; init; } = "";
    /// <summary>"" si los tiempos van en la vista previa: entonces el nombre puede ocupar más líneas.</summary>
    public string TimeText { get; init; } = "";
    public Visibility TimeVisibility => TimeText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public double NameMaxHeight => TimeText.Length > 0 ? 34 : 68;
    public string VideoName { get; init; } = "";
    public Visibility VideoVisibility => VideoName.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public string KeyText { get; init; } = "";
    public string SearchText { get; init; } = "";

    public ImageSource? Thumb
    {
        get => _thumb;
        set { _thumb = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumb))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch (FormatException) { return (Color)ColorConverter.ConvertFromString(BookmarkColors.Default); }
    }
}
