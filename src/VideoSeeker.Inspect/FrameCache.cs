namespace VideoSeeker.Inspect;

/// <summary>
/// Frames decodificados ordenados por tiempo, con un presupuesto de memoria de vídeo. Cuando no cabe uno
/// nuevo se descarta el más alejado del punto de interés (el "foco"). El frame que está en pantalla queda
/// fijado y nunca se descarta. Thread-safe: lo rellenan hilos de fondo y lo lee la interfaz.
/// </summary>
public sealed class FrameCache(long budgetBytes) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly SortedList<double, CachedFrame> _frames = [];
    private long _bytes;
    private double _focus;
    private CachedFrame? _pinned;

    /// <summary>Se ha añadido o quitado algún frame (se lanza en el hilo que lo hizo).</summary>
    public event Action? Changed;

    public long BudgetBytes { get; set; } = budgetBytes;

    public long UsedBytes
    {
        get { lock (_gate) return _bytes; }
    }

    public void SetFocus(double time)
    {
        lock (_gate)
            _focus = time;
    }

    public double FocusTime
    {
        get { lock (_gate) return _focus; }
    }

    /// <summary>
    /// Recorre la caché desde el foco hacia atrás hasta "start" y devuelve el instante justo después del
    /// primer hueco (lo que hay que decodificar es lo anterior a ese instante), o null si no falta nada.
    /// </summary>
    public double? FirstGapBefore(double focus, double start, double frameDuration)
    {
        lock (_gate)
        {
            var i = IndexNear(focus, frameDuration / 2);
            if (i < 0)
                return focus + frameDuration / 2; // falta el propio frame del foco
            var keys = _frames.Keys;
            var later = keys[i];
            for (var j = i - 1; j >= 0 && later > start; j--)
            {
                if (later - keys[j] > frameDuration * 1.5)
                    return later;
                later = keys[j];
            }
            // Llega hasta el primer frame guardado: si aún queda ventana por detrás, ese es el hueco.
            return later - frameDuration / 2 > start ? later : null;
        }
    }

    /// <summary>
    /// Recorre la caché desde el foco hacia delante hasta "end" y devuelve el instante del último frame
    /// antes del primer hueco (hay que decodificar lo posterior), o null si no falta nada.
    /// </summary>
    public double? FirstGapAfter(double focus, double end, double frameDuration)
    {
        lock (_gate)
        {
            var i = IndexNear(focus, frameDuration / 2);
            if (i < 0)
                return focus - frameDuration; // falta el propio frame del foco
            var keys = _frames.Keys;
            var earlier = keys[i];
            for (var j = i + 1; j < keys.Count && earlier < end; j++)
            {
                if (keys[j] - earlier > frameDuration * 1.5)
                    return earlier;
                earlier = keys[j];
            }
            return earlier + frameDuration / 2 < end ? earlier : null;
        }
    }

    public bool Contains(double time, double tolerance)
    {
        lock (_gate)
            return IndexNear(time, tolerance) >= 0;
    }

    /// <summary>Añade el frame; si no cabe y es el más lejano del foco, lo libera y devuelve false.</summary>
    public bool TryAdd(CachedFrame frame)
    {
        lock (_gate)
        {
            if (_frames.ContainsKey(frame.Time))
            {
                frame.Dispose();
                return true;
            }
            var distance = Math.Abs(frame.Time - _focus);
            while (_bytes + frame.Bytes > BudgetBytes && _frames.Count > 0)
            {
                var victim = FarthestEvictable();
                if (victim < 0 || Math.Abs(_frames.Keys[victim] - _focus) <= distance)
                {
                    frame.Dispose();
                    return false;
                }
                Remove(victim);
            }
            _frames.Add(frame.Time, frame);
            _bytes += frame.Bytes;
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// La tarjeta no tiene memoria para otro frame: el presupuesto baja a lo que ya ocupa la caché (menos un margen
    /// para que quede sitio al decodificador y a mpv), y desde ahí se van sustituyendo frames en vez de añadirlos.
    /// </summary>
    public void ShrinkToUsed()
    {
        lock (_gate)
            BudgetBytes = Math.Min(BudgetBytes, _bytes * 9 / 10);
    }

    /// <summary>
    /// Usa el frame que se ve en el instante dado (dentro de la tolerancia) sin fijarlo: "use" corre dentro del cerrojo,
    /// así que mientras dura nadie puede liberarlo (que sea breve). False si no está en caché.
    /// </summary>
    public bool UseNear(double time, double tolerance, Action<CachedFrame> use)
    {
        lock (_gate)
        {
            var i = IndexNear(time, tolerance);
            if (i < 0)
                return false;
            use(_frames.Values[i]);
            return true;
        }
    }

    /// <summary>El frame que se ve en el instante dado (dentro de la tolerancia), fijándolo para pintarlo.</summary>
    public CachedFrame? PinNear(double time, double tolerance)
    {
        lock (_gate)
        {
            var i = IndexNear(time, tolerance);
            return i < 0 ? null : _pinned = _frames.Values[i];
        }
    }

    /// <summary>
    /// El frame siguiente (direction &gt; 0) o anterior al que se ve en "time", fijándolo. Null si no está
    /// en caché o si hay un hueco entre ambos (más de 1,5 frames), para no saltarse frames sin decir nada.
    /// </summary>
    public CachedFrame? PinStep(double time, int direction, double frameDuration)
    {
        lock (_gate)
        {
            var current = IndexNear(time, frameDuration / 2);
            if (current < 0)
                return null;
            var next = current + Math.Sign(direction);
            if (next < 0 || next >= _frames.Count)
                return null;
            if (Math.Abs(_frames.Keys[next] - _frames.Keys[current]) > frameDuration * 1.5)
                return null;
            return _pinned = _frames.Values[next];
        }
    }

    /// <summary>El primer frame en [start, end), fijándolo; null si no hay ninguno.</summary>
    public CachedFrame? PinFirstBetween(double start, double end)
    {
        lock (_gate)
        {
            var keys = _frames.Keys;
            int lo = 0, hi = keys.Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                if (keys[mid] < start) lo = mid + 1; else hi = mid - 1;
            }
            return lo < keys.Count && keys[lo] < end ? _pinned = _frames.Values[lo] : null;
        }
    }

    /// <summary>El último frame en (start, end], fijándolo; null si no hay ninguno.</summary>
    public CachedFrame? PinLastBetween(double start, double end)
    {
        lock (_gate)
        {
            var keys = _frames.Keys;
            int lo = 0, hi = keys.Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                if (keys[mid] <= end) lo = mid + 1; else hi = mid - 1;
            }
            // hi es el último <= end.
            return hi >= 0 && keys[hi] > start ? _pinned = _frames.Values[hi] : null;
        }
    }

    /// <summary>Tramos contiguos ya decodificados, para mostrarlos en la interfaz.</summary>
    public List<(double Start, double End)> Ranges(double frameDuration)
    {
        var ranges = new List<(double, double)>();
        lock (_gate)
        {
            double? start = null, last = null;
            foreach (var t in _frames.Keys)
            {
                if (last is { } l && t - l > frameDuration * 1.5)
                {
                    ranges.Add((start!.Value, l + frameDuration));
                    start = null;
                }
                start ??= t;
                last = t;
            }
            if (start is { } s && last is { } e)
                ranges.Add((s, e + frameDuration));
        }
        return ranges;
    }

    public void Clear()
    {
        lock (_gate)
        {
            foreach (var frame in _frames.Values)
                if (frame != _pinned)
                    frame.Dispose();
            _frames.Clear();
            _bytes = 0;
            // El fijado puede estar en pantalla todavía: se libera cuando deje de usarse.
            _pinned?.Dispose();
            _pinned = null;
        }
        Changed?.Invoke();
    }

    private int IndexNear(double time, double tolerance)
    {
        var keys = _frames.Keys;
        int lo = 0, hi = keys.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (keys[mid] < time) lo = mid + 1; else hi = mid - 1;
        }
        // lo es el primero >= time; el más cercano es lo o lo - 1.
        var best = -1;
        var bestDistance = double.MaxValue;
        foreach (var i in new[] { lo - 1, lo })
        {
            if (i < 0 || i >= keys.Count)
                continue;
            var d = Math.Abs(keys[i] - time);
            if (d < bestDistance)
            {
                best = i;
                bestDistance = d;
            }
        }
        return bestDistance <= tolerance ? best : -1;
    }

    private int FarthestEvictable()
    {
        int first = 0, last = _frames.Count - 1;
        if (_frames.Values[first] == _pinned) first++;
        if (_frames.Values[last] == _pinned) last--;
        if (first > last)
            return -1;
        return Math.Abs(_frames.Keys[first] - _focus) >= Math.Abs(_frames.Keys[last] - _focus) ? first : last;
    }

    private void Remove(int index)
    {
        var frame = _frames.Values[index];
        _frames.RemoveAt(index);
        _bytes -= frame.Bytes;
        frame.Dispose();
    }

    public void Dispose() => Clear();
}
