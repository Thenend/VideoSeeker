using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

// Historial de vídeos abiertos, como el de una pestaña del navegador: ‹ › junto a «Abrir vídeo», botones laterales
// del ratón y teclas de navegador (Alt + ← / → es el salto largo desde el 12 de noviembre). Cada entrada recuerda dónde se dejó el vídeo para volver justo ahí. Dura lo que la sesión
// o, con el ajuste «Guardar el historial de vídeos», sigue en history.json al volver a abrir la app. Las flechas descartan
// lo de «adelante» al abrir otro, como el navegador; el clic derecho en ellas lista todos los recientes (también esos,
// para no perderlos: Mario, 19 de noviembre) y permite limpiar el historial.
public partial class MainWindow
{
    private sealed class HistoryEntry(string path, double position = 0)
    {
        public string Path { get; } = path;
        public double Position { get; set; } = position;
    }

    private const int HistoryLimit = 50;
    private const string HistoryFile = "history.json";
    private readonly List<HistoryEntry> _videoHistory = [];
    // Los vídeos recientes, el último abierto primero y sin repetir, con dónde se dejó cada uno.
    private readonly List<HistoryEntry> _recentVideos = [];
    // Limpiado antes de leer el de la vez anterior: ya no se añade.
    private bool _historyCleared;
    // El vídeo abierto en el historial; -1 sin historial. Recién arrancado con el de la vez anterior y sin vídeo abierto,
    // uno más que el último: «atrás» lleva al que se tenía abierto al cerrar.
    private int _videoHistoryIndex = -1;
    // Hasta haber leído el de la vez anterior no se guarda (se perdería).
    private bool _historyRestored;
    private bool? _appliedKeepHistory;

    private HistoryEntry? CurrentHistory =>
        _videoHistoryIndex >= 0 && _videoHistoryIndex < _videoHistory.Count ? _videoHistory[_videoHistoryIndex] : null;

    private void InitHistory()
    {
        PreviewMouseDown += OnHistoryMouseButton;
        // Que el soltar tampoco llegue a Windows, que lo convertiría en otro «atrás».
        PreviewMouseUp += (_, e) => e.Handled |= e.ChangedButton is MouseButton.XButton1 or MouseButton.XButton2;
        _surface.SideButton += forward => GoHistory(forward ? 1 : -1);
        UpdateHistoryUi();
        RestoreHistory();
    }

    /// <summary>
    /// El historial de la vez anterior, sin los vídeos que ya no están (mirarlo puede tardar con unidades de red, así
    /// que va aparte); si mientras tanto se abrió uno, van antes de él.
    /// </summary>
    private async void RestoreHistory()
    {
        try
        {
            if (!_store.Settings.KeepVideoHistory)
                return;
            var (saved, recent) = await Task.Run(() =>
            {
                var file = _store.Load<VideoHistoryFile>(HistoryFile);
                var exists = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                List<HistoryEntry> Existing(IEnumerable<VideoHistoryItem>? items) => (items ?? [])
                    .Where(v => v.Path is { Length: > 0 } path && (exists.TryGetValue(path, out var e) ? e : exists[path] = File.Exists(path)))
                    .Select(v => new HistoryEntry(v.Path, v.Position)).ToList();
                var videos = file.Videos ?? [];
                var current = file.Current < 0 ? videos.Count - 1 : Math.Min(file.Current, videos.Count - 1);
                return (Existing(videos.Take(current + 1)).TakeLast(HistoryLimit).ToList(), Existing(file.Recent).Take(HistoryLimit).ToList());
            });
            if (_historyCleared)
                return;
            // Los recientes de esta sesión (si ya se abrió alguno) van delante.
            foreach (var entry in recent)
                if (_recentVideos.Count < HistoryLimit && !_recentVideos.Any(r => SamePath(r.Path, entry.Path)))
                    _recentVideos.Add(entry);
            if (saved.Count == 0)
            {
                UpdateHistoryUi();
                return;
            }
            if (_videoHistory.Count > 0 && SamePath(saved[^1].Path, _videoHistory[0].Path))
                saved.RemoveAt(saved.Count - 1);
            _videoHistory.InsertRange(0, saved);
            _videoHistoryIndex = _videoHistoryIndex < 0 ? _videoHistory.Count : _videoHistoryIndex + saved.Count;
            var excess = _videoHistory.Count - HistoryLimit;
            if (excess > 0)
            {
                _videoHistory.RemoveRange(0, excess);
                _videoHistoryIndex -= excess;
            }
            UpdateHistoryUi();
        }
        catch (Exception)
        {
            // Sin el de la vez anterior (o con una ruta rara) se empieza de cero.
        }
        finally
        {
            _historyRestored = true;
        }
    }

    /// <summary>
    /// Con el ajuste, guarda el historial, cada vídeo con dónde se dejó; al cerrar, el abierto con dónde está ahora (al
    /// abrir otro, el que se deja ya lo apuntó RecordHistory).
    /// </summary>
    private void SaveHistory(bool closing = false)
    {
        if (!_store.Settings.KeepVideoHistory || !_historyRestored)
            return;
        if (closing)
            NoteLeftPosition();
        static List<VideoHistoryItem> Items(List<HistoryEntry> entries) =>
            entries.Select(h => new VideoHistoryItem { Path = h.Path, Position = h.Position }).ToList();
        var file = new VideoHistoryFile
        {
            Videos = Items(_videoHistory),
            Current = Math.Min(_videoHistoryIndex, _videoHistory.Count - 1),
            Recent = Items(_recentVideos),
        };
        try
        {
            _store.Save(HistoryFile, file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Se intenta otra vez con el siguiente cambio.
        }
    }

    // Al activarlo se guarda el de esta sesión; al desactivarlo se borra el guardado.
    private void ApplyHistorySetting()
    {
        var keep = _store.Settings.KeepVideoHistory;
        if (_appliedKeepHistory == keep)
            return;
        _appliedKeepHistory = keep;
        if (keep)
            SaveHistory();
        else
            _store.Delete(HistoryFile);
    }

    /// <summary>
    /// Al abrir un vídeo: el que se deja guarda su posición; si se llega desde el historial solo se mueve en él, y si
    /// no, el nuevo se añade tras el actual y se descarta lo que hubiera «adelante», como en el navegador.
    /// </summary>
    private void RecordHistory(string path, int? historyIndex)
    {
        NoteLeftPosition();
        if (historyIndex is { } index)
        {
            _videoHistoryIndex = index;
        }
        else if (CurrentHistory is not { } open || !SamePath(open.Path, path))
        {
            // Recién arrancado con el historial de la vez anterior, el que se tenía abierto al cerrar sigue en su sitio.
            if (_videoHistoryIndex == _videoHistory.Count && _videoHistory.Count > 0 && SamePath(_videoHistory[^1].Path, path))
            {
                _videoHistoryIndex = _videoHistory.Count - 1;
            }
            else
            {
                var keep = Math.Min(_videoHistoryIndex + 1, _videoHistory.Count);
                _videoHistory.RemoveRange(keep, _videoHistory.Count - keep);
                _videoHistory.Add(new HistoryEntry(Path.GetFullPath(path)));
                if (_videoHistory.Count > HistoryLimit)
                    _videoHistory.RemoveAt(0);
                _videoHistoryIndex = _videoHistory.Count - 1;
            }
        }
        // El que se abre pasa a ser el más reciente (con la posición que tuviera, que se apunta al dejarlo).
        var found = _recentVideos.FindIndex(r => SamePath(r.Path, path));
        var recent = found >= 0 ? _recentVideos[found] : new HistoryEntry(Path.GetFullPath(path));
        if (found >= 0)
            _recentVideos.RemoveAt(found);
        _recentVideos.Insert(0, recent);
        if (_recentVideos.Count > HistoryLimit)
            _recentVideos.RemoveAt(_recentVideos.Count - 1);
        UpdateHistoryUi();
        SaveHistory();
    }

    // Dónde está el vídeo abierto, en su entrada del historial y en la de recientes (al dejarlo y al cerrar).
    private void NoteLeftPosition()
    {
        if (_current == null || _currentPath == null)
            return;
        if (CurrentHistory is { } current)
            current.Position = _position;
        if (_recentVideos.FirstOrDefault(r => SamePath(r.Path, _currentPath)) is { } recent)
            recent.Position = _position;
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private void GoHistory(int step) => GoToHistory(_videoHistoryIndex + step);

    private void GoToHistory(int index, double? at = null)
    {
        if (index < 0 || index >= _videoHistory.Count || index == _videoHistoryIndex)
            return;
        var entry = _videoHistory[index];
        if (!File.Exists(entry.Path))
        {
            // Borrado o movido desde entonces: se quita y se avisa, sin cuadro de diálogo.
            _videoHistory.RemoveAt(index);
            if (index < _videoHistoryIndex)
                _videoHistoryIndex--;
            ShowToast(S.VideoGone(Path.GetFileName(entry.Path)));
            UpdateHistoryUi();
            SaveHistory();
            return;
        }
        Open(entry.Path, at ?? entry.Position, index);
    }

    // Desde el menú de las flechas: si sigue en el historial se va a su entrada (la más cercana) y si no, se abre como
    // uno nuevo; en los dos casos donde se dejó la última vez.
    private void OpenRecent(HistoryEntry entry)
    {
        if (!File.Exists(entry.Path))
        {
            _recentVideos.Remove(entry);
            ShowToast(S.VideoGone(Path.GetFileName(entry.Path)));
            UpdateHistoryUi();
            SaveHistory();
            return;
        }
        var index = Enumerable.Range(0, _videoHistory.Count)
            .Where(i => i != _videoHistoryIndex && SamePath(_videoHistory[i].Path, entry.Path))
            .OrderBy(i => Math.Abs(i - _videoHistoryIndex)).DefaultIfEmpty(-1).First();
        if (index >= 0)
            GoToHistory(index, entry.Position);
        else
            Open(entry.Path, entry.Position);
    }

    // Se queda solo el vídeo abierto (si hay uno).
    private void ClearHistory()
    {
        _historyCleared = true;
        var current = _currentPath != null ? CurrentHistory : null;
        var recent = current != null ? _recentVideos.FirstOrDefault(r => SamePath(r.Path, current.Path)) : null;
        _videoHistory.Clear();
        _recentVideos.Clear();
        if (current != null)
            _videoHistory.Add(current);
        if (recent != null)
            _recentVideos.Add(recent);
        _videoHistoryIndex = current != null ? 0 : -1;
        UpdateHistoryUi();
        SaveHistory();
    }

    private List<HistoryEntry> OtherRecentVideos() =>
        _recentVideos.Where(r => _currentPath == null || !SamePath(r.Path, _currentPath)).ToList();

    private void UpdateHistoryUi()
    {
        HistoryBackButton.IsEnabled = _videoHistoryIndex > 0;
        HistoryForwardButton.IsEnabled = _videoHistoryIndex >= 0 && _videoHistoryIndex < _videoHistory.Count - 1;
        var menu = OtherRecentVideos().Count > 1;
        HistoryBackButton.ToolTip = HistoryTip(S.HistoryBackTip, _videoHistoryIndex - 1, menu);
        HistoryForwardButton.ToolTip = HistoryTip(S.HistoryForwardTip, _videoHistoryIndex + 1, menu);
    }

    // Con destino: su nombre (y, si hay más recientes, cómo verlos todos); sin él, solo la acción.
    private string HistoryTip(string action, int index, bool menu)
    {
        if (index < 0 || index >= _videoHistory.Count)
            return action;
        var tip = $"{action}\n{Path.GetFileName(_videoHistory[index].Path)}";
        return menu ? $"{tip}\n{S.HistoryMenuTip}" : tip;
    }

    private static string HistoryLabel(HistoryEntry entry) =>
        $"{Path.GetFileName(entry.Path)} · {TimeFormat.Format(entry.Position, withMilliseconds: false)}";

    private void OnHistoryBackClick(object sender, RoutedEventArgs e) => GoHistory(-1);

    private void OnHistoryForwardClick(object sender, RoutedEventArgs e) => GoHistory(1);

    // Clic derecho en una flecha: los vídeos recientes, el último abierto arriba (también los que las flechas ya
    // descartaron), y limpiar el historial.
    private void OnHistoryRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (HistoryMenu((Button)sender) is { } menu)
            menu.IsOpen = true;
    }

    private ContextMenu? HistoryMenu(Button button)
    {
        var others = OtherRecentVideos();
        if (others.Count == 0)
            return null;
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Top };
        foreach (var entry in others)
        {
            var item = new MenuItem { Header = HistoryLabel(entry) };
            item.Click += (_, _) => OpenRecent(entry);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var clear = WithIcon(new MenuItem { Header = S.ClearHistory }, "ClearHistoryMenuIcon");
        clear.Click += (_, _) => ClearHistory();
        menu.Items.Add(clear);
        return menu;
    }

    private void OnHistoryMouseButton(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not (MouseButton.XButton1 or MouseButton.XButton2))
            return;
        GoHistory(e.ChangedButton == MouseButton.XButton2 ? 1 : -1);
        e.Handled = true;
    }
    public static bool HistorySelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "historial";

    // Abre otros dos vídeos de la carpeta del primero y va y viene por el historial, comprobando vídeo y posición.
    private async Task RunHistorySelfTest()
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
        async Task WaitLoaded(string path)
        {
            for (var i = 0; i < 100 && (_awaitingDuration || !SamePath(_currentPath!, path)); i++)
                await Task.Delay(100);
            await Task.Delay(800);
        }
        // Que avance de verdad, no solo que el estado diga «reproduciendo».
        async Task<bool> Advancing()
        {
            var before = _position;
            await Task.Delay(600);
            return !_nav.Paused && _position > before + 0.3;
        }
        async Task SeekAndWait(double time)
        {
            _nav.SeekTo(time);
            await Task.Delay(800);
        }

        var first = _currentPath!;
        var others = Directory.GetFiles(Path.GetDirectoryName(first)!)
            .Where(f => VideoFilter.Contains(Path.GetExtension(f).ToLowerInvariant()) && !SamePath(f, first)
                && new FileInfo(f).Length < 500_000_000)
            .Take(2).ToArray();
        // Las posiciones guardadas de estos vídeos se devuelven como estaban.
        var saved = _store.Library.Videos.Values.Select(v => (Entry: v, v.LastPosition, v.LastOpened)).ToList();
        try
        {
            Check(others.Length == 2, $"otros dos vídeos: {string.Join(", ", others.Select(Path.GetFileName))}");
            await WaitLoaded(first);
            Check(!HistoryBackButton.IsEnabled && !HistoryForwardButton.IsEnabled, "al empezar no hay adónde ir");
            await SeekAndWait(12);
            var firstPos = _position;

            Open(others[0]);
            await WaitLoaded(others[0]);
            await SeekAndWait(3);
            var secondPos = _position;
            Check(HistoryBackButton.IsEnabled && !HistoryForwardButton.IsEnabled, "tras abrir otro, solo atrás");
            Log($"tooltip atrás: {HistoryBackButton.ToolTip}");

            // En pausa: el vídeo al que se vuelve empieza a reproducirse, como en VLC.
            _nav.Pause();
            await Task.Delay(300);
            GoHistory(-1);
            await WaitLoaded(first);
            Check(await Advancing(), "en pausa, ir atrás reproduce el otro vídeo");
            Check(SamePath(_currentPath!, first) && Math.Abs(_position - firstPos) < 2,
                $"atrás vuelve al primero en {_position:0.00} (se dejó en {firstPos:0.00})");
            Check(!HistoryBackButton.IsEnabled && HistoryForwardButton.IsEnabled, "en el primero, solo adelante");

            GoHistory(1);
            await WaitLoaded(others[0]);
            Check(SamePath(_currentPath!, others[0]) && Math.Abs(_position - secondPos) < 1,
                $"adelante vuelve al segundo en {_position:0.00} (se dejó en {secondPos:0.00})");

            // Dejarlo acabar (se para en el último frame): el que se abre después también se reproduce.
            Check(!_nav.Paused, "reproduciendo antes de llegar al final");
            // Arrastrando la barra reproduciendo: se queda en el frame bajo el cursor y, al soltar, sigue reproduciendo.
            foreach (var at in new[] { 10.0, 20.0 })
            {
                _nav.DragTo(at);
                await Task.Delay(700);
                Check(_nav.Paused && Math.Abs(_position - at) < 0.1, $"arrastrando a {at:0}: quieto en {_position:0.00}");
            }
            _nav.SeekTo(20);
            await Task.Delay(500);
            Check(await Advancing() && _position < 22, $"al soltar sigue reproduciendo desde ahí ({_position:0.00})");
            _nav.SeekTo(_duration - 0.5);
            for (var i = 0; i < 30 && !_nav.Paused; i++)
                await Task.Delay(100);
            Check(_nav.Paused, $"al final se para ({_position:0.00} de {_duration:0.00})");
            async Task PlayToEnd()
            {
                _nav.SeekTo(_duration - 0.5);
                if (_nav.Paused)
                    _nav.TogglePlay();
                await Task.Delay(400);
                for (var i = 0; i < 30 && !_nav.Paused; i++)
                    await Task.Delay(100);
                await Task.Delay(200);
            }
            // Parado al final, play vuelve a empezar desde el principio, como en cualquier reproductor.
            _nav.TogglePlay();
            await Task.Delay(500);
            Check(await Advancing() && _position < 3, $"play al final vuelve a empezar ({_position:0.00})");
            // Con un tramo de zoom que llega al final, desde el principio del tramo.
            SetFreeZoom((_duration - 4, _duration));
            await PlayToEnd();
            _nav.TogglePlay();
            await Task.Delay(500);
            Check(await Advancing() && _position > _duration - 4.1 && _position < _duration - 2,
                $"con el tramo hasta el final, vuelve al principio del tramo ({_position:0.00} de {_duration:0.00})");
            // Con el tramo en otro sitio, desde el principio del vídeo.
            SetFreeZoom((1, 3));
            await PlayToEnd();
            _nav.TogglePlay();
            await Task.Delay(500);
            Check(await Advancing() && _position < 3, $"con el tramo en otro sitio, vuelve al principio del vídeo ({_position:0.00})");
            CloseZone();
            // Marcha atrás desde el primer frame, desde el final.
            _nav.Pause();
            _nav.SeekTo(0);
            await Task.Delay(800);
            _nav.Backward();
            await Task.Delay(1200);
            Check(_nav.Reversing && _position > _duration - 3 && _position < _duration - 0.2,
                $"marcha atrás en el primer frame sigue desde el final ({_position:0.00} de {_duration:0.00})");
            _nav.Pause();
            await PlayToEnd();
            Check(_nav.Paused, $"otra vez parado al final ({_position:0.00} de {_duration:0.00})");
            // Desde atrás, abrir otro descarta lo de adelante.
            GoHistory(-1);
            await WaitLoaded(first);
            Check(await Advancing(), $"tras acabar un vídeo, ir atrás reproduce el otro ({_position:0.00})");
            Open(others[1]);
            await WaitLoaded(others[1]);
            Check(_videoHistory.Count == 2 && !HistoryForwardButton.IsEnabled, "abrir otro desde atrás descarta lo de adelante");
            Open(others[1]);
            Check(_videoHistory.Count == 2, "reabrir el mismo vídeo no añade entrada");
            await WaitLoaded(others[1]);

            // El menú de las flechas tiene también el que se descartó de «adelante», y se puede volver a él.
            var items = HistoryMenu(HistoryBackButton)!.Items.OfType<MenuItem>().Select(i => (string)i.Header).ToList();
            Check(items.Count == 3 && items[0].StartsWith(Path.GetFileName(first)) && items[1].StartsWith(Path.GetFileName(others[0]))
                && items[2] == S.ClearHistory, $"clic derecho en atrás: {string.Join(" | ", items)}");
            OpenRecent(_recentVideos.First(r => SamePath(r.Path, others[0])));
            await WaitLoaded(others[0]);
            Check(SamePath(_currentPath!, others[0]) && _videoHistory.Count == 3 && _videoHistoryIndex == 2,
                $"desde el menú, al descartado ({_videoHistory.Count} entradas, en la {_videoHistoryIndex})");
            ClearHistory();
            Check(_videoHistory.Count == 1 && _recentVideos.Count == 1 && !HistoryBackButton.IsEnabled && !HistoryForwardButton.IsEnabled
                && HistoryMenu(HistoryBackButton) == null, "limpiar el historial deja solo el abierto");
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            SavePosition();
            _current = null;
            foreach (var (entry, position, opened) in saved)
                (entry.LastPosition, entry.LastOpened) = (position, opened);
            _store.SaveLibrary();
            Log(failures == 0 ? "TODO OK" : $"{failures} FALLOS");
            File.WriteAllLines(Path.Combine(_store.Folder, "selftest-historial.log"), lines);
            Close();
        }
    }
}
