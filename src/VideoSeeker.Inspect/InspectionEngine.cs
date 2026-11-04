namespace VideoSeeker.Inspect;

/// <summary>
/// Punto de entrada del motor de inspección: precarga frames alrededor del foco y los pinta encima del
/// vídeo de mpv. Se usa desde el hilo de la interfaz; la decodificación va en hilos propios.
/// </summary>
public sealed class InspectionEngine : IDisposable
{
    private readonly GpuDevice _gpu;
    private readonly FrameRenderer _renderer;
    private long _budgetBytes;
    // Una caché por archivo: así los hilos del archivo anterior nunca meten frames en la del nuevo.
    private FrameCache _cache;
    private CacheFiller? _filler;
    private Task<CacheFiller?>? _opening;
    private CachedFrame? _shown;
    private readonly Lock _gate = new();
    // Foco pedido mientras se abren los decodificadores: solo cuenta el último.
    private double? _pendingFocus;
    private (double Start, double End)? _region;
    private double[]? _keyframes;

    /// <summary>Cambia lo que hay en caché (se lanza en hilos de fondo).</summary>
    public event Action? CacheChanged;

    /// <param name="budgetBytes">Memoria de la caché de frames; 0, la recomendada para la tarjeta.</param>
    public InspectionEngine(nint videoHwnd, long budgetBytes)
    {
        _gpu = new GpuDevice();
        _renderer = new FrameRenderer(_gpu, videoHwnd);
        BudgetBytes = budgetBytes;
        _cache = NewCache();
    }

    private FrameCache NewCache()
    {
        var cache = new FrameCache(_budgetBytes);
        cache.Changed += () => CacheChanged?.Invoke();
        return cache;
    }

    /// <summary>
    /// Memoria de la caché de frames (al darle 0, la recomendada para la tarjeta); un cambio se aplica al abrir el
    /// siguiente vídeo.
    /// </summary>
    public long BudgetBytes
    {
        get => _budgetBytes;
        set => _budgetBytes = value > 0 ? value : _gpu.Adapter?.RecommendedCacheBytes ?? AdapterInfo.FallbackCacheBytes;
    }

    public bool Showing => _renderer.Visible;
    public double? ShownTime => _renderer.Visible ? _shown?.Time : null;
    public double FrameDuration => _filler?.FrameDuration ?? 1 / 30.0;

    /// <summary>Abre los decodificadores del archivo en segundo plano. Hasta que estén, no hay caché.</summary>
    public void Open(string path)
    {
        Close();
        var cache = _cache = NewCache();
        lock (_gate)
        {
            _pendingFocus = null;
            _keyframes = null;
        }
        var opening = _opening = Task.Run(() =>
        {
            try { return new CacheFiller(path, _gpu, cache); }
            catch (FFmpegException) { return null; } // formato que FFmpeg no abre: solo funcionará mpv
        });
        // Si entretanto se abrió otro archivo, este decodificador ya no es el actual.
        opening.ContinueWith(t =>
        {
            lock (_gate)
            {
                if (_opening != opening)
                    return;
                _filler = t.Result;
                _filler?.SetRegion(_region);
                if (_keyframes != null)
                    _filler?.SetKeyframes(_keyframes);
                if (_pendingFocus is { } focus)
                    _filler?.Focus(focus);
                _pendingFocus = null;
            }
        }, TaskScheduler.Default);
    }

    public void Close()
    {
        Hide();
        _shown = null;
        var opening = _opening;
        var cache = _cache;
        _opening = null;
        _filler = null;
        // Liberar decodificadores puede tardar un poco (esperan a sus hilos): fuera de la interfaz.
        Task.Run(async () =>
        {
            if (opening != null)
                (await opening)?.Dispose();
            cache.Dispose();
        });
    }

    /// <summary>
    /// Indica dónde está el usuario para precargar a su alrededor. Con "trail", también se guardan los
    /// frames desde ese instante hasta el foco (para enseñarlos en orden mientras se persigue al cursor).
    /// </summary>
    public void Focus(double time, double? trail = null)
    {
        lock (_gate)
        {
            if (_filler is { } filler)
                filler.Focus(time, trail);
            else
                _pendingFocus = time;
        }
    }

    /// <summary>Keyframes del índice de frames propio del archivo abierto (más fiables que los del contenedor).</summary>
    public void SetKeyframes(double[] keyframes)
    {
        lock (_gate)
        {
            _keyframes = keyframes;
            _filler?.SetKeyframes(keyframes);
        }
    }

    /// <summary>
    /// Tramo de la barra de zoom: mientras el foco esté dentro y quepa en la caché, se precarga entero en vez
    /// de la ventana de alrededor del foco. Null lo quita. Se aplica en el siguiente Focus.
    /// </summary>
    public void SetRegion((double Start, double End)? region)
    {
        lock (_gate)
        {
            _region = region;
            _filler?.SetRegion(region);
        }
    }

    /// <summary>Muestra el frame contiguo al que se ve en "fromTime". False si no está en caché.</summary>
    public bool ShowStep(double fromTime, int direction)
    {
        if (_cache.PinStep(fromTime, direction, FrameDuration) is not { } frame)
            return false;
        Show(frame);
        return true;
    }

    /// <summary>Muestra el frame que se ve en "time". False si no está en caché.</summary>
    public bool ShowAt(double time, bool moveFocus = true)
    {
        if (_cache.PinNear(time, FrameDuration / 2) is not { } frame)
            return false;
        Show(frame, moveFocus);
        return true;
    }

    /// <summary>
    /// Marcha atrás: muestra el frame más cercano a "target" de los que hay entre él y el que se ve en
    /// "fromTime" (si faltan los del medio, al menos se avanza). False si no hay ninguno en caché.
    /// </summary>
    public bool ShowBackTowards(double target, double fromTime)
    {
        var half = FrameDuration / 2;
        if (_cache.PinFirstBetween(target - half, fromTime - half) is not { } frame)
            return false;
        Show(frame);
        return true;
    }

    /// <summary>
    /// Persiguiendo al cursor: muestra, de los frames que hay entre el que se ve en "fromTime" y "target",
    /// el más cercano a "target". No mueve el foco (lo lleva quien persigue). False si no hay ninguno.
    /// </summary>
    public bool ShowTowards(double target, double fromTime)
    {
        var half = FrameDuration / 2;
        var frame = target >= fromTime
            ? _cache.PinLastBetween(fromTime + half, target + half)
            : _cache.PinFirstBetween(target - half, fromTime - half);
        if (frame == null)
            return false;
        Show(frame, moveFocus: false);
        return true;
    }

    /// <summary>
    /// Muestra el frame que se ve en "time": el último que empieza en o antes de él, si no está más de
    /// "maxBack" por detrás. No mueve el foco. False si no hay ninguno en caché.
    /// </summary>
    public bool ShowLatest(double time, double maxBack)
    {
        if (_cache.PinLastBetween(time - maxBack, time + 1e-4) is not { } frame)
            return false;
        Show(frame, moveFocus: false);
        return true;
    }

    /// <summary>Muestra el frame siguiente o anterior al que se ve en "fromTime", sin mover el foco.</summary>
    public bool ShowStepInPlace(double fromTime, int direction)
    {
        if (_cache.PinStep(fromTime, direction, FrameDuration) is not { } frame)
            return false;
        Show(frame, moveFocus: false);
        return true;
    }

    /// <summary>Vídeo caro de decodificar (4K, keyframes muy separados). False si aún no se sabe.</summary>
    public bool IsHeavy => _filler?.IsHeavy == true;

    /// <summary>Los decodificadores están abiertos (no lo estarán nunca si FFmpeg no abre el archivo, o en AVI).</summary>
    public bool Ready => _filler != null;

    /// <summary>Vuelve a pintar el frame mostrado (por ejemplo, tras cambiar el tamaño de la ventana).</summary>
    public void Refresh()
    {
        if (_renderer.Visible && _shown is { } frame)
            _renderer.Render(frame);
    }

    public void Hide() => _renderer.Hide();

    /// <summary>Keyframe más cercano a "time" según el índice del archivo; null si no se conoce.</summary>
    public double? NearestKeyframe(double time) => _filler?.NearestKeyframe(time);

    public List<(double Start, double End)> CachedRanges() => _cache.Ranges(FrameDuration);

    private void Show(CachedFrame frame, bool moveFocus = true)
    {
        _shown = frame;
        _renderer.Render(frame);
        if (moveFocus)
            Focus(frame.Time);
    }

    public void Dispose()
    {
        Hide();
        _renderer.Dispose();
        try { _opening?.Result?.Dispose(); } catch (AggregateException) { }
        _cache.Dispose();
        _gpu.Dispose();
    }
}
