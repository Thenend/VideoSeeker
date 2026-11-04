using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace VideoSeeker.App;

// Historial de vídeos abiertos, como el de una pestaña del navegador: ‹ › junto a «Abrir vídeo», botones laterales
// del ratón y Alt + ← / →. Cada entrada recuerda dónde se dejó el vídeo para volver justo ahí. Dura lo que la sesión.
public partial class MainWindow
{
    private sealed class HistoryEntry(string path)
    {
        public string Path { get; } = path;
        public double Position { get; set; }
    }

    private const int HistoryLimit = 50;
    private readonly List<HistoryEntry> _videoHistory = [];
    private int _videoHistoryIndex = -1;

    private void InitHistory()
    {
        PreviewMouseDown += OnHistoryMouseButton;
        // Que el soltar tampoco llegue a Windows, que lo convertiría en otro «atrás».
        PreviewMouseUp += (_, e) => e.Handled |= e.ChangedButton is MouseButton.XButton1 or MouseButton.XButton2;
        _surface.SideButton += forward => GoHistory(forward ? 1 : -1);
        UpdateHistoryUi();
    }

    /// <summary>
    /// Al abrir un vídeo: el que se deja guarda su posición; si se llega desde el historial solo se mueve en él, y si
    /// no, el nuevo se añade tras el actual y se descarta lo que hubiera «adelante», como en el navegador.
    /// </summary>
    private void RecordHistory(string path, int? historyIndex)
    {
        if (_videoHistoryIndex >= 0)
            _videoHistory[_videoHistoryIndex].Position = _position;
        if (historyIndex is { } index)
        {
            _videoHistoryIndex = index;
        }
        else if (_videoHistoryIndex < 0 || !SamePath(_videoHistory[_videoHistoryIndex].Path, path))
        {
            _videoHistory.RemoveRange(_videoHistoryIndex + 1, _videoHistory.Count - _videoHistoryIndex - 1);
            _videoHistory.Add(new HistoryEntry(Path.GetFullPath(path)));
            if (_videoHistory.Count > HistoryLimit)
                _videoHistory.RemoveAt(0);
            _videoHistoryIndex = _videoHistory.Count - 1;
        }
        UpdateHistoryUi();
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private void GoHistory(int step) => GoToHistory(_videoHistoryIndex + step);

    private void GoToHistory(int index)
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
            return;
        }
        Open(entry.Path, entry.Position, index);
    }

    private void UpdateHistoryUi()
    {
        HistoryBackButton.IsEnabled = _videoHistoryIndex > 0;
        HistoryForwardButton.IsEnabled = _videoHistoryIndex >= 0 && _videoHistoryIndex < _videoHistory.Count - 1;
        HistoryBackButton.ToolTip = HistoryTip(S.HistoryBackTip, _videoHistoryIndex - 1,
            Math.Max(_videoHistoryIndex, 0), S.HistoryAllBack);
        HistoryForwardButton.ToolTip = HistoryTip(S.HistoryForwardTip, _videoHistoryIndex + 1,
            _videoHistory.Count - 1 - _videoHistoryIndex, S.HistoryAllForward);
    }

    // Con destino: su nombre (y, si hay más de uno de ese lado, cómo verlos todos); sin él, solo la acción.
    private string HistoryTip(string action, int index, int count, Func<int, string> all)
    {
        if (index < 0 || index >= _videoHistory.Count)
            return action;
        var tip = $"{action}\n{Path.GetFileName(_videoHistory[index].Path)}";
        return count > 1 ? $"{tip}\n{all(count)}" : tip;
    }

    private static string HistoryLabel(HistoryEntry entry) =>
        $"{Path.GetFileName(entry.Path)} · {TimeFormat.Format(entry.Position, withMilliseconds: false)}";

    private void OnHistoryBackClick(object sender, RoutedEventArgs e) => GoHistory(-1);

    private void OnHistoryForwardClick(object sender, RoutedEventArgs e) => GoHistory(1);

    // Clic derecho en una flecha: los vídeos de ese lado, el más cercano arriba, para saltar varios de golpe.
    private void OnHistoryRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var menu = HistoryMenu((Button)sender);
        if (menu.Items.Count > 0)
            menu.IsOpen = true;
    }

    private ContextMenu HistoryMenu(Button button)
    {
        var step = button == HistoryBackButton ? -1 : 1;
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Top };
        for (var i = _videoHistoryIndex + step; i >= 0 && i < _videoHistory.Count; i += step)
        {
            var index = i;
            var item = new MenuItem { Header = HistoryLabel(_videoHistory[i]) };
            item.Click += (_, _) => GoToHistory(index);
            menu.Items.Add(item);
        }
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

            var items = HistoryMenu(HistoryBackButton).Items.OfType<MenuItem>().Select(i => (string)i.Header).ToList();
            Check(items.Count == 1 && items[0].StartsWith(Path.GetFileName(first)), $"clic derecho en atrás: {string.Join(" | ", items)}");
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
