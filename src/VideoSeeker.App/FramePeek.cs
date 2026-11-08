using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VideoSeeker.Inspect;

namespace VideoSeeker.App;

/// <summary>
/// Vistazo al pasar el ratón por las barras: el frame que se ve en un instante, decodificado en CPU en un hilo propio
/// de baja prioridad (sin tocar la caché del motor ni la reproducción). Manda la última petición: lo que se pidió
/// antes y aún no empezó se olvida, y una decodificación que tarda se abandona si ya se quiere otro instante. Lo hecho
/// se guarda en memoria (solo del vídeo de la última petición), para volver a pasar por ahí al momento. Lo que está en
/// la caché de frames de la GPU no pasa por aquí: lo reduce el motor al momento (MainWindow.Peek.cs).
/// </summary>
public sealed class FramePeek : IDisposable
{
    public const int Width = 320;
    // Una decodificación lenta (desde un keyframe lejano) que ya no es la que se quiere se deja tras este tiempo.
    // Las rápidas terminan antes: así, moviendo el ratón, se van viendo frames del camino.
    private static readonly TimeSpan AbandonAfter = TimeSpan.FromMilliseconds(250);
    private const int MaxKept = 150;

    private readonly Task _ffmpegReady;
    private readonly Dispatcher _dispatcher;
    private readonly object _lock = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _worker;
    // Bajo _lock: la última petición sin empezar y la que se está decodificando.
    private (string Path, double Time)? _wanted;
    private (string Path, double Time, CancellationTokenSource Cancel, DateTime Since)? _running;
    // Solo en el hilo de la interfaz.
    private readonly Dictionary<long, ImageSource> _kept = [];
    private readonly Queue<long> _keptOrder = [];
    private string? _keptPath;

    /// <summary>Un frame pedido ya está listo (en el hilo de la interfaz): vídeo, instante e imagen.</summary>
    public event Action<string, double, ImageSource>? Ready;

    public FramePeek(Task ffmpegReady, Dispatcher dispatcher)
    {
        _ffmpegReady = ffmpegReady;
        _dispatcher = dispatcher;
        _worker = new Thread(Work) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Vistazo" };
        _worker.Start();
    }

    private static long Key(double time) => (long)Math.Round(time * 1000);

    /// <summary>El frame si ya está hecho (en memoria), sin pedir nada.</summary>
    public ImageSource? Kept(string path, double time)
    {
        if (path != _keptPath)
        {
            _kept.Clear();
            _keptOrder.Clear();
            _keptPath = path;
        }
        return _kept.TryGetValue(Key(time), out var image) ? image : null;
    }

    /// <summary>Guarda un frame hecho por otro camino (de la caché de la GPU) para volver a él sin pedirlo.</summary>
    public void Keep(string path, double time, ImageSource image)
    {
        Kept(path, time);
        Store(path, time, image);
    }

    public static ImageSource ToImage((byte[] Pixels, int Width, int Height) frame)
    {
        var bitmap = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Pixels, frame.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>El frame si ya está hecho; si no, se pide (en lugar de lo que se pidiera antes) y llegará con Ready.</summary>
    public ImageSource? Get(string path, double time)
    {
        if (Kept(path, time) is { } image)
        {
            lock (_lock)
                _wanted = null;
            return image;
        }
        lock (_lock)
        {
            _wanted = (path, time);
            if (_running is { } running && (running.Path != path || Key(running.Time) != Key(time)))
            {
                var left = AbandonAfter - (DateTime.UtcNow - running.Since);
                running.Cancel.CancelAfter(left > TimeSpan.Zero ? left : TimeSpan.Zero);
            }
        }
        _signal.Set();
        return null;
    }

    /// <summary>Ya no hace falta nada (el ratón salió de la barra): se olvida lo pedido y se deja lo que esté a medias.</summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _wanted = null;
            _running?.Cancel.Cancel();
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
                (string Path, double Time) job;
                CancellationTokenSource cancel;
                lock (_lock)
                {
                    if (_wanted is not { } wanted)
                        continue;
                    job = wanted;
                    _wanted = null;
                    cancel = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    _running = (job.Path, job.Time, cancel, DateTime.UtcNow);
                }
                ImageSource? image = null;
                try
                {
                    // Abrir el archivo cuesta: se mantiene abierto mientras se pidan frames del mismo vídeo.
                    if (decoder?.Path != job.Path)
                    {
                        decoder?.Dispose();
                        decoder = null;
                        decoder = new Thumbnailer(job.Path);
                    }
                    if (decoder.Grab(job.Time, Width, cancel.Token) is { } grabbed)
                        image = ToImage(grabbed);
                }
                catch (OperationCanceledException) when (!_stop.IsCancellationRequested)
                {
                    // Abandonada por otra más nueva: el decodificador sigue valiendo (cada Grab busca y vacía).
                }
                catch (Exception e) when (e is FFmpegException or IOException or UnauthorizedAccessException)
                {
                    decoder?.Dispose();
                    decoder = null;
                }
                lock (_lock)
                    _running = null;
                cancel.Dispose();
                if (image != null)
                    _dispatcher.BeginInvoke(() => Done(job.Path, job.Time, image));
                // Si llegó otra petición mientras tanto, la señal ya está puesta y la siguiente vuelta la recoge.
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

    private void Done(string path, double time, ImageSource image)
    {
        Store(path, time, image);
        Ready?.Invoke(path, time, image);
    }

    private void Store(string path, double time, ImageSource image)
    {
        if (path == _keptPath && _kept.TryAdd(Key(time), image))
        {
            _keptOrder.Enqueue(Key(time));
            while (_keptOrder.Count > MaxKept)
                _kept.Remove(_keptOrder.Dequeue());
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _signal.Set();
    }
}
