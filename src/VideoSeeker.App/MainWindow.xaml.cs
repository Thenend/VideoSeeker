using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using VideoSeeker.App.Controls;
using VideoSeeker.App.Data;
using VideoSeeker.Inspect;
using VideoSeeker.Playback;

namespace VideoSeeker.App;

public partial class MainWindow : Window
{
    private const string GlyphPlay = "", GlyphPause = "";
    private const string GlyphVolume = "", GlyphMute = "";
    private const string GlyphFullscreen = "", GlyphBackToWindow = "";

    private static readonly string VideoFilter =
        "Vídeos|*.mkv;*.mp4;*.m4v;*.avi;*.wmv;*.mov;*.webm;*.flv;*.ts;*.m2ts;*.mts;*.mpg;*.mpeg;*.vob;*.3gp;*.ogv|Todos los archivos|*.*";

    private readonly DataStore _store;
    private readonly MpvPlayer _player;
    private readonly Task _playerReady;
    private readonly Task _ffmpegReady;
    private readonly Navigator _nav;
    private InspectionEngine? _engine;
    private readonly DispatcherTimer _cacheTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private volatile bool _cacheDirty;
    private string? _currentPath;
    private readonly string? _initialPath;
    private readonly DateTime _processStart;
    private readonly VideoSurface _surface;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly Thumbnails _thumbs;

    private VideoEntry? _current;
    // Índice de frames del vídeo abierto (número de frame exacto); hasta tenerlo, se estima con el fps.
    private FrameIndex? _frameIndex;
    private CancellationTokenSource? _indexing;
    private double _position, _duration, _fps;
    private IReadOnlyList<Track> _tracks = [];
    private bool _updatingVolume;
    private bool _startupLogged, _firstFrameSeen, _windowShown, _started;
    private double _scrubStart;
    private long _scrubLastFrame;
    private int _scrubStartFrame;
    // Velocidad a la que iba antes de arrastrar sobre la imagen (0 en pausa): al soltar se reanuda.
    private double _scrubResumeSpeed;
    private (WindowState State, WindowStyle Style, ResizeMode Resize)? _beforeFullscreen;
    // Tramo de la barra de zoom (null si está cerrada).
    private (double Start, double End)? _zoomRange;
    // Dos bucles separados: el del vídeo entero (R, junto al play) y el del tramo ampliado (Shift + R, junto a la X
    // del tramo). Dentro del tramo manda el suyo; fuera, el del vídeo.
    private bool _loopVideo;
    private bool _loopRange;
    private bool RangeLooping => _loopRange && _zoomRange != null;
    // Estado antes del primer clic sobre la imagen: el doble clic lo deshace.
    private (bool Paused, double Time)? _beforeClick;
    // Cuándo se vio reproducir por última vez (para el final del vídeo con los dos bucles).
    private DateTime _lastPlaying;
    private bool _wasPlaying;
    // Si mpv tiene puesto el bucle del archivo entero (el bucle del vídeo).
    private bool _loopFile;

    public MainWindow(DataStore store, MpvPlayer player, Task playerReady, Task ffmpegReady, string? initialPath, DateTime processStart)
    {
        _store = store;
        _player = player;
        _playerReady = playerReady;
        _ffmpegReady = ffmpegReady;
        _nav = new Navigator(player);
        _initialPath = initialPath;
        _processStart = processStart;
        InitializeComponent();
        App.Mark("xaml");
        Dialog.UseDarkTitleBar(this);
        RestorePlacement();
        // Por software mientras arranca, como toda la app (App.OnStartup); así, al volver a la tarjeta, cambia de verdad.
        if (HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.CompositionTarget is { } target)
            target.RenderMode = RenderMode.SoftwareOnly;
        App.Mark("hwnd");
        _thumbs = new Thumbnails(store.Folder, ffmpegReady, Dispatcher);
        _nav.PositionShown += t =>
        {
            // Nunca más allá del último frame (al llegar al final, mpv puede dar el instante en que acaba).
            if (_lastFrameStart > 0)
                t = Math.Min(t, _lastFrameStart);
            _position = t;
            Seek.Position = t;
            Zoom.Position = t;
            UpdateTimeText();
        };
        _nav.SpeedChanged += UpdatePlayState;

        _surface = new VideoSurface(this, VideoArea);
        _surface.Clicked += OnVideoClicked;
        // El primer clic ya pausó o reanudó: el doble clic lo deshace y cambia a pantalla completa.
        _surface.DoubleClicked += () =>
        {
            UndoVideoClick();
            ToggleFullscreen();
        };
        // Arrastrar en horizontal sobre la imagen mueve frame a frame, como ↑/↓ pero proporcional al ratón.
        _surface.DragStarted += () =>
        {
            ReturnFocusToVideo();
            // Al soltar se sigue como estaba: reproduciendo (hacia delante o atrás) o en pausa, como con la barra.
            _scrubResumeSpeed = _nav.Speed;
            _nav.Pause();
            _scrubStart = _nav.CurrentTime;
            _scrubStartFrame = Zoom.Grid.FrameAt(_scrubStart);
            _scrubLastFrame = 0;
        };
        _surface.DragEnded += () =>
        {
            var speed = _scrubResumeSpeed;
            _scrubResumeSpeed = 0;
            if (speed == 0 || !_nav.Paused)
                return;
            if (speed < 0)
                _nav.Backward();
            else
                _nav.TogglePlay();
        };
        _surface.DragMoved += dx =>
        {
            long frames = dx / Math.Max(1, _store.Settings.ScrubPixelsPerFrame);
            if (frames == _scrubLastFrame)
                return;
            _scrubLastFrame = frames;
            // Con bucle (empezando dentro del tramo, el suyo), al pasar el último frame se sigue por el primero y al revés.
            if (LoopFrames(_scrubStartFrame) is { } loop)
            {
                var count = loop.Last - loop.First + 1;
                var n = loop.First + (int)(((_scrubStartFrame - loop.First + frames) % count + count) % count);
                _nav.ScrubTo(Zoom.Grid.TimeOf(n));
                return;
            }
            _nav.ScrubTo(_scrubStart + frames / Fps);
        };
        _surface.Resized += () => _engine?.Refresh();
        _surface.Wheel += OnVideoWheel;
        _surface.MiddleClicked += ResetRate;
        // Arrastrando (sobre la imagen o en las barras), pulsar el botón derecho marca un tramo desde el frame que
        // se ve hasta el que se vea al soltarlo.
        _surface.MarkStarted += BeginMark;
        _surface.MarkEnded += EndMark;
        Seek.MarkStarted += BeginMark;
        Seek.MarkEnded += EndMark;
        Zoom.MarkStarted += BeginMark;
        Zoom.MarkEnded += EndMark;

        Seek.SeekRequested += OnTimelineSeek;
        Seek.WheelSeekRequested += (notches, modifiers) => _nav.SeekRelative(notches * SeekStep(modifiers));
        Zoom.SeekRequested += OnTimelineSeek;
        // Zoom: arrastrar con botón derecho en cualquiera de las dos barras, menú contextual o Ctrl + Shift + rueda
        // (en la de zoom basta Ctrl + rueda; en la principal, Ctrl + rueda salta como Ctrl + ← / →).
        Seek.RangeSelected += (start, end) => SetFreeZoom((start, end));
        Zoom.RangeSelected += (start, end) => SetFreeZoom((start, end));
        Seek.MenuRequested += t => ShowTimelineMenu(Seek, t);
        Zoom.MenuRequested += t => ShowTimelineMenu(Zoom, t);
        Seek.ZoomWheel += OnZoomWheel;
        Zoom.ZoomWheel += OnZoomWheel;
        Seek.ZoomDragged += OnZoomDragged;
        Seek.ZoomDragEnded += OnZoomDragEnded;
        Zoom.PanRequested += s => { if (_zoomRange is { } z) SetFreeZoom((z.Start + s, z.End + s)); };
        Zoom.StepRequested += frames =>
        {
            for (var i = 0; i < Math.Abs(frames); i++)
                StepFrame(Math.Sign(frames));
        };
        _cacheTimer.Tick += (_, _) =>
        {
            if (_cacheDirty && _engine != null)
            {
                _cacheDirty = false;
                Seek.CachedRanges = Zoom.CachedRanges = _engine.CachedRanges();
            }
        };

        App.Mark("eventos");
        InitBookmarks();
        App.Mark("bookmarks");
        InitClipExport();
        InitHistory();
        ApplySettings();
        App.Mark("ajustes");
        SetVolumeUi(_store.Settings.Volume, _store.Settings.Mute);
        // Los bucles, como se dejaron (se le pasan a mpv al cargar el vídeo, con su duración).
        _loopVideo = _store.Settings.Loop;
        _loopRange = _store.Settings.LoopRange;
        UpdateLoopButtons();

        _saveTimer.Tick += (_, _) => SavePosition();
        Loaded += OnLoaded;
        ContentRendered += (_, _) =>
        {
            App.Mark("ventana visible");
            _windowShown = true;
            OnStartupShown();
            // Si el vídeo no llega a verse (no se pudo abrir), la tarjeta vuelve a pintar igualmente.
            _ = Task.Delay(3000).ContinueWith(_ => Dispatcher.BeginInvoke(UseHardwareRendering));
        };
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
        Drop += OnDrop;
    }

    /// <summary>
    /// Crea ya las ventanas nativas (sin mostrarlas) y arranca mpv: así carga el vídeo mientras WPF
    /// termina de montar y pintar la ventana, en lugar de después.
    /// </summary>
    public void StartEarly()
    {
        _surface.Create();
        StartPlayback();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        // Abierta sin vídeo: el panel con los bookmarks de todos los vídeos, listo para ir a uno.
        if (_initialPath == null)
        {
            ScopeAll.IsChecked = true;
            OpenSearch();
            if (Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "inicio")
                _ = StartupSnapshot();
        }
        if (!_started)
        {
            _surface.Create();
            StartPlayback();
        }
    }

    private async void StartPlayback()
    {
        _started = true;
        await _playerReady;
        _player.SetWindow(_surface.Handle);
        HookPlayerEvents();
        _player.Start();

        if (_initialPath != null)
        {
            Open(_initialPath);
            App.Mark("loadfile enviado");
        }
    }

    private void HookPlayerEvents()
    {
        // Los eventos de mpv llegan en su propio hilo: los pasamos al de la interfaz.
        void Ui(Action action) => Dispatcher.BeginInvoke(action);

        _player.PositionChanged += t => Ui(() => _nav.OnMpvPosition(t));
        _player.Restarted += () => Ui(_nav.OnMpvRestart);
        // Con los dos bucles, si se reproducía desde después del tramo y se llega al final, el del vídeo vuelve a
        // empezar (y al llegar al tramo lo atrapa el suyo). Por si mpv se queda parado al final, se sigue por el tramo.
        _player.EndReached += () => Ui(async () =>
        {
            // Solo si se estaba reproduciendo (no al dejar la imagen en el último frame a mano). La pausa de mpv llega
            // justo después del aviso del final: se espera un momento.
            var playing = DateTime.UtcNow - _lastPlaying < TimeSpan.FromSeconds(1) || !_nav.Paused;
            await Task.Delay(150);
            if (playing && _loopVideo && RangeLooping && _nav.Paused && !_nav.Reversing)
                _nav.TogglePlay();
        });
        _player.DurationChanged += d => Ui(() =>
        {
            _duration = d;
            Seek.Duration = Zoom.Duration = _nav.Duration = d;
            UpdateLastFrame();
            ApplyLoop(); // el bucle del vídeo entero va hasta su duración
            if (_current != null)
                _current.Duration = d;
            UpdateTimeText();
            if (d > 0)
                ApplyPendingBookmark();
        });
        _player.FpsChanged += fps => Ui(() =>
        {
            _fps = fps;
            _nav.Fps = Fps;
            // Para los timecodes de sus bookmarks en el panel cuando ya no esté abierto.
            if (_current != null && fps > 0)
                _current.Fps = fps;
            UpdateFrameGrid();
        });
        _player.PauseChanged += paused => Ui(() => _nav.OnMpvPause(paused));
        _player.VolumeChanged += v => Ui(() => SetVolumeUi(v, _store.Settings.Mute));
        _player.MuteChanged += m => Ui(() => SetVolumeUi(_store.Settings.Volume, m));
        _player.TracksChanged += tracks => Ui(() =>
        {
            _tracks = tracks;
            // Sin nada que elegir (una sola pista de audio y sin subtítulos), el botón se apaga.
            TracksButton.IsEnabled = tracks.Count(t => t.Type == "audio") > 1 || tracks.Any(t => t.Type == "sub");
        });
        _player.FileLoaded += () => Ui(() => { VideoArea.Visibility = Visibility.Visible; EmptyHint.Visibility = Visibility.Collapsed; });
        _player.FirstFrame += () => Ui(() =>
        {
            if (!_firstFrameSeen)
            {
                _firstFrameSeen = true;
                App.Mark("primer frame");
                OnStartupShown();
                // El motor de inspección se crea después del primer frame para no retrasar el arranque.
                Dispatcher.BeginInvoke(CreateEngine, DispatcherPriority.Background);
            }
        });
    }

    // Arranque terminado cuando la ventana ya se pintó y se ve el primer frame (o no hay vídeo): se apunta lo que tardó
    // cada parte y la ventana vuelve a pintar con la tarjeta.
    private void OnStartupShown()
    {
        if (_startupLogged || !_windowShown || !_firstFrameSeen && _initialPath != null)
            return;
        _startupLogged = true;
        if (_firstFrameSeen)
            LogStartup(string.Join(", ", App.StartupMarks.Select(m => $"{m.Stage} {(m.At - _processStart).TotalMilliseconds:0}")) +
                $" (ms desde el arranque; JIT: {System.Runtime.JitInfo.GetCompiledMethodCount()} métodos, " +
                $"{System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds:0} ms)");
        Dispatcher.BeginInvoke(UseHardwareRendering, DispatcherPriority.Background);
    }

    private void UseHardwareRendering()
    {
        RenderOptions.ProcessRenderMode = RenderMode.Default;
        if (HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.CompositionTarget is { RenderMode: RenderMode.SoftwareOnly } target)
            target.RenderMode = RenderMode.Default;
    }

    private async void CreateEngine()
    {
        try
        {
            await _ffmpegReady;
            _engine = new InspectionEngine(_surface.Handle, _store.Settings.FrameCacheMegabytes * 1024L * 1024);
            _engine.CacheChanged += () => _cacheDirty = true;
            _nav.AttachEngine(_engine, _currentPath);
            if (_frameIndex != null)
                _engine.SetKeyframes(_frameIndex.Keyframes);
            _cacheTimer.Start();
            if (BookmarkSelfTestEnabled)
                _ = RunBookmarkSelfTest();
            if (ZoneSelfTestEnabled)
                _ = RunZoneSelfTest();
            if (JumpSelfTestEnabled)
                _ = RunJumpSelfTest();
            if (SettingsSelfTestEnabled)
                _ = RunSettingsSelfTest();
            if (ClipSelfTestEnabled)
                _ = RunClipSelfTest();
            if (HistorySelfTestEnabled)
                _ = RunHistorySelfTest();
            // VIDEOSEEKER_SELFTEST=arranque: cierra al poco del primer frame (para medir el arranque y el cierre).
            if (Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "arranque")
                _ = Task.Delay(int.TryParse(Environment.GetEnvironmentVariable("VIDEOSEEKER_WAIT"), out var wait) ? wait : 1500).ContinueWith(_ => Dispatcher.BeginInvoke(() =>
                {
                    LogStartup($"pintado: {HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.CompositionTarget.RenderMode} " +
                        $"(la app: {RenderOptions.ProcessRenderMode})");
                    Close();
                }));
            if (SelfTest.Enabled)
                _ = new SelfTest(_nav, () => _engine, _duration, Path.Combine(_store.Folder, "selftest.log"), Close,
                    (start, end) => SetFreeZoom((start, end))).RunAsync();
        }
        catch (Exception e)
        {
            // Sin motor la app sigue funcionando: mpv hace todo el trabajo, solo que más lento al ir atrás.
            LogStartup($"motor de inspección no disponible: {e.Message}");
        }
    }

    public void OpenFromOtherInstance(string path)
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        if (!string.IsNullOrEmpty(path))
            OpenByHand(path);
    }

    // Abierto a mano (arrastrando, con Abrir o desde otra instancia): el panel pasa a los bookmarks de este vídeo,
    // para empezar a trabajar en ellos. Desde el panel no: ahí se está navegando por la colección.
    private void OpenByHand(string path)
    {
        ScopeThis.IsChecked = true;
        Open(path);
    }

    /// <summary>
    /// Abre un vídeo donde se dejó o, con "startAt", en ese instante (al ir a un bookmark de otro vídeo o volver por
    /// el historial; "historyIndex" es la entrada del historial a la que se va). Empieza a reproducirse aunque el
    /// anterior estuviera en pausa, como en VLC o mpv; con "keepPause" (bookmark de otro vídeo) sigue como estaba.
    /// </summary>
    private void Open(string path, double? startAt = null, int? historyIndex = null, bool keepPause = false)
    {
        if (!File.Exists(path))
        {
            Dialog.Inform(this, $"No se encuentra el archivo:\n{path}");
            return;
        }

        SavePosition();
        RecordHistory(path, historyIndex);

        var key = Fingerprint.Compute(path);
        if (!_store.Library.Videos.TryGetValue(key, out var entry))
            _store.Library.Videos[key] = entry = new VideoEntry();
        entry.Path = Path.GetFullPath(path);
        entry.LastOpened = DateTime.Now;
        _current = entry;

        // Reanudar donde se dejó (si está en los ajustes), salvo que fuera casi al principio o al final.
        double? start = startAt ?? (_store.Settings.ResumePlayback && entry.LastPosition > 5
            && (entry.Duration <= 0 || entry.LastPosition < entry.Duration - 10) ? entry.LastPosition : null);

        _position = start ?? 0;
        _currentPath = path;
        Seek.CachedRanges = Zoom.CachedRanges = [];
        CloseZone();
        _awaitingDuration = true;
        // Con "keepPause" se sigue como estaba. Hay que mirarlo antes de OnFileOpening, que para el bucle desde la caché
        // dejando la pausa puesta: con un vídeo corto entero en caché y el bucle del vídeo, ir al bookmark de otro
        // vídeo lo dejaba en pausa aunque se estuviera reproduciendo.
        var play = !keepPause || _nav.Playing;
        _nav.OnFileOpening(path, play);
        _player.Load(path, start, play);
        LoadFrameIndex(path, key);
        LoadBookmarks(key, entry.Path);
        Title = $"{Path.GetFileName(path)} — VideoSeeker";
        _store.SaveLibrary();
        _saveTimer.Start();
    }

    // El índice guardado se lee al momento; si no hay, se construye en segundo plano y se guarda.
    private async void LoadFrameIndex(string path, string key)
    {
        _indexing?.Cancel();
        var cts = _indexing = new CancellationTokenSource();
        _frameIndex = null;
        UpdateFrameGrid();
        var file = Path.Combine(_store.Folder, "cache", "index", key + ".bin");
        try
        {
            var index = await Task.Run(async () =>
            {
                if (FrameIndex.Load(file) is { } saved)
                    return saved;
                await _ffmpegReady;
                FrameIndex built;
                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                try { built = FrameIndex.Build(path, cts.Token); }
                finally { Thread.CurrentThread.Priority = ThreadPriority.Normal; }
                if (built.Count > 0)
                    built.Save(file);
                return built;
            }, cts.Token);
            if (!cts.IsCancellationRequested && index.Count > 0)
            {
                _frameIndex = index;
                _engine?.SetKeyframes(index.Keyframes);
                UpdateFrameGrid();
            }
        }
        catch (Exception e) when (e is OperationCanceledException or FFmpegException or IOException or UnauthorizedAccessException)
        {
            // Sin índice se sigue estimando el número de frame.
        }
    }

    private void SavePosition()
    {
        if (_current == null)
            return;
        _current.LastPosition = _position;
        _store.SaveLibrary();
    }

    private double Fps => _fps > 0 ? _fps : 30;

    private void UpdateTimeText()
    {
        // El total es el instante del último frame (con la misma precisión que el actual): así, al final, los dos son
        // iguales. La duración del contenedor acaba un frame (o más, si el audio dura más) después.
        var total = _lastFrameStart > 0 ? _lastFrameStart : _duration;
        TimeText.Text = $"{TimeFormat.Precise(_position, Fps)} / {TimeFormat.Precise(total, Fps)}";
        FrameText.Text = _frameIndex is { } index ? $"frame {index.FrameNumber(_position)}"
            // Mientras se construye el índice, aproximado a partir del fps del contenedor.
            : _fps > 0 ? $"frame ~{Math.Round(_position * _fps):0}" : "";
        BookmarkText.Text = CurrentBookmarkText();
        UpdateRangePosition();
    }

    // Inicio del último frame del vídeo (del índice si ya está; si no, el que cae justo antes del final).
    private double _lastFrameStart;

    private void UpdateLastFrame()
    {
        _lastFrameStart = _duration <= 0 ? 0
            : _frameIndex is { Count: > 0 } index ? index.TimeOf(index.Count - 1)
            : Zoom.Grid.TimeOf(Zoom.Grid.FrameAt(_duration - 1e-4));
        Seek.LastFrameStart = _nav.LastFrameStart = _lastFrameStart;
    }

    private void UpdateFrameGrid()
    {
        Zoom.Grid = new FrameGrid(_frameIndex, Fps);
        // Los bookmarks sin fin en el archivo (de antes) miden un frame de esta rejilla.
        _bookmarks?.RefreshOpenEnds();
        UpdateLastFrame();
        UpdateTimeText();
        UpdateZoomUi();
    }

    private void OnTimelineSeek(double time, bool final)
    {
        if (final)
            _nav.SeekTo(time);
        else
            _nav.DragTo(time);
    }

    // Tramo mínimo: un frame (puesto a mano con primer y último frame). Con Ctrl + rueda, no menos de 10.
    private double MinZoomWidth => 1 / Fps;
    private double MinWheelZoomWidth => 10 / Fps;

    // ---- Zona ampliada ----
    // Una sola: libre (Z, Ctrl + rueda, arrastre con el botón derecho, marcado) o igual a la región seleccionada
    // (_zoomBookmark). Seleccionar un bookmark la convierte en la zona; I/O, los botones ⇤ ⇥, asignar frames y las
    // banderitas cambian la región (EditZone, con su paso de deshacer); lo que crea una zona libre suelta la región.

    // Arrastrando el inicio o el fin dentro de la propia barra de zoom: no se reencuadra hasta soltar (la escala no
    // debe cambiar bajo el ratón).
    private bool _freezeZoomView;

    /// <summary>La región que es la zona ampliada, o null si la zona es libre (o no hay).</summary>
    private Bookmark? ZoneBookmark => _bookmarks?.Find(_zoomBookmark);

    /// <summary>
    /// Pone la barra de zoom en el tramo dado (se ajusta al vídeo); null la cierra. Con "exact" (una región) no se
    /// ensancha ni se desplaza: la zona es la región tal cual.
    /// </summary>
    private void ApplyZone((double Start, double End)? range, bool exact = false)
    {
        if (range is { } r)
        {
            if (_duration <= 0)
                return;
            if (exact)
            {
                range = (Math.Max(0, r.Start), Math.Max(r.End, r.Start + 1e-3));
            }
            else
            {
                var width = Math.Clamp(r.End - r.Start, Math.Min(MinZoomWidth, _duration), _duration);
                var start = Math.Clamp(r.Start, 0, _duration - width);
                range = (start, start + width);
            }
        }
        _zoomRange = range;
        Seek.Zoom = range;
        if (range is { } z && !_freezeZoomView)
            Zoom.SetRange(z.Start, z.End);
        ZoomRow.Visibility = range != null ? Visibility.Visible : Visibility.Collapsed;
        _nav.SetRegion(range);
        ApplyLoop();
        UpdateZoomUi();
    }

    /// <summary>Zona libre (o null para cerrarla): suelta la región seleccionada, que no cambia.</summary>
    private void SetFreeZoom((double Start, double End)? range)
    {
        if (_zoomBookmark != null)
            Select(null);
        ApplyZone(range);
    }

    /// <summary>Esc, la X o Alt + X: cierra la zona y deselecciona.</summary>
    private void CloseZone() => SetFreeZoom(null);

    /// <summary>La zona sigue a su región (al seleccionarla, al cambiarla, al deshacer).</summary>
    private void FollowZoneBookmark()
    {
        // Entre abrir un vídeo y saber su duración no se puede ampliar: ApplyPendingBookmark lo hace.
        if (ZoneBookmark is not { } b || _awaitingDuration || _duration <= 0)
            return;
        ApplyZone((b.Start, EndOf(b)), exact: true);
    }

    /// <summary>
    /// Cambia los extremos de la zona: con una región, la región (un paso de deshacer); si no, la zona libre.
    /// </summary>
    private void EditZone(double start, double end, string what = "ajustar")
    {
        if (ZoneBookmark is { } b && _bookmarks != null)
            _bookmarks.Update(b.Id, x => { x.Start = start; x.End = end; }, label: what);
        else
            ApplyZone((start, end));
    }

    /// <summary>"del tramo" o "de «Nombre»", para los menús y tooltips.</summary>
    private string ZoneOf() => ZoneBookmark is { } b ? $"de «{b.Name}»" : "del tramo";

    /// <summary>
    /// Arrastrando un extremo de un tramo: el otro hace de ancla. Si se lleva más allá de él, se intercambian (el frame
    /// del ancla sigue dentro); nunca queda menos de un frame. Devuelve el tramo y el frame que enseñar (el del extremo
    /// que se mueve).
    /// </summary>
    private (double Start, double End, double Shown) DragEdge((double Start, double End) origin, BookmarkPart part, double time)
    {
        var grid = Zoom.Grid;
        time = Math.Clamp(time, 0, _duration);
        if (part == BookmarkPart.End)
        {
            var anchor = grid.FrameAt(origin.Start);
            // El fin cae en el borde entre frames más cercano.
            var k = grid.FrameAt(time + 0.5 / Fps);
            return k > anchor
                ? (origin.Start, ClampEnd(grid.TimeOf(k), grid.TimeOf(k - 1)), grid.TimeOf(k - 1))
                : (grid.TimeOf(k), ClampEnd(grid.TimeOf(anchor + 1), grid.TimeOf(anchor)), grid.TimeOf(k));
        }
        var last = grid.FrameAt(Math.Max(origin.Start, origin.End - 1e-4));
        var frame = grid.FrameAt(time);
        return frame <= last
            ? (grid.TimeOf(frame), origin.End, grid.TimeOf(frame))
            : (grid.TimeOf(last), ClampEnd(grid.TimeOf(frame + 1), grid.TimeOf(frame)), grid.TimeOf(frame));
    }

    // Un fin no pasa de la duración del vídeo (salvo que quedara antes del inicio de su último frame).
    private double ClampEnd(double end, double lastFrameStart) => _duration > lastFrameStart + 1e-4 ? Math.Min(end, _duration) : end;

    // Inicio del frame siguiente al que empieza en "time" (el fin de una región de un frame).
    private double NextFrameStart(double time)
    {
        var grid = Zoom.Grid;
        var frame = grid.FrameAt(time);
        return ClampEnd(grid.TimeOf(frame + 1), grid.TimeOf(frame));
    }

    // Arrastrando una banderita (o la banda) de la zona libre: cómo era al empezar, y el frame que se enseña al soltar
    // si el ajuste lo pide. Con una región, el arrastre es el de la región (OnBookmarkDragged).
    private (double Start, double End)? _zoneDragOrigin;
    private double? _zoomDragEdge;

    private void OnZoomDragged(BookmarkPart part, double time)
    {
        if (ZoneBookmark is { } region)
        {
            OnBookmarkDragged(region, part, time);
            return;
        }
        if (_zoomRange is not { } z)
            return;
        var origin = _zoneDragOrigin ??= z;
        double edge;
        if (part == BookmarkPart.Body)
        {
            ApplyZone((time, time + (origin.End - origin.Start)));
            edge = Zoom.Grid.Snap(_zoomRange!.Value.Start);
        }
        else
        {
            var (start, end, shown) = DragEdge(origin, part, time);
            ApplyZone((start, end));
            edge = shown;
        }
        if (!_store.Settings.ShowDraggedEdgeFrame)
            return;
        _zoomDragEdge = edge;
        _nav.DragTo(edge);
    }

    private void OnZoomDragEnded()
    {
        _zoneDragOrigin = null;
        if (_dragOrigin != null && ZoneBookmark is { } region)
        {
            OnBookmarkReleased(region, moved: true);
            return;
        }
        if (_zoomDragEdge is not { } edge)
            return;
        _zoomDragEdge = null;
        _nav.SeekTo(edge);
    }

    /// <summary>Lo que se ve de la zona en la fila del zoom y en la barra: nombre y color de su región, tooltips.</summary>
    private void UpdateZoneUi()
    {
        var region = ZoneBookmark;
        Seek.ZoomColor = region != null ? ColorOf(region) : null;
        if (region != null)
        {
            ZoneName.Text = region.Name;
            ZoneName.Foreground = TimelineBar.LineBrushFor(ColorOf(region));
            ZoneName.ToolTip = $"La zona es la región «{region.Name}»: I / O, ⇤ ⇥ y las banderitas la cambian";
            ZoneName.Visibility = Visibility.Visible;
        }
        else
        {
            ZoneName.Visibility = Visibility.Collapsed;
        }
        SaveZoneButton.Visibility = region == null && _zoomRange != null ? Visibility.Visible : Visibility.Collapsed;
        MarkFirstButton.ToolTip = $"Asignar el frame en pantalla al inicio {ZoneOf()} (I)";
        MarkLastButton.ToolTip = $"Asignar el frame en pantalla al fin {ZoneOf()} (O)";
        CloseZoomButton.ToolTip = region != null ? $"Cerrar zoom y deseleccionar «{region.Name}» (Esc)" : "Cerrar zoom (Esc)";
        UpdateLoopButtons();
    }

    // "Ampliar sección": ±5 s alrededor del instante.
    private void ZoomAround(double time) => SetFreeZoom((time - 5, time + 5));

    // Ctrl + rueda: acerca o aleja manteniendo quieto el instante bajo el cursor. Alejado del todo, se cierra.
    private void OnZoomWheel(double anchor, int delta)
    {
        if (_duration <= 0)
            return;
        var (start, end) = _zoomRange ?? (0, _duration);
        var factor = Math.Pow(2, -delta / 120.0);
        var newStart = anchor - (anchor - start) * factor;
        var newEnd = anchor + (end - anchor) * factor;
        if (newEnd - newStart < MinWheelZoomWidth && newEnd - newStart < end - start)
        {
            var scale = MinWheelZoomWidth / (newEnd - newStart);
            newStart = anchor - (anchor - newStart) * scale;
            newEnd = anchor + (newEnd - anchor) * scale;
        }
        SetFreeZoom(newEnd - newStart >= _duration ? null : (newStart, newEnd));
    }

    private int? _markFrom;

    private void BeginMark()
    {
        var frame = Zoom.Grid.FrameAt(_nav.CurrentTime);
        _markFrom = frame;
        Seek.MarkAnchor = Zoom.MarkAnchor = Zoom.Grid.TimeOf(frame);
    }

    private void EndMark(bool commit)
    {
        if (_markFrom is not { } from)
            return;
        _markFrom = null;
        Seek.MarkAnchor = Zoom.MarkAnchor = null;
        if (!commit)
            return;
        var to = Zoom.Grid.FrameAt(_nav.CurrentTime);
        var (first, last) = (Math.Min(from, to), Math.Max(from, to));
        SetFreeZoom((Zoom.Grid.TimeOf(first), Math.Min(Zoom.Grid.TimeOf(last + 1), _duration)));
    }

    // Bucle del vídeo entero (R o el botón junto al play). Se recuerda.
    private void ToggleVideoLoop()
    {
        _loopVideo = !_loopVideo;
        _store.Settings.Loop = _loopVideo;
        _store.SaveSettings();
        ApplyLoop();
        ShowToast(_loopVideo ? "Bucle del vídeo" : "Sin bucle del vídeo");
    }

    // Bucle del tramo ampliado (Shift + R o el botón junto a la X del tramo). Se recuerda para los siguientes tramos;
    // sin él, ampliar no cambia cómo se reproduce.
    private void ToggleRangeLoop()
    {
        _loopRange = !_loopRange;
        _store.Settings.LoopRange = _loopRange;
        _store.SaveSettings();
        ApplyLoop();
        // Si se está fuera del tramo, el bucle empieza por su principio.
        if (RangeLooping && _zoomRange is { } z && (_nav.CurrentTime < z.Start || _nav.CurrentTime > z.End))
            _nav.SeekTo(z.Start);
        ShowToast(_loopRange ? "Bucle " + ZoneOf() : "Sin bucle " + ZoneOf());
    }

    private void ApplyLoop()
    {
        // Navigator recibe el bucle que manda (el del tramo o, si no, el del vídeo entero) y si hay bucle del vídeo
        // para lo que queda fuera del tramo.
        _nav.LoopVideo = _loopVideo;
        _nav.SetLoop(LoopRange, wholeFile: !RangeLooping && _loopVideo);
        // A mpv solo cuando cambia (el tramo cambia a cada paso de un arrastre). Su A-B (el del tramo) manda sobre
        // el loop-file dentro del tramo.
        if (_loopVideo != _loopFile)
            _player.SetLoopFile(_loopFile = _loopVideo);
        UpdateLoopButtons();
    }

    private void UpdateLoopButtons()
    {
        LoopButton.Foreground = (Brush)FindResource(_loopVideo ? "Accent" : "Fg");
        LoopButton.ToolTip = _loopVideo ? "Quitar el bucle del vídeo (R)" : "Bucle del vídeo (R)";
        RangeLoopButton.Foreground = (Brush)FindResource(_loopRange ? "Accent" : "Fg");
        RangeLoopButton.ToolTip = (_loopRange ? "Quitar el bucle " : "Bucle ") + ZoneOf() + " (Shift + R)";
    }

    // Lo que repite el bucle que manda: el tramo con su bucle puesto o, si no, el vídeo entero con el suyo.
    private (double Start, double End)? LoopRange =>
        RangeLooping ? _zoomRange : _loopVideo && _duration > 0 ? (0.0, _duration) : null;

    // Primer y último frame de lo que repite el bucle para moverse desde "frame": el tramo si tiene el bucle puesto y
    // "frame" está dentro; si no, el vídeo entero si tiene el suyo; null si no hay bucle que dé la vuelta.
    private (int First, int Last)? LoopFrames(int frame)
    {
        var grid = Zoom.Grid;
        if (RangeLooping && _zoomRange is { } z)
        {
            // El final del tramo es el inicio del frame siguiente, que ya queda fuera.
            var (first, last) = (grid.FrameAt(z.Start), grid.FrameAt(z.End - 1e-4));
            if (frame >= first && frame <= last)
                return (first, last);
        }
        return _loopVideo && _duration > 0 ? (0, grid.FrameAt(_duration - 1e-4)) : null;
    }

    // Un frame adelante o atrás; con bucle y dentro del tramo, del último se pasa al primero y al revés,
    // para revisar que un ciclo encaja.
    private void StepFrame(int direction)
    {
        var frame = Zoom.Grid.FrameAt(_nav.CurrentTime);
        if (LoopFrames(frame) is { } loop)
        {
            if (direction > 0 && frame == loop.Last)
            {
                _nav.ScrubTo(Zoom.Grid.TimeOf(loop.First));
                return;
            }
            if (direction < 0 && frame == loop.First)
            {
                _nav.ScrubTo(Zoom.Grid.TimeOf(loop.Last));
                return;
            }
        }
        _nav.Step(direction);
    }

    // La duración del tramo y sus frames van abajo, junto al tiempo (UpdateRangePosition).
    private void UpdateZoomUi()
    {
        UpdateRangePosition();
        UpdateZoneUi();
    }

    /// <summary>
    /// Junto al tiempo y al frame, entre corchetes, lo mismo respecto al tramo ampliado: dentro, el tiempo desde su
    /// primer frame / lo que dura y "frame N/total"; antes de él, con "−", lo que falta hasta su primer frame; después, con "+", lo
    /// que hay desde su último frame. Sin tramo, nada.
    /// </summary>
    private void UpdateRangePosition()
    {
        if (_zoomRange is not { } z)
        {
            RangePosText.Text = "";
            return;
        }
        var grid = Zoom.Grid;
        var (first, last, frame) = (grid.FrameAt(z.Start), grid.FrameAt(z.End - 1e-4), grid.FrameAt(_position));
        // Por debajo del minuto, segundos ("0.583 s"; sin la unidad delante de "/": "0.250 / 0.583 s").
        // Dentro del tramo, los dos tiempos con el mismo formato (el de su duración); fuera, cada uno el suyo.
        string Time(double seconds, bool unit = true, double? scale = null) => TimeFormat.UseFrames && Fps > 0 ? TimeFormat.Timecode(seconds, Fps)
            : (scale ?? seconds) < 60 ? seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + (unit ? " s" : "")
            : TimeFormat.Format(seconds, withMilliseconds: true);
        string Frames(int n) => n == 1 ? "1 frame" : $"{n} frames";
        RangePosText.Text = frame < first
            ? $"[−{Time(grid.TimeOf(first) - grid.TimeOf(frame))} · −{Frames(first - frame)}]"
            : frame > last
                ? $"[+{Time(grid.TimeOf(frame) - grid.TimeOf(last))} · +{Frames(frame - last)}]"
                : $"[{Time(grid.TimeOf(frame) - grid.TimeOf(first), unit: false, scale: z.End - z.Start)} / {Time(z.End - z.Start)} · frame {frame - first + 1}/{last - first + 1}]";
    }

    private void ShowTimelineMenu(UIElement target, double time)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        AddBookmarkMenuItems(menu, time);
        menu.Items.Add(new Separator());
        var zoom = new MenuItem { Header = "Ampliar sección (±5 s)", InputGestureText = "Z" };
        zoom.Click += (_, _) => ZoomAround(time);
        menu.Items.Add(zoom);
        var current = Zoom.Grid.FrameAt(_nav.CurrentTime);
        var markFirst = new MenuItem { Header = $"Asignar el frame en pantalla ({current}) al inicio {ZoneOf()}", InputGestureText = "I" };
        markFirst.Click += (_, _) => MarkFirstFrame();
        menu.Items.Add(markFirst);
        var markLast = new MenuItem { Header = $"Asignar el frame en pantalla ({current}) al fin {ZoneOf()}", InputGestureText = "O" };
        markLast.Click += (_, _) => MarkLastFrame();
        menu.Items.Add(markLast);
        if (_zoomRange != null)
        {
            // En la barra de zoom, "time" es el inicio del frame bajo el cursor: sirve para medir tramos exactos.
            if (target == Zoom)
            {
                var grid = Zoom.Grid;
                var frame = grid.FrameAt(time);
                menu.Items.Add(new Separator());
                var first = new MenuItem { Header = $"Asignar el frame bajo el cursor ({frame}) al inicio {ZoneOf()}" };
                first.Click += (_, _) => SetZoomFirstFrame(frame);
                menu.Items.Add(first);
                var last = new MenuItem { Header = $"Asignar el frame bajo el cursor ({frame}) al fin {ZoneOf()}" };
                last.Click += (_, _) => SetZoomLastFrame(frame);
                menu.Items.Add(last);
                menu.Items.Add(new Separator());
            }
            var loop = new MenuItem { Header = $"Bucle {ZoneOf()}", InputGestureText = "Shift+R", IsCheckable = true, IsChecked = _loopRange };
            loop.Click += (_, _) => ToggleRangeLoop();
            menu.Items.Add(loop);
            var close = new MenuItem
            {
                Header = ZoneBookmark is { } region ? $"Cerrar zoom y deseleccionar «{region.Name}»" : "Cerrar zoom", InputGestureText = "Esc",
            };
            close.Click += (_, _) => CloseZone();
            menu.Items.Add(close);
            if (ExportTarget() is { } export)
            {
                menu.Items.Add(new Separator());
                AddExportMenu(menu, export.Video, export.Start, export.End, export.Name);
            }
        }
        menu.IsOpen = true;
    }

    // El tramo empieza al inicio del frame; si quedaba por detrás del final, el final pasa a ser ese mismo frame.
    // Con una región, se cambia la región. Sin tramo, se abre uno de 10 s que empieza ahí.
    private void SetZoomFirstFrame(int frame)
    {
        var grid = Zoom.Grid;
        if (_zoomRange is not { } z)
        {
            SetFreeZoom((grid.TimeOf(frame), grid.TimeOf(frame) + 10));
            return;
        }
        var start = grid.TimeOf(frame);
        EditZone(start, z.End > start + 1e-4 ? z.End : NextFrameStart(start), "asignar el inicio de");
    }

    // El tramo acaba al final del frame (inicio del siguiente), así que el frame queda dentro. Con una región, se
    // cambia la región. Sin tramo, se abre uno de 10 s que acaba ahí.
    private void SetZoomLastFrame(int frame)
    {
        var grid = Zoom.Grid;
        var end = NextFrameStart(grid.TimeOf(frame));
        if (_zoomRange is not { } z)
        {
            SetFreeZoom((end - 10, end));
            return;
        }
        EditZone(z.Start < end - 1e-4 ? z.Start : grid.TimeOf(frame), end, "asignar el fin de");
    }

    // Con el frame que se ve: para ajustar el tramo sin apuntar con el ratón (en vídeos largos la banda de la
    // barra principal mide un píxel).
    private void MarkFirstFrame() => SetZoomFirstFrame(Zoom.Grid.FrameAt(_nav.CurrentTime));

    private void MarkLastFrame() => SetZoomLastFrame(Zoom.Grid.FrameAt(_nav.CurrentTime));

    // Shift + I / Shift + O: al primer o último frame del tramo.
    private void GoToRangeEdge(bool first)
    {
        if (_zoomRange is not { } z)
            return;
        var grid = Zoom.Grid;
        _nav.ScrubTo(grid.TimeOf(first ? grid.FrameAt(z.Start) : grid.FrameAt(z.End - 1e-4)));
    }

    // Fin: al último frame del vídeo, en pausa.
    private void GoToLastFrame()
    {
        if (_duration <= 0)
            return;
        var grid = Zoom.Grid;
        _nav.ScrubTo(grid.TimeOf(grid.FrameAt(Math.Max(0, _duration - 1e-3))));
    }

    private void OnMarkFirstClick(object sender, RoutedEventArgs e) => MarkFirstFrame();

    private void OnMarkLastClick(object sender, RoutedEventArgs e) => MarkLastFrame();

    private void OnCloseZoomClick(object sender, RoutedEventArgs e) => CloseZone();

    // Lo mismo que B con el tramo libre: una región con sus límites, seleccionada, y el campo del nombre abierto.
    private void OnSaveZoneClick(object sender, RoutedEventArgs e)
    {
        if (_zoomRange is { } z && _zoomBookmark == null)
            AddRegion(z.Start, z.End);
    }

    private void OnLoopClick(object sender, RoutedEventArgs e) => ToggleVideoLoop();

    private void OnRangeLoopClick(object sender, RoutedEventArgs e) => ToggleRangeLoop();
    private void OnStepBackClick(object sender, RoutedEventArgs e) => StepFrame(-1);
    private void OnStepForwardClick(object sender, RoutedEventArgs e) => StepFrame(1);

    private void UpdatePlayState()
    {
        var speed = _nav.Speed;
        if (speed != 0 || _wasPlaying)
            _lastPlaying = DateTime.UtcNow;
        _wasPlaying = speed != 0;
        PlayButton.Content = speed == 0 ? GlyphPlay : GlyphPause;
        // La velocidad a la que va (J/L la multiplican) o, en pausa, la elegida. Si no es 1×, resaltada en ámbar
        // para que no pase desapercibida.
        var shown = speed == 0 ? _nav.Rate : speed;
        SpeedText.Text = SpeedLabel(shown);
        var changed = shown != 1;
        SpeedText.Foreground = changed ? SpeedChangedBrush : (Brush)FindResource("Fg");
        SpeedButton.Background = changed ? SpeedChangedBack : Brushes.Transparent;
    }

    private static readonly Brush SpeedChangedBrush = Frozen(Color.FromRgb(0xF0, 0xB4, 0x29));
    private static readonly Brush SpeedChangedBack = Frozen(Color.FromArgb(0x40, 0xF0, 0xB4, 0x29));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    // Negativa, hacia atrás (también la marcha atrás de J).
    private static string SpeedLabel(double speed) => speed < 0 ? $"−{-speed:0.##}×" : $"{speed:0.##}×";

    // Velocidad: el botón abre las habituales; la rueda encima (y, según el ajuste, sobre el vídeo) va de 0,1 en 0,1,
    // de -20× a 20×; el botón central del ratón la devuelve a 1×.
    private static readonly double[] SpeedPresets = [-2, -1, -0.5, 0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 4, 10, 20];

    private void OnSpeedClick(object sender, RoutedEventArgs e) => ShowSpeedMenu();

    private ContextMenu ShowSpeedMenu()
    {
        var menu = new ContextMenu { PlacementTarget = SpeedButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
        foreach (var preset in SpeedPresets)
        {
            if (preset == 0.25)
                menu.Items.Add(new Separator());
            var item = new MenuItem { Header = preset == 1 ? "1× (normal)" : SpeedLabel(preset), IsCheckable = true, IsChecked = preset == _nav.Rate };
            item.Click += (_, _) => SetRate(preset);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
        return menu;
    }

    private void OnSpeedWheel(object sender, MouseWheelEventArgs e)
    {
        WheelSteps(e.Delta, steps => SetRate(Navigator.StepRate(_nav.Rate, steps)));
        e.Handled = true;
    }

    private void OnMiddleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;
        ResetRate();
        e.Handled = true;
    }

    private void ResetRate()
    {
        if (_duration > 0)
            SetRate(1);
    }

    private void SetRate(double rate)
    {
        _nav.SetRate(rate);
        ShowToast($"Velocidad {SpeedLabel(_nav.Rate)}");
    }

    // Rueda sobre el vídeo (la ventana nativa o, sin vídeo, el fondo): velocidad o volumen, según el ajuste.
    private void OnVideoWheel(object sender, MouseWheelEventArgs e)
    {
        OnVideoWheel(e.Delta);
        e.Handled = true;
    }

    private void OnVideoWheel(int delta)
    {
        if (_duration <= 0)
            return;
        if (_store.Settings.WheelOnVideo == Settings.WheelVolume)
            WheelSteps(delta, steps =>
            {
                var volume = Math.Clamp(Math.Round(_store.Settings.Volume / 5) * 5 + 5 * steps, 0, 100);
                _player.SetVolume(volume);
                ShowToast(_store.Settings.Mute ? $"Volumen {volume:0} % (silenciado)" : $"Volumen {volume:0} %");
            });
        else
            WheelSteps(delta, steps => SetRate(Navigator.StepRate(_nav.Rate, steps)));
    }

    // Muescas enteras de la rueda (las de los touchpads llegan en trocitos y se van sumando).
    private int _wheelRest;

    private void WheelSteps(int delta, Action<int> apply)
    {
        _wheelRest += delta;
        var steps = _wheelRest / 120;
        if (steps == 0)
            return;
        _wheelRest -= steps * 120;
        apply(steps);
    }

    // Salto de ← / → y de la rueda sobre la barra principal: el de Ctrl, el de Shift o el normal, según los ajustes.
    private double SeekStep(ModifierKeys modifiers)
    {
        var s = _store.Settings;
        return modifiers.HasFlag(ModifierKeys.Control) ? s.SeekSecondsCtrl
            : modifiers.HasFlag(ModifierKeys.Shift) ? s.SeekSecondsShift
            : s.SeekSeconds;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        // Con Alt, WPF manda Key.System y la tecla real en SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        // Escribiendo (nombre de un bookmark, buscador) las teclas son del cuadro de texto.
        if (Keyboard.FocusedElement is TextBox)
        {
            if (ctrl && key == Key.B)
            {
                TogglePanel();
                e.Handled = true;
            }
            else if (ctrl && key == Key.OemComma)
            {
                OpenSettings();
                e.Handled = true;
            }
            return;
        }
        // Con el foco en la lista del panel, las flechas, Enter, F2, Supr y Esc son suyas.
        if (BookmarkList.IsKeyboardFocusWithin
            && key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End or Key.Enter or Key.F2 or Key.Delete or Key.Escape)
            return;
        switch (key)
        {
            case Key.Space:
                _nav.TogglePlay();
                break;
            // En pantalla completa: fijar los controles o que se escondan (como la chincheta). En el panel, Tab es suyo.
            case Key.Tab when _beforeFullscreen != null && !BookmarkPanel.IsKeyboardFocusWithin:
                TogglePinControls();
                break;
            // J/K/L: atrás / pausa / adelante; cada pulsación de J o L duplica la velocidad. Con K pulsada,
            // J y L van frame a frame.
            case Key.K:
                if (!e.IsRepeat)
                    _nav.Pause();
                break;
            case Key.J or Key.L when Keyboard.IsKeyDown(Key.K):
                StepFrame(e.Key == Key.L ? 1 : -1);
                break;
            case Key.J:
                if (!e.IsRepeat)
                {
                    _nav.Backward();
                    ShowToast($"Velocidad {SpeedLabel(_nav.Speed)}");
                }
                break;
            case Key.L:
                if (!e.IsRepeat)
                {
                    _nav.Forward();
                    if (_nav.Speed != 1)
                        ShowToast($"Velocidad {SpeedLabel(_nav.Speed)}");
                }
                break;
            case Key.Up:
                StepFrame(1);
                break;
            case Key.Down:
                StepFrame(-1);
                break;
            // Alt + ← / →: vídeo anterior / siguiente del historial, como en el navegador.
            case Key.Left or Key.Right when Keyboard.Modifiers == ModifierKeys.Alt:
                GoHistory(key == Key.Left ? -1 : 1);
                break;
            case Key.BrowserBack or Key.BrowserForward:
                GoHistory(key == Key.BrowserBack ? -1 : 1);
                break;
            case Key.Left or Key.Right:
                var seconds = SeekStep(Keyboard.Modifiers);
                _nav.SeekRelative(e.Key == Key.Left ? -seconds : seconds);
                break;
            // Bookmarks: B (o Shift + B) crea una región con la zona libre o, sin ella, de un frame en el frame actual;
            // Ctrl + B, el panel.
            case Key.B when ctrl && shift:
                ToggleMarks();
                break;
            case Key.B when ctrl:
                TogglePanel();
                break;
            case Key.B:
                AddBookmark();
                break;
            case Key.F when ctrl:
                OpenSearch();
                break;
            case Key.F2:
                RenameSelected();
                break;
            case Key.Delete:
                DeleteSelected();
                break;
            case Key.Z when ctrl && shift:
            case Key.Y when ctrl:
                UndoBookmarks(redo: true);
                break;
            case Key.Z when ctrl:
                UndoBookmarks(redo: false);
                break;
            // 1–9 y 0: a los diez primeros bookmarks. Re Pág / Av Pág, como los capítulos de mpv: al anterior / siguiente
            // (también [ y ], que llegan como texto).
            // Alt + 1…9: la etiqueta 1…9 al bookmark seleccionado (o a los elegidos en el panel); sustituye su color.
            case (>= Key.D0 and <= Key.D9) or (>= Key.NumPad0 and <= Key.NumPad9) when Keyboard.Modifiers == ModifierKeys.Alt:
                LabelShortcut(e);
                break;
            case >= Key.D0 and <= Key.D9 when Keyboard.Modifiers == ModifierKeys.None:
                JumpToNumber(key - Key.D0);
                break;
            case >= Key.NumPad0 and <= Key.NumPad9 when Keyboard.Modifiers == ModifierKeys.None:
                JumpToNumber(key - Key.NumPad0);
                break;
            case Key.PageUp:
                JumpAdjacent(-1);
                break;
            case Key.PageDown:
                JumpAdjacent(1);
                break;
            // Enter con un bookmark seleccionado va a él (y amplía la región); sin selección, o con Alt + Enter, pantalla completa.
            case Key.Enter when !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && JumpToSelected():
                break;
            case Key.F or Key.Enter:
                ToggleFullscreen();
                break;
            case Key.Escape when _beforeFullscreen != null:
                ToggleFullscreen();
                break;
            // Esc cierra la zona y deselecciona.
            case Key.Escape when _zoomRange != null || _selectedBookmark != null:
                CloseZone();
                break;
            case Key.Escape when BookmarkPanel.Visibility == Visibility.Visible:
                ShowPanel(false);
                break;
            case Key.Z:
                ZoomAround(_nav.CurrentTime);
                break;
            case Key.R when shift:
                ToggleRangeLoop();
                break;
            case Key.R:
                ToggleVideoLoop();
                break;
            // Atajos habituales de los editores de vídeo: I / O marcan el primer / último frame del tramo,
            // Shift + I / O van a ellos y Alt + X quita el tramo.
            case Key.I when shift:
                GoToRangeEdge(first: true);
                break;
            case Key.O when shift:
                GoToRangeEdge(first: false);
                break;
            case Key.I:
                MarkFirstFrame();
                break;
            case Key.O when !ctrl:
                MarkLastFrame();
                break;
            case Key.X when Keyboard.Modifiers.HasFlag(ModifierKeys.Alt):
                CloseZone();
                break;
            // Ctrl + coma: ajustes, como en VS Code y otros editores.
            case Key.OemComma when ctrl:
                OpenSettings();
                break;
            // Como en mpv, MPC-HC o YouTube: coma y punto, un frame atrás / adelante.
            case Key.OemComma:
                StepFrame(-1);
                break;
            case Key.OemPeriod:
                StepFrame(1);
                break;
            case Key.Home:
                _nav.SeekTo(0);
                break;
            case Key.End:
                GoToLastFrame();
                break;
            // Ctrl + M: la zona ampliada (libre o la región seleccionada), como clip.
            case Key.M when ctrl:
                ShowExportMenu();
                break;
            case Key.M:
                _player.SetMute(!_store.Settings.Mute);
                break;
            case Key.A:
                _player.CycleTrack("audio");
                break;
            case Key.S:
                _player.CycleTrack("sub");
                break;
            case Key.O when ctrl:
                ShowOpenDialog();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void ShowOpenDialog()
    {
        var dialog = new OpenFileDialog { Filter = VideoFilter, Title = "Abrir vídeo" };
        if (_current != null)
            dialog.InitialDirectory = Path.GetDirectoryName(_current.Path);
        if (dialog.ShowDialog(this) == true)
            OpenByHand(dialog.FileName);
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            Activate();
            // Archivos de bookmarks o colecciones compartidos: se importan; si no, el primero se abre como vídeo.
            var shared = files.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList();
            if (shared.Count > 0)
                shared.ForEach(ImportSharedFile);
            else
                OpenByHand(files[0]);
        }
    }

    private void OnVideoClicked()
    {
        ReturnFocusToVideo();
        _beforeClick = (_nav.Paused, _nav.CurrentTime);
        _nav.TogglePlay();
    }

    // El primer clic del doble clic reanudó o pausó: se deshace y, si estaba en pausa, vuelve al mismo frame.
    private void UndoVideoClick()
    {
        _nav.TogglePlay();
        if (_beforeClick is { Paused: true } before && Math.Abs(_nav.CurrentTime - before.Time) > 0.5 / Fps)
            _nav.SeekTo(before.Time);
        _beforeClick = null;
    }

    private void ToggleFullscreen()
    {
        SuspendWindowAnimations();
        if (_beforeFullscreen is { } before)
        {
            WindowStyle = before.Style;
            ResizeMode = before.Resize;
            WindowState = before.State;
            _beforeFullscreen = null;
            FullscreenButton.Content = GlyphFullscreen;
            ApplyControlsMode();
        }
        else
        {
            _beforeFullscreen = (WindowState, WindowStyle, ResizeMode);
            // Pasar por Normal hace que el maximizado sin bordes cubra también la barra de tareas.
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            FullscreenButton.Content = GlyphBackToWindow;
            ApplyControlsMode();
        }
    }

    private void SetVolumeUi(double volume, bool mute)
    {
        _store.Settings.Volume = volume;
        _store.Settings.Mute = mute;
        _updatingVolume = true;
        VolumeSlider.Value = volume;
        _updatingVolume = false;
        MuteButton.Content = mute ? GlyphMute : GlyphVolume;
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updatingVolume)
            _player.SetVolume(e.NewValue);
    }

    private void OnMuteClick(object sender, RoutedEventArgs e) => _player.SetMute(!_store.Settings.Mute);

    private void OnOpenClick(object sender, RoutedEventArgs e) => ShowOpenDialog();

    private void OnPlayClick(object sender, RoutedEventArgs e) => _nav.TogglePlay();

    // Rueda sobre el play y los botones de frame: siempre frame a frame (hacia arriba, el siguiente), con o sin modificadores.
    private void OnFrameWheel(object sender, MouseWheelEventArgs e)
    {
        if (_duration > 0)
            WheelSteps(e.Delta, steps =>
            {
                for (var i = 0; i < Math.Abs(steps); i++)
                    StepFrame(Math.Sign(steps));
            });
        e.Handled = true;
    }

    private void OnFullscreenClick(object sender, RoutedEventArgs e) => ToggleFullscreen();

    // El engranaje abre los ajustes y, si ya están abiertos, los cierra (como el botón del panel de bookmarks).
    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is { } open)
            open.Close();
        else
            OpenSettings();
    }

    private void OnTracksClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = TracksButton };
        AddTrackItems(menu, "audio", "Audio", allowNone: false);
        menu.Items.Add(new Separator());
        AddTrackItems(menu, "sub", "Subtítulos", allowNone: true);
        menu.IsOpen = true;
    }

    private void AddTrackItems(ContextMenu menu, string type, string header, bool allowNone)
    {
        menu.Items.Add(new MenuItem { Header = header, IsEnabled = false });
        var tracks = _tracks.Where(t => t.Type == type).ToList();
        if (allowNone)
        {
            var none = new MenuItem { Header = "Desactivados", IsCheckable = true, IsChecked = tracks.All(t => !t.Selected) };
            none.Click += (_, _) => _player.SelectTrack(type, null);
            menu.Items.Add(none);
        }
        foreach (var track in tracks)
        {
            var item = new MenuItem { Header = track.Label, IsCheckable = true, IsChecked = track.Selected };
            item.Click += (_, _) => _player.SelectTrack(type, track.Id);
            menu.Items.Add(item);
        }
        if (tracks.Count == 0 && !allowNone)
            menu.Items.Add(new MenuItem { Header = "Sin pistas", IsEnabled = false });
    }

    private void LogStartup(string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(_store.Folder, "startup.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmCloseWhileExporting())
        {
            e.Cancel = true;
            return;
        }
        // Lo que tarda cada paso va a startup.log, para ver qué hace lento el cierre.
        var watch = Stopwatch.StartNew();
        var steps = new List<string>();
        void Step(string name) => steps.Add($"{name} {watch.ElapsedMilliseconds}");
        // La ventana desaparece ya (con su sitio guardado antes): cerrar mpv y el motor lleva unos cientos de ms.
        SavePlacement();
        ShowWindow(new WindowInteropHelper(this).Handle, SW_HIDE);
        _exporter.Dispose();
        _saveTimer.Stop();
        _settingsWindow?.Close();
        CloseNaming(commit: true);
        _thumbs.Dispose();
        _indexing?.Cancel();
        Step("miniaturas");
        SavePosition();
        _store.SaveSettings();
        Step("datos guardados");
        App.ReleaseSingleInstance();
        _cacheTimer.Stop();
        _player.Dispose();
        Step("mpv cerrado");
        _engine?.Dispose();
        Step("motor cerrado");
        _surface.Dispose();
        Step("superficie");
        LogStartup($"cierre a las {DateTime.Now - watch.Elapsed:HH:mm:ss.fff}: {string.Join(", ", steps)} (ms)");
    }
}
