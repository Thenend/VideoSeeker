using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using VideoSeeker.Inspect;

namespace VideoSeeker.App;

/// <summary>Las maneras de exportar un tramo.</summary>
public enum ClipPreset
{
    /// <summary>Recodificado con el mismo códec y el bitrate del tramo: empieza y acaba en el frame exacto.</summary>
    Exact,
    /// <summary>Copia directa, sin pérdida e instantánea, pero empieza en el keyframe en o antes del inicio.</summary>
    Copy,
    /// <summary>H.264 + AAC en MP4, del tamaño elegido (≈1 MB/s), para chats y Jira.</summary>
    Light,
}

/// <summary>Un tramo de vídeo para exportar como archivo.</summary>
public sealed class ClipJob
{
    public required string Source { get; init; }
    public required double Start { get; init; }
    /// <summary>Exclusivo, como en los tramos y las regiones: el inicio del frame siguiente al último.</summary>
    public required double End { get; init; }
    public required ClipPreset Preset { get; init; }
    /// <summary>Para el nombre del archivo: el del bookmark de región; null, los tiempos.</summary>
    public string? Name { get; init; }
    /// <summary>Copia directa: el keyframe en o antes de Start donde empieza de verdad; null si no se sabe.</summary>
    public double? Keyframe { get; init; }
    /// <summary>Ligero: la pista de audio que se oye (índice de FFmpeg); null, la primera.</summary>
    public int? AudioStream { get; init; }
    /// <summary>Carpeta de destino; null, junto al vídeo.</summary>
    public string? Folder { get; init; }
    public double LightMegabytesPerSecond { get; init; } = 1;
    /// <summary>Comparando: el segundo vídeo, que sale junto al primero en el mismo clip (null, un vídeo solo).</summary>
    public CompareClip? Compare { get; init; }
    public bool HardwareDecoding { get; init; } = true;
    /// <summary>
    /// Velocidad del clip (1, la normal): por debajo de 1, a cámara lenta. Cada frame original dura más (ni se inventan
    /// ni se repiten frames) y el sonido se estira sin cambiar de tono, como se oye al reproducir a esa velocidad.
    /// </summary>
    public double Speed { get; init; } = 1;
    /// <summary>A otra velocidad, sin sonido (el vídeo está silenciado).</summary>
    public bool NoAudio { get; init; }

    /// <summary>El archivo que se está escribiendo (o el que quedó).</summary>
    public string? Output { get; internal set; }
    /// <summary>De 0 a 1.</summary>
    public double Progress { get; internal set; }
    public bool Cancelled => Cancel.IsCancellationRequested;
    internal CancellationTokenSource Cancel { get; } = new();
}

/// <summary>
/// El segundo vídeo de un clip de los dos (comparando): su tramo es el del primero más el desfase, y sale a su lado
/// como se ven (lado a lado o uno encima del otro, en su sitio). Lo que cae fuera de él sale en negro.
/// </summary>
public sealed class CompareClip
{
    public required string Source { get; init; }
    /// <summary>Dónde empieza su tramo (puede ser negativo o pasar de su final).</summary>
    public required double Start { get; init; }
    /// <summary>Fin del último frame (hasta dónde tiene imagen).</summary>
    public required double End { get; init; }
    public bool Stacked { get; init; }
    /// <summary>B a la izquierda o arriba.</summary>
    public bool First { get; init; }
    /// <summary>El sonido de B (el que se oye al exportar); si no, el de A.</summary>
    public bool AudioFromB { get; init; }
}

/// <summary>
/// Exporta tramos de vídeo de uno en uno, en segundo plano, con el ffmpeg.exe que va junto a las DLL de FFmpeg. Los
/// codificadores se prueban en orden (NVENC, AMD, Intel, CPU, Media Foundation) hasta que uno funciona.
/// </summary>
public sealed class ClipExporter : IDisposable
{
    private static readonly string FfmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

    private readonly object _lock = new();
    private readonly Queue<ClipJob> _queue = new();
    private ClipJob? _current;
    private bool _running;
    private Task? _worker;

    /// <summary>Empieza, avanza o acaba una exportación (desde otro hilo).</summary>
    public event Action? Changed;
    /// <summary>Terminó una: error null si salió bien o se canceló (desde otro hilo).</summary>
    public event Action<ClipJob, string?>? Finished;

    public ClipJob? Current { get { lock (_lock) return _current; } }
    public int Pending { get { lock (_lock) return _queue.Count; } }
    public bool Busy { get { lock (_lock) return _running; } }

    public void Enqueue(ClipJob job)
    {
        lock (_lock)
        {
            _queue.Enqueue(job);
            if (!_running)
            {
                _running = true;
                _worker = Task.Run(Run);
            }
        }
        Changed?.Invoke();
    }

    public void CancelCurrent() => Current?.Cancel.Cancel();

    public void CancelAll()
    {
        lock (_lock)
        {
            foreach (var job in _queue)
                job.Cancel.Cancel();
            _current?.Cancel.Cancel();
        }
    }

    public void Dispose()
    {
        CancelAll();
        // Que dé tiempo a matar ffmpeg y borrar el archivo a medias.
        _worker?.Wait(TimeSpan.FromSeconds(3));
    }

    private async Task Run()
    {
        while (true)
        {
            ClipJob job;
            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    _current = null;
                    _running = false;
                    break;
                }
                job = _current = _queue.Dequeue();
            }
            string? error = null;
            if (!job.Cancelled)
            {
                Changed?.Invoke();
                try
                {
                    await Export(job);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
            }
            lock (_lock)
                _current = null;
            Finished?.Invoke(job, error);
        }
        Changed?.Invoke();
    }

    private async Task Export(ClipJob job)
    {
        if (!File.Exists(FfmpegPath))
            throw new FileNotFoundException(S.FfmpegMissing);
        var token = job.Cancel.Token;
        var source = job.Preset == ClipPreset.Copy ? null
            : await Task.Run(() => ClipProbe.Measure(job.Source, job.Start, job.End, token), token);
        var second = job.Compare is not { } compare ? null
            : await Task.Run(() => ClipProbe.Measure(compare.Source, Math.Max(0, compare.Start), Math.Max(0, compare.Start) + (job.End - job.Start), token), token);
        var keyframe = job.Preset != ClipPreset.Copy ? null
            : job.Keyframe ?? await Task.Run(() => ClipProbe.KeyframeAtOrBefore(job.Source, job.Start, token), token);
        var folder = OutputFolder(job);
        var name = FileName(job);
        string? lastError = null;
        foreach (var attempt in job.Compare != null ? ComparePlan(job, source!, second!) : Plan(job, source, keyframe))
        {
            var output = UniquePath(folder, name, attempt.Extension);
            job.Output = output;
            job.Progress = 0;
            try
            {
                lastError = await RunFfmpeg(attempt, output, job);
            }
            catch
            {
                TryDelete(output);
                throw;
            }
            if (lastError == null && new FileInfo(output) is { Exists: true, Length: > 0 })
                return;
            TryDelete(output);
        }
        throw new IOException(lastError ?? S.FfmpegFailed);
    }

    /// <summary>Lanza ffmpeg y sigue su progreso; devuelve el error, o null si terminó bien.</summary>
    private async Task<string?> RunFfmpeg(Attempt attempt, string output, ClipJob job)
    {
        var info = new ProcessStartInfo(FfmpegPath)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in attempt.Arguments)
            info.ArgumentList.Add(arg);
        info.ArgumentList.Add(output);

        using var process = Process.Start(info) ?? throw new IOException(S.FfmpegNotStarted);
        // Que no le quite tiempo a la reproducción.
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        var errors = new List<string>();
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;
            lock (errors)
            {
                errors.Add(e.Data.Trim());
                if (errors.Count > 6)
                    errors.RemoveAt(0);
            }
        };
        process.BeginErrorReadLine();
        using var kill = job.Cancel.Token.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        });

        // -progress escribe bloques clave=valor; out_time_us es lo que lleva escrito del clip.
        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("out_time_us=", StringComparison.Ordinal)
                && long.TryParse(line.AsSpan(12), NumberStyles.Integer, CultureInfo.InvariantCulture, out var us) && attempt.Length > 0)
            {
                job.Progress = Math.Clamp(us / 1e6 / attempt.Length, 0, 1);
                Changed?.Invoke();
            }
        }
        await process.WaitForExitAsync();
        job.Cancel.Token.ThrowIfCancellationRequested();
        if (process.ExitCode == 0)
            return null;
        lock (errors)
            return errors.Count > 0 ? string.Join("\n", errors) : S.FfmpegExitCode(process.ExitCode);
    }

    // ---- Qué se le pide a ffmpeg ----

    private sealed record Attempt(string Extension, double Length, List<string> Arguments);

    private static readonly string[] Common = ["-hide_banner", "-nostdin", "-nostats", "-loglevel", "error", "-progress", "pipe:1", "-y"];

    /// <summary>Los intentos en orden: cada codificador posible y, si el contenedor original no admite algo, MKV.</summary>
    private static List<Attempt> Plan(ClipJob job, ClipSource? source, double? keyframe)
    {
        var sourceExt = Path.GetExtension(job.Source).ToLowerInvariant();
        var attempts = new List<Attempt>();
        switch (job.Preset)
        {
            case ClipPreset.Copy:
            {
                // Desde el keyframe, sin nada de antes: la búsqueda puede caer bastante antes (en MKV con subtítulos
                // FFmpeg retrocede a propósito) y ffmpeg copia por defecto desde donde cae. Sin keyframe conocido,
                // desde donde caiga la búsqueda.
                var from = keyframe ?? job.Start;
                string[] trim = keyframe != null ? ["-copypriorss", "0"] : [];
                foreach (var ext in Containers(sourceExt))
                    attempts.Add(new(ext, job.End - from,
                    [
                        .. Common, "-ss", Seconds(from - 0.0005), "-i", job.Source, "-t", Seconds(job.End - from),
                        .. Maps(ext), "-c", "copy", .. trim, "-avoid_negative_ts", "make_zero", .. Muxer(ext),
                    ]));
                break;
            }
            case ClipPreset.Exact when job.Speed != 1:
            {
                // A otra velocidad hay que recodificar también el sonido; los subtítulos irían a destiempo y se quitan.
                var ext = sourceExt is ".mp4" or ".m4v" or ".mov" ? ".mp4" : sourceExt == ".webm" ? ".webm" : ".mkv";
                var codec = source!.VideoCodec is "h264" or "hevc" or "av1" ? source.VideoCodec : ext == ".webm" ? "av1" : "h264";
                var rate = source.VideoBitrate > 0 ? Math.Max(source.VideoBitrate, 500_000) : 8_000_000;
                var length = (job.End - job.Start) / job.Speed;
                var fps = SpeedFps(source.Fps, job.Speed, cap: Math.Max(source.Fps, 60));
                foreach (var encoder in Encoders(codec))
                    foreach (var container in Containers(ext))
                        attempts.Add(new(container, length,
                        [
                            .. Common, .. HardwareInput(encoder, gpuFrames: false, job.HardwareDecoding),
                            "-ss", Seconds(job.Start - 0.0005), "-i", job.Source,
                            .. SpeedMaps(job, source), "-vf", SpeedVideo(job.Speed, fps),
                            "-c:v", encoder, .. EncoderOptions(encoder, rate, peak: 2, light: false),
                            .. SpeedAudio(job, source, container == ".webm" ? "libopus" : "aac", 192_000),
                            "-t", Seconds(length), .. Muxer(container),
                        ]));
                break;
            }
            case ClipPreset.Exact:
            {
                var ext = sourceExt is ".mp4" or ".m4v" or ".mov" ? ".mp4" : sourceExt == ".webm" ? ".webm" : ".mkv";
                // El mismo códec si hay codificador para él; si no (MPEG-4 de un AVI, VP9…), uno moderno que quepa.
                var codec = source!.VideoCodec is "h264" or "hevc" or "av1" ? source.VideoCodec : ext == ".webm" ? "av1" : "h264";
                var rate = source.VideoBitrate > 0 ? Math.Max(source.VideoBitrate, 500_000) : 8_000_000;
                foreach (var encoder in Encoders(codec))
                    foreach (var container in Containers(ext))
                        attempts.Add(new(container, job.End - job.Start,
                        [
                            .. Common, .. HardwareInput(encoder, gpuFrames: true, job.HardwareDecoding),
                            // Medio milisegundo antes: el primer frame empieza justo en Start y no debe quedarse fuera.
                            "-ss", Seconds(job.Start - 0.0005), "-i", job.Source, "-t", Seconds(job.End - job.Start),
                            // Lo que se copia (audio, subtítulos) también desde Start: ffmpeg copia por defecto desde el keyframe.
                            .. Maps(container), "-c", "copy", "-copypriorss", "0",
                            "-c:v", encoder, .. EncoderOptions(encoder, rate, peak: 2, light: false),
                            .. Muxer(container),
                        ]));
                break;
            }
            case ClipPreset.Light:
            {
                const string ext = ".mp4";
                var total = job.LightMegabytesPerSecond * 8_000_000;
                var audio = total >= 3_000_000 ? 160_000 : 96_000;
                var video = (long)(total - audio);
                // Sin pasarse del original: más bitrate que el suyo solo haría el archivo más grande.
                if (source!.VideoBitrate > 0)
                    video = Math.Min(video, source.VideoBitrate);
                video = Math.Max(video, 200_000);
                var length = (job.End - job.Start) / job.Speed;
                var filters = new List<string>();
                // Como mucho 60 fps (también a otra velocidad) y 1080p (el lado corto, para que un vídeo vertical no se
                // quede diminuto).
                if (job.Speed != 1)
                    filters.Add(SpeedVideo(job.Speed, SpeedFps(source.Fps, job.Speed, cap: 60)));
                else if (source.Fps > 61)
                    filters.Add("fps=60");
                if (Math.Min(source.Width, source.Height) > 1080)
                    filters.Add(source.Width >= source.Height ? "scale=-2:1080" : "scale=1080:-2");
                filters.Add("format=yuv420p");
                foreach (var encoder in Encoders("h264"))
                    attempts.Add(new(ext, length, job.Speed != 1
                    ?
                    [
                        .. Common, .. HardwareInput(encoder, gpuFrames: false, job.HardwareDecoding),
                        "-ss", Seconds(job.Start - 0.0005), "-i", job.Source,
                        .. SpeedMaps(job, source), "-vf", string.Join(",", filters), "-c:v", encoder, .. EncoderOptions(encoder, video, peak: 1, light: true),
                        .. SpeedAudio(job, source, "aac", audio), "-t", Seconds(length), .. Muxer(ext),
                    ]
                    :
                    [
                        .. Common, .. HardwareInput(encoder, gpuFrames: false, job.HardwareDecoding),
                        "-ss", Seconds(job.Start - 0.0005), "-i", job.Source, "-t", Seconds(job.End - job.Start),
                        "-map", "0:V:0", "-map", job.AudioStream is { } a ? $"0:{a}?" : "0:a:0?", "-sn", "-dn", "-map_chapters", "-1",
                        "-vf", string.Join(",", filters), "-c:v", encoder, .. EncoderOptions(encoder, video, peak: 1, light: true),
                        "-c:a", "aac", "-b:a", audio.ToString(CultureInfo.InvariantCulture), "-ac", "2", .. Muxer(ext),
                    ]));
                break;
            }
        }
        return attempts;
    }

    /// <summary>
    /// Los dos vídeos en un clip H.264 + AAC en MP4: cada uno cortado en su frame exacto, a los fps del más rápido, del
    /// mismo alto (lado a lado) o ancho (uno encima del otro) y en su sitio; lo que no tiene imagen de B, en negro. El
    /// sonido, el del que se oye. Alta calidad: la suma de los dos bitrates; ligero: el tamaño elegido.
    /// </summary>
    private static List<Attempt> ComparePlan(ClipJob job, ClipSource a, ClipSource b)
    {
        var compare = job.Compare!;
        var length = job.End - job.Start;
        var light = job.Preset == ClipPreset.Light;
        var fps = Math.Max(a.Fps > 0 ? a.Fps : 30, b.Fps > 0 ? b.Fps : 30);
        if (light)
            fps = Math.Min(fps, 60);
        // Lo que B tiene de imagen dentro del tramo y cuánto negro va antes.
        var bFrom = Math.Max(0, compare.Start);
        var bTo = Math.Min(compare.Start + length, compare.End);
        var bLength = bTo - bFrom;
        var lead = Math.Max(0, bFrom - compare.Start);
        double Aspect(ClipSource s) => s.Width > 0 && s.Height > 0 ? s.Width / (double)s.Height : 16 / 9.0;
        var (ra, rb) = (Aspect(a), Aspect(b));
        // Tamaño común: el del más pequeño, sin pasar de 3840 (ligero, también 1080 de alto cada uno) ni de 4096 en total.
        string scale;
        if (compare.Stacked)
        {
            double w = Math.Min(a.Width, b.Width);
            if (light)
                w = Math.Min(w, 1920);
            w = Math.Min(w, (light ? 2160 : 4096) / (1 / ra + 1 / rb));
            scale = $"scale={Even(w)}:-2";
        }
        else
        {
            double h = Math.Min(a.Height, b.Height);
            if (light)
                h = Math.Min(h, 1080);
            h = Math.Min(h, (light ? 3840 : 4096) / (ra + rb));
            scale = $"scale=-2:{Even(h)}";
        }
        var f = fps.ToString("0.###", CultureInfo.InvariantCulture);
        var common = $"setpts=PTS-STARTPTS,fps={f},{scale},setsar=1,format=yuv420p";
        var graph = $"[0:v]{common},trim=duration={Seconds(length)}[va];" +
            $"[1:v]{common},tpad=start_duration={Seconds(lead)}:stop_mode=add:stop_duration={Seconds(length)}:color=black," +
            $"trim=duration={Seconds(length)},setpts=PTS-STARTPTS[vb];" +
            (compare.First ? "[vb][va]" : "[va][vb]") + (compare.Stacked ? "vstack" : "hstack") + "=inputs=2" +
            // A otra velocidad, los dos juntos ya sincronizados; el tramo de cada uno sigue siendo el mismo.
            (job.Speed != 1 ? "," + SpeedVideo(job.Speed, SpeedFps(fps, job.Speed, cap: light ? 60 : Math.Max(fps, 60))) : "") + "[v]";
        var output = length / job.Speed;
        var tempo = job.Speed != 1 ? "," + Tempo(job.Speed) : "";
        var audioFromB = !job.NoAudio && compare.AudioFromB && b.HasAudio && bLength > 0;
        var audioFromA = !job.NoAudio && !compare.AudioFromB && a.HasAudio;
        if (audioFromB)
        {
            var delay = (long)Math.Round(lead * 1000);
            graph += $";[1:a]asetpts=PTS-STARTPTS,adelay={delay}:all=1,apad,atrim=duration={Seconds(length)}{tempo}[aud]";
        }
        else if (audioFromA && job.Speed != 1)
        {
            graph += $";[{(job.AudioStream is { } track ? $"0:{track}" : "0:a:0")}]asetpts=PTS-STARTPTS{tempo}[aud]";
        }
        string[] audioMap = audioFromB || (audioFromA && job.Speed != 1) ? ["-map", "[aud]"]
            : audioFromA ? ["-map", job.AudioStream is { } s ? $"0:{s}?" : "0:a:0?"]
            : ["-an"];
        long video;
        int audio;
        if (light)
        {
            var total = job.LightMegabytesPerSecond * 8_000_000;
            audio = total >= 3_000_000 ? 160_000 : 96_000;
            video = Math.Max(200_000, (long)(total - audio));
        }
        else
        {
            audio = 192_000;
            video = Math.Clamp(Math.Max(a.VideoBitrate, 0) + Math.Max(b.VideoBitrate, 0), 4_000_000, 60_000_000);
        }
        var attempts = new List<Attempt>();
        foreach (var encoder in Encoders("h264"))
        {
            // B: su trozo con imagen o, si no tiene ninguno, una imagen negra de su tamaño.
            string[] second = bLength > 0
                ? [.. HardwareInput(encoder, gpuFrames: false, job.HardwareDecoding), "-ss", Seconds(bFrom - 0.0005), "-t", Seconds(bLength), "-i", compare.Source]
                : ["-f", "lavfi", "-i", $"color=c=black:s={Math.Max(2, b.Width)}x{Math.Max(2, b.Height)}:r={f}:d={Seconds(length)}"];
            attempts.Add(new(".mp4", output,
            [
                .. Common, .. HardwareInput(encoder, gpuFrames: false, job.HardwareDecoding),
                "-ss", Seconds(job.Start - 0.0005), "-t", Seconds(length), "-i", job.Source, .. second,
                "-filter_complex", graph, "-map", "[v]", .. audioMap, "-sn", "-dn", "-map_chapters", "-1",
                "-c:v", encoder, .. EncoderOptions(encoder, video, peak: light ? 1 : 2, light),
                .. audioMap[0] == "-an" ? (string[])[] : ["-c:a", "aac", "-b:a", audio.ToString(CultureInfo.InvariantCulture), "-ac", "2"],
                "-t", Seconds(output), .. Muxer(".mp4"),
            ]));
        }
        return attempts;
    }

    private static int Even(double value) => Math.Max(2, (int)Math.Floor(value / 2) * 2);

    // ---- A otra velocidad ----

    /// <summary>Los fps del clip a otra velocidad: los del original por la velocidad (60 fps a 0,5×, 30), sin pasar de cap.</summary>
    private static double SpeedFps(double fps, double speed, double cap) => Math.Min((fps > 0 ? fps : 30) * speed, cap);

    /// <summary>Cada frame dura 1/speed más; fps fija la cadencia (sin repetir frames salvo en un vídeo de fps variable).</summary>
    private static string SpeedVideo(double speed, double fps) =>
        $"setpts=(PTS-STARTPTS)/{Number(speed)},fps={Number(fps)}";

    /// <summary>El sonido estirado sin cambiar de tono: atempo va de 0,5 a 100, así que por debajo de 0,5 se encadena.</summary>
    private static string Tempo(double speed)
    {
        var parts = new List<string>();
        while (speed < 0.5)
        {
            parts.Add("atempo=0.5");
            speed /= 0.5;
        }
        parts.Add($"atempo={Number(speed)}");
        return string.Join(",", parts);
    }

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static bool SpeedHasAudio(ClipJob job, ClipSource source) => !job.NoAudio && source.HasAudio;

    // La pista de vídeo y, si se oye, la de audio que se está oyendo; sin subtítulos (irían a destiempo).
    private static IEnumerable<string> SpeedMaps(ClipJob job, ClipSource source) =>
    [
        "-map", "0:V:0", .. SpeedHasAudio(job, source) ? ["-map", job.AudioStream is { } a ? $"0:{a}" : "0:a:0"] : (string[])["-an"],
        "-sn", "-dn", "-map_chapters", "-1",
    ];

    private static IEnumerable<string> SpeedAudio(ClipJob job, ClipSource source, string codec, int bitrate) =>
        !SpeedHasAudio(job, source) ? []
        : ["-af", $"asetpts=PTS-STARTPTS,{Tempo(job.Speed)}", "-c:a", codec, "-b:a", bitrate.ToString(CultureInfo.InvariantCulture), "-ac", "2"];

    private static IEnumerable<string> Containers(string ext) => ext == ".mkv" ? [ext] : [ext, ".mkv"];

    // La pista de vídeo principal (V: sin portadas), todos los audios y subtítulos y, en MKV, las fuentes adjuntas.
    private static IEnumerable<string> Maps(string ext) => ext switch
    {
        ".mkv" => ["-map", "0:V:0", "-map", "0:a?", "-map", "0:s?", "-map", "0:t?"],
        ".mp4" or ".m4v" or ".mov" or ".webm" => ["-map", "0:V:0", "-map", "0:a?", "-map", "0:s?"],
        _ => ["-map", "0:V:0", "-map", "0:a?"],
    };

    // MP4 con el índice al principio: se reproduce en el navegador (Jira, Slack) antes de descargarse entero.
    private static IEnumerable<string> Muxer(string ext) => ext is ".mp4" or ".m4v" or ".mov" ? ["-movflags", "+faststart"] : [];

    private static IEnumerable<string> Encoders(string codec) => codec switch
    {
        "hevc" => ["hevc_nvenc", "hevc_amf", "hevc_qsv", "libx265", "hevc_mf"],
        "av1" => ["av1_nvenc", "av1_amf", "av1_qsv", "libsvtav1"],
        _ => ["h264_nvenc", "h264_amf", "h264_qsv", "libx264", "h264_mf"],
    };

    // Con NVENC, decodificar con CUDA; sin filtros, los frames ni salen de la tarjeta. Si la tarjeta no sabe
    // decodificar ese códec, ffmpeg decodifica por CPU sin más.
    private static IEnumerable<string> HardwareInput(string encoder, bool gpuFrames, bool allowed) =>
        !allowed ? []
        : encoder.EndsWith("_nvenc", StringComparison.Ordinal)
            ? gpuFrames ? ["-hwaccel", "cuda", "-hwaccel_output_format", "cuda"] : ["-hwaccel", "cuda"]
            : ["-hwaccel", "auto"];

    private static IEnumerable<string> EncoderOptions(string encoder, long rate, double peak, bool light)
    {
        var b = rate.ToString(CultureInfo.InvariantCulture);
        var max = ((long)(rate * peak)).ToString(CultureInfo.InvariantCulture);
        // El ligero, con un segundo de búfer: si no, en clips cortos se pasa bastante del tamaño pedido.
        var buffer = (rate * (light ? 1 : 2)).ToString(CultureInfo.InvariantCulture);
        string[] rates = ["-b:v", b, "-maxrate", max, "-bufsize", buffer];
        if (encoder.EndsWith("_nvenc", StringComparison.Ordinal))
            return ["-preset", light ? "p4" : "p5", "-tune", "hq", "-rc", "vbr", "-spatial-aq", "1",
                .. light ? (string[])["-profile:v", "high"] : [], .. rates];
        if (encoder.EndsWith("_amf", StringComparison.Ordinal))
            return ["-quality", "quality", "-rc", "vbr_peak", .. rates];
        if (encoder.EndsWith("_qsv", StringComparison.Ordinal))
            return ["-preset", "slow", .. rates];
        return encoder switch
        {
            "libx264" or "libx265" => ["-preset", light ? "fast" : "medium", .. rates],
            "libsvtav1" => ["-preset", "8", "-b:v", b],
            _ => ["-b:v", b],
        };
    }

    private static string Seconds(double t) => Math.Max(0, t).ToString("0.######", CultureInfo.InvariantCulture);

    // ---- Dónde y con qué nombre ----

    /// <summary>La carpeta elegida o la del vídeo; si no se puede escribir en ella, Vídeos\VideoSeeker.</summary>
    private static string OutputFolder(ClipJob job)
    {
        var folder = job.Folder is { Length: > 0 } chosen ? chosen : Path.GetDirectoryName(job.Source) ?? "";
        if (CanWrite(folder))
            return folder;
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VideoSeeker");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool CanWrite(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            using (File.Create(Path.Combine(folder, $".videoseeker-{Guid.NewGuid():N}.tmp"), 1, FileOptions.DeleteOnClose))
                return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>«vídeo 12.03-12.20», «vídeo - nombre del bookmark» y, en el ligero, « (ligero)».</summary>
    public static string FileName(ClipJob job)
    {
        var video = Path.GetFileNameWithoutExtension(job.Source);
        if (job.Compare is { } compare)
            video = $"{video} vs {Path.GetFileNameWithoutExtension(compare.Source)}";
        var name = job.Name is { Length: > 0 } bookmark ? $"{video} - {bookmark}" : $"{video} {Stamp(job.Start)}-{Stamp(job.End)}";
        if (job.Speed != 1)
            name += $" ({job.Speed.ToString("0.##", Loc.Culture)}×)";
        if (job.Preset == ClipPreset.Light)
            name += S.LightClipSuffix;
        // «Jefe: fase 2/3?» → «Jefe. fase 2-3».
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => c switch { ':' => '.', '/' or '\\' or '|' => '-', _ => c })
            .Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
        return clean.Length > 150 ? clean[..150].TrimEnd() : clean;
    }

    private static string Stamp(double t) => TimeFormat.Format(t, withMilliseconds: false).Replace(':', '.');

    private static string UniquePath(string folder, string name, string ext)
    {
        var path = Path.Combine(folder, name + ext);
        for (var i = 2; File.Exists(path); i++)
            path = Path.Combine(folder, $"{name} ({i}){ext}");
        return path;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
