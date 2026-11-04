namespace VideoSeeker.Inspect;

/// <summary>
/// Rellena la caché alrededor del foco con dos hilos que buscan huecos: el de delante rellena el primer
/// hueco que haya por delante del foco (y, si puede, sigue decodificando por donde iba sin volver a buscar),
/// y el de atrás el primero que haya por detrás. La ventana a rellenar se mueve con el foco y se adelanta
/// hacia donde va el usuario, así que no hace falta reiniciar nada al moverse.
/// </summary>
public sealed class CacheFiller : IDisposable
{
    private const double MaxHalfWindow = 5;
    private const double MinHalfWindow = 1;
    // Al rellenar hacia atrás se guarda un poco más de lo pedido, para no volver a decodificar el mismo
    // GOP por cada frame que se retrocede.
    private const double BackwardPrefill = 1;
    // Si el hueco está a menos de esto por delante de donde va el decodificador, se sigue decodificando
    // en vez de buscar el keyframe (que puede estar mucho antes).
    private const double ContinueLimit = 2;

    private readonly FrameCache _cache;
    private readonly FrameDecoder _forward, _backward;
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _wake = new(0);
    private readonly CancellationTokenSource _cts = new();
    private Task? _running;
    private double _frameBytes;
    private double _lastFocus;
    private int _direction;
    private double _windowStart, _windowEnd;
    // Persiguiendo al cursor: también se guardan los frames entre lo que se ve y el foco, aunque queden
    // antes de la ventana, para enseñarlos en orden según llega el decodificador. NaN si no.
    private double _trail = double.NaN;
    // Tramo de la barra de zoom: si cabe en la caché y el foco está dentro, se precarga entero.
    private (double Start, double End)? _region;
    // Keyframes conocidos; vacío si no hay índice fiable. Puede cambiar cuando llega el índice de frames propio.
    private double[] _keyframes;

    public double FrameDuration => _forward.FrameDuration;
    /// <summary>
    /// Vídeo caro de decodificar (más de 1440p o keyframes muy separados): ir al frame exacto cuesta tanto
    /// que al arrastrar se persigue al cursor con lo que va saliendo del decodificador.
    /// </summary>
    public bool IsHeavy => (long)_forward.Width * _forward.Height > 2560L * 1440
        || (Volatile.Read(ref _keyframes) is { Length: > 1 } k && (k[^1] - k[0]) / (k.Length - 1) > 4);
    public double Duration => _forward.Duration;

    public CacheFiller(string path, GpuDevice gpu, FrameCache cache)
    {
        _cache = cache;
        _forward = new FrameDecoder(path, gpu);
        // En AVI las marcas de tiempo se reconstruyen y FFmpeg y mpv no siempre coinciden: el frame del
        // motor podría no ser el que mpv llama igual. Son vídeos ligeros en los que mpv ya va rápido.
        if (_forward.ContainerName == "avi")
        {
            _forward.Dispose();
            throw new FFmpegException("AVI: se deja a mpv");
        }
        try
        {
            _backward = new FrameDecoder(path, gpu);
        }
        catch
        {
            _forward.Dispose();
            throw;
        }
        // Estimación hasta ver el primer frame real (NV12 de 8 bits).
        _frameBytes = _forward.Width * _forward.Height * 1.5;
        // El seek obliga a MKV a leer su índice (cues); luego se copia la lista de keyframes.
        _forward.SeekKeyframe(_forward.Duration / 2);
        _keyframes = Reliable(_forward.KeyframeTimes(), _forward.Duration);
    }

    /// <summary>
    /// Keyframes del índice propio (leído paquete a paquete), que sustituyen a los del contenedor.
    /// </summary>
    public void SetKeyframes(double[] keyframes) => Volatile.Write(ref _keyframes, Reliable(keyframes, Duration));

    // Algunos archivos (MP4 fragmentados, por ejemplo) solo traen en el índice lo que FFmpeg ha leído hasta
    // ahora: en un vídeo de 10 h, dos keyframes (el 0 y el de la mitad, por el seek inicial). Fiarse de eso
    // hacía creer al decodificador que no había keyframes más adelante y nunca saltaba, así que al arrastrar
    // hacia delante la imagen se quedaba quieta. Un índice que no llega cerca del final o con keyframes a
    // más de un minuto de media se descarta, y se sigue como sin índice.
    private static double[] Reliable(double[] keyframes, double duration) =>
        keyframes.Length >= 2 && keyframes[^1] >= duration * 0.9 - 30
        && (keyframes[^1] - keyframes[0]) / (keyframes.Length - 1) <= 60 ? keyframes : [];

    /// <summary>El keyframe más cercano a "time" (antes o después), o null si el archivo no tiene índice.</summary>
    public double? NearestKeyframe(double time)
    {
        var keys = Volatile.Read(ref _keyframes);
        if (keys.Length == 0)
            return null;
        var i = Array.BinarySearch(keys, time);
        if (i >= 0)
            return keys[i];
        i = ~i;
        if (i == 0)
            return keys[0];
        if (i == keys.Length)
            return keys[^1];
        return time - keys[i - 1] <= keys[i] - time ? keys[i - 1] : keys[i];
    }

    // Último keyframe en o antes de "time"; null si el archivo no tiene índice.
    private double? KeyframeAtOrBefore(double time)
    {
        var keys = Volatile.Read(ref _keyframes);
        if (keys.Length == 0)
            return null;
        var i = Array.BinarySearch(keys, time);
        if (i < 0)
            i = ~i - 1;
        return i >= 0 ? keys[i] : keys[0];
    }

    /// <summary>
    /// Mueve el foco: la ventana a precargar se desplaza con él. Con "trail", también se guardan los frames
    /// desde ese instante hasta el foco.
    /// </summary>
    public void Focus(double time, double? trail = null)
    {
        _cache.SetFocus(time);
        Volatile.Write(ref _trail, trail ?? double.NaN);
        lock (_gate)
        {
            if (_running != null && time != _lastFocus)
                _direction = Math.Sign(time - _lastFocus);
            _lastFocus = time;

            // Con presupuesto justo (4K) la ventana se estrecha y se adelanta hacia donde se va.
            var framesThatFit = _cache.BudgetBytes / Math.Max(1, _frameBytes);
            var half = Math.Clamp(framesThatFit * FrameDuration / 2 * 0.9, MinHalfWindow, MaxHalfWindow);
            var ahead = _direction switch { > 0 => 1.4, < 0 => 0.6, _ => 1.0 };
            if (_region is { } r && time >= r.Start - FrameDuration && time <= r.End + FrameDuration
                && (r.End - r.Start) / FrameDuration <= framesThatFit * 0.9)
            {
                Volatile.Write(ref _windowStart, Math.Max(0, r.Start));
                Volatile.Write(ref _windowEnd, r.End);
            }
            else
            {
                Volatile.Write(ref _windowStart, Math.Max(0, time - half * (2 - ahead)));
                Volatile.Write(ref _windowEnd, time + half * ahead);
            }

            if (_running == null)
            {
                var token = _cts.Token;
                _running = Task.WhenAll(
                    Task.Run(() => Guard(() => FillForward(token))),
                    Task.Run(() => Guard(() => FillBackward(token))));
            }
            else if (_wake.CurrentCount == 0)
            {
                _wake.Release();
            }
        }
    }

    /// <summary>Tramo a precargar entero mientras el foco esté dentro (si cabe); null lo quita. Vale desde el siguiente Focus.</summary>
    public void SetRegion((double Start, double End)? region)
    {
        lock (_gate)
            _region = region;
    }

    public void Stop()
    {
        _cts.Cancel();
        _wake.Release();
    }

    private void FillForward(CancellationToken token)
    {
        double? position = null; // último frame decodificado; null si hay que buscar
        double? seekedFor = null; // hueco para el que se hizo el último seek
        double? endOfFile = null;
        while (!token.IsCancellationRequested)
        {
            var end = Volatile.Read(ref _windowEnd);
            if (_cache.FirstGapAfter(_cache.FocusTime, end, FrameDuration) is not { } gap
                || (endOfFile is { } eof && gap >= eof - FrameDuration))
            {
                Sleep(token);
                continue;
            }

            // Buscar si no hay posición o si el decodificador ya pasó el hueco. Si va por detrás, solo compensa
            // buscar cuando hay un keyframe entre medias: si no, el seek volvería a empezar desde el mismo
            // keyframe (en 4K con keyframes cada 10 s, el decodificador nunca alcanzaría al cursor). Sin índice
            // de keyframes, se sigue si el hueco está cerca o es al que ya se va de camino.
            var onTheWay = seekedFor is { } target && Math.Abs(target - gap) < ContinueLimit;
            var seekSkipsAhead = position is { } behind && (KeyframeAtOrBefore(gap + FrameDuration / 2) is { } key
                ? key > behind + FrameDuration / 2
                : gap - behind > ContinueLimit && !onTheWay);
            if (position is not { } p || p > gap + FrameDuration / 2 || seekSkipsAhead)
            {
                _forward.SeekKeyframe(gap + FrameDuration / 2);
                seekedFor = gap;
            }

            if (_forward.Next() is not { } t)
            {
                endOfFile = gap;
                position = null;
                continue;
            }
            endOfFile = null;
            position = t;

            // Por delante de la ventana: se espera con el frame decodificado a que el foco avance. Pero si falta
            // algo entre el foco y este frame (el foco ha retrocedido), esperar no sirve: hay que volver atrás.
            // Antes se quedaba aquí atascado y, arrastrando la barra hacia atrás menos de 10 s, la imagen no
            // llegaba nunca al cursor.
            while (!token.IsCancellationRequested && t > Volatile.Read(ref _windowEnd) && t - _cache.FocusTime < 2 * MaxHalfWindow
                   && !(_cache.FirstGapAfter(_cache.FocusTime, Volatile.Read(ref _windowEnd), FrameDuration) is { } hole && hole < t - 1.5 * FrameDuration))
                Sleep(token);
            var trail = Volatile.Read(ref _trail);
            var beforeWindow = t < Volatile.Read(ref _windowStart) && !(t > trail);
            if (beforeWindow || t > Volatile.Read(ref _windowEnd) || _cache.Contains(t, FrameDuration / 4))
                continue;
            // Caché llena y este frame es el más lejano: se espera un poco a que el foco se acerque.
            for (var attempt = 0; attempt < 20 && !token.IsCancellationRequested && !TryCache(_forward, t); attempt++)
                Sleep(token);
        }
    }

    private void FillBackward(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var start = Volatile.Read(ref _windowStart);
            var focus = _cache.FocusTime;
            // El frame del foco y lo que sigue los trae el hilo de delante: si este decodificara el mismo GOP
            // a la vez, solo competirían por el decodificador de la GPU.
            if (!_cache.Contains(focus, FrameDuration / 2)
                || _cache.FirstGapBefore(focus, start, FrameDuration) is not { } gapEnd
                || gapEnd <= 2 * FrameDuration // el hueco sería anterior al primer frame del vídeo
                || !FillBefore(gapEnd, start - BackwardPrefill, token))
            {
                Sleep(token);
            }
        }
    }

    // Decodifica desde el keyframe anterior a "end" hasta "end", guardando lo que caiga desde "from".
    // Devuelve si guardó algún frame nuevo.
    private bool FillBefore(double end, double from, CancellationToken token)
    {
        var added = false;
        // Algunos contenedores no tienen el índice exacto: si el seek cae después, se retrocede más.
        foreach (var back in new[] { FrameDuration / 2, 1, 3, 10 })
        {
            _backward.SeekKeyframe(end - back);
            double? first = null;
            while (!token.IsCancellationRequested && _backward.Next() is { } t)
            {
                first ??= t;
                if (t >= end - FrameDuration / 2)
                    break;
                if (t >= from && !_cache.Contains(t, FrameDuration / 4) && TryCache(_backward, t))
                    added = true;
            }
            if (first is not { } f || f < end - FrameDuration / 2)
                return added;
        }
        return added;
    }

    private bool TryCache(FrameDecoder decoder, double time)
    {
        CachedFrame frame;
        try
        {
            frame = decoder.Capture(time);
        }
        catch (SharpGen.Runtime.SharpGenException e) when (e.ResultCode == SharpGen.Runtime.Result.OutOfMemory)
        {
            // Sin memoria en la tarjeta: la caché se queda con lo que tiene y sigue llenándose dentro de eso.
            _cache.ShrinkToUsed();
            return false;
        }
        _frameBytes = frame.Bytes;
        return _cache.TryAdd(frame);
    }

    // Espera a que cambie el foco (Focus despierta a los hilos) o, como mucho, 50 ms.
    private void Sleep(CancellationToken token)
    {
        try { _wake.Wait(50, token); }
        catch (OperationCanceledException) { }
    }

    private static void Guard(Action fill)
    {
        try
        {
            fill();
        }
        catch (FFmpegException)
        {
            // Un archivo dañado en un tramo no debe tumbar la app: la caché queda como esté.
        }
    }

    public void Dispose()
    {
        Stop();
        try { _running?.Wait(TimeSpan.FromSeconds(3)); } catch (AggregateException) { }
        _forward.Dispose();
        _backward.Dispose();
        _wake.Dispose();
        _cts.Dispose();
    }
}
