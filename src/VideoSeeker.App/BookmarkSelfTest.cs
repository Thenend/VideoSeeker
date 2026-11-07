using System.IO;
using System.Windows;
using System.Windows.Input;
using VideoSeeker.App.Controls;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

/// <summary>
/// Prueba automática de los bookmarks (VIDEOSEEKER_SELFTEST=bookmarks): los crea, renombra, convierte en
/// región arrastrando el asa, deshace, salta con las teclas, busca en el panel, exporta y mezcla el archivo
/// compartido, y espera a las miniaturas. Al terminar borra lo que creó, escribe selftest.log y cierra.
/// </summary>
public partial class MainWindow
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, nint extra);

    public static bool BookmarkSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "bookmarks";

    // Los dos botones de bookmarks, a 6 aumentos, para revisar los iconos.
    private void SaveIcons(string path)
    {
        UpdateLayout();
        var visual = new System.Windows.Media.DrawingVisual();
        var (w, h) = (MarksButton.ActualWidth + BookmarksButton.ActualWidth, Math.Max(MarksButton.ActualHeight, BookmarksButton.ActualHeight));
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle((System.Windows.Media.Brush)FindResource("Bg"), null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new System.Windows.Media.VisualBrush(MarksButton), null, new Rect(0, 0, MarksButton.ActualWidth, MarksButton.ActualHeight));
            dc.DrawRectangle(new System.Windows.Media.VisualBrush(BookmarksButton), null, new Rect(MarksButton.ActualWidth, 0, BookmarksButton.ActualWidth, BookmarksButton.ActualHeight));
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(w * 6), (int)(h * 6), 96 * 6, 96 * 6, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private void SaveSnapshot(string path)
    {
        var root = (FrameworkElement)Content;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(root);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX),
            (int)(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    // VIDEOSEEKER_SELFTEST=inicio, sin vídeo: captura de cómo arranca la ventana y cierra.
    private async Task StartupSnapshot()
    {
        await Task.Delay(1500);
        File.WriteAllText(Path.Combine(_store.Folder, "selftest.log"),
            $"panel visible: {BookmarkPanel.IsVisible}, todos: {ScopeAll.IsChecked}, foco en el buscador: {BookmarkSearch.IsKeyboardFocused}, filas: {_rows.Count}");
        SaveSnapshot(Path.Combine(_store.Folder, "selftest-inicio.png"));
        // La barra de desplazamiento del panel, oscura (con la ventana baja para que salga).
        var height = Height;
        Height = 500;
        await Task.Delay(500);
        if (FindScrollViewer(BookmarkList)?.Template.FindName("PART_VerticalScrollBar", FindScrollViewer(BookmarkList)) is FrameworkElement bar && bar.IsVisible)
        {
            var thumb = ((System.Windows.Controls.Primitives.ScrollBar)bar).Track?.Thumb;
            File.AppendAllText(Path.Combine(_store.Folder, "selftest.log"),
                $"{Environment.NewLine}barra: {bar.ActualWidth:0}×{bar.ActualHeight:0}, pulgar {thumb?.ActualWidth:0}×{thumb?.ActualHeight:0}, " +
                $"color {((thumb?.Template.FindName("Bd", thumb) as System.Windows.Controls.Border)?.Background)}, desplazable {FindScrollViewer(BookmarkList)?.ScrollableHeight:0}");
        }
        Height = height;
        await Task.Delay(500);
        // Alto de cada fila (con la línea del vídeo en "Todos los vídeos"): lo marca la miniatura, no el texto.
        var heights = Enumerable.Range(0, _rows.Count)
            .Select(i => BookmarkList.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement)
            .Where(c => c != null).Select(c => Math.Round(c!.ActualHeight, 1)).ToList();
        File.AppendAllText(Path.Combine(_store.Folder, "selftest.log"), $"{Environment.NewLine}altos de las filas: {string.Join(", ", heights)}");
        // Vista previa grande al llegar a una fila con el teclado: a la izquierda de la fila, con su nombre.
        if (_rows.Count > 1)
        {
            BookmarkList.SelectedIndex = 0;
            BookmarkList.UpdateLayout();
            (BookmarkList.ItemContainerGenerator.ContainerFromIndex(0) as System.Windows.Controls.ListBoxItem)?.Focus();
            BookmarkList.SelectedIndex = 1;
            await Task.Delay(500);
            File.AppendAllText(Path.Combine(_store.Folder, "selftest.log"),
                $"{Environment.NewLine}vista previa con el teclado: abierta {HoverPopup.IsOpen}, a la izquierda {HoverPopup.Placement == System.Windows.Controls.Primitives.PlacementMode.Left}, " +
                $"de la fila elegida {HoverName.Text.EndsWith(_rows[1].Name)}");
            // No queda "siempre encima" de las demás ventanas (otras apps, los ajustes).
            var popupHwnd = ((System.Windows.Interop.HwndSource)PresentationSource.FromVisual(HoverPopup.Child)).Handle;
            File.AppendAllText(Path.Combine(_store.Folder, "selftest.log"),
                $"{Environment.NewLine}vista previa no está siempre encima: {(GetWindowLongW(popupHwnd, -20) & 0x8) == 0}");
            // Con el teclado se desvanece a los 3 s sin pulsar nada.
            await Task.Delay(2700); // más los 500 ms de antes: 3,2 s desde la flecha
            File.AppendAllText(Path.Combine(_store.Folder, "selftest.log"),
                $"{Environment.NewLine}a los 3,2 s se está desvaneciendo: {HoverPopup.IsOpen && HoverPopup.Child.Opacity < 1}");
            await Task.Delay(500);
            File.AppendAllText(Path.Combine(_store.Folder, "selftest.log"), $"{Environment.NewLine}y a los 3,7 s se ha quitado: {!HoverPopup.IsOpen}");
            BookmarkList.SelectedIndex = 0;
            await Task.Delay(300);
            // Mover la ventana: la vista previa va con ella.
            var child = (FrameworkElement)HoverPopup.Child;
            var was = child.PointToScreen(new Point(0, 0));
            Left += 60;
            await Task.Delay(400);
            var now = child.PointToScreen(new Point(0, 0));
            Left -= 60;
            File.AppendAllText(Path.Combine(_store.Folder, "selftest.log"),
                $"{Environment.NewLine}la vista previa sigue a la ventana: {Math.Abs(now.X - was.X - 60 * VisualTreeHelperDpi()) < 3} ({was.X:0} → {now.X:0})");
            await Task.Delay(300);
            BookmarkSearch.Focus();
            await Task.Delay(300);
            // ↓ desde el buscador lleva a la primera fila: también con su vista previa.
            BookmarkSearch.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(BookmarkSearch)!, 0, Key.Down) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            await Task.Delay(400);
            File.AppendAllText(Path.Combine(_store.Folder, "selftest.log"),
                $"{Environment.NewLine}↓ desde el buscador: foco en la lista {BookmarkList.IsKeyboardFocusWithin}, vista previa {HoverPopup.IsOpen} de «{HoverName.Text}» (primera «{_rows[0].Name}»)");
            BookmarkSearch.Focus();
            await Task.Delay(300);
            File.AppendAllText(Path.Combine(_store.Folder, "selftest.log"), $"{Environment.NewLine}al salir de la lista se quita: {!HoverPopup.IsOpen}");
        }
        Close();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLongW(nint hwnd, int index);

    private double VisualTreeHelperDpi() => System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;

    // Lo mismo que elegir un color en el menú de varios.
    private void ShowMultiMenuColor(List<BookmarkRow> rows, string hex) =>
        EditRows(rows, "color", (file, ids) => { foreach (var b in file.Bookmarks.Where(b => ids.Contains(b.Id))) b.Color = hex; });

    private async Task RunBookmarkSelfTest()
    {
        var lines = new List<string>();
        var failures = 0;
        void Log(string text) => lines.Add($"{DateTime.Now:HH:mm:ss.fff} {text}");
        void Check(bool ok, string what)
        {
            Log((ok ? "OK    " : "FALLO ") + what);
            if (!ok)
                failures++;
        }

        while (_bookmarks == null || _duration <= 0)
            await Task.Delay(100);
        var collection = _store.Settings.BookmarkCollection;
        var file = Path.Combine(BookmarkCollections.PathOf(_store, collection), _bookmarks.Fingerprint + ".json");
        // Si el vídeo ya tenía bookmarks, se dejan como estaban al terminar.
        var saved = File.Exists(file) ? File.ReadAllText(file) : null;
        try
        {
            await Task.Delay(1500);
            var set = _bookmarks;
            var sidecar = BookmarkSet.SidecarPath(set.VideoPath);
            var before = set.Items.Count;
            var grid = Zoom.Grid;
            var t0 = grid.Snap(_duration * 0.3);

            // B: bookmark nuevo con el campo del nombre abierto y enfocado.
            _nav.ScrubTo(t0);
            await Task.Delay(400);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Con la tecla de verdad si la ventana está delante (para ver que la "b" no acaba escrita en el nombre).
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (GetForegroundWindow() == hwnd)
            {
                keybd_event(0x42, 0, 0, 0);
                keybd_event(0x42, 0, 2, 0);
                Log("tecla B enviada");
            }
            else
            {
                Log("(la ventana no está delante: B por código)");
                AddBookmarkAt(_nav.CurrentTime);
            }
            await Task.Delay(300);
            Check(NamePopup.IsOpen && NameBox.IsKeyboardFocused, $"B abre el nombre con el foco en el cuadro ({sw.ElapsedMilliseconds} ms)");
            Check(NameBox.Text == $"Bookmark {set.Items.Count}" && NameBox.SelectionLength == NameBox.Text.Length,
                $"nombre por defecto seleccionado: «{NameBox.Text}»");
            NameBox.Text = "Jefe final ÁÉ";
            CloseNaming(commit: true);
            var point = set.Items.FirstOrDefault(b => b.Name == "Jefe final ÁÉ");
            Check(point != null && Math.Abs(point.Start - grid.Snap(t0)) < 1e-3, $"creado y renombrado en su frame ({point?.Start:0.000} s)");
            Check(point != null && Math.Abs(EndOf(point) - NextFrameStart(point.Start)) < 1e-3, $"sin zona, B crea una región de un frame (fin {point?.End:0.000} s)");
            Check(!NamePopup.IsOpen && IsKeyboardFocusWithin && Keyboard.FocusedElement is not System.Windows.Controls.TextBox, "el foco vuelve al vídeo");
            Check(File.Exists(file), "guardado en data\\bookmarks");

            // Dos veces B en el mismo frame no duplica.
            AddBookmarkAt(_nav.CurrentTime);
            await Task.Delay(100);
            CloseNaming(commit: false);
            Check(set.Items.Count == before + 1, "B en el mismo frame no duplica");

            // B con una zona libre: región con ella; Esc deja el nombre por defecto.
            SetFreeZoom((t0 + 2, t0 + 4));
            AddBookmark();
            await Task.Delay(100);
            CloseNaming(commit: false);
            var region = set.Items.FirstOrDefault(b => Math.Abs(b.Start - (t0 + 2)) < 1e-3);
            Check(region != null && Math.Abs(EndOf(region) - (t0 + 4)) < 1e-3, "B con la zona libre crea la región de la zona");
            CloseZone();

            // Arrastrar el fin de la región de un frame la estira; deshacer la devuelve.
            Select(point!.Id);
            OnBookmarkPressed(point, BookmarkPart.End);
            OnBookmarkDragged(point, BookmarkPart.End, point.Start + 0.5);
            OnBookmarkDragged(point, BookmarkPart.End, point.Start + 1.01);
            OnBookmarkReleased(point, moved: true);
            point = set.Find(point.Id)!;
            Check(Math.Abs(EndOf(point) - grid.Snap(point.Start + 1.01 + 0.5 / Fps)) < 1e-3,
                $"arrastrar el fin la estira ({point.Start:0.000}–{point.End:0.000})");
            UndoBookmarks(redo: false);
            point = set.Find(point.Id)!;
            Check(Math.Abs(EndOf(point) - NextFrameStart(point.Start)) < 1e-3, "Ctrl + Z deshace el arrastre entero de una vez (vuelve a un frame)");
            UndoBookmarks(redo: true);
            Check(EndOf(set.Find(point.Id)!) > point.Start + 0.5, "Ctrl + Y lo rehace");
            UndoBookmarks(redo: false);

            // Arrastrar el inicio hacia atrás la alarga (el fin se queda).
            var endBefore = EndOf(point);
            OnBookmarkDragged(point, BookmarkPart.Start, point.Start - 1);
            OnBookmarkReleased(point, moved: true);
            point = set.Find(point.Id)!;
            Check(Math.Abs(point.Start - grid.Snap(t0 - 1)) < 1e-3 && Math.Abs(EndOf(point) - endBefore) < 1e-3, "arrastrar el inicio lo lleva a otro frame");
            await Task.Delay(300);

            // Teclas: 1 y 2 por orden de inicio; ] y [.
            JumpToNumber(1);
            await Task.Delay(500);
            Check(Math.Abs(_nav.CurrentTime - set.Items[0].Start) < 0.5 / Fps + 1e-3, $"1 va al primero ({_nav.CurrentTime:0.000})");
            JumpAdjacent(1);
            await Task.Delay(500);
            Check(Math.Abs(_nav.CurrentTime - set.Items[Math.Min(1, set.Items.Count - 1)].Start) < 0.5 / Fps + 1e-3, "] va al siguiente");
            Check(_zoomRange is { } zr && set.Find(_selectedBookmark) is { } jumped && Math.Abs(zr.Start - jumped.Start) < 1e-3 && Math.Abs(zr.End - EndOf(jumped)) < 1e-3,
                "ir a una región la hace la zona ampliada");
            Check(JumpToSelected() && _zoomRange != null && _beforeFullscreen == null, "Enter con una región seleccionada va a ella (no cambia a pantalla completa)");
            JumpAdjacent(-1);
            await Task.Delay(500);
            Check(Math.Abs(_nav.CurrentTime - set.Items[0].Start) < 0.5 / Fps + 1e-3, "[ vuelve al anterior");
            Check(BookmarkText.Text.Contains("Jefe final"), $"texto bajo el vídeo: «{BookmarkText.Text}»");

            // Ojo: ocultar y volver a mostrar las marcas.
            var shown = _store.Settings.ShowBookmarkMarks;
            if (!shown)
                ToggleMarks();
            ToggleMarks();
            Check(!Seek.ShowBookmarks && !Zoom.ShowBookmarks && BookmarkText.Visibility != Visibility.Visible, "el ojo oculta las marcas");
            SaveIcons(Path.Combine(_store.Folder, "selftest-iconos-ocultas.png"));
            ToggleMarks();
            Check(Seek.ShowBookmarks && Zoom.ShowBookmarks, "y las vuelve a mostrar");
            await Task.Delay(50);
            SaveIcons(Path.Combine(_store.Folder, "selftest-iconos.png"));
            if (!shown)
                ToggleMarks();

            // Panel: Ctrl + B lo abre con el buscador enfocado; búsqueda sin tildes ni mayúsculas.
            TogglePanel();
            await Task.Delay(100);
            Check(BookmarkPanel.IsVisible && BookmarkSearch.IsKeyboardFocused, "Ctrl + B abre el panel con el foco en el buscador");
            ReturnFocusToVideo();
            TogglePanel();
            Check(BookmarkPanel.IsVisible && BookmarkSearch.IsKeyboardFocused, "Ctrl + B con el panel abierto pero sin teclado le da el foco");
            TogglePanel();
            Check(!BookmarkPanel.IsVisible, "y otro Ctrl + B lo cierra");
            TogglePanel();
            OnPanelKeyDown(BookmarkPanel, new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, Key.Tab) { RoutedEvent = PreviewKeyDownEvent });
            Check(ScopeAll.IsChecked == true && BookmarkSearch.IsKeyboardFocused, "Tab en el panel pasa a «todos los vídeos» sin perder el foco");
            OnPanelKeyDown(BookmarkPanel, new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, Key.Tab) { RoutedEvent = PreviewKeyDownEvent });
            Check(ScopeThis.IsChecked == true, "y otro Tab vuelve a «este vídeo»");
            BookmarkSearch.Text = "jefe FINAL ae";
            await Task.Delay(100);
            Check(_rows.Count == 1 && _rows[0].Id == point.Id, $"buscar «jefe FINAL ae» encuentra 1 ({_rows.Count})");
            ScopeAll.IsChecked = true;
            await Task.Delay(100);
            Check(_rows.Count >= 1, $"«todos los vídeos» ({_rows.Count})");
            BookmarkSearch.Text = "";
            // Clic en el panel: va, la selecciona y es la zona ampliada.
            if (_rows.FirstOrDefault(r => r.IsCurrentVideo) is { } regionRow)
            {
                CloseZone();
                OpenRow(regionRow);
                Check(_selectedBookmark == regionRow.Id && _zoomRange is { } rz && Math.Abs(rz.Start - regionRow.Bookmark.Start) < 1e-3,
                    "clic en una región del panel la selecciona y la amplía");
                CloseZone();
            }
            // En «todos», por nombre de vídeo con el abierto en su sitio (no el primero).
            var folder = BookmarkCollections.PathOf(_store, collection);
            DataStore.SaveFile(Path.Combine(folder, "0000000000000001.json"), new BookmarkFile
                { Video = "aaa antes.mp4", Fingerprint = "0000000000000001", Bookmarks = [new Bookmark { Id = "x1", Name = "otro antes", Start = 1 }] });
            DataStore.SaveFile(Path.Combine(folder, "0000000000000002.json"), new BookmarkFile
                { Video = "zzz después.mp4", Fingerprint = "0000000000000002", Bookmarks = [new Bookmark { Id = "x2", Name = "otro después", Start = 1 }] });
            _otherFiles = null;
            RefreshRows();
            // Puede haber bookmarks de otros vídeos de verdad: se mira el orden relativo, no las posiciones.
            var x1 = _rows.ToList().FindIndex(r => r.Id == "x1");
            var x2 = _rows.ToList().FindIndex(r => r.Id == "x2");
            var here = _rows.ToList().FindIndex(r => r.IsCurrentVideo);
            var hereLast = _rows.ToList().FindLastIndex(r => r.IsCurrentVideo);
            Check(x1 >= 0 && x2 >= 0 && here > x1 && hereLast < x2,
                $"«todos» va por nombre de vídeo, el abierto en su sitio: {string.Join(", ", _rows.Select(r => r.Name))}");
            foreach (var f in new[] { "0000000000000001.json", "0000000000000002.json" })
            {
                File.Delete(Path.Combine(folder, f));
                File.Delete(Path.Combine(folder, f + ".bak"));
            }
            _otherFiles = null;
            ScopeThis.IsChecked = true;
            // Tras usar la lista, un clic fuera del panel devuelve las flechas al vídeo, y no vuelven a la lista
            // al reactivar la ventana.
            await Task.Delay(100);
            (BookmarkList.ItemContainerGenerator.ContainerFromIndex(0) as System.Windows.Controls.ListBoxItem)?.Focus();
            Check(BookmarkList.IsKeyboardFocusWithin, "la lista tiene el foco tras pinchar en ella");
            await Task.Delay(100);
            SaveSnapshot(Path.Combine(_store.Folder, "selftest-lista.png"));
            BookmarkList.SelectedIndex = 0;
            OnBookmarkListKeyDown(BookmarkList, new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, Key.Up) { RoutedEvent = PreviewKeyDownEvent });
            Check(BookmarkSearch.IsKeyboardFocused, "↑ desde el primero sube al buscador");
            (BookmarkList.ItemContainerGenerator.ContainerFromIndex(0) as System.Windows.Controls.ListBoxItem)?.Focus();
            Seek.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = PreviewMouseDownEvent });
            Check(!BookmarkList.IsKeyboardFocusWithin && Keyboard.FocusedElement == this, "un clic en la barra devuelve el teclado al vídeo");
            Keyboard.ClearFocus();
            Focus();
            Check(!BookmarkList.IsKeyboardFocusWithin, "y no vuelve a la lista al recuperar el foco la ventana");
            (BookmarkList.ItemContainerGenerator.ContainerFromIndex(0) as System.Windows.Controls.ListBoxItem)?.Focus();
            ReturnFocusToVideo(); // lo que hace un clic en la imagen
            Check(!BookmarkList.IsKeyboardFocusWithin, "un clic en la imagen también");
            ShowPanel(false);

            // Miniaturas.
            var waited = 0;
            while (set.Items.Any(b => _thumbs.Get(set.Fingerprint, set.VideoPath, b.Start) == null) && waited < 15000)
            {
                await Task.Delay(250);
                waited += 250;
            }
            Check(set.Items.All(b => _thumbs.Get(set.Fingerprint, null, b.Start) != null), $"miniaturas listas ({waited} ms)");

            // Miniatura elegida a mano: el frame actual en vez del primero.
            var withThumb = set.Items[0];
            var thumbAt = grid.Snap(withThumb.Start + 2);
            set.Update(withThumb.Id, b => b.Thumb = thumbAt);
            waited = 0;
            while (_thumbs.Get(set.Fingerprint, set.VideoPath, thumbAt) == null && waited < 15000)
            {
                await Task.Delay(250);
                waited += 250;
            }
            Check(_thumbs.Get(set.Fingerprint, null, thumbAt) != null && set.Find(withThumb.Id)!.ThumbTime == thumbAt,
                $"miniatura con el frame elegido ({waited} ms)");

            // Captura de la interfaz WPF (sin la imagen del vídeo, que es una ventana nativa) para revisar el dibujo.
            ShowPanel(true);
            Select(set.Items[0].Id);
            await Task.Delay(500);
            SaveSnapshot(Path.Combine(_store.Folder, "selftest.png"));
            ShowPanel(false);
            CloseZone();

            // Exportar y mezclar: un bookmark escrito a mano sin id entra una sola vez.
            var hadSidecar = File.Exists(sidecar);
            if (!hadSidecar)
            {
                ExportBookmarks();
                Check(File.Exists(sidecar), "exportar escribe el archivo junto al vídeo");
                var shared = DataStore.LoadFile<BookmarkFile>(sidecar)!;
                shared.Bookmarks.Add(new Bookmark { Name = "Escrito a mano", Start = 1.5 });
                DataStore.SaveFile(sidecar, shared);
                LoadBookmarks(set.Fingerprint, set.VideoPath);
                LoadBookmarks(set.Fingerprint, set.VideoPath);
                Check(_bookmarks!.Items.Count(b => b.Name == "Escrito a mano") == 1, "el archivo compartido se mezcla sin duplicar");
                var manual = _bookmarks.Items.First(b => b.Name == "Escrito a mano");
                _bookmarks.Remove(manual.Id);
                LoadBookmarks(set.Fingerprint, set.VideoPath);
                Check(_bookmarks!.Items.All(b => b.Name != "Escrito a mano"), "lo borrado aquí no vuelve al mezclar");
                File.Delete(sidecar);
                File.Delete(sidecar + ".bak");
            }
            else
            {
                Log("(ya había un archivo junto al vídeo: no se prueba exportar)");
            }

            // Colecciones: una nueva empieza vacía para este vídeo y al volver están los de antes.
            var countHere = _bookmarks!.Items.Count;
            const string testCollection = "Prueba de la autoprueba";
            BookmarkCollections.Create(_store, testCollection);
            // Mover dos a otra colección y deshacer; cambiar el color de varios a la vez.
            var two = _bookmarks.Items.Take(2).Select(b => b.Id).ToList();
            var otherFile = Path.Combine(BookmarkCollections.PathOf(_store, testCollection), _bookmarks.Fingerprint + ".json");
            MoveToCollection(two, testCollection);
            Check(_bookmarks.Items.Count == countHere - 2 && DataStore.LoadFile<BookmarkFile>(otherFile)?.Bookmarks.Count == 2,
                "mover dos a otra colección los pasa allí");
            UndoBookmarks(redo: false);
            Check(_bookmarks.Items.Count == countHere && (DataStore.LoadFile<BookmarkFile>(otherFile)?.Bookmarks.Count ?? 0) == 0,
                "Ctrl + Z los devuelve y los quita de allí");
            List<BookmarkRow> RowsOf(IEnumerable<string> ids) =>
                [.. ids.Select(id => MakeRow(_bookmarks!.Find(id)!, _bookmarks.Fingerprint, _bookmarks.VideoPath, null, "", current: true))];
            ShowMultiMenuColor(RowsOf(two), "#2EC8D6");
            Check(two.All(id => _bookmarks.Find(id)!.Color == "#2EC8D6"), "color de varios a la vez");
            UndoBookmarks(redo: false);
            Check(two.All(id => _bookmarks.Find(id)!.Color != "#2EC8D6"), "y se deshace en un paso");

            // Desde «todos»: borrar a la vez uno de este vídeo y uno de otro, y deshacerlo con un solo Ctrl + Z.
            var otherPath = Path.Combine(BookmarkCollections.PathOf(_store, collection), "0000000000000003.json");
            DataStore.SaveFile(otherPath, new BookmarkFile
                { Video = "otro.mp4", Fingerprint = "0000000000000003", Bookmarks = [new Bookmark { Id = "y1", Name = "de otro vídeo", Start = 1 }] });
            var otherRow = new BookmarkRow(DataStore.LoadFile<BookmarkFile>(otherPath)!.Bookmarks[0], "0000000000000003", null, false) { VideoName = "otro.mp4" };
            var mixed = RowsOf(two.Take(1)).Append(otherRow).ToList();
            DeleteRows(mixed);
            Check(_bookmarks.Items.Count == countHere - 1 && DataStore.LoadFile<BookmarkFile>(otherPath)!.Bookmarks.Count == 0,
                "borrar uno de este vídeo y otro de otro vídeo a la vez");
            UndoBookmarks(redo: false);
            Check(_bookmarks.Items.Count == countHere && DataStore.LoadFile<BookmarkFile>(otherPath)!.Bookmarks.Count == 1,
                "un Ctrl + Z recupera los dos");
            MoveRows([otherRow], testCollection);
            Check(DataStore.LoadFile<BookmarkFile>(Path.Combine(BookmarkCollections.PathOf(_store, testCollection), "0000000000000003.json"))?.Bookmarks.Count == 1
                && DataStore.LoadFile<BookmarkFile>(otherPath)!.Bookmarks.Count == 0, "mover uno de otro vídeo a otra colección");
            UndoBookmarks(redo: false);
            Check(DataStore.LoadFile<BookmarkFile>(otherPath)!.Bookmarks.Count == 1, "y deshacerlo");
            File.Delete(otherPath);
            File.Delete(otherPath + ".bak");

            // Vídeo encontrado en otro archivo distinto: sus bookmarks pasan a la huella nueva, y Ctrl + Z los devuelve.
            var lostPath = Path.Combine(BookmarkCollections.PathOf(_store, collection), "0000000000000004.json");
            var foundPath = Path.Combine(BookmarkCollections.PathOf(_store, collection), "0000000000000005.json");
            DataStore.SaveFile(lostPath, new BookmarkFile
                { Video = "perdido.mp4", Fingerprint = "0000000000000004", Bookmarks = [new Bookmark { Id = "z1", Name = "del perdido", Start = 1 }] });
            RekeyBookmarks("0000000000000004", "0000000000000005", "perdido.mp4", "encontrado.mp4");
            Check(!File.Exists(lostPath) && DataStore.LoadFile<BookmarkFile>(foundPath)?.Bookmarks.Single().Id == "z1",
                "buscar un vídeo distinto le pasa los bookmarks");
            UndoBookmarks(redo: false);
            Check(DataStore.LoadFile<BookmarkFile>(lostPath)?.Bookmarks.Count == 1 && !File.Exists(foundPath), "y Ctrl + Z lo deshace");
            foreach (var f in new[] { lostPath, foundPath })
            {
                File.Delete(f);
                File.Delete(f + ".bak");
            }
            SetCollection(testCollection);
            Check(_bookmarks!.Items.Count == 0 && Seek.Bookmarks.Count == 0, "una colección nueva empieza sin bookmarks");
            AddBookmarkAt(_nav.CurrentTime);
            CloseNaming(commit: false);
            Check(_bookmarks.Items.Count == 1 && File.Exists(Path.Combine(BookmarkCollections.PathOf(_store, testCollection), _bookmarks.Fingerprint + ".json")),
                "sus bookmarks se guardan en su carpeta");
            await LabelSelfTest(Check);
            BookmarkCollections.Rename(_store, testCollection, testCollection + " 2");
            SetCollection(testCollection + " 2", announce: false);
            Check(_bookmarks.Items.Count == 1, "renombrarla conserva sus bookmarks");
            ScopeThis.IsChecked = true;
            SetCollection(collection, showAll: true);
            Check(ScopeAll.IsChecked == true, "cambiar de colección en el menú pasa a «todos los vídeos»");
            ScopeThis.IsChecked = true;
            Check(_bookmarks!.Items.Count == countHere, $"al volver a «{collection}» están los de antes ({countHere})");
            BookmarkCollections.Delete(_store, testCollection + " 2");
            Check(!BookmarkCollections.Exists(_store, testCollection + " 2"), "borrar una colección quita su carpeta");

            // Borrar.
            set = _bookmarks!;
            var count = set.Items.Count;
            Select(set.Items[0].Id);
            DeleteSelected();
            Check(set.Items.Count == count - 1, "Supr borra el seleccionado");
            UndoBookmarks(redo: false);
            Check(set.Items.Count == count, "Ctrl + Z lo recupera");
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            // Deja el vídeo como estaba.
            if (_store.Settings.BookmarkCollection != collection)
                SetCollection(collection, announce: false);
            File.Delete(file + ".bak");
            if (saved != null)
                File.WriteAllText(file, saved);
            else
                File.Delete(file);
            Log(failures == 0 ? "TODO BIEN" : $"{failures} FALLOS");
            File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
            Close();
        }
    }

    // Etiquetas, en la colección de prueba (con un bookmark): crear, poner, la última se repite, filtrar, buscar, cambiar
    // su color, borrarla y deshacerlo, y moverla con un bookmark a otra colección.
    private async Task LabelSelfTest(Action<bool, string> check)
    {
        var set = _bookmarks!;
        var first = set.Items[0];
        // Desde un menú (sin elemento al que anclarlo), el cuadro sale bajo el ratón.
        var mouse = Mouse.GetPosition((UIElement)Content);
        PromptNewLabel(null, null);
        var at = NamePopup.PlacementRectangle;
        check(ReferenceEquals(NamePopup.PlacementTarget, Content) && !at.IsEmpty && (at.Location - mouse).Length < 20,
            $"«Nueva etiqueta…» desde un menú abre el cuadro junto al ratón ({(at.IsEmpty ? "sin punto" : $"{at.X:0},{at.Y:0}")}; ratón {mouse.X:0},{mouse.Y:0}; " +
            $"ancla {NamePopup.PlacementTarget?.GetType().Name})");
        NameBox.Text = "Boss";
        CloseNaming(commit: true);
        PromptNewLabel(CollectionButton, null);
        NameBox.Text = "Puzzle";
        CloseNaming(commit: true);
        check(_labels.Labels.Select(l => l.Name).SequenceEqual(["Boss", "Puzzle"]) && _labels.Labels[0].Color != _labels.Labels[1].Color
            && File.Exists(LabelsPath), "crear dos etiquetas, cada una con su color");
        var boss = _labels.Labels[0];
        var puzzle = _labels.Labels[1];
        PromptNewLabel(CollectionButton, null);
        NameBox.Text = "boss";
        CloseNaming(commit: true);
        check(_labels.Labels.Count == 2, "no se repite un nombre");
        SetBookmarkLabel(first, boss.Id);
        check(set.Find(first.Id)!.Label == boss.Id && ColorOf(set.Find(first.Id)!) == boss.Color, "poner una etiqueta: se pinta con su color");

        // B: el nuevo lleva la última etiqueta; en el cuadro se elige otra.
        _nav.SeekTo(Math.Min(_duration - 1, first.Start + 3));
        await Task.Delay(300);
        var repeat = _store.Settings.RepeatLastLabel;
        _store.Settings.RepeatLastLabel = true;
        AddBookmarkAt(_nav.CurrentTime);
        var added = set.Find(_selectedBookmark)!;
        check(added.Label == boss.Id && NameLabels.Visibility == Visibility.Visible && NameLabels.Children.Count == 3
                && NameColors.Children.Count == BookmarkColors.Palette.Length,
            "un bookmark nuevo lleva la última etiqueta; el cuadro enseña las etiquetas, «+ Nueva» y los colores");
        SaveElementSnapshot((FrameworkElement)NamePopup.Child, Path.Combine(_store.Folder, "selftest-nombre.png"));
        ChooseNamingLabel(puzzle.Id);
        ChooseNamingLabel(puzzle.Id);
        check(_namingLabel == puzzle.Id, "volver a pinchar la etiqueta marcada no la quita");
        NameBox.Text = "Sala de los espejos";
        CloseNaming(commit: true);
        check(set.Find(added.Id) is { Label: var l, Name: "Sala de los espejos" } && l == puzzle.Id, "elegir otra en el cuadro del nombre");
        UndoBookmarks(redo: false);
        check(set.Find(added.Id) == null, "Ctrl + Z quita el nuevo con su etiqueta de una vez");
        UndoBookmarks(redo: true);

        // Menú «Etiqueta o color»: etiquetas y colores en un solo submenú (captura en selftest-menu-etiqueta.png).
        var probe = new System.Windows.Controls.ContextMenu();
        AddLabelOrColorMenu(probe, boss.Id, first.Color, mixedLabels: false, _ => { }, _ => { });
        var submenu = (System.Windows.Controls.MenuItem)probe.Items[0];
        var entries = submenu.Items.OfType<System.Windows.Controls.MenuItem>().ToList();
        check((string)submenu.Header == "Etiqueta o color" && entries.Count == 2 + BookmarkColors.Palette.Length + 1
                && entries[0].IsChecked && entries.Skip(1).All(e => !e.IsChecked),
            $"«Etiqueta o color»: {entries.Count} opciones, la etiqueta puesta marcada");
        var shown = new System.Windows.Controls.ContextMenu { PlacementTarget = Seek };
        foreach (var entry in submenu.Items.Cast<object>().ToList())
        {
            submenu.Items.Remove(entry);
            shown.Items.Add(entry);
        }
        shown.IsOpen = true;
        await Task.Delay(300);
        SaveElementSnapshot(shown, Path.Combine(_store.Folder, "selftest-menu-etiqueta.png"));
        shown.IsOpen = false;

        // Con etiqueta puesta, elegir un color se la quita y pone ese color, en un paso de deshacer.
        var withLabel = set.Find(first.Id)!;
        SetBookmarkColor(withLabel, "#2EC8D6");
        check(set.Find(first.Id) is { Label: null, Color: "#2EC8D6" }, "con etiqueta, elegir un color la quita y pone ese color");
        UndoBookmarks(redo: false);
        check(set.Find(first.Id)!.Label == boss.Id, "y un Ctrl + Z lo deshace");
        // Lo último que se eligió fue ese color: el nuevo va sin etiqueta y con él.
        _nav.SeekTo(Math.Min(_duration - 1, first.Start + 5));
        await Task.Delay(300);
        AddBookmarkAt(_nav.CurrentTime);
        var colored = set.Find(_selectedBookmark)!;
        check(colored.Label == null && colored.Color == "#2EC8D6", $"el nuevo repite lo último elegido: un color suelto ({colored.Label ?? "sin etiqueta"}, {colored.Color})");
        // Crear una etiqueta desde el cuadro del nombre: queda puesta y pasa a ser lo que llevan los nuevos.
        ShowNewLabelRow();
        check(NewLabelRow.Visibility == Visibility.Visible, "«+ Nueva» abre el campo en el mismo cuadro");
        NewLabelBox.Text = "Jefe";
        CreateNamingLabel(NewLabelBox.Text);
        var jefe = _labels.Labels.FirstOrDefault(x => x.Name == "Jefe");
        check(jefe != null && _namingLabel == jefe.Id && NewLabelRow.Visibility != Visibility.Visible && NamePopup.IsOpen,
            "Enter la crea, la elige y vuelve al nombre");
        NameBox.Text = "Con etiqueta nueva";
        CloseNaming(commit: true);
        check(set.Find(colored.Id) is { Name: "Con etiqueta nueva" } c && c.Label == jefe?.Id, "al guardar, el bookmark lleva la etiqueta nueva");
        check(NewBookmarkLabel() == jefe?.Id, "y los nuevos la repiten");
        // Un color en el cuadro: sin etiqueta y con ese color.
        _nav.SeekTo(Math.Min(_duration - 1, first.Start + 6));
        await Task.Delay(300);
        AddBookmarkAt(_nav.CurrentTime);
        var lime = set.Find(_selectedBookmark)!;
        ChooseNamingColor("#A8D84E");
        CloseNaming(commit: true);
        check(set.Find(lime.Id) is { Label: null, Color: "#A8D84E" }, "un color en el cuadro del nombre: sin etiqueta y con ese color");
        set.Remove(lime.Id);
        set.Remove(colored.Id);
        _store.Settings.RepeatLastLabel = repeat;

        // Panel: filtrar y buscar.
        ShowPanel(true);
        ScopeThis.IsChecked = true;
        ToggleLabelFilter(boss.Id);
        check(_rows.Count == 1 && _rows[0].Id == first.Id && _rows[0].LabelText.StartsWith("Boss"), $"filtrar por «Boss» ({_rows.Count})");
        ToggleLabelFilter(puzzle.Id);
        check(_rows.Count == 2, "sumar «Puzzle» al filtro");
        ToggleLabelFilter(boss.Id);
        ToggleLabelFilter(puzzle.Id);
        BookmarkSearch.Text = "puzzle";
        check(_rows.Count == 1 && _rows[0].Id == added.Id, "el buscador encuentra por etiqueta");
        BookmarkSearch.Text = "";

        // Cambiar su color cambia el de sus bookmarks; borrarla los deja con ese color, y Ctrl + Z la devuelve.
        ChangeLabels("color", f => f.Find(boss.Id)!.Color = "#8B8D98");
        check(ColorOf(set.Find(first.Id)!) == "#8B8D98" && _rows.First(r => r.Id == first.Id).ColorHex == "#8B8D98", "cambiar el color de la etiqueta");
        EditCollection("borrar", b => b.Label == boss.Id, b => { b.Label = null; b.Color = "#8B8D98"; }, f => f.Labels.RemoveAll(x => x.Id == boss.Id));
        check(_labels.Find(boss.Id) == null && set.Find(first.Id)!.Label == null && ColorOf(set.Find(first.Id)!) == "#8B8D98", "borrar la etiqueta deja su color");
        UndoBookmarks(redo: false);
        check(_labels.Find(boss.Id) != null && set.Find(first.Id)!.Label == boss.Id, "y Ctrl + Z la devuelve con sus bookmarks");

        // Moverlo a otra colección se lleva su etiqueta.
        const string other = "Prueba de etiquetas";
        BookmarkCollections.Create(_store, other);
        MoveToCollection([first.Id], other);
        var moved = DataStore.LoadFile<BookmarkFile>(Path.Combine(BookmarkCollections.PathOf(_store, other), set.Fingerprint + ".json"))?.Bookmarks.SingleOrDefault();
        check(moved?.Label == boss.Id && BookmarkLabels.Load(_store, other).Find(boss.Id)?.Name == "Boss", "mover a otra colección se lleva la etiqueta");
        UndoBookmarks(redo: false);
        check(set.Find(first.Id)?.Label == boss.Id && BookmarkLabels.Load(_store, other).Labels.Count == 0, "y Ctrl + Z lo devuelve");
        BookmarkCollections.Delete(_store, other);
        set.Remove(added.Id);
        ShowPanel(false);
    }
}
