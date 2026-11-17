using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VideoSeeker.App.Controls;

/// <summary>
/// Barra fina bajo el segundo vídeo al comparar: dónde está y, con clic o arrastre, ir a otro sitio (como la principal,
/// pero sin zoom ni bookmarks). Pide el frame bajo el cursor mientras se arrastra y confirma al soltar.
/// </summary>
public sealed class CompareBar : FrameworkElement
{
    private static readonly Brush TrackBrush = Frozen(Color.FromRgb(0x3A, 0x3F, 0x47));
    private static readonly Brush PlayedBrush = Frozen(Color.FromRgb(0x4C, 0x9B, 0xFF));
    private static readonly Brush BandBrush = Frozen(Color.FromArgb(0x55, 0xF0, 0xB4, 0x29));
    private static readonly Brush BandEdgeBrush = Frozen(Color.FromRgb(0xF0, 0xB4, 0x29));
    private const double Side = 6, Track = 3, Head = 4;
    private (double Start, double End)? _band;

    /// <summary>Bloqueados y con una zona en A: el tramo de B que le corresponde, como la banda ámbar de A.</summary>
    public (double Start, double End)? Band
    {
        get => _band;
        set
        {
            if (value == _band)
                return;
            _band = value;
            InvalidateVisual();
        }
    }
    private double _duration, _position;

    /// <summary>Instante y si es el definitivo (al soltar).</summary>
    public event Action<double, bool>? SeekRequested;

    public CompareBar()
    {
        Cursor = Cursors.Hand;
        Focusable = false;
    }

    public double Duration
    {
        get => _duration;
        set { _duration = value; InvalidateVisual(); }
    }

    public double Position
    {
        get => _position;
        set
        {
            if (value == _position)
                return;
            _position = value;
            InvalidateVisual();
        }
    }

    private double Width0 => Math.Max(1, ActualWidth - 2 * Side);

    public double TimeAt(double x) => _duration <= 0 ? 0 : Math.Clamp((x - Side) / Width0, 0, 1) * _duration;

    public double XAt(double time) => Side + (_duration > 0 ? Math.Clamp(time / _duration, 0, 1) : 0) * Width0;

    protected override void OnRender(DrawingContext dc)
    {
        // Fondo transparente: así toda la altura recibe el ratón, no solo la línea.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var y = ActualHeight / 2;
        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(Side, y - Track / 2, Width0, Track), 1.5, 1.5);
        if (_band is { } band && _duration > 0)
        {
            var (x0, x1) = (XAt(band.Start), XAt(band.End));
            dc.DrawRectangle(BandBrush, null, new Rect(x0, 1, Math.Max(1, x1 - x0), ActualHeight - 2));
            dc.DrawRectangle(BandEdgeBrush, null, new Rect(x0 - 0.5, 1, 1, ActualHeight - 2));
            dc.DrawRectangle(BandEdgeBrush, null, new Rect(x1 - 0.5, 1, 1, ActualHeight - 2));
        }
        var x = XAt(_position);
        dc.DrawRoundedRectangle(PlayedBrush, null, new Rect(Side, y - Track / 2, Math.Max(0, x - Side), Track), 1.5, 1.5);
        dc.DrawEllipse(Brushes.White, null, new Point(x, y), Head, Head);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_duration <= 0)
            return;
        CaptureMouse();
        SeekRequested?.Invoke(TimeAt(e.GetPosition(this).X), false);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (IsMouseCaptured)
            SeekRequested?.Invoke(TimeAt(e.GetPosition(this).X), false);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured)
            return;
        ReleaseMouseCapture();
        SeekRequested?.Invoke(TimeAt(e.GetPosition(this).X), true);
        e.Handled = true;
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
