using System.Diagnostics;
using System.IO;
using System.Windows.Media;

namespace VideoSeeker.App;

/// <summary>
/// Prueba automática de la tira de miniaturas de la barra de zoom (VIDEOSEEKER_SELFTEST=tira): con el ajuste, la barra
/// crece y se llena (cuánto tarda y cuántas salen de la GPU o de la CPU) en un tramo de 12 s y en uno de 30 frames, cada
/// miniatura en el inicio de un frame; desplazar el tramo por partes sin caché no frena la interfaz (el hueco más largo
/// entre dos pintados, con y sin tira); apagado, la barra vuelve a su alto. Guarda selftest-tira-*.png.
/// </summary>
public partial class MainWindow
{
    public static bool FilmstripSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "tira";

    private async Task RunFilmstripSelfTest()
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
        var setting = _store.Settings.ZoomFilmstrip;
        try
        {
            await Task.Delay(1000);
            _nav.Pause();
            Log($"vídeo de {_duration:0.0} s a {Fps:0.###} fps");
            var grid = Zoom.Grid;

            // Hasta que no falta ninguna; los milisegundos, o -1 si no llega.
            async Task<long> WaitFilled(int timeoutMs = 30000)
            {
                var clock = Stopwatch.StartNew();
                await Task.Delay(50);
                while (clock.ElapsedMilliseconds < timeoutMs)
                {
                    if (_filmstrip.WantedCount > 0 && _filmstrip.MissingCount == 0)
                        return clock.ElapsedMilliseconds;
                    await Task.Delay(10);
                }
                return -1;
            }

            // Hueco más largo entre dos pintados mientras se desplaza el tramo por partes del vídeo sin caché.
            async Task<double> PanGap(double from)
            {
                var max = 0.0;
                var last = Stopwatch.StartNew();
                void OnRendering(object? s, EventArgs e)
                {
                    max = Math.Max(max, last.Elapsed.TotalMilliseconds);
                    last.Restart();
                }
                CompositionTarget.Rendering += OnRendering;
                for (var i = 0; i < 40; i++)
                {
                    var start = grid.TimeOf(grid.FrameAt(from + i * 0.5));
                    SetFreeZoom((start, start + 12), animate: false);
                    _nav.SeekTo(start + 6);
                    await Task.Delay(60);
                }
                CompositionTarget.Rendering -= OnRendering;
                return max;
            }

            var plainHeight = Zoom.Height;
            var offGap = await PanGap(_duration * 0.15);
            Log($"sin tira: hueco más largo entre pintados al desplazar el tramo, {offGap:0} ms");
            CloseZone();
            await Task.Delay(300);

            _store.Settings.ZoomFilmstrip = true;
            ApplySettings();
            Check(Zoom.Height > plainHeight + 15, $"con la tira la barra crece ({plainHeight:0} → {Zoom.Height:0})");

            // Tramo de 12 s.
            var zoneStart = grid.TimeOf(grid.FrameAt(_duration * 0.4));
            SetFreeZoom((zoneStart, zoneStart + 12), animate: false);
            var gpu = _filmstrip.FromGpuCount;
            var cpu = _filmstrip.FromCpuCount;
            var ms = await WaitFilled();
            Check(ms >= 0, $"tramo de 12 s: {_filmstrip.WantedCount} miniaturas en {ms} ms ({_filmstrip.FromGpuCount - gpu} de la GPU, {_filmstrip.FromCpuCount - cpu} de la CPU)");
            Check(_filmstrip.WantedTimes.All(t => Math.Abs(grid.TimeOf(grid.FrameAt(t + 0.0006)) - t) < 0.001),
                "cada miniatura en el inicio de un frame");
            await Task.Delay(200);
            SavePeekSnapshot(ZoomRow, "selftest-tira-12s.png");
            Log("caché de frames: " + string.Join(", ", (_engine?.CachedRanges() ?? []).Select(r => $"{r.Start:0.00}–{r.End:0.00}")));

            // Volver a un tramo ya visto: al momento.
            SetFreeZoom((zoneStart + 2, zoneStart + 14), animate: false);
            await WaitFilled();
            SetFreeZoom((zoneStart, zoneStart + 12), animate: false);
            ms = await WaitFilled();
            Check(ms is >= 0 and < 200, $"volver al mismo tramo: {ms} ms");

            // 30 frames: una miniatura cada pocos frames, en su marca.
            var frameStart = grid.FrameAt(zoneStart + 3);
            SetFreeZoom((grid.TimeOf(frameStart), grid.TimeOf(frameStart + 30)), animate: false);
            gpu = _filmstrip.FromGpuCount;
            cpu = _filmstrip.FromCpuCount;
            ms = await WaitFilled();
            var times = _filmstrip.WantedTimes;
            var frames = times.Select(t => grid.FrameAt(t + 0.0006)).ToList();
            var steps = frames.Zip(frames.Skip(1), (a, b) => b - a).Distinct().ToList();
            Check(ms >= 0, $"30 frames: {times.Count} miniaturas en {ms} ms ({_filmstrip.FromGpuCount - gpu} de la GPU, {_filmstrip.FromCpuCount - cpu} de la CPU), cada {string.Join("/", steps)} frames");
            Check(steps.Count == 1, "todas con el mismo paso de frames");
            await Task.Delay(200);
            SavePeekSnapshot(ZoomRow, "selftest-tira-30f.png");

            // Desplazar por partes sin caché: la tira no frena la interfaz.
            var onGap = await PanGap(_duration * 0.65);
            Check(onGap < Math.Max(150, offGap * 2), $"con tira: hueco más largo entre pintados al desplazar el tramo, {onGap:0} ms (sin tira {offGap:0} ms)");
            ms = await WaitFilled();
            Check(ms >= 0, $"tras desplazar, el último tramo se llena en {ms} ms");
            SavePeekSnapshot(ZoomRow, "selftest-tira-final.png");

            _store.Settings.ZoomFilmstrip = false;
            ApplySettings();
            Check(Math.Abs(Zoom.Height - plainHeight) < 0.5 && Zoom.Filmstrip == null, "apagada, la barra vuelve a su alto");
            CloseZone();
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            _store.Settings.ZoomFilmstrip = setting;
            ApplySettings();
            Log(failures == 0 ? "TODO BIEN" : $"{failures} FALLOS");
            File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
            Close();
        }
    }
}
