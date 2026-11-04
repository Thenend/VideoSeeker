using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VideoSeeker.Inspect;

namespace VideoSeeker.App;

/// <summary>
/// Miniaturas de 320 px de los bookmarks: JPEG en data\cache\thumbs\&lt;huella&gt;_&lt;ms&gt;.jpg, generadas en un hilo
/// propio de baja prioridad (decodificando en CPU, sin tocar la caché del motor) y guardadas en memoria.
/// </summary>
public sealed class Thumbnails : IDisposable
{
    public const int Width = 320;

    private readonly string _folder;
    private readonly Task _ffmpegReady;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, ImageSource> _loaded = [];
    private readonly HashSet<string> _failed = [];
    private readonly BlockingCollection<(string Key, string Path, double Time)> _queue = [];
    private readonly ConcurrentDictionary<string, byte> _pending = [];
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _worker;

    /// <summary>Una miniatura pedida ya está lista (en el hilo de la interfaz), con su clave.</summary>
    public event Action<string>? Ready;

    public Thumbnails(string dataFolder, Task ffmpegReady, Dispatcher dispatcher)
    {
        _folder = Path.Combine(dataFolder, "cache", "thumbs");
        _ffmpegReady = ffmpegReady;
        _dispatcher = dispatcher;
        _worker = new Thread(Work) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Miniaturas" };
        _worker.Start();
    }

    public static string Key(string fingerprint, double time) => $"{fingerprint}_{(long)Math.Round(time * 1000)}";

    /// <summary>La miniatura si ya existe; si no, se pide (si se conoce el vídeo) y llegará con Ready.</summary>
    public ImageSource? Get(string fingerprint, string? videoPath, double time)
    {
        var key = Key(fingerprint, time);
        if (_loaded.TryGetValue(key, out var image))
            return image;
        var file = Path.Combine(_folder, key + ".jpg");
        if (File.Exists(file) && LoadJpeg(file) is { } saved)
            return _loaded[key] = saved;
        if (videoPath != null && !_failed.Contains(key) && File.Exists(videoPath) && _pending.TryAdd(key, 0))
            _queue.Add((key, videoPath, time));
        return null;
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
            foreach (var (key, path, time) in _queue.GetConsumingEnumerable(_stop.Token))
            {
                ImageSource? image = null;
                try
                {
                    // Seguidas suelen ser del mismo vídeo: se mantiene abierto.
                    if (decoder?.Path != path)
                    {
                        decoder?.Dispose();
                        decoder = null;
                        decoder = new Thumbnailer(path);
                    }
                    if (decoder.Grab(time, Width, _stop.Token) is { } grabbed)
                        image = Save(key, grabbed.Pixels, grabbed.Width, grabbed.Height);
                }
                catch (Exception e) when (e is FFmpegException or IOException or UnauthorizedAccessException)
                {
                    decoder?.Dispose();
                    decoder = null;
                }
                _pending.TryRemove(key, out _);
                _dispatcher.BeginInvoke(() =>
                {
                    if (image != null)
                        _loaded[key] = image;
                    else
                        _failed.Add(key);
                    Ready?.Invoke(key);
                });
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

    private ImageSource Save(string key, byte[] pixels, int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        Directory.CreateDirectory(_folder);
        var file = Path.Combine(_folder, key + ".jpg");
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(file + ".tmp"))
            encoder.Save(stream);
        File.Move(file + ".tmp", file, overwrite: true);
        return bitmap;
    }

    private static ImageSource? LoadJpeg(string file)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(file);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _queue.CompleteAdding();
    }
}
