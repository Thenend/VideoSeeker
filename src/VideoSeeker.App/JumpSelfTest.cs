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
            BookmarkPanel.Visibility = System.Windows.Visibility.Visible;
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
