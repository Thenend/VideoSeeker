using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using VideoSeeker.Inspect;

namespace VideoSeeker.App;

/// <summary>
/// Miniaturas de la tira de la barra de zoom (ajuste «Miniaturas en la barra del zoom»). La barra pide con Want los
/// instantes de sus celdas y dibuja lo que haya (o la más cercana, atenuada, mientras llega). Primero se sacan de la
/// caché de frames de la GPU (al momento, en el hilo de la interfaz y a prioridad baja, para no frenar el scrubbing);
/// lo que no está ahí se decodifica en CPU en un hilo propio de baja prioridad, en orden y siguiendo la decodificación
/// cuando los instantes están cerca (Thumbnailer en modo seguido). Solo en memoria y solo del vídeo abierto.
/// </summary>
public sealed class ZoomFilmstrip : IDisposable
{
    private const int MaxKept = 800;
    // Tiempo máximo por pasada de la GPU en el hilo de la interfaz; lo que falte, en la siguiente.
    private static readonly TimeSpan GpuSlice = TimeSpan.FromMilliseconds(6);

    private readonly Task _ffmpegReady;
    private readonly Dispatcher _dispatcher;
    private readonly object _lock = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _worker;

    // Solo en el hilo de la interfaz: lo hecho, en orden de instante (para la más cercana) y de llegada (para olvidar).
    private readonly Dictionary<long, ImageSource> _kept = [];
    private readonly List<long> _sorted = [];
    private readonly Queue<long> _arrival = [];
    private string? _path;
    private long[] _wanted = [];
    private int _width = 160;
    private bool _gpuPending;

    // Bajo _lock: lo que tiene que decodificar el hilo (lo que falta tras mirar la GPU), su versión, lo que ya tiene
    // (aunque la interfaz aún no lo haya recogido) y lo que está decodificando.
    private string? _jobPath;
    private long[] _jobTimes = [];
    private int _jobWidth = 160;
    private int _jobVersion;
    private readonly HashSet<long> _done = [];
    private (long Time, CancellationTokenSource Cancel)? _running;

    /// <summary>Miniatura del frame de la caché de la GPU (instante, ancho en píxeles), o null si no está.</summary>
    public Func<double, int, (byte[] Pixels, int Width, int Height)?>? FromGpu { get; set; }

    /// <summary>Llegó alguna miniatura nueva (en el hilo de la interfaz).</summary>
    public event Action? Changed;

    /// <summary>Ancho / alto de las miniaturas (16:9 hasta que llega la primera).</summary>
    public double Aspect { get; private set; } = 16.0 / 9;

    public ZoomFilmstrip(Task ffmpegReady, Dispatcher dispatcher)
    {
        _ffmpegReady = ffmpegReady;
        _dispatcher = dispatcher;
        _worker = new Thread(Work) { IsBackground = true, Priority = ThreadPriority.Lowest, Name = "Tira del zoom" };
        _worker.Start();
    }

    private static long Key(double time) => (long)Math.Round(time * 1000);

    /// <summary>Vídeo abierto: al cambiar, se olvida todo lo del anterior.</summary>
    public string? Path
    {
        get => _path;
        set
        {
            if (value == _path)
                return;
            _path = value;
            _kept.Clear();
            _sorted.Clear();
            _arrival.Clear();
            _wanted = [];
            Aspect = 16.0 / 9;
            lock (_lock)
            {
                _jobPath = value;
                _jobTimes = [];
                _jobVersion++;
                _done.Clear();
                _running?.Cancel.Cancel();
            }
        }
    }

    // Para la autoprueba: cuántas se quieren y faltan, y de dónde han salido.
    internal int WantedCount => _wanted.Length;
    internal int MissingCount => _wanted.Count(k => !_kept.ContainsKey(k));
    internal IReadOnlyList<double> WantedTimes => [.. _wanted.Select(k => k / 1000.0)];
    internal int FromGpuCount { get; private set; }
    internal int FromCpuCount { get; private set; }

    /// <summary>La miniatura del instante, si ya está.</summary>
    public ImageSource? Get(double time) => _kept.TryGetValue(Key(time), out var image) ? image : null;

    /// <summary>La miniatura hecha más cercana al instante, a menos de "within" segundos (para tapar el hueco mientras llega).</summary>
    public ImageSource? Nearest(double time, double within)
    {
        if (_sorted.Count == 0)
            return null;
        var key = Key(time);
        var i = _sorted.BinarySearch(key);
        if (i >= 0)
            return _kept[key];
        i = ~i;
        long? best = null;
        if (i < _sorted.Count)
            best = _sorted[i];
        if (i > 0 && (best == null || key - _sorted[i - 1] < best.Value - key))
            best = _sorted[i - 1];
        return best is { } b && Math.Abs(b - key) <= within * 1000 ? _kept[b] : null;
    }

    /// <summary>
    /// Los instantes de las celdas que se ven, en orden, y el ancho en píxeles al que hacen falta. Lo que no esté se
    /// pide (en lugar de lo que se pidiera antes). Se llama al pintar la barra: si es lo mismo de antes, no hace nada.
    /// </summary>
    public void Want(IReadOnlyList<double> times, int width)
    {
        if (_path == null)
            return;
        var keys = new long[times.Count];
        for (var i = 0; i < keys.Length; i++)
            keys[i] = Key(times[i]);
        if (width == _width && keys.AsSpan().SequenceEqual(_wanted))
            return;
        _wanted = keys;
        _width = width;
        // Lo que ya no se quiere no sigue decodificándose.
        lock (_lock)
        {
            if (_running is { } running && Array.IndexOf(keys, running.Time) < 0)
                running.Cancel.Cancel();
        }
        ScheduleGpu();
    }

    /// <summary>La caché de frames cambió: lo que falta puede estar ya en ella.</summary>
    public void CacheChanged()
    {
        if (_wanted.Any(k => !_kept.ContainsKey(k)))
            ScheduleGpu();
    }

    private void ScheduleGpu()
    {
        if (_gpuPending)
            return;
        _gpuPending = true;
        // Por debajo de la entrada y del pintado: el scrubbing va primero.
        _dispatcher.BeginInvoke(DispatcherPriority.Background, GpuPass);
    }

    private void GpuPass()
    {
        _gpuPending = false;
        if (_path is not { } path)
            return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var added = false;
        var missing = new List<long>();
        foreach (var key in _wanted)
        {
            if (_kept.ContainsKey(key))
                continue;
            if (clock.Elapsed > GpuSlice)
            {
                // Lo que queda, en otra pasada (sin mandarlo aún a la CPU: puede estar en la GPU).
                ScheduleGpu();
                if (added)
                    Changed?.Invoke();
                return;
            }
            if (FromGpu?.Invoke(key / 1000.0, _width) is { } frame)
            {
                Store(path, key, FramePeek.ToImage(frame));
                FromGpuCount++;
                added = true;
            }
            else
                missing.Add(key);
        }
        if (added)
            Changed?.Invoke();
        lock (_lock)
        {
            if (!missing.SequenceEqual(_jobTimes) || _jobWidth != _width)
            {
                _jobTimes = [.. missing];
                _jobWidth = _width;
                _jobVersion++;
            }
        }
        if (missing.Count > 0)
            _signal.Set();
    }

    private void Store(string path, long key, ImageSource image)
    {
        if (path != _path || _kept.ContainsKey(key))
            return;
        if (_kept.Count == 0 && image.Height > 0)
            Aspect = image.Width / image.Height;
        _kept[key] = image;
        var i = _sorted.BinarySearch(key);
        _sorted.Insert(i < 0 ? ~i : i, key);
        _arrival.Enqueue(key);
        lock (_lock)
            _done.Add(key);
        while (_arrival.Count > MaxKept)
        {
            var old = _arrival.Dequeue();
            // Las que se ven no se olvidan (vuelven al final de la cola).
            if (Array.IndexOf(_wanted, old) >= 0)
            {
                _arrival.Enqueue(old);
                if (_arrival.Count <= _wanted.Length + 1)
                    break;
                continue;
            }
            _kept.Remove(old);
            _sorted.Remove(old);
            lock (_lock)
                _done.Remove(old);
        }
    }

    private void Work()
    {
        try
        {
            _ffmpegReady.Wait(_stop.Token);
        }
        catch (Exception)
        {
            return;
        }
        Thumbnailer? decoder = null;
        try
        {
            while (true)
            {
                WaitHandle.WaitAny([_signal, _stop.Token.WaitHandle]);
                _stop.Token.ThrowIfCancellationRequested();
                // Vuelve a empezar mientras cambie lo pedido (lo ya hecho se salta).
                while (true)
                {
                    string? path;
                    long[] times;
                    int width, version;
                    lock (_lock)
                        (path, times, width, version) = (_jobPath, _jobTimes, _jobWidth, _jobVersion);
                    if (path == null || times.Length == 0)
                        break;
                    var changed = false;
                    foreach (var key in times)
                    {
                        CancellationTokenSource cancel;
                        lock (_lock)
                        {
                            if (_jobVersion != version)
                            {
                                changed = true;
                                break;
                            }
                            if (_done.Contains(key))
                                continue;
                            cancel = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                            _running = (key, cancel);
                        }
                        (byte[] Pixels, int Width, int Height)? frame = null;
                        try
                        {
                            if (decoder?.Path != path)
                            {
                                decoder?.Dispose();
                                decoder = null;
                                decoder = new Thumbnailer(path);
                            }
                            frame = decoder.Grab(key / 1000.0, width, cancel.Token, sequential: true);
                        }
                        catch (OperationCanceledException) when (!_stop.IsCancellationRequested)
                        {
                        }
                        catch (Exception e) when (e is FFmpegException or IOException or UnauthorizedAccessException)
                        {
                            decoder?.Dispose();
                            decoder = null;
                        }
                        lock (_lock)
                        {
                            _running = null;
                            if (frame != null && path == _jobPath)
                                _done.Add(key);
                        }
                        cancel.Dispose();
                        if (frame is { } f)
                        {
                            var image = FramePeek.ToImage(f);
                            _dispatcher.BeginInvoke(DispatcherPriority.Background, () => Arrived(path, key, image));
                        }
                        else if (decoder == null)
                            break; // el archivo no se abre: no insistir con el resto
                    }
                    if (!changed)
                    {
                        // Si nada cambió mientras tanto, a esperar.
                        lock (_lock)
                            if (_jobVersion == version)
                                break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            decoder?.Dispose();
        }
    }

    private void Arrived(string path, long key, ImageSource image)
    {
        if (path != _path || _kept.ContainsKey(key))
            return;
        Store(path, key, image);
        FromCpuCount++;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _stop.Cancel();
        _signal.Set();
    }
}
