using System.Diagnostics;
using System.IO;
using VideoSeeker.Inspect;

namespace VideoSeeker.App;

/// <summary>
/// Prueba automática del motor de inspección (VIDEOSEEKER_SELFTEST=1): pausa al 30 % del vídeo, espera a
/// la precarga y recorre frames hacia atrás y hacia delante por los mismos caminos que las flechas,
/// midiendo cuánto tarda cada paso. Escribe selftest.log en la carpeta de datos y cierra la app.
/// </summary>
public sealed class SelfTest(Navigator nav, Func<InspectionEngine?> engine, double duration, string logPath, Action close,
    Action<double, double> zoom)
{
    public static bool Enabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") is "1" or "resume" or "drag" or "reverse" or "zoom" or "loop" or "longdrag";
    // Modo "longdrag": arrastres largos hacia delante (de horas en vídeos largos), como hace un usuario.
    private static bool LongDragMode => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "longdrag";
    // Modo "loop": tramo de 2 s precargado en bucle; mide si el motor pinta todos los frames a tiempo.
    private static bool LoopMode => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "loop";
    // Modo "zoom": amplía ±5 s, espera a que se precargue el tramo y hace clic en frames sueltos de él.
    private static bool ZoomMode => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "zoom";
    // Modo "drag": simula arrastrar la barra despacio y mide cuánto se queda atrás la imagen.
    private static bool DragMode => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "drag";
    // Modo "resume": se aleja 2,5 s con las flechas y reanuda, para capturar en ráfaga si hay fogonazo.
    // Modo "reverse": marcha atrás (J) a 1×, 2×, 4× y 8×, contando cuántos frames se ven por segundo.
    private static bool ReverseMode => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "reverse";
    private static bool ResumeMode => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "resume";

    private readonly List<string> _lines = [];

    public async Task RunAsync()
    {
        try
        {
            while (engine() == null)
                await Task.Delay(100);

            // Marcha atrás necesita 30 s de vídeo por detrás.
            var start = ReverseMode ? Math.Max(duration * 0.3, duration - 20) : duration * 0.3;
            nav.Pause();
            nav.SeekAbsolute(start, exact: true);
            Log($"pausa en {start:0.000} s; esperando la precarga");
            await Task.Delay(4000);
            Log($"precargado: {string.Join(", ", engine()!.CachedRanges().Select(r => $"{r.Start:0.00}-{r.End:0.00}"))}");

            if (DragMode)
            {
                await Drag(start, 0.05, 150);   // 3 s de vídeo en ~2,3 s de arrastre
                await Drag(nav.CurrentTime, 0.3, 60);   // rápido: 18 s de vídeo en ~1 s
                await Drag(nav.CurrentTime, -0.1, 60);  // hacia atrás: 6 s en ~1 s
                // Con la precarga ya hecha, saltos cortos hacia atrás (el caso en que se quedaba atascado).
                foreach (var back in new[] { 4.0, 2.0, 7.0 })
                {
                    await Task.Delay(2500);
                    await Jump(nav.CurrentTime - back);
                }
                await Wiggle(duration * 0.75);
                File.WriteAllLines(logPath, _lines);
                close();
                return;
            }

            if (LongDragMode)
            {
                await LongDrags(start);
                File.WriteAllLines(logPath, _lines);
                close();
                return;
            }

            if (LoopMode)
            {
                await LoopPlayback(start);
                File.WriteAllLines(logPath, _lines);
                close();
                return;
            }

            if (ZoomMode)
            {
                await ZoomClicks(start);
                // Un tramo más ancho que la ventana normal (±5 s): si cabe en la caché, se precarga entero.
                var (wideStart, wideEnd) = (Math.Max(0, start - 15), Math.Min(duration, start + 15));
                zoom(wideStart, wideEnd);
                await Task.Delay(8000);
                var covered = engine()!.CachedRanges().Sum(r => Math.Max(0, Math.Min(r.End, wideEnd) - Math.Max(r.Start, wideStart)));
                Log($"tramo ancho {wideStart:0.00}-{wideEnd:0.00}: {covered / (wideEnd - wideStart) * 100:0}% precargado a los 8 s");
                File.WriteAllLines(logPath, _lines);
                close();
                return;
            }

            if (ReverseMode)
            {
                for (var speed = 1; speed <= 8; speed *= 2)
                {
                    var from = nav.CurrentTime;
                    var frames = nav.EngineFrames;
                    var sw = Stopwatch.StartNew();
                    nav.Backward();
                    await Task.Delay(2000);
                    var seconds = sw.Elapsed.TotalSeconds;
                    Log($"marcha atrás a {speed}×: {(nav.EngineFrames - frames) / seconds:0.0} frames/s del motor, " +
                        $"{(from - nav.CurrentTime) / seconds:0.00} s de vídeo por segundo");
                }
                nav.Pause();
                await Task.Delay(500);
                File.WriteAllLines(logPath, _lines);
                close();
                return;
            }

            if (ResumeMode)
            {
                await Steps(+1, 150);
                Log($"RESUME {nav.CurrentTime:0.000}");
                await Task.Delay(1500);
                nav.TogglePlay();
                await Task.Delay(3000);
                File.WriteAllLines(logPath, _lines);
                close();
                return;
            }

            // Como mantener la tecla pulsada (unas 30 repeticiones por segundo) durante un buen rato.
            await Steps(-1, 300);
            await Steps(+1, 300);

            Log($"OVERLAY {nav.CurrentTime:0.000}");
            await Task.Delay(4000);
            // mpv al mismo frame, para comparar colores entre su imagen y la nuestra.
            nav.SeekAbsolute(nav.CurrentTime, exact: true);
            await Task.Delay(1500);
            Log($"MPV {nav.CurrentTime:0.000} motor visible={engine()!.Showing}");
            await Task.Delay(4000);
        }
        catch (Exception e)
        {
            Log($"ERROR {e}");
        }
        File.WriteAllLines(logPath, _lines);
        close();
    }

    private async Task Steps(int direction, int count)
    {
        var times = new List<double>();
        var fromCache = 0;
        for (var i = 0; i < count; i++)
        {
            var sw = Stopwatch.StartNew();
            nav.Step(direction);
            times.Add(sw.Elapsed.TotalMilliseconds);
            if (engine()!.Showing)
                fromCache++;
            await Task.Delay(33);
        }
        times.Sort();
        Log($"{count} pasos {(direction < 0 ? "atrás" : "adelante")}: {fromCache} desde caché, mediana {times[count / 2]:0.0} ms, máximo {times[^1]:0.0} ms, posición final {nav.CurrentTime:0.000}");
    }

    private async Task LoopPlayback(double from)
    {
        var (start, end) = (from, Math.Min(duration, from + 2));
        zoom(start, end);
        await Task.Delay(3000);
        nav.SetLoop((start, end));
        nav.SeekTo(start);
        await Task.Delay(300);
        var changes = new List<(long Ms, double T)>();
        var sw = Stopwatch.StartNew();
        void OnShown(double t) => changes.Add((sw.ElapsedMilliseconds, t));
        nav.PositionShown += OnShown;
        nav.TogglePlay();
        var looping = nav.LoopPlaying;
        await Task.Delay(6000);
        nav.PositionShown -= OnShown;
        nav.TogglePlay();
        var outside = changes.Count(c => c.T >= end - 1e-4 || c.T < start - 0.5 / nav.Fps);
        Log($"frames fuera del tramo durante el bucle: {outside}");
        var wraps = changes.Zip(changes.Skip(1)).Count(p => p.Second.T < p.First.T - 0.5);
        var gaps = changes.Zip(changes.Skip(1)).Select(p => p.Second.Ms - p.First.Ms).OrderBy(g => g).ToList();
        var skipped = changes.Zip(changes.Skip(1)).Count(p => p.Second.T > p.First.T + 1.5 / nav.Fps);
        foreach (var (a, b) in changes.Zip(changes.Skip(1)).Where(p => p.Second.Ms - p.First.Ms > 60 || p.Second.T > p.First.T + 1.5 / nav.Fps).Take(12))
            Log($"  hueco: {a.T:0.000} → {b.T:0.000} en {b.Ms - a.Ms} ms (a los {a.Ms} ms)");
        Log($"bucle de 2 s desde la caché={looping}: {changes.Count / 6.0:0.0} frames/s (fps del vídeo {nav.Fps:0.##}), " +
            $"{wraps} vueltas, {skipped} saltos de más de un frame, hueco máximo entre frames {(gaps.Count > 0 ? gaps[^1] : 0)} ms; " +
            $"al parar: pausa={nav.Paused}, en {nav.CurrentTime:0.000}");
        await Task.Delay(1500);
        nav.TogglePlay();
        await Task.Delay(1500);
        Log($"reanudar tras parar: bucle desde la caché={nav.LoopPlaying}, se ve {nav.CurrentTime:0.000}");
        nav.SetLoop(null);
        await Task.Delay(1500);
        Log($"quitar el bucle reproduciendo: bucle desde la caché={nav.LoopPlaying}, pausa={nav.Paused}, se ve {nav.CurrentTime:0.000}");

        // Doble clic en un bookmark que contiene al del bucle mientras este se reproduce desde la caché: el tramo de
        // fuera aún no está entero en caché, pero su primer frame sí. La imagen se quedaba congelada en él con el audio en marcha.
        nav.Pause();
        (double Start, double End) inner = (start + 0.5, start + 1.5), outer = (start, Math.Min(duration, start + 60));
        nav.SetRegion(inner);
        nav.SetLoop(inner);
        nav.SeekTo(inner.Start);
        await Task.Delay(300);
        nav.TogglePlay();
        await Task.Delay(1500);
        var innerLooping = nav.LoopPlaying;
        nav.SetRegion(outer);
        nav.SetLoop(outer);
        nav.SeekTo(outer.Start);
        await Task.Delay(2500);
        var at = nav.CurrentTime;
        Log($"bucle interior desde la caché={innerLooping}; tras el doble clic en el exterior: pausa={nav.Paused}, se ve {at:0.000} " +
            $"({(at > outer.Start + 1 ? "avanza" : "CONGELADO")})");
        nav.SetLoop(null);
        nav.SetRegion(null);
        nav.Pause();
    }

    // "Cualquier frame de 10 s a un clic": amplía ±5 s y salta a 40 instantes al azar del tramo.
    private async Task ZoomClicks(double center)
    {
        var (start, end) = (Math.Max(0, center - 5), Math.Min(duration, center + 5));
        zoom(start, end);
        var sw = Stopwatch.StartNew();
        double Covered() => engine()!.CachedRanges().Sum(r => Math.Max(0, Math.Min(r.End, end) - Math.Max(r.Start, start)));
        while (Covered() < (end - start) * 0.98 && sw.ElapsedMilliseconds < 15000)
            await Task.Delay(50);
        Log($"tramo {start:0.00}-{end:0.00}: {Covered() / (end - start) * 100:0}% precargado en {sw.ElapsedMilliseconds} ms; " +
            $"caché: {string.Join(", ", engine()!.CachedRanges().Select(r => $"{r.Start:0.00}-{r.End:0.00}"))}");

        var random = new Random(1);
        var instant = 0;
        var times = new List<double>();
        for (var i = 0; i < 40; i++)
        {
            var target = start + random.NextDouble() * (end - start);
            var click = Stopwatch.StartNew();
            nav.SeekTo(target);
            if (Math.Abs(nav.CurrentTime - target) <= 0.5 / nav.Fps + 1e-6)
                instant++;
            // mpv va al frame que empieza en o después del instante: puede quedar casi un frame más allá.
            while (Math.Abs(nav.CurrentTime - target) > 1.0 / nav.Fps + 1e-6 && click.ElapsedMilliseconds < 8000)
                await Task.Delay(5);
            times.Add(click.Elapsed.TotalMilliseconds);
            if (click.ElapsedMilliseconds >= 8000)
                Log($"clic en {target:0.000} no llega: se ve {nav.CurrentTime:0.000}, motor visible={engine()!.Showing}");
            else if (click.ElapsedMilliseconds > 50)
                Log($"clic en {target:0.000} fuera de la caché: llega en {click.ElapsedMilliseconds} ms");
            await Task.Delay(100);
        }
        times.Sort();
        await MouseClicksAhead();
        Log($"40 clics en el tramo: {instant} al momento desde la caché; mediana {times[20]:0.0} ms, máximo {times[^1]:0.0} ms");
    }

    private async Task LongDrags(double from)
    {
        // Barridos hacia delante a distintas velocidades (fracción del vídeo por tick de 15 ms), soltando al final.
        foreach (var (fraction, ticks) in new[] { (0.002, 100), (0.0005, 150), (0.00005, 150) })
        {
            from = nav.CurrentTime;
            var step = duration * fraction;
            var stuckTicks = 0;
            var previous = nav.CurrentTime;
            var lags = new List<double>();
            for (var i = 1; i <= ticks; i++)
            {
                var target = Math.Min(duration - 5, from + i * step);
                nav.DragTo(target);
                await Task.Delay(15);
                lags.Add(target - nav.CurrentTime);
                if (Math.Abs(nav.CurrentTime - previous) < 1e-6)
                    stuckTicks++;
                previous = nav.CurrentTime;
            }
            var last = Math.Min(duration - 5, from + ticks * step);
            var sw = Stopwatch.StartNew();
            nav.SeekTo(last);
            while (Math.Abs(nav.CurrentTime - last) > 1.0 / nav.Fps && sw.ElapsedMilliseconds < 6000)
                await Task.Delay(10);
            lags.Sort();
            Log($"barrido de {step:0.0} s por tick ({ticks} ticks): imagen quieta en {stuckTicks} ticks, retraso mediano {lags[lags.Count / 2]:0.0} s, " +
                $"máximo {lags[^1]:0.0} s; al soltar en {last:0.000} {(Math.Abs(nav.CurrentTime - last) <= 1.0 / nav.Fps ? $"llega en {sw.ElapsedMilliseconds} ms" : $"NO LLEGA, se ve {nav.CurrentTime:0.000}")}");
            await Task.Delay(1500);
        }

        // Clic lejos hacia delante y play: debe reproducir desde ahí.
        var far = Math.Min(duration - 60, nav.CurrentTime + duration * 0.2);
        nav.DragTo(far);
        await Task.Delay(80);
        nav.SeekTo(far);
        await Task.Delay(1500);
        var seen = nav.CurrentTime;
        nav.TogglePlay();
        await Task.Delay(1500);
        Log($"clic lejos en {far:0.000}: se ve {seen:0.000}; tras play 1,5 s, en {nav.CurrentTime:0.000}");
        nav.TogglePlay();
        await Task.Delay(500);
    }

    // Clics de ratón (pulsar y soltar sin mover) 4 s por delante, fuera de la caché: la imagen debe ir directa
    // al destino, sin enseñar frames intermedios.
    private async Task MouseClicksAhead()
    {
        var intermediate = 0;
        var slowest = 0L;
        for (var i = 0; i < 6; i++)
        {
            await Task.Delay(1500);
            var from = nav.CurrentTime;
            var target = Math.Min(duration - 1, from + 4);
            var shown = new List<double>();
            void OnShown(double t) => shown.Add(t);
            nav.PositionShown += OnShown;
            var sw = Stopwatch.StartNew();
            nav.DragTo(target);
            await Task.Delay(80);
            nav.SeekTo(target);
            while (Math.Abs(nav.CurrentTime - target) > 1.0 / nav.Fps && sw.ElapsedMilliseconds < 6000)
                await Task.Delay(5);
            nav.PositionShown -= OnShown;
            slowest = Math.Max(slowest, sw.ElapsedMilliseconds);
            intermediate += shown.Count(t => Math.Abs(t - target) > 1.0 / nav.Fps && Math.Abs(t - from) > 1e-3);
        }
        Log($"6 clics de ratón 4 s por delante: {intermediate} frames intermedios enseñados; el más lento llega en {slowest} ms");
    }

    // Como un usuario: arrastre rápido hasta una zona, se para un momento y se mueve despacio adelante y atrás.
    private async Task Wiggle(double target)
    {
        var from = target - 15;
        for (var i = 0; i <= 30; i++)
        {
            nav.DragTo(from + i * 0.5);
            await Task.Delay(15);
        }
        await Task.Delay(1500);
        var engineBefore = nav.EngineFrames;
        var ticks = 0;
        for (var round = 0; round < 3; round++)
            foreach (var offset in Enumerable.Range(-40, 81).Select(k => k * 0.025).Concat(Enumerable.Range(-40, 81).Reverse().Select(k => k * 0.025)))
            {
                nav.DragTo(target + offset);
                ticks++;
                await Task.Delay(25);
            }
        nav.SeekTo(target);
        await Task.Delay(500);
        Log($"vaivén de ±1 s tras pararse: {nav.EngineFrames - engineBefore} de {ticks} posiciones desde la caché");
    }

    // Arrastre corto hasta "target" y soltar: cuánto tarda en verse ese frame.
    private async Task Jump(double target)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 5; i++)
        {
            nav.DragTo(target + (5 - i) * 0.02);
            await Task.Delay(15);
        }
        nav.SeekTo(target);
        while (Math.Abs(nav.CurrentTime - target) > 0.5 / nav.Fps && sw.ElapsedMilliseconds < 6000)
            await Task.Delay(10);
        Log($"salto atrás a {target:0.000}: {(Math.Abs(nav.CurrentTime - target) <= 0.5 / nav.Fps ? $"llega en {sw.ElapsedMilliseconds} ms" : $"NO LLEGA, se ve {nav.CurrentTime:0.000}")}");
    }

    private async Task Drag(double from, double stepSeconds, int ticks)
    {
        var lags = new List<double>();
        var restarts = nav.MpvRestarts;
        var engineFrames = nav.EngineFrames;
        var backwards = 0;
        var previous = nav.CurrentTime;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < ticks; i++)
        {
            var target = from + i * stepSeconds;
            nav.DragTo(target);
            await Task.Delay(15);
            lags.Add(Math.Abs(nav.CurrentTime - target));
            // La imagen nunca debería ir en sentido contrario al arrastre.
            if ((nav.CurrentTime - previous) * Math.Sign(stepSeconds) < -0.001)
                backwards++;
            previous = nav.CurrentTime;
        }
        var engineShownPerSecond = (nav.EngineFrames - engineFrames) / sw.Elapsed.TotalSeconds;
        var shownPerSecond = (nav.MpvRestarts - restarts) / sw.Elapsed.TotalSeconds;
        var last = from + (ticks - 1) * stepSeconds;
        var released = Stopwatch.StartNew();
        nav.SeekTo(last);
        while (Math.Abs(nav.CurrentTime - last) > 0.5 / nav.Fps && released.ElapsedMilliseconds < 5000)
            await Task.Delay(10);
        var arrival = released.ElapsedMilliseconds;
        await Task.Delay(1000);
        lags.Sort();
        Log($"arrastre de {stepSeconds * 1000:0} ms por tick: {shownPerSecond:0.0} frames exactos de mpv por segundo, " +
            $"{engineShownPerSecond:0.0} del motor; retraso mediano {lags[lags.Count / 2]:0.00} s, máximo {lags[^1]:0.00} s; " +
            $"{backwards} retrocesos; al soltar en {last:0.000} llega en {arrival} ms y se ve {nav.CurrentTime:0.000}");
    }

    private void Log(string line)
    {
        _lines.Add($"{DateTime.Now:HH:mm:ss.fff} {line}");
        File.WriteAllLines(logPath, _lines);
    }
}
