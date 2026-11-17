using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VideoSeeker.App.Controls;
using VideoSeeker.App.Data;
using VideoSeeker.Inspect;
using VideoSeeker.Playback;

namespace VideoSeeker.App;

/// <summary>
/// El segundo vídeo al comparar (B): una cadena entera como la del vídeo abierto, con su mpv (sin audio), su ventana
/// nativa sobre <see cref="Placeholder"/>, su motor de inspección (con el dispositivo de la tarjeta del primero) y su
/// <see cref="Navigator"/>, que es quien lo mueve. Todo desde el hilo de la interfaz.
/// </summary>
public sealed class ComparePane
{
    private readonly Dispatcher _ui;
    private readonly string _dataFolder;
    private readonly Task _ffmpegReady;
    private readonly Task _playerReady;
    private bool _started, _disposed;
    private CancellationTokenSource? _indexing;
    // Cada Open sube el número: lo que llegue de un archivo anterior (su índice) ya no cuenta.
    private int _openSeq;

    public MpvPlayer Player { get; } = new();
    public Navigator Nav { get; }
    public VideoSurface Surface { get; }
    public InspectionEngine? Engine { get; private set; }
    /// <summary>El hueco de la imagen (la ventana nativa se coloca encima).</summary>
    public Border Placeholder { get; } = new();

    public string? Path { get; private set; }
    public string? Fingerprint { get; private set; }
    public FrameIndex? Index { get; private set; }
    public double Duration { get; private set; }
    /// <summary>Proporción de la imagen (ancho / alto); 0 hasta el primer frame.</summary>
    public double Aspect { get; private set; }
    private double _containerFps;
    public double Fps => _containerFps > 0 ? _containerFps : 30;
    public bool FpsKnown => _containerFps > 0;
    public FrameGrid Grid => new(Index, Fps);
    /// <summary>Inicio del último frame (0 si aún no se sabe la duración).</summary>
    public double LastFrameStart { get; private set; }
    public double FirstFrameStart { get; private set; }
    /// <summary>Ya se sabe la duración y se ha visto el primer frame del archivo actual.</summary>
    public bool Ready => Duration > 0 && _firstFrame;
    private bool _firstFrame;
    // Dónde se pidió abrir: si mpv, con mucho trabajo, cae en otro frame (el keyframe siguiente), se le lleva ahí.
    private double _start;

    /// <summary>Cambió algo de lo que se sabe del archivo (duración, fps, índice, proporción).</summary>
    public event Action? Changed;

    public ComparePane(Window owner, Dispatcher ui, string dataFolder, Task ffmpegReady, bool hardwareDecoding)
    {
        _ui = ui;
        _dataFolder = dataFolder;
        _ffmpegReady = ffmpegReady;
        Surface = new VideoSurface(owner, Placeholder);
        Nav = new Navigator(Player);
        Player.SubtitlesOnOpen = false;
        // mpv tarda un poco en arrancar: en paralelo, mientras se monta la vista doble.
        _playerReady = Task.Run(() => Player.Initialize(0, true, hardwareDecoding, System.IO.Path.Combine(dataFolder, "cache", "mpv"), audio: false));
    }

    /// <summary>
    /// Abre un vídeo en "start" (en pausa). "shareWith" es el motor del primero (su dispositivo de la tarjeta se
    /// comparte); "knownIndex", su índice si es el mismo archivo.
    /// </summary>
    public async Task OpenAsync(string path, double start, InspectionEngine? shareWith, long cacheBudget, FrameIndex? knownIndex)
    {
        var seq = ++_openSeq;
        await _playerReady;
        if (_disposed || seq != _openSeq)
            return;
        if (!_started)
        {
            _started = true;
            Surface.Create();
            Player.SetWindow(Surface.Handle);
            Hook();
            Player.Start();
            Surface.Resized += () => Engine?.Refresh();
            try
            {
                await _ffmpegReady;
                if (_disposed)
                    return;
                Engine = new InspectionEngine(Surface.Handle, cacheBudget, shareWith?.Gpu) { CacheShare = 2 };
                Nav.AttachEngine(Engine, null);
            }
            catch (Exception)
            {
                // Sin motor también funciona: mpv hace todo, solo que más lento al ir atrás.
                Engine = null;
            }
        }
        Path = path;
        Fingerprint = Data.Fingerprint.Compute(path);
        Duration = 0;
        Aspect = 0;
        _firstFrame = false;
        _containerFps = 0;
        Index = null;
        UpdateLastFrame();
        _start = start;
        Nav.Pause();
        Nav.OnFileOpening(path, play: false);
        Player.SetPause(true);
        Player.Load(path, start, play: false);
        if (knownIndex != null)
        {
            _indexing?.Cancel();
            SetIndex(knownIndex);
        }
        else
        {
            LoadIndex(path, Fingerprint, seq);
        }
        Changed?.Invoke();
    }

    private void Hook()
    {
        void Ui(Action action) => _ui.BeginInvoke(() =>
        {
            if (!_disposed)
                action();
        });
        Player.PositionChanged += t => Ui(() => Nav.OnMpvPosition(t));
        Player.Restarted += () => Ui(Nav.OnMpvRestart);
        Player.PauseChanged += paused => Ui(() => Nav.OnMpvPause(paused));
        Player.DurationChanged += d => Ui(() =>
        {
            Duration = d;
            Nav.Duration = d;
            UpdateLastFrame();
            Changed?.Invoke();
        });
        Player.FpsChanged += fps => Ui(() =>
        {
            _containerFps = fps;
            Nav.Fps = Fps;
            UpdateLastFrame();
            Changed?.Invoke();
        });
        Player.FirstFrame += () => Ui(() =>
        {
            _firstFrame = true;
            Aspect = Player.VideoAspect ?? 0;
            if (Math.Abs(Nav.CurrentTime - _start) > 0.5 / Fps)
                Nav.SeekTo(_start);
            Changed?.Invoke();
        });
    }

    // Como el del primero: el índice guardado se lee al momento; si no hay, se construye en segundo plano.
    private async void LoadIndex(string path, string key, int seq)
    {
        _indexing?.Cancel();
        var cts = _indexing = new CancellationTokenSource();
        var file = System.IO.Path.Combine(_dataFolder, "cache", "index", key + ".bin");
        try
        {
            var index = await Task.Run(async () =>
            {
                if (FrameIndex.Load(file) is { } saved)
                    return saved;
                await _ffmpegReady;
                var built = FrameIndex.Build(path, cts.Token);
                if (built.Count > 0)
                    built.Save(file);
                return built;
            }, cts.Token);
            if (!cts.IsCancellationRequested && !_disposed && seq == _openSeq && index.Count > 0)
                SetIndex(index);
        }
        catch (Exception e) when (e is OperationCanceledException or FFmpegException or IOException or UnauthorizedAccessException)
        {
        }
    }

    private void SetIndex(FrameIndex index)
    {
        Index = index;
        Engine?.SetKeyframes(index.Keyframes);
        UpdateLastFrame();
        Changed?.Invoke();
    }

    private void UpdateLastFrame()
    {
        var grid = Grid;
        LastFrameStart = Duration <= 0 ? 0
            : Index is { Count: > 0 } index ? index.TimeOf(index.Count - 1)
            : grid.TimeOf(grid.FrameAt(Duration - 1e-4));
        FirstFrameStart = Duration > 0 && Index is { Count: > 0 } first ? first.TimeOf(0) : 0;
        Nav.LastFrameStart = LastFrameStart;
        Nav.FirstFrameStart = FirstFrameStart;
    }

    /// <summary>
    /// Cierra sin bloquear la interfaz: la imagen ya está oculta (el hueco fuera de la vista); lo que tarda mpv en cerrarse,
    /// en segundo plano; el motor y la ventana nativa, después, en este hilo. "immediate" (al cerrar la app) lo hace ya.
    /// </summary>
    public async Task CloseAsync(bool immediate = false)
    {
        if (_disposed)
            return;
        _disposed = true;
        _indexing?.Cancel();
        Nav.DetachEngine();
        Nav.Pause();
        try
        {
            if (!immediate)
            {
                // Lo que Navigator dejó pendiente (sincronizar mpv tras 250 ms) llega antes de cerrar mpv.
                await Task.Delay(400);
                await _playerReady;
                await Task.Run(Player.Dispose);
            }
            else
            {
                _playerReady.Wait();
                Player.Dispose();
            }
        }
        catch (Exception e)
        {
            CloseError = e;
        }
        try
        {
            Engine?.Dispose();
        }
        catch (Exception e)
        {
            CloseError ??= e;
        }
        Engine = null;
        Surface.Dispose();
        Closed = true;
    }

    /// <summary>Ya cerrado del todo (mpv, motor y ventana). Para la autoprueba.</summary>
    public bool Closed { get; private set; }
    /// <summary>Lo que falló al cerrar, si algo falló. Para la autoprueba.</summary>
    public Exception? CloseError { get; private set; }
}
