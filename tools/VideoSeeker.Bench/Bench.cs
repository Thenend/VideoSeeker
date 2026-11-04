using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using VideoSeeker.Playback;

namespace VideoSeeker.Bench;

public sealed record FileResult(
    string Name, Probe Probe, string? Hwdec,
    double FirstFrameMs, double GopEndSeekMs,
    double[] BackStepMs, double[] ForwardStepMs, double[] ForwardSeekStepMs,
    double[] RandomExactSeekMs, double[] RandomKeyframeSeekMs);

/// <summary>Mide en mpv la latencia de abrir, buscar y avanzar/retroceder frame a frame.</summary>
public sealed class Bench(Mpv mpv, Action<string> status)
{
    public static readonly (string, string)[] MpvOptions =
    [
        ("config", "no"), ("load-scripts", "no"), ("terminal", "no"),
        ("vo", "gpu-next"), ("gpu-api", "d3d11"), ("hwdec", "auto-safe"),
        ("idle", "yes"), ("keep-open", "yes"), ("pause", "yes"), ("mute", "yes"),
        ("osc", "no"), ("osd-level", "0"), ("input-default-bindings", "no"), ("input-vo-keyboard", "no"),
        ("cache", "yes"), ("demuxer-max-bytes", "512MiB"), ("demuxer-max-back-bytes", "512MiB"),
        ("hr-seek", "yes"),
    ];

    private const int Steps = 8;
    private const double TimeoutSeconds = 5;
    private const ulong TimePosId = 1;

    // Con BENCH_TRACE=1 se vuelcan los eventos de mpv a bench-trace.log para diagnosticar.
    private static readonly StreamWriter? Trace =
        Environment.GetEnvironmentVariable("BENCH_TRACE") == "1" ? new StreamWriter("bench-trace.log") { AutoFlush = true } : null;

    private static readonly string[] VideoExtensions = [".mkv", ".mp4", ".avi", ".wmv", ".webm", ".mov"];

    public static string[] FindVideos(string dir) =>
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Order()
            .ToArray();

    public List<FileResult> Run(IEnumerable<string> files)
    {
        mpv.ObserveDouble(TimePosId, "time-pos");
        var results = new List<FileResult>();
        foreach (var file in files)
        {
            status($"Midiendo {Path.GetFileName(file)}…");
            results.Add(RunFile(file));
        }
        return results;
    }

    private FileResult RunFile(string path)
    {
        var probe = Probe.Run(path);
        Drain();

        var sw = Stopwatch.StartNew();
        mpv.Command("loadfile", path);
        WaitFor(e => e.Id == MpvEventId.FileLoaded);
        var firstFrame = WaitFor(e => e.Id == MpvEventId.PlaybackRestart) ? sw.Elapsed.TotalMilliseconds : double.NaN;
        var hwdec = mpv.GetString("hwdec-current");

        // Peor caso: justo antes del siguiente keyframe, cada paso atrás decodifica casi todo el GOP.
        var gopEnd = Math.Max(0, probe.NextKeyframe - 2 * probe.FrameDuration);
        var gopEndSeek = TimeSeek(gopEnd, "absolute+exact");

        var back = Series(() => TimeCommand(MpvEventId.PlaybackRestart, "frame-back-step"));
        var forward = Series(() => TimeFrameStep("frame-step"));
        // Variante que avanza buscando el frame en vez de reproducirlo (no espera al ritmo de pantalla).
        var forwardSeek = Series(() => TimeCommand(MpvEventId.PlaybackRestart, "frame-step", "1", "seek"));

        var rnd = new Random(42);
        var exact = new double[5];
        var keyframe = new double[5];
        for (var i = 0; i < 5; i++)
        {
            exact[i] = TimeSeek(probe.Duration * (0.05 + 0.9 * rnd.NextDouble()), "absolute+exact");
            keyframe[i] = TimeSeek(probe.Duration * (0.05 + 0.9 * rnd.NextDouble()), "absolute+keyframes");
        }

        return new FileResult(Path.GetFileName(path), probe, hwdec, firstFrame, gopEndSeek, back, forward, forwardSeek, exact, keyframe);
    }

    // Repite una medición; si una da timeout, el resto de la serie se marca igual para no esperar en vano.
    private static double[] Series(Func<double> measure)
    {
        var values = Enumerable.Repeat(double.NaN, Steps).ToArray();
        for (var i = 0; i < Steps; i++)
        {
            values[i] = measure();
            if (double.IsNaN(values[i]))
                break;
        }
        return values;
    }

    private double TimeSeek(double seconds, string flags) =>
        TimeCommand(MpvEventId.PlaybackRestart, "seek", seconds.ToString("0.000", CultureInfo.InvariantCulture), flags);

    private double TimeCommand(MpvEventId until, params string[] command)
    {
        Drain();
        var sw = Stopwatch.StartNew();
        mpv.Command(command);
        return WaitFor(e => e.Id == until) ? sw.Elapsed.TotalMilliseconds : double.NaN;
    }

    // frame-step no es un seek: se considera hecho cuando cambia time-pos.
    private double TimeFrameStep(params string[] command)
    {
        Drain();
        var before = mpv.GetDouble("time-pos");
        Trace?.WriteLine($"frame-step desde {before}");
        var sw = Stopwatch.StartNew();
        mpv.Command(command);
        var ok = WaitFor(e => e.Id == MpvEventId.PropertyChange && e.ReplyUserdata == TimePosId
                              && e.DoubleValue is { } v && v != before);
        return ok ? sw.Elapsed.TotalMilliseconds : double.NaN;
    }

    private bool WaitFor(Func<MpvEvent, bool> predicate)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed.TotalSeconds < TimeoutSeconds)
        {
            var e = mpv.WaitEvent(TimeoutSeconds - deadline.Elapsed.TotalSeconds);
            if (e.Id == MpvEventId.None)
                continue;
            Trace?.WriteLine($"{deadline.Elapsed.TotalMilliseconds,8:0.0} {e.Id} {e.PropertyName} {e.DoubleValue}");
            if (predicate(e))
                return true;
        }
        return false;
    }

    private void Drain()
    {
        while (mpv.WaitEvent(0).Id != MpvEventId.None) { }
    }

    public static string Report(List<FileResult> results, double windowShownMs, double mpvInitMs)
    {
        static string Ms(double v) => double.IsNaN(v) ? "timeout" : v.ToString("0", CultureInfo.InvariantCulture);
        static double Median(double[] v) { var s = v.Order().ToArray(); return s[s.Length / 2]; }

        var sb = new StringBuilder();
        sb.AppendLine("# Fase 0: mediciones de mpv");
        sb.AppendLine();
        sb.AppendLine($"Generado por `tools/VideoSeeker.Bench` el {DateTime.Now:yyyy-MM-dd HH:mm}. WPF + libmpv en HwndHost, `vo=gpu-next`, `gpu-api=d3d11`, `hwdec=auto-safe`, caché de demuxer de 512 MiB adelante y atrás.");
        sb.AppendLine();
        sb.AppendLine($"- Ventana visible desde el arranque del proceso: {Ms(windowShownMs)} ms");
        sb.AppendLine($"- Inicialización de mpv: {Ms(mpvInitMs)} ms");
        sb.AppendLine();
        sb.AppendLine("Tiempos en ms. Paso atrás y adelante: mediana / máximo de 8 pasos seguidos empezando al final de un GOP (peor caso para ir atrás).");
        sb.AppendLine();
        sb.AppendLine("| Archivo | Vídeo | GOP (s) | hwdec | Primer frame | Seek al final del GOP | Paso atrás | Paso adelante | Paso adelante (modo seek) | Seek exacto aleatorio (med / máx) | Seek a keyframe (med) |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var r in results)
        {
            var p = r.Probe;
            sb.AppendLine($"| {r.Name} | {p.Codec} {p.Width}x{p.Height} {p.PixFmt} {p.Fps:0.###} fps | {p.Gop:0.0} | {r.Hwdec ?? "no"} | {Ms(r.FirstFrameMs)} | {Ms(r.GopEndSeekMs)} | {Ms(Median(r.BackStepMs))} / {Ms(r.BackStepMs.Max())} | {Ms(Median(r.ForwardStepMs))} / {Ms(r.ForwardStepMs.Max())} | {Ms(Median(r.ForwardSeekStepMs))} / {Ms(r.ForwardSeekStepMs.Max())} | {Ms(Median(r.RandomExactSeekMs))} / {Ms(r.RandomExactSeekMs.Max())} | {Ms(Median(r.RandomKeyframeSeekMs))} |");
        }
        sb.AppendLine();
        sb.AppendLine("Pasos atrás individuales (ms):");
        sb.AppendLine();
        foreach (var r in results)
            sb.AppendLine($"- {r.Name}: {string.Join(", ", r.BackStepMs.Select(Ms))}");
        return sb.ToString();
    }
}

/// <summary>Datos del vídeo y posición de keyframes obtenidos con ffprobe.</summary>
public sealed record Probe(string Codec, int Width, int Height, string PixFmt, double Fps, double Duration,
    double Keyframe, double NextKeyframe)
{
    public double FrameDuration => 1 / Fps;
    public double Gop => NextKeyframe - Keyframe;

    public static Probe Run(string path)
    {
        using var info = JsonDocument.Parse(FfProbe(
            "-v error -select_streams v:0 -show_entries stream=codec_name,width,height,pix_fmt,r_frame_rate:format=duration -of json", path));
        var stream = info.RootElement.GetProperty("streams")[0];
        var duration = double.Parse(info.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        var rate = stream.GetProperty("r_frame_rate").GetString()!.Split('/');
        var fps = double.Parse(rate[0], CultureInfo.InvariantCulture) / double.Parse(rate[1], CultureInfo.InvariantCulture);

        // Dos keyframes consecutivos a partir del 30 % del vídeo.
        var start = duration * 0.3;
        var keyframes = FfProbe(
                $"-v error -select_streams v:0 -skip_frame nokey -read_intervals {start.ToString("0.000", CultureInfo.InvariantCulture)}%+60 -show_entries frame=best_effort_timestamp_time -of csv=p=0", path)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => double.TryParse(l.TrimEnd(','), CultureInfo.InvariantCulture, out var t) ? t : double.NaN)
            .Where(t => !double.IsNaN(t) && t >= start)
            .ToArray();
        var k1 = keyframes.Length > 0 ? keyframes[0] : start;
        var k2 = keyframes.Length > 1 ? keyframes[1] : Math.Min(duration, k1 + 10);

        return new Probe(stream.GetProperty("codec_name").GetString()!, stream.GetProperty("width").GetInt32(),
            stream.GetProperty("height").GetInt32(), stream.TryGetProperty("pix_fmt", out var pf) ? pf.GetString()! : "?",
            fps, duration, k1, k2);
    }

    private static string FfProbe(string args, string path)
    {
        var psi = new ProcessStartInfo("ffprobe", $"{args} \"{path}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
