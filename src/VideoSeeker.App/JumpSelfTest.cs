using System.IO;
using VideoSeeker.App.Controls;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

/// <summary>
/// Prueba automática de ir a un bookmark sin cambiar si se reproduce (VIDEOSEEKER_SELFTEST=saltar): reproduciendo
/// sigue reproduciendo desde el bookmark y en pausa se queda en pausa en su primer frame, tanto desde el panel como con
/// un clic en las barras, [ ] y los números; también con el bucle del tramo puesto y en marcha atrás. Deja los
/// bookmarks del vídeo como estaban, escribe selftest.log y cierra.
/// </summary>
public partial class MainWindow
{
    public static bool JumpSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "saltar";

    // Desde ahora hasta llamar a lo que devuelve: true si en algún momento la fila del zoom dejó de verse.
    private Func<bool> WatchSmoothOpen()
    {
        var closed = false;
        void Watch(object? sender, EventArgs e) => closed |= !ZoomReveal.IsOpen;
        LayoutUpdated += Watch;
        return () =>
        {
            LayoutUpdated -= Watch;
            return closed;
        };
    }

    private async Task RunJumpSelfTest()
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
        for (var i = 0; i < 300 && _frameIndex == null; i++)
            await Task.Delay(100);
        var collection = _store.Settings.BookmarkCollection;
        var file = Path.Combine(BookmarkCollections.PathOf(_store, collection), _bookmarks.Fingerprint + ".json");
        var saved = File.Exists(file) ? File.ReadAllText(file) : null;
        var (fingerprint, videoPath) = (_bookmarks.Fingerprint, _bookmarks.VideoPath);
        var (loopRange, loopVideo) = (_loopRange, _loopVideo);
        try
        {
            await Task.Delay(1000);
            var set = _bookmarks;
            var grid = Zoom.Grid;
            Log($"vídeo de {_duration:0.0} s a {Fps:0.###} fps");
            CloseZone();
            foreach (var b in set.Items.ToList())
                set.Remove(b.Id);
            var length = Math.Min(4, _duration / 12);
            var marks = new[] { 0.2, 0.4, 0.6 }
                .Select((p, i) => set.Add($"Salto {i + 1}", grid.TimeOf(grid.FrameAt(_duration * p)),
                    grid.TimeOf(grid.FrameAt(_duration * p + length)), "#4A9EFF", null))
                .ToList();
            // El panel abierto, para ir desde sus filas.
            PanelReveal.SetOpen(true);
            RefreshRows();

            async Task SetPlaying(bool playing)
            {
                if (_nav.Reversing || _nav.LoopPlaying || _nav.Paused != !playing)
                {
                    if (!_nav.Paused || _nav.Reversing || _nav.LoopPlaying)
                        _nav.TogglePlay();
                    if (playing && _nav.Paused)
                        _nav.TogglePlay();
                }
                await Task.Delay(700);
            }

            async Task Try(string what, bool playing, Bookmark target, Func<Task> action, bool reverse = false)
            {
                // Lejos de los bookmarks (que el de delante/detrás sea el esperado lo pone cada caso).
                await SetPlaying(false);
                if (reverse)
                {
                    _nav.Backward();
                    await Task.Delay(500);
                }
                else
                {
                    await SetPlaying(playing);
                }
                await action();
                if (reverse)
                    for (var i = 0; i < 30; i++)
                    {
                        Log($"   {_nav.CurrentTime:0.000} {(_nav.Reversing ? "atrás" : _nav.Paused ? "pausa" : "adelante")}");
                        await Task.Delay(150);
                    }
                else
                    await Task.Delay(1200);
                var t = _nav.CurrentTime;
                var paused = _nav.Paused && !_nav.Reversing && !_nav.LoopPlaying;
                var moved = reverse ? t < target.Start - 0.1 : t > target.Start + 0.3;
                bool ok = playing
                    ? !paused && moved && Math.Abs(t - target.Start) < (reverse ? 6 : 3) && (!reverse || _nav.Reversing)
                    : paused && Math.Abs(t - target.Start) < 0.5 / Fps + 1e-3;
                Check(ok, $"{what} {(reverse ? "en marcha atrás" : playing ? "reproduciendo" : "en pausa")}: " +
                    $"{(paused ? "en pausa" : _nav.Reversing ? "marcha atrás" : "reproduce")} en {t:0.000} (bookmark en {target.Start:0.000})");
            }

            async Task ClickOn(TimelineBar bar, Bookmark target)
            {
                var point = bar.LanePoint(Math.Min(EndOf(target) - 1e-3, target.Start + 2 / Fps));
                bar.SimulateLeftDown(point);
                await Task.Delay(80);
                bar.SimulateLeftUp();
            }

            Bookmark Row(int i) => set.Find(marks[i].Id)!;
            var cases = new (string What, Bookmark Target, Func<Task> Action, Func<Task>? Prepare)[]
            {
                ("panel", marks[1], () => { OpenRow(_rows.First(r => r.Id == marks[1].Id)); return Task.CompletedTask; }, null),
                ("barra (otro bookmark)", marks[2], () => ClickOn(Seek, Row(2)), async () => { JumpTo(Row(0)); await Task.Delay(300); }),
                ("barra (el seleccionado)", marks[0], () => ClickOn(Seek, Row(0)), async () => { JumpTo(Row(0)); await Task.Delay(300); }),
                ("barra de zoom", marks[0], () => ClickOn(Zoom, Row(0)), async () => { JumpTo(Row(0)); await Task.Delay(300); }),
                ("]", marks[2], () => { JumpAdjacent(1); return Task.CompletedTask; },
                    async () => { CloseZone(); _nav.SeekTo(marks[1].Start + 1); await Task.Delay(300); }),
                ("[", marks[0], () => { JumpAdjacent(-1); return Task.CompletedTask; },
                    async () => { CloseZone(); _nav.SeekTo(marks[1].Start - 3); await Task.Delay(300); }),
                ("número 2", marks[1], () => { JumpToNumber(2); return Task.CompletedTask; }, null),
            };
            // VIDEOSEEKER_ONLY=otro: solo lo del bookmark de otro vídeo.
            if (Environment.GetEnvironmentVariable("VIDEOSEEKER_ONLY") == "otro")
                goto OtherVideo;
            foreach (var (tag, rangeLoop, videoLoop) in new[] { ("", false, false), ("[bucle del tramo] ", true, false), ("[bucle del vídeo] ", false, true) })
            {
                if (_loopRange != rangeLoop)
                    ToggleRangeLoop();
                if (_loopVideo != videoLoop)
                    ToggleVideoLoop();
                foreach (var playing in new[] { true, false })
                    foreach (var (what, target, action, prepare) in cases)
                    {
                        if (prepare != null)
                            await prepare();
                        else
                        {
                            CloseZone();
                            _nav.SeekTo(_duration * 0.05);
                            await Task.Delay(300);
                        }
                        await Try(tag + what, playing, target, action);
                    }
            }
            if (_loopRange)
                ToggleRangeLoop();
            if (_loopVideo)
                ToggleVideoLoop();

            // Marcha atrás (J) sigue en marcha atrás desde el bookmark.
            CloseZone();
            _nav.SeekTo(_duration * 0.9);
            await Task.Delay(300);
            await Try("panel", true, marks[1], () => { OpenRow(_rows.First(r => r.Id == marks[1].Id)); return Task.CompletedTask; }, reverse: true);
            await SetPlaying(false);

        OtherVideo:
            // Bookmark de otro vídeo (VIDEOSEEKER_OTHER): como lo hace Mario, clic en la imagen para reproducir, otro para
            // pausar, otro para reproducir y, al rato (también enseguida), ir al bookmark del otro vídeo.
            if (Environment.GetEnvironmentVariable("VIDEOSEEKER_OTHER") is { Length: > 0 } other && File.Exists(other))
            {
                var paths = new[] { videoPath, other };
                // Como lo tiene Mario: con el bucle del vídeo.
                if (Environment.GetEnvironmentVariable("VIDEOSEEKER_LOOPVIDEO") == "1" && !_loopVideo)
                    ToggleVideoLoop();
                async Task WaitOpened(string path)
                {
                    for (var i = 0; i < 100 && (_awaitingDuration || !string.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase)); i++)
                        await Task.Delay(100);
                }
                // Un bookmark en cada vídeo, y el panel con todos los vídeos de la colección (como el de Mario).
                var ids = new string[2];
                string? otherFile = null, otherSaved = null;
                try
                {
                    ids[0] = _bookmarks!.Add("Este", 0, _duration, "#4A9EFF", null).Id; // el vídeo entero, como los de Mario
                    Open(other);
                    await WaitOpened(other);
                    otherFile = Path.Combine(BookmarkCollections.PathOf(_store, collection), _bookmarks!.Fingerprint + ".json");
                    otherSaved = File.Exists(otherFile) ? File.ReadAllText(otherFile) : null;
                    ids[1] = _bookmarks.Add("Otro", 0, _duration, "#4A9EFF", null).Id;
                    Open(videoPath);
                    await WaitOpened(videoPath);
                    PanelReveal.SetOpen(true);
                    ScopeAll.IsChecked = true;
                    _otherFiles = null;
                    RefreshRows();
                    var round = 0;
                    foreach (var pauseMs in new[] { 150, 1500, 4000 })
                        foreach (var playMs in new[] { 100, 400, 1500 })
                            foreach (var reverse in new[] { false, true })
                            {
                                var to = paths[++round % 2];
                                if (_nav.Paused)
                                    OnVideoClicked();
                                // Con la pausa larga, antes se ve el vídeo entero y da la vuelta (bucle del vídeo).
                                await Task.Delay(pauseMs >= 4000 ? 11000 : 800);
                                OnVideoClicked();
                                await Task.Delay(pauseMs);
                                if (reverse)
                                    _nav.Backward();
                                else
                                    OnVideoClicked();
                                await Task.Delay(playMs);
                                var how = $"{(reverse ? "marcha atrás" : "reproduciendo")} (pausa {pauseMs} ms, play {playMs} ms)";
                                Log($"   antes: {(_nav.LoopPlaying ? "bucle desde la caché" : _nav.Paused ? "en pausa" : "reproduce")}; caché {string.Join(" ", _engine?.CachedRanges().Select(r => $"{r.Start:0.000}-{r.End:0.000}") ?? [])}; duración {_duration:0.000}, bucle {_loopVideo}");
                                var row = _rows.First(r => r.Id == ids[round % 2]);
                                var smooth = WatchSmoothOpen();
                                OpenRow(row);
                                var (instantDuration, instantView) = (Seek.Duration, Zoom.View);
                                await WaitOpened(to);
                                var before = _nav.CurrentTime;
                                await Task.Delay(1000);
                                var t = _nav.CurrentTime;
                                var closed = smooth();
                                Check(!closed && Math.Abs(instantDuration - _duration) < 1e-3
                                        && Math.Abs(instantView.Start - Zoom.View.Start) < 1e-3 && Math.Abs(instantView.End - Zoom.View.End) < 1e-3,
                                    $"otro vídeo sin saltos: fila del zoom {(closed ? "se cerró" : "siempre abierta")}, " +
                                    $"barras al abrir {instantDuration:0.000} s, {instantView.Start:0.000}-{instantView.End:0.000} " +
                                    $"(luego {_duration:0.000} s, {Zoom.View.Start:0.000}-{Zoom.View.End:0.000})");
                                // En marcha atrás, el otro vídeo se reproduce hacia delante (lo que importa es que no se pare).
                                Check(!_nav.Paused && !row.IsCurrentVideo && t > before + 0.3,
                                    $"otro vídeo {how}: {(_nav.Reversing ? "marcha atrás" : _nav.Paused ? "en pausa" : "reproduce")}, {before:0.00} → {t:0.00}");
                                if (_nav.Reversing)
                                    _nav.TogglePlay();
                            }
                    // En pausa sigue en pausa.
                    _nav.Pause();
                    await Task.Delay(500);
                    var last = paths[++round % 2];
                    OpenRow(_rows.First(r => r.Id == ids[round % 2]));
                    await WaitOpened(last);
                    await Task.Delay(1000);
                    Check(_nav.Paused, "otro vídeo en pausa: sigue en pausa");
                }
                finally
                {
                    ScopeAll.IsChecked = false;
                    if (otherFile != null)
                    {
                        File.Delete(otherFile + ".bak");
                        if (otherSaved != null)
                            File.WriteAllText(otherFile, otherSaved);
                        else
                            File.Delete(otherFile);
                    }
                    if (!string.Equals(_currentPath, videoPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Open(videoPath);
                        await WaitOpened(videoPath);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            if (_loopRange != loopRange)
                ToggleRangeLoop();
            if (_loopVideo != loopVideo)
                ToggleVideoLoop();
            File.Delete(file + ".bak");
            if (saved != null)
                File.WriteAllText(file, saved);
            else
                File.Delete(file);
            LoadBookmarks(fingerprint, videoPath);
            Log(failures == 0 ? "TODO BIEN" : $"{failures} FALLOS");
            File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
            Close();
        }
    }
}
