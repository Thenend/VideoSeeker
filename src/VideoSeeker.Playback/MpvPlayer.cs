using System.Globalization;
using System.Text.Json;

namespace VideoSeeker.Playback;

public sealed record Track(long Id, string Type, string? Title, string? Lang, string? Codec, bool Selected)
{
    /// <summary>Índice de la pista en el archivo según FFmpeg (ff-index de mpv); null en pistas externas.</summary>
    public int? FfIndex { get; init; }

    public string Label =>
        string.Join(" · ", new[] { Title, Lang, Codec }.Where(s => !string.IsNullOrEmpty(s))) is { Length: > 0 } text
            ? $"{Id}: {text}" : $"Pista {Id}";
}

/// <summary>
/// Reproductor de alto nivel sobre mpv: opciones por defecto, comandos y un hilo que traduce
/// los eventos de mpv a eventos .NET. Los eventos se lanzan en ese hilo, no en el de la UI.
/// </summary>
public sealed class MpvPlayer : IDisposable
{
    private const ulong TimePosId = 1, DurationId = 2, PauseId = 3, VolumeId = 4, MuteId = 5, TracksId = 6, FpsId = 7, EofId = 8;

    private static readonly (string, string)[] DefaultOptions =
    [
        ("config", "no"), ("load-scripts", "no"), ("terminal", "no"),
        ("vo", "gpu-next"), ("gpu-api", "d3d11"), ("hwdec", "auto-safe"),
        ("idle", "yes"), ("keep-open", "yes"), ("force-window", "no"),
        ("osc", "no"), ("osd-level", "1"), ("input-default-bindings", "no"), ("input-vo-keyboard", "no"),
        ("cursor-autohide", "no"), ("input-cursor-passthrough", "yes"), ("drag-and-drop", "no"),
        ("cache", "yes"), ("demuxer-max-bytes", "512MiB"), ("demuxer-max-back-bytes", "512MiB"),
        ("hr-seek", "yes"),
        ("sub-auto", "fuzzy"),
    ];

    // Se crea en Initialize: cargar libmpv cuesta, y así puede hacerse fuera del hilo de la interfaz.
    private Mpv _mpv = null!;
    private Thread? _eventThread;
    private volatile bool _disposed;
    private bool _waitingFirstFrame;

    // Scrubbing: como mucho un seek en vuelo; si llegan más, solo se guarda el último.
    private readonly Lock _scrubGate = new();
    private bool _scrubInFlight;
    private (double Seconds, bool Exact)? _scrubPending;
    private long _scrubSentAt;
    private bool _scrubSentExact;

    /// <summary>Lo que tarda de media un seek exacto de scrubbing en este vídeo (ms, suavizado).</summary>
    public double ExactSeekCostMs { get; private set; }

    /// <summary>Idiomas preferidos de audio y subtítulos ("spa,eng"); se aplican al abrir cada vídeo.</summary>
    public string AudioLanguages { get; set; } = "";
    public string SubtitleLanguages { get; set; } = "";
    /// <summary>Al abrir un vídeo, activar una pista de subtítulos (la del idioma preferido o, si no, otra); si no, ninguna.</summary>
    public bool SubtitlesOnOpen { get; set; } = true;

    public event Action<double>? PositionChanged;
    public event Action<double>? DurationChanged;
    public event Action<double>? FpsChanged;
    public event Action<bool>? PauseChanged;
    public event Action<double>? VolumeChanged;
    public event Action<bool>? MuteChanged;
    public event Action<IReadOnlyList<Track>>? TracksChanged;
    public event Action? FileLoaded;
    /// <summary>Primer frame del archivo recién cargado ya en pantalla.</summary>
    public event Action? FirstFrame;
    public event Action? EndOfFile;
    /// <summary>La reproducción llegó al final y se quedó en el último frame (keep-open).</summary>
    public event Action? EndReached;
    /// <summary>Terminó un seek (o la carga) y ya se ve el frame pedido.</summary>
    public event Action? Restarted;

    /// <summary>
    /// Crea e inicializa mpv. Se puede llamar desde un hilo de fondo antes de que exista la ventana. En
    /// <paramref name="cacheFolder"/> guarda los shaders compilados.
    /// </summary>
    public void Initialize(double volume, bool mute, bool hardwareDecoding = true, string? cacheFolder = null)
    {
        _mpv = new Mpv();
        foreach (var (name, value) in DefaultOptions)
            _mpv.SetOption(name, value);
        // Sin carpeta (config=no) mpv recompila sus shaders en cada arranque: unos 200 ms antes del primer frame.
        if (cacheFolder != null)
        {
            Directory.CreateDirectory(cacheFolder);
            _mpv.SetOption("gpu-shader-cache-dir", cacheFolder);
            _mpv.SetOption("icc-cache-dir", cacheFolder);
        }
        _mpv.SetOption("hwdec", HwdecValue(hardwareDecoding));
        _mpv.SetOption("volume", volume.ToString(CultureInfo.InvariantCulture));
        _mpv.SetOption("mute", mute ? "yes" : "no");
        // Para depurar: registro detallado de mpv en el archivo indicado.
        if (Environment.GetEnvironmentVariable("VIDEOSEEKER_MPVLOG") is { Length: > 0 } log)
            _mpv.SetOption("log-file", log);
        _mpv.Initialize();
    }

    /// <summary>Empieza a emitir eventos. Llamar después de suscribirse para no perder los valores iniciales.</summary>
    public void Start()
    {
        _mpv.ObserveDouble(TimePosId, "time-pos");
        _mpv.ObserveDouble(DurationId, "duration");
        _mpv.ObserveDouble(FpsId, "container-fps");
        _mpv.ObserveFlag(PauseId, "pause");
        _mpv.ObserveFlag(EofId, "eof-reached");
        _mpv.ObserveDouble(VolumeId, "volume");
        _mpv.ObserveFlag(MuteId, "mute");
        _mpv.ObserveString(TracksId, "track-list");

        _eventThread = new Thread(EventLoop) { IsBackground = true, Name = "mpv-events" };
        _eventThread.Start();
    }

    /// <summary>Ventana Win32 donde pintar. Debe fijarse antes del primer Load.</summary>
    public void SetWindow(nint hwnd) => _mpv.SetProperty("wid", hwnd.ToInt64().ToString(CultureInfo.InvariantCulture));

    /// <summary>Carga un vídeo; con "play" empieza a reproducirse aunque estuviera en pausa (o parado al final).</summary>
    public void Load(string path, double? start = null, bool play = false)
    {
        _waitingFirstFrame = true;
        SetVideoEnabled(true);
        lock (_scrubGate)
        {
            _scrubInFlight = false;
            _scrubPending = null;
            ExactSeekCostMs = 0;
        }
        // Opciones solo para este archivo: no se arrastran al siguiente (p. ej. la pista elegida con S).
        var options = new List<string>();
        if (start is { } s)
            options.Add($"start={s.ToString("0.###", CultureInfo.InvariantCulture)}");
        if (AudioLanguages.Trim() is { Length: > 0 } alang)
            options.Add(FileOption("alang", alang));
        if (SubtitleLanguages.Trim() is { Length: > 0 } slang)
            options.Add(FileOption("slang", slang));
        // subs-fallback=yes: aunque ninguna pista sea del idioma preferido ni venga marcada por defecto, se activa una.
        options.Add(SubtitlesOnOpen ? "sid=auto,subs-fallback=yes" : "sid=no");
        _mpv.Command("loadfile", path, "replace", "-1", string.Join(",", options));
        // Después del loadfile: antes, si el anterior había llegado al final, keep-open lo volvería a pausar.
        if (play)
            SetPause(false);
    }

    // %n%valor: así las comas de "spa,eng" no se toman como separadores de opciones.
    private static string FileOption(string name, string value) =>
        $"{name}=%{System.Text.Encoding.UTF8.GetByteCount(value)}%{value}";

    private static string HwdecValue(bool enabled) => enabled ? "auto-safe" : "no";

    /// <summary>Decodificación por hardware sí / no; mpv la cambia en caliente.</summary>
    public void SetHardwareDecoding(bool enabled) => _mpv.SetProperty("hwdec", HwdecValue(enabled));

    public double? Position => _mpv.GetDouble("time-pos");

    public void TogglePause() => _mpv.Command("cycle", "pause");
    public void SetPause(bool paused) => _mpv.SetProperty("pause", paused ? "yes" : "no");
    public void SetSpeed(double speed) => _mpv.SetProperty("speed", speed.ToString("0.###", CultureInfo.InvariantCulture));

    /// <summary>Exacto es preciso al frame; por keyframes es mucho más rápido (para arrastrar la barra).</summary>
    public void SeekAbsolute(double seconds, bool exact) =>
        _mpv.Command("seek", seconds.ToString("0.######", CultureInfo.InvariantCulture), exact ? "absolute+exact" : "absolute+keyframes");

    /// <summary>
    /// Seek exacto para scrubbing: si el anterior aún no ha terminado, se recuerda solo el último destino
    /// y se lanza al acabar. Así, en vídeos pesados se saltan frames intermedios en vez de acumular retraso.
    /// </summary>
    public void ScrubTo(double seconds, bool exact = true)
    {
        lock (_scrubGate)
        {
            // Si un seek no llegara a confirmarse, a los 2 s dejamos de esperarlo.
            if (_scrubInFlight && Environment.TickCount64 - _scrubSentAt < 2000)
            {
                _scrubPending = (seconds, exact);
                return;
            }
            _scrubInFlight = true;
            _scrubSentAt = Environment.TickCount64;
            _scrubSentExact = exact;
        }
        SeekAbsolute(seconds, exact);
    }

    private void OnSeekFinished()
    {
        (double Seconds, bool Exact) next;
        lock (_scrubGate)
        {
            if (_scrubInFlight && _scrubSentExact)
            {
                var cost = Environment.TickCount64 - _scrubSentAt;
                ExactSeekCostMs = ExactSeekCostMs == 0 ? cost : 0.7 * ExactSeekCostMs + 0.3 * cost;
            }
            if (_scrubPending is not { } pending)
            {
                _scrubInFlight = false;
                return;
            }
            next = pending;
            _scrubPending = null;
            _scrubSentAt = Environment.TickCount64;
            _scrubSentExact = next.Exact;
        }
        SeekAbsolute(next.Seconds, next.Exact);
    }

    public void SeekRelative(double seconds) =>
        _mpv.Command("seek", seconds.ToString("0.######", CultureInfo.InvariantCulture), "relative+exact");

    /// <summary>Bucle A-B de mpv: al llegar al final del tramo vuelve al principio. Null lo quita.</summary>
    public void SetLoop((double Start, double End)? range)
    {
        _mpv.SetProperty("ab-loop-a", range?.Start.ToString("0.######", CultureInfo.InvariantCulture) ?? "no");
        _mpv.SetProperty("ab-loop-b", range?.End.ToString("0.######", CultureInfo.InvariantCulture) ?? "no");
    }

    /// <summary>Bucle del archivo entero: al llegar al final vuelve al principio.</summary>
    public void SetLoopFile(bool on) => _mpv.SetProperty("loop-file", on ? "inf" : "no");

    private string? _videoTrack;

    /// <summary>
    /// Quita o devuelve la pista de vídeo. Sin vídeo, mpv solo reproduce el audio y sus seeks son casi
    /// instantáneos (para el bucle que pinta el motor de inspección desde su caché).
    /// </summary>
    public void SetVideoEnabled(bool enabled)
    {
        if (!enabled)
        {
            var current = _mpv.GetString("vid");
            if (current is null or "no")
                return;
            _videoTrack = current;
            _mpv.SetProperty("vid", "no");
        }
        else if (_videoTrack != null)
        {
            _mpv.SetProperty("vid", _videoTrack);
            _videoTrack = null;
        }
    }

    public void FrameStep() => _mpv.Command("frame-step");
    public void FrameBackStep() => _mpv.Command("frame-back-step");

    public void SetVolume(double volume) => _mpv.SetProperty("volume", volume.ToString("0.#", CultureInfo.InvariantCulture));
    public void SetMute(bool mute) => _mpv.SetProperty("mute", mute ? "yes" : "no");

    /// <summary>Selecciona pista: type es "audio" o "sub"; null desactiva.</summary>
    public void SelectTrack(string type, long? id) =>
        _mpv.SetProperty(type == "audio" ? "aid" : "sid", id?.ToString(CultureInfo.InvariantCulture) ?? "no");

    public void CycleTrack(string type) => _mpv.Command("cycle", type == "audio" ? "audio" : "sub");

    public void ShowText(string text, int ms = 1200) => _mpv.Command("show-text", text, ms.ToString(CultureInfo.InvariantCulture));

    private void EventLoop()
    {
        while (!_disposed)
        {
            var e = _mpv.WaitEvent(-1);
            switch (e.Id)
            {
                case MpvEventId.Shutdown:
                    return;
                case MpvEventId.FileLoaded:
                    FileLoaded?.Invoke();
                    break;
                case MpvEventId.PlaybackRestart:
                    if (_waitingFirstFrame)
                    {
                        _waitingFirstFrame = false;
                        FirstFrame?.Invoke();
                    }
                    OnSeekFinished();
                    Restarted?.Invoke();
                    break;
                case MpvEventId.EndFile:
                    EndOfFile?.Invoke();
                    break;
                case MpvEventId.PropertyChange:
                    OnPropertyChange(e);
                    break;
            }
        }
    }

    private void OnPropertyChange(MpvEvent e)
    {
        switch (e.ReplyUserdata)
        {
            case TimePosId when e.DoubleValue is { } v: PositionChanged?.Invoke(v); break;
            case DurationId when e.DoubleValue is { } v: DurationChanged?.Invoke(v); break;
            case FpsId when e.DoubleValue is { } v: FpsChanged?.Invoke(v); break;
            case PauseId when e.FlagValue is { } v: PauseChanged?.Invoke(v); break;
            case EofId when e.FlagValue is true: EndReached?.Invoke(); break;
            case VolumeId when e.DoubleValue is { } v: VolumeChanged?.Invoke(v); break;
            case MuteId when e.FlagValue is { } v: MuteChanged?.Invoke(v); break;
            case TracksId when e.StringValue is { } json: TracksChanged?.Invoke(ParseTracks(json)); break;
        }
    }

    private static List<Track> ParseTracks(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(t => new Track(
            t.GetProperty("id").GetInt64(),
            t.GetProperty("type").GetString() ?? "",
            t.TryGetProperty("title", out var title) ? title.GetString() : null,
            t.TryGetProperty("lang", out var lang) ? lang.GetString() : null,
            t.TryGetProperty("codec", out var codec) ? codec.GetString() : null,
            t.TryGetProperty("selected", out var sel) && sel.GetBoolean())
        {
            FfIndex = t.TryGetProperty("ff-index", out var ff) && ff.ValueKind == JsonValueKind.Number
                && !(t.TryGetProperty("external", out var ext) && ext.ValueKind == JsonValueKind.True) ? ff.GetInt32() : null,
        }).ToList();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        // El hilo de eventos debe salir antes de destruir el contexto: "quit" le hace llegar Shutdown.
        if (_eventThread != null)
        {
            try { _mpv.Command("quit"); } catch (MpvException) { }
            _eventThread.Join(TimeSpan.FromSeconds(2));
        }
        _mpv?.Dispose();
    }
}
