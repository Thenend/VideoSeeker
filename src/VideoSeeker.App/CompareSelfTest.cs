using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using VideoSeeker.App.Controls;

namespace VideoSeeker.App;

/// <summary>
/// Prueba automática de comparar dos vídeos (VIDEOSEEKER_SELFTEST=comparar): abre el mismo vídeo como B con un desfase
/// conocido, bloquea y, en unos 200 pasos, saltos, clics y arrastres al azar, mira que B siempre esté en el frame que toca
/// (o fuera, oscuro, si cae fuera de B); reproduce casi un minuto midiendo cuánto se aleja B; con otro vídeo de otros fps,
/// que cada paso vaya al siguiente cambio de imagen de cualquiera de los dos; la vista apilada, la animación del panel con
/// las dos imágenes y abrir y cerrar varias veces sin fugas. Guarda capturas (selftest-comparar-*.png), escribe
/// selftest.log y cierra.
/// </summary>
public partial class MainWindow
{
    public static bool CompareSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "comparar";

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(nint process, uint flags);

    private async Task RunCompareSelfTest()
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
        string Png(string name) => Path.Combine(_store.Folder, $"selftest-comparar-{name}.png");
        (int Handles, int Threads, uint Gdi, uint User, long MemoryMb) Resources()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var process = Process.GetCurrentProcess();
            process.Refresh();
            return (process.HandleCount, process.Threads.Count, GetGuiResources(process.Handle, 0), GetGuiResources(process.Handle, 1),
                process.PrivateMemorySize64 / (1024 * 1024));
        }

        try
        {
            while (_duration <= 0 || _engine == null)
                await Task.Delay(100);
            for (var i = 0; i < 300 && _frameIndex == null; i++)
                await Task.Delay(100);
            await Task.Delay(800);
            _nav.Pause();
            CloseZone();
            var path = _currentPath!;
            var gridA = Zoom.Grid;
            Log($"vídeo {Path.GetFileName(path)}: {_duration:0.000} s a {Fps:0.###} fps, índice {(_frameIndex != null ? "sí" : "no")}");

            async Task<ComparePane> WaitCompare()
            {
                for (var i = 0; i < 200 && !(_compare is { Ready: true, Index: not null } p && p.Engine?.Ready != false); i++)
                    await Task.Delay(100);
                await Task.Delay(400);
                return _compare ?? throw new InvalidOperationException("no se abrió la comparación");
            }

            // Espera a que los dos estén quietos (los mismos frames durante 250 ms) y devuelve si B está donde debe.
            async Task<(bool Ok, string Detail)> Settle(int timeoutMs = 4000)
            {
                var pane = _compare!;
                var sync = _sync!;
                var watch = Stopwatch.StartNew();
                (int, int, CompareSync.BRange) last = default;
                var stable = 0;
                while (watch.ElapsedMilliseconds < timeoutMs)
                {
                    await Task.Delay(50);
                    var now = (gridA.FrameAt(_nav.CurrentTime), pane.Grid.FrameAt(pane.Nav.CurrentTime), sync.Range);
                    stable = now == last ? stable + 1 : 0;
                    last = now;
                    if (stable >= 5 && Expected().Ok)
                        break;
                }
                return Expected();

                (bool Ok, string Detail) Expected()
                {
                    var target = sync.Target;
                    var gridB = pane.Grid;
                    var fa = gridA.FrameAt(_nav.CurrentTime);
                    var fb = gridB.FrameAt(pane.Nav.CurrentTime);
                    var hidden = !pane.Placeholder.IsVisible;
                    var frameB = 1 / pane.Fps;
                    var range = target < pane.FirstFrameStart - frameB / 2 ? CompareSync.BRange.Before
                        : target >= pane.LastFrameStart + frameB ? CompareSync.BRange.After : CompareSync.BRange.Inside;
                    var detail = $"A frame {fa} ({_nav.CurrentTime:0.000}), B frame {fb} ({pane.Nav.CurrentTime:0.000}), objetivo {target:0.000} " +
                        $"→ frame {gridB.FrameAt(target)}, {range}{(hidden ? ", B oculto" : "")}; " +
                        $"A {(_nav.Moving ? "en marcha" : _nav.Playing ? "a punto" : "quieto")} (mpv en {_player.Position:0.000}, motor {(_engine?.Showing == true ? "encima" : "oculto")}), " +
                        $"B {(pane.Nav.Moving ? "en marcha" : pane.Nav.Playing ? "a punto" : "quieto")} (mpv en {pane.Player.Position:0.000})";
                    if (range != CompareSync.BRange.Inside)
                        return (sync.Range == range && hidden && _cmpOutside.IsVisible, detail);
                    return (sync.Range == range && !hidden && fb == gridB.FrameAt(target) && !_nav.Moving && !pane.Nav.Moving, detail);
                }
            }

            var baseline = Resources();
            Log($"recursos antes de comparar: {baseline.Handles} handles, {baseline.Threads} hilos, GDI {baseline.Gdi}, USER {baseline.User}, {baseline.MemoryMb} MB");

            // ---- 1. El mismo vídeo como B, 1,25 s por delante ----
            var aStart = gridA.TimeOf(gridA.FrameAt(Math.Min(10, _duration * 0.2)));
            _nav.SeekTo(aStart);
            await Task.Delay(600);
            var offsetFrames = (int)Math.Round(1.25 * Fps);
            var bStart = gridA.TimeOf(gridA.FrameAt(aStart) + offsetFrames);
            var budget = _engine.BudgetBytes;
            var history = (_videoHistory.Count, _videoHistoryIndex);
            OpenCompare(path, bStart);
            var positions = new System.Collections.Concurrent.ConcurrentQueue<string>();
            var openClock = Stopwatch.StartNew();
            _compare!.Player.PositionChanged += t => { if (openClock.ElapsedMilliseconds < 4000) positions.Enqueue($"{openClock.ElapsedMilliseconds}:{t:0.000}"); };
            _compare.Player.PauseChanged += p => positions.Enqueue($"{openClock.ElapsedMilliseconds}:pausa={p}");
            _compare.Player.Restarted += () => positions.Enqueue($"{openClock.ElapsedMilliseconds}:restart");
            var b = await WaitCompare();
            Log("   B al abrir: " + string.Join(" ", positions));
            Check(_engine.CacheShare == 2 && b.Engine?.CacheShare == 2,
                $"caché repartida: A {_engine.CacheShare}, B {b.Engine?.CacheShare} (presupuesto {budget / (1024 * 1024)} MB, el ajuste no cambia: {_engine.BudgetBytes == budget})");
            Check(b.Engine == null || ReferenceEquals(b.Engine.Gpu, _engine.Gpu), "B usa el dispositivo de la tarjeta de A");
            Check(_compareActiveB && Math.Abs(b.Nav.CurrentTime - bStart) < 0.5 / Fps,
                $"B abierto en {b.Nav.CurrentTime:0.000} (pedido {bStart:0.000}), activo: {_compareActiveB}");
            Check((_videoHistory.Count, _videoHistoryIndex) == history, $"abrir B no entra en el historial ({_videoHistory.Count} entradas)");
            var placementA = _surface.Placement;
            var placementB = b.Surface.Placement;
            Check(placementA.W > 100 && placementB.W > 100 && placementA.X + placementA.W <= placementB.X
                    && Math.Abs(placementA.W - placementB.W) <= 1 && Math.Abs(placementA.H - placementB.H) <= 1 && placementA.Y == placementB.Y,
                $"lado a lado y del mismo tamaño: A {placementA.X},{placementA.Y} {placementA.W}×{placementA.H}, B {placementB.X},{placementB.Y} {placementB.W}×{placementB.H}");
            await Task.Delay(500);
            CaptureWindowContent(Png("lado"), Log);

            // Sueltos, el teclado mueve el activo (B) y no A.
            var aBefore = _nav.CurrentTime;
            var fbBefore = b.Grid.FrameAt(b.Nav.CurrentTime);
            for (var i = 0; i < 3; i++)
            {
                PressKey(System.Windows.Input.Key.Up);
                await Task.Delay(200);
            }
            await Task.Delay(600);
            Check(Math.Abs(_nav.CurrentTime - aBefore) < 1e-4 && b.Grid.FrameAt(b.Nav.CurrentTime) == fbBefore + 3,
                $"sueltos, ↑ con B activo mueve solo B: B {fbBefore} → {b.Grid.FrameAt(b.Nav.CurrentTime)}, A quieto");
            for (var i = 0; i < 3; i++)
            {
                PressKey(System.Windows.Input.Key.Down);
                await Task.Delay(200);
            }
            await Task.Delay(600);
            // Un clic en la imagen inactiva solo la activa.
            OnVideoClicked();
            await Task.Delay(300);
            Check(!_compareActiveB && _nav.Paused && !_nav.Moving, "clic en A (inactiva): solo la activa, sigue en pausa");
            OnCompareClicked();
            await Task.Delay(300);
            Check(_compareActiveB && b.Nav.Paused && !b.Nav.Moving, "clic en B (inactiva): solo la activa");

            // ---- 2. Bloquear ----
            var expectedOffset = b.Nav.CurrentTime - _nav.CurrentTime;
            Log($"   antes de bloquear: B {b.Grid.FrameAt(b.Nav.CurrentTime)}, A {gridA.FrameAt(_nav.CurrentTime)} (al abrir, {offsetFrames} frames de diferencia)");
            PressKey(System.Windows.Input.Key.L, ctrl: true);
            await Task.Delay(300);
            var sync = _sync!;
            Check(sync.Locked && Math.Abs(sync.Offset - expectedOffset) < 1e-3 && Math.Abs(expectedOffset - (bStart - aStart)) < 1e-3,
                $"Ctrl + L bloquea con desfase {sync.Offset:0.0000} s (B − A {expectedOffset:0.0000}, al abrir {bStart - aStart:0.0000}); texto «{_cmpOffset.Text}»");

            // ---- 3. Pasos, saltos, clics y arrastres al azar ----
            var events = new System.Collections.Concurrent.ConcurrentQueue<string>();
            Stopwatch? tracing = null;
            _player.PauseChanged += p => { if (tracing is { } w) events.Enqueue($"{w.ElapsedMilliseconds}:mpv pausa={p}"); };
            _player.Restarted += () => { if (tracing is { } w) events.Enqueue($"{w.ElapsedMilliseconds}:mpv restart"); };
            _nav.SpeedChanged += () => { if (tracing is { } w) events.Enqueue($"{w.ElapsedMilliseconds}:nav paused={_nav.Paused}"); };
            var random = new Random(20261117);
            var hits = 0;
            var total = 0;
            var kinds = new Dictionary<string, (int Hits, int Total)>();
            for (var i = 0; i < 200; i++)
            {
                string what;
                switch (random.Next(11))
                {
                    case 0 or 1:
                        what = "↑";
                        StepFrame(1);
                        break;
                    case 2 or 3:
                        what = "↓";
                        StepFrame(-1);
                        break;
                    case 4:
                        var seconds = new[] { -5.0, 5, -1, 1, 30, -30 }[random.Next(6)];
                        what = $"salto {seconds:+0;-0}";
                        _nav.SeekRelative(seconds);
                        break;
                    case 5 or 6:
                        var t = random.NextDouble() * _duration;
                        what = $"clic en la barra ({t:0.0})";
                        Seek.SimulateLeftDown(Seek.TrackPoint(t));
                        await Task.Delay(40);
                        Seek.SimulateLeftUp();
                        break;
                    case 7:
                        var (from, to) = (random.NextDouble() * _duration, random.NextDouble() * _duration);
                        what = $"arrastre en la barra ({from:0.0} → {to:0.0})";
                        Seek.SimulateLeftDrag(Seek.TrackPoint(from), Seek.TrackPoint(to), steps: 12);
                        break;
                    case 8:
                        var home = random.Next(4) == 0;
                        what = home ? "Inicio" : "Fin";
                        if (home)
                            _nav.SeekTo(0);
                        else
                            GoToLastFrame();
                        break;
                    case 9:
                        var dir = random.Next(2) == 0 ? -1 : 1;
                        what = dir > 0 ? "Alt + ." : "Alt + ,";
                        sync.NudgeB(dir);
                        break;
                    default:
                        what = "reproducir 0,4 s";
                        events.Clear();
                        tracing = Stopwatch.StartNew();
                        events.Enqueue($"0:toggle(paused={_nav.Paused},playing={_nav.Playing},motor={_engine.Showing})");
                        _nav.TogglePlay();
                        events.Enqueue($"{tracing.ElapsedMilliseconds}:tras toggle(paused={_nav.Paused},playing={_nav.Playing})");
                        await Task.Delay(400);
                        events.Enqueue($"{tracing.ElapsedMilliseconds}:toggle(paused={_nav.Paused},playing={_nav.Playing},motor={_engine.Showing})");
                        _nav.TogglePlay();
                        events.Enqueue($"{tracing.ElapsedMilliseconds}:tras toggle(paused={_nav.Paused},playing={_nav.Playing})");
                        break;
                }
                var (ok, detail) = await Settle();
                if (!ok && tracing != null && what.StartsWith("reproducir"))
                    Log("   sucesos de A: " + string.Join(" ", events));
                tracing = null;
                total++;
                if (ok)
                    hits++;
                var kind = what.Split(' ')[0];
                var k = kinds.GetValueOrDefault(kind);
                kinds[kind] = (k.Hits + (ok ? 1 : 0), k.Total + 1);
                if (!ok)
                    Log($"   fallo en {what}: {detail}");
            }
            Check(hits == total, $"bloqueados, B en el frame esperado: {hits} de {total} ({string.Join(", ", kinds.Select(p => $"{p.Key} {p.Value.Hits}/{p.Value.Total}"))})");

            // ---- 4. Reproducir casi un minuto y medir cuánto se aleja B ----
            sync.Unlock();
            var playFrom = gridA.TimeOf(gridA.FrameAt(0.3));
            _nav.SeekTo(playFrom);
            var playOffsetFrames = (int)Math.Round(0.25 * Fps);
            b.Nav.SeekTo(gridA.TimeOf(gridA.FrameAt(playFrom) + playOffsetFrames));
            await Task.Delay(1000);
            sync.Lock();
            await Settle();
            var playSeconds = Math.Min(60, (b.LastFrameStart - sync.Offset) - playFrom - 1);
            var watch = Stopwatch.StartNew();
            var seeksBefore = sync.CatchUpSeeks;
            _nav.TogglePlay();
            var drifts = new List<(double At, double Drift, int FrameDiff)>();
            var trace = new List<string>();
            var (lastFa, lastFb, lastAt) = (gridA.FrameAt(_nav.CurrentTime), b.Grid.FrameAt(b.Nav.CurrentTime), 0.0);
            while (watch.Elapsed.TotalSeconds < playSeconds)
            {
                await Task.Delay(100);
                var (fa, fb, at) = (gridA.FrameAt(_nav.CurrentTime), b.Grid.FrameAt(b.Nav.CurrentTime), watch.Elapsed.TotalSeconds);
                var fd = fb - fa - playOffsetFrames;
                drifts.Add((at, sync.LastDrift, fd));
                // Cuánto avanzó cada uno desde la muestra anterior (lo normal: lo que dura el intervalo); así se ve cuál se atasca.
                var expected = (at - lastAt) * Fps;
                trace.Add($"{at:0.0}:{sync.LastDrift * Fps:+0.00;-0.00}/{fd:+0;-0}/{(sync.Trim - 1) * 100:+0;-0}%" +
                    $"[A{fa - lastFa - expected:+0;-0} B{fb - lastFb - expected:+0;-0}]");
                (lastFa, lastFb, lastAt) = (fa, fb, at);
            }
            // Solo alrededor de donde se aleja más de un frame.
            var marks = drifts.Select((d, i) => (d, i)).Where(x => Math.Abs(x.d.Drift) * Fps > 1.2).Select(x => x.i).ToList();
            var shown = new SortedSet<int>(marks.SelectMany(i => Enumerable.Range(Math.Max(0, i - 3), 8)).Where(i => i < trace.Count));
            if (shown.Count > 0)
                Log("   deriva (s:frames/frames enseñados/% retoque) " + string.Join(" ", shown.Take(400).Select(i => trace[i])));
            _nav.TogglePlay();
            var settled = drifts.Where(d => d.At > 2).ToList();
            var frame = 1 / Fps;
            // Una muestra suelta de un frame (justo entre el aviso de un vídeo y el del otro) es del medidor, no de la imagen:
            // se toma la mediana de cada tres seguidas.
            var smooth = settled.Select((d, i) => i == 0 || i == settled.Count - 1 ? d.Drift
                : new[] { settled[i - 1].Drift, d.Drift, settled[i + 1].Drift }.Order().ElementAt(1)).ToList();
            // Tirones: uno de los dos se atasca de golpe (más de 3 frames de una muestra a otra). Desde ahí hasta que vuelve a
            // estar a un frame o menos es la corrección; lo que cuenta es la deriva fuera de esos tramos y lo que tarda en volver.
            var recovering = new bool[smooth.Count];
            var recoveries = new List<double>();
            for (var i = 0; i < smooth.Count; i++)
            {
                if (Math.Abs(smooth[i]) <= 3 * frame || i > 0 && recovering[i - 1])
                    continue;
                var start = i;
                while (i < smooth.Count && Math.Abs(smooth[i]) > frame)
                    recovering[i++] = true;
                recoveries.Add(i < smooth.Count ? settled[i].At - settled[start].At : double.PositiveInfinity);
            }
            var maxDrift = smooth.Where((_, i) => !recovering[i]).Select(Math.Abs).DefaultIfEmpty(0).Max() / frame;
            Log($"   tirones: {recoveries.Count}{(recoveries.Count > 0 ? $", vuelve a un frame o menos en {string.Join(", ", recoveries.Select(r => $"{r:0.0} s"))}" : "")}");
            var maxRaw = settled.Count > 0 ? settled.Max(d => Math.Abs(d.Drift)) / frame : double.NaN;
            var within = settled.Count > 0 ? settled.Count(d => Math.Abs(d.Drift) <= frame / 2) * 100.0 / settled.Count : 0;
            Log($"   deriva sin filtrar: hasta {maxRaw:0.00} frames; {settled.Count(d => Math.Abs(d.Drift) <= frame) * 100.0 / Math.Max(1, settled.Count):0.0}% de las muestras a un frame o menos");
            var maxFrames = settled.Count > 0 ? settled.Max(d => Math.Abs(d.FrameDiff)) : 0;
            var firstSecond = drifts.Where(d => d.At <= 2).Select(d => Math.Abs(d.Drift) / frame).DefaultIfEmpty(0).Max();
            Log($"   deriva los 2 primeros segundos: hasta {firstSecond:0.00} frames");
            Check(maxDrift <= 1 && sync.CatchUpSeeks == seeksBefore && recoveries.All(r => r <= 3),
                $"reproduciendo {playSeconds:0} s: deriva máxima {maxDrift:0.00} frames tras los 2 primeros segundos (fuera de los tirones) " +
                $"({within:0}% de las muestras a menos de medio frame; diferencia de frames enseñados hasta {maxFrames}); " +
                $"seeks para alcanzarlo: {sync.CatchUpSeeks - seeksBefore}");
            var (pausedOk, pausedDetail) = await Settle();
            Check(pausedOk, $"al pausar, B en su frame exacto: {pausedDetail}");

            // ---- 5. Fuera de B: antes de que empiece ----
            sync.Unlock();
            _nav.SeekTo(gridA.TimeOf(gridA.FrameAt(Math.Min(4, _duration / 4))));
            b.Nav.SeekTo(0);
            await Task.Delay(800);
            sync.Lock();
            _nav.SeekTo(0);
            var (beforeOk, beforeDetail) = await Settle();
            Check(beforeOk && sync.Range == CompareSync.BRange.Before && _cmpOutside.Text.Contains("B "),
                $"antes de B: «{_cmpOutside.Text}» ({beforeDetail})");
            await Task.Delay(300);
            CaptureWindowContent(Png("fuera"), Log);
            // Reproduciendo, B arranca al llegar a su principio.
            _nav.TogglePlay();
            await Task.Delay((int)(Math.Abs(sync.Offset) * 1000) + 1500);
            Check(sync.Range == CompareSync.BRange.Inside && b.Nav.Moving && b.Placeholder.IsVisible,
                $"reproduciendo, B arranca al llegar a su principio: {sync.Range}, B {(b.Nav.Moving ? "en marcha" : "quieto")}, deriva {sync.LastDrift * Fps:0.0} frames");
            _nav.TogglePlay();
            await Settle();
            sync.Unlock();

            // ---- 6. Otros fps: cada paso, al siguiente cambio de imagen de cualquiera de los dos ----
            var other = Environment.GetEnvironmentVariable("VIDEOSEEKER_OTHER") is { Length: > 0 } o && File.Exists(o) ? o
                : Directory.EnumerateFiles(Path.GetDirectoryName(path)!)
                    .Where(f => !string.Equals(f, path, StringComparison.OrdinalIgnoreCase) && f.EndsWith("hevc_ac3-5.1.mp4", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
            if (other != null)
            {
                _nav.SeekTo(gridA.TimeOf(gridA.FrameAt(5)));
                OpenCompare(other, 5.3);
                b = await WaitCompare();
                sync = _sync!;
                Log($"   B: {Path.GetFileName(other)} a {b.Fps:0.###} fps");
                if (Math.Abs(b.Fps - Fps) < 0.01)
                {
                    Log("   mismo fps que A: se salta");
                }
                else
                {
                    sync.Lock();
                    await Settle();
                    var stepsOk = 0;
                    var changesBoth = 0;
                    var steps = 0;
                    foreach (var direction in new[] { 1, -1 })
                        for (var i = 0; i < 40; i++)
                        {
                            var (fa0, fb0) = (gridA.FrameAt(_nav.CurrentTime), b.Grid.FrameAt(b.Nav.CurrentTime));
                            var virtual0 = sync.ATime;
                            StepFrame(direction);
                            var (ok, detail) = await Settle();
                            var (fa1, fb1) = (gridA.FrameAt(_nav.CurrentTime), b.Grid.FrameAt(b.Nav.CurrentTime));
                            var changed = fa1 != fa0 || fb1 != fb0;
                            // Ningún cambio de imagen de ninguno de los dos entre medias.
                            var aNext = direction > 0 ? gridA.TimeOf(fa0 + 1) : gridA.TimeOf(fa0);
                            var single = Math.Abs(fa1 - fa0) <= 1 && Math.Abs(fb1 - fb0) <= 1;
                            steps++;
                            if (ok && changed && single && (sync.ATime - virtual0) * direction > 0)
                                stepsOk++;
                            else
                                Log($"   paso {(direction > 0 ? "↑" : "↓")} {i}: A {fa0}→{fa1}, B {fb0}→{fb1}, {detail}");
                            if (fa1 != fa0 && fb1 != fb0)
                                changesBoth++;
                        }
                    Check(stepsOk == steps, $"otros fps ({Fps:0.###} y {b.Fps:0.###}): {stepsOk} de {steps} pasos al siguiente cambio de imagen " +
                        $"(en {changesBoth} cambian los dos)");
                    sync.Unlock();
                }
            }
            else
            {
                Log("   sin vídeo de otros fps (samples\\generated\\hevc_ac3-5.1.mp4 o VIDEOSEEKER_OTHER): se salta");
            }

            // ---- 7. Apilados en una ventana estrecha y alta ----
            var (oldState, oldWidth, oldHeight) = (WindowState, Width, Height);
            WindowState = WindowState.Normal;
            Width = 760;
            Height = 1000;
            await Task.Delay(800);
            placementA = _surface.Placement;
            placementB = _compare!.Surface.Placement;
            Check(_compareStacked && placementA.Y + placementA.H <= placementB.Y,
                $"ventana estrecha: uno encima del otro ({(_compareStacked ? "apilados" : "lado a lado")}; A y {placementA.Y}+{placementA.H}, B y {placementB.Y})");
            CaptureWindowContent(Png("apilado"), Log);
            (Width, Height) = (oldWidth, oldHeight);
            WindowState = oldState;
            await Task.Delay(800);

            // ---- 8. La animación del panel redimensiona las dos imágenes ----
            var animate = Reveal.Animate;
            Reveal.Animate = true;
            var widths = new List<int>();
            void Sample(object? sender, EventArgs e) => widths.Add(_compare!.Surface.Placement.W);
            System.Windows.Media.CompositionTarget.Rendering += Sample;
            ShowPanel(true);
            await Task.Delay(500);
            System.Windows.Media.CompositionTarget.Rendering -= Sample;
            var distinct = widths.Distinct().Count();
            Check(distinct >= 3, $"panel animado: la imagen de B pasa por {distinct} anchos ({widths.FirstOrDefault()} → {widths.LastOrDefault()})");
            ShowPanel(false);
            await Task.Delay(400);
            Reveal.Animate = animate;

            // ---- 9. Cerrar y abrir varias veces, sin fugas ----
            CloseCompare();
            await Task.Delay(1500);
            Check(_compare == null && _engine.CacheShare == 1 && VideoGrid.ColumnDefinitions.Count + VideoGrid.RowDefinitions.Count == 0
                && VideoArea.Margin == new Thickness(0),
                $"cerrar: vista de uno, caché entera para A ({_engine.CacheShare})");
            var warm = Resources();
            var perCycle = new List<(int Handles, int Threads, long Memory)>();
            var openPeaks = new List<long>();
            const int Cycles = 6;
            for (var i = 0; i < Cycles; i++)
            {
                OpenCompare(path, Math.Min(_duration / 2, 20 + i));
                await WaitCompare();
                if (i % 2 == 0)
                {
                    PressKey(System.Windows.Input.Key.L, ctrl: true);
                    _nav.TogglePlay();
                    await Task.Delay(1000);
                    _nav.TogglePlay();
                }
                var cacheB = _compare?.Engine?.CacheUsedBytes ?? 0;
                var open = Resources();
                openPeaks.Add(open.MemoryMb - _engine.CacheUsedBytes / (1024 * 1024));
                var closing = _compare!;
                CloseCompare();
                for (var w = 0; w < 50 && !closing.Closed; w++)
                    await Task.Delay(100);
                await Task.Delay(500);
                var r = Resources();
                perCycle.Add((r.Handles, r.Threads, r.MemoryMb - _engine.CacheUsedBytes / (1024 * 1024)));
                Log($"   ciclo {i + 1}: cerrado {(closing.Closed ? "del todo" : "a medias")}{(closing.CloseError != null ? $" ({closing.CloseError.Message})" : "")}; " +
                    $"{r.Handles} handles, {r.Threads} hilos, {r.MemoryMb} MB; caché de A {_engine.CacheUsedBytes / (1024 * 1024)} MB, la de B era {cacheB / (1024 * 1024)} MB");
            }
            await Task.Delay(1500);
            var after = Resources();
            Log($"recursos tras el primer cierre: {warm.Handles} handles, {warm.Threads} hilos, GDI {warm.Gdi}, USER {warm.User}, {warm.MemoryMb} MB");
            Log($"recursos tras {Cycles} aperturas más: {after.Handles} handles, {after.Threads} hilos, GDI {after.Gdi}, USER {after.User}, {after.MemoryMb} MB");
            // La memoria privada cuenta también la de la tarjeta (las cachés de frames), y el driver la devuelve cuando quiere:
            // sube y baja de un ciclo a otro. Una fuga haría que tras cerrar quedara más que con los dos abiertos.
            var worstAfter = perCycle.Skip(1).Max(c => c.Memory);
            var peakOpen = openPeaks.Max();
            Check(after.Handles - warm.Handles < 60 && after.Threads - warm.Threads < 6 && after.Gdi - warm.Gdi < 10 && after.User - warm.User < 10
                    && worstAfter <= peakOpen + 300,
                $"sin fugas al abrir y cerrar {Cycles} veces: handles {after.Handles - warm.Handles:+0;-0}, hilos {after.Threads - warm.Threads:+0;-0}, " +
                $"GDI {(int)after.Gdi - (int)warm.Gdi:+0;-0}, USER {(int)after.User - (int)warm.User:+0;-0}; memoria sin la caché de A: " +
                $"con los dos abiertos hasta {peakOpen} MB, tras cerrar hasta {worstAfter} MB ({string.Join(", ", perCycle.Select(c => c.Memory))})");
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            CloseCompare(immediate: true);
            Log(failures == 0 ? "TODO BIEN" : $"{failures} FALLOS");
            File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
            Close();
        }
    }

    // Una tecla como si se pulsara con el teclado (por OnPreviewKeyDown), con Ctrl si se pide.
    private void PressKey(System.Windows.Input.Key key, bool ctrl = false)
    {
        if (ctrl)
        {
            // Los modificadores de WPF salen del teclado de verdad: se llama directamente a lo que hace la combinación.
            if (key == System.Windows.Input.Key.L)
                ToggleCompareLock();
            return;
        }
        var source = PresentationSource.FromVisual(this)!;
        var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
        };
        OnPreviewKeyDown(this, args);
    }
}
