using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VideoSeeker.App.Controls;

namespace VideoSeeker.App;

// ---- Vistazo: al pasar el ratón por las barras, el frame que hay bajo el cursor en miniatura, sin ir a él ----
public partial class MainWindow
{
    private readonly FramePeek _peek;
    // Instante del frame que se quiere ver (null sin vistazo) y si la imagen que se ve es ya la suya.
    private double? _peekTime;
    private bool _peekExact;
    // Vídeo de la imagen que se ve (al cambiar de vídeo no vale ni atenuada).
    private string? _peekImagePath;
    private const double PeekLongSide = 240;
    // Lo que el borde y el relleno del recuadro suman al ancho de la imagen (MainWindow.xaml).
    private const double PeekChrome = 2 * 3 + 2 * 1;
    // Para la autoprueba: cuántas han salido de la caché de la GPU.
    private int PeekFromGpu { get; set; }

    private void InitPeek()
    {
        foreach (var bar in new TimelineBar[] { Seek, Zoom })
        {
            var b = bar;
            bar.PeekRequested += (time, x) => OnPeekRequested(b, time, x);
        }
        _peek.Ready += OnPeekReady;
        PeekPopup.Opened += (_, _) => NotTopmost(PeekPopup);
        Deactivated += (_, _) => HidePeek();
        LocationChanged += (_, _) => HidePeek();
        SizeChanged += (_, _) => HidePeek();
    }

    private void OnPeekRequested(TimelineBar bar, double? time, double x)
    {
        if (time is not { } t || !_store.Settings.HoverPreview || !IsActive || _currentPath is not { } path
            || _duration <= 0 || _naming != null)
        {
            HidePeek();
            return;
        }
        // El instante en que empieza su frame: pasar por varios puntos del mismo frame no lo decodifica otra vez.
        var grid = Zoom.Grid;
        t = grid.TimeOf(grid.FrameAt(t));
        if (_lastFrameStart > 0)
            t = Math.Min(t, _lastFrameStart);
        _peekTime = t;
        // Ya visto, o en la caché de frames (la línea verde): al momento. Si no, se decodifica desde el archivo.
        var image = _peek.Kept(path, t);
        if (image == null && _engine?.Thumbnail(t, FramePeek.Width) is { } cached)
        {
            image = FramePeek.ToImage(cached);
            _peek.Keep(path, t, image);
            PeekFromGpu++;
        }
        if (image != null)
        {
            _peek.Cancel();
            ShowPeekImage(image, path, exact: true);
        }
        else if (_peek.Get(path, t) is { } decoded)
            ShowPeekImage(decoded, path, exact: true);
        else if (PeekImage.Source != null && _peekImagePath == path)
        {
            // Hasta que llegue, la anterior atenuada (mejor que un hueco que parpadea).
            _peekExact = false;
            PeekImage.Opacity = 0.5;
        }
        else
        {
            PeekImage.Source = null;
            PeekWait.Visibility = Visibility.Visible;
        }

        if (PeekPopup.PlacementTarget != bar)
        {
            PeekPopup.IsOpen = false;
            PeekPopup.PlacementTarget = bar;
        }
        PeekPopup.Placement = PlacementMode.Top;
        PeekPopup.HorizontalOffset = -(PeekBox.Width + PeekChrome) / 2;
        PeekPopup.PlacementRectangle = new Rect(Math.Clamp(x, 0, bar.ActualWidth), 0, 0, 0);
        PeekPopup.IsOpen = true;
    }

    private void OnPeekReady(string path, double time, ImageSource image)
    {
        if (!PeekPopup.IsOpen || _peekTime is not { } wanted || path != _currentPath)
            return;
        var exact = Math.Abs(time - wanted) < 0.0005;
        // Uno del camino (el ratón ya está en otro sitio) solo si aún no se ve el bueno.
        if (exact || !_peekExact)
            ShowPeekImage(image, path, exact);
    }

    private void ShowPeekImage(ImageSource image, string path, bool exact)
    {
        PeekImage.Source = image;
        PeekImage.Opacity = exact ? 1 : 0.5;
        PeekWait.Visibility = Visibility.Collapsed;
        _peekExact = exact;
        _peekImagePath = path;
        // El recuadro con la proporción del vídeo: 240 de ancho, o de alto si es vertical.
        if (image.Width > 0 && image.Height > 0)
        {
            var aspect = image.Width / image.Height;
            var (width, height) = aspect >= 1 ? (PeekLongSide, Math.Round(PeekLongSide / aspect)) : (Math.Round(PeekLongSide * aspect), PeekLongSide);
            if (Math.Abs(PeekBox.Width - width) > 0.5 || Math.Abs(PeekBox.Height - height) > 0.5)
            {
                PeekBox.Width = width;
                PeekBox.Height = height;
                if (PeekPopup.IsOpen)
                {
                    // Reabrir para que se recoloque con el tamaño nuevo (pasa una vez por vídeo, como mucho).
                    PeekPopup.HorizontalOffset = -(width + PeekChrome) / 2;
                    PeekPopup.IsOpen = false;
                    PeekPopup.IsOpen = true;
                }
            }
        }
    }

    private void HidePeek()
    {
        if (_peekTime == null && !PeekPopup.IsOpen)
            return;
        _peekTime = null;
        PeekPopup.IsOpen = false;
        _peek.Cancel();
    }
}
