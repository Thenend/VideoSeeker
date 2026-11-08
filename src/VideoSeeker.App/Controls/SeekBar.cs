using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VideoSeeker.App.Controls;

/// <summary>
/// Barra principal de la línea de tiempo. Mientras se arrastra pide el frame exacto bajo el cursor
/// y al soltar confirma la posición; la rueda salta como ← / → (con Shift y Ctrl, sus saltos) y Ctrl + Shift + rueda
/// acerca o aleja. El tramo ampliado en la barra de zoom se
/// ve como una banda con dos banderitas encima de la pista: arrastrando una se mueve ese extremo y
/// arrastrando el espacio entre ambas se mueve el tramo entero.
/// </summary>
public sealed class SeekBar : TimelineBar
{
    private const double BarTrackHeight = 6, FlagWidth = 7, FlagHeight = 7;

    private enum Grab { None, Start, End, Move }

    private (double Start, double End)? _zoom;
    private Grab _grab;
    private double _grabOffset;
    private int _wheel;

    /// <summary>Rueda: muescas (positivo, adelante) y modificadores pulsados; se salta lo mismo que con ← / →.</summary>
    public event Action<int, ModifierKeys>? WheelSeekRequested;
    /// <summary>Ctrl + Shift + rueda: instante bajo el cursor y giro de la rueda (positivo acerca).</summary>
    public event Action<double, int>? ZoomWheel;
    /// <summary>
    /// Se arrastra una banderita del tramo ampliado (Start, End) o la banda (Body): el instante bajo el cursor o, con
    /// Body, el nuevo inicio del tramo. Quien lo recibe calcula el tramo (el otro extremo hace de ancla).
    /// </summary>
    public event Action<BookmarkPart, double>? ZoomDragged;
    /// <summary>Se suelta lo que se arrastraba del tramo ampliado.</summary>
    public event Action? ZoomDragEnded;

    public SeekBar()
    {
        // Etiqueta, banderitas del tramo, pista, línea de la caché y franja de bookmarks.
        Height = LabelHeight + FlagHeight + 1 + BarTrackHeight + 5 + LaneHeight + 2;
    }

    /// <summary>
    /// Inicio del último frame: en él la cabeza se pinta al final de la barra (si no, nunca llegaría: el último frame
    /// empieza un frame antes del final del vídeo, y arrastrando hasta el final se quedaba un poco antes).
    /// </summary>
    public double LastFrameStart
    {
        get => _lastFrameStart;
        set { _lastFrameStart = value; InvalidateVisual(); }
    }

    private double _lastFrameStart;

    /// <summary>Tramo que muestra la barra de zoom; null si está cerrada.</summary>
    public (double Start, double End)? Zoom
    {
        get => _zoom;
        set { _zoom = value; InvalidateVisual(); }
    }

    /// <summary>
    /// Color de la región seleccionada si el tramo es ella: las banderitas toman su color y no se pinta la banda ámbar
    /// (la región ya se ve en la franja de bookmarks). Null, tramo libre.
    /// </summary>
    public string? ZoomColor
    {
        get => _zoomColor;
        set { _zoomColor = value; InvalidateVisual(); }
    }

    private string? _zoomColor;

    protected override double ViewStart => 0;
    protected override double ViewEnd => Duration;
    protected override double TrackHeight => BarTrackHeight;
    protected override double TrackTop => LabelHeight + FlagHeight + 1;

    protected override string HoverLabel(double time) => TimeFormat.Format(time, withMilliseconds: false);

    protected override void RenderBar(DrawingContext dc)
    {
        var top = TrackTop;
        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(TrackLeft, top, TrackWidth, BarTrackHeight), 3, 3);
        DrawCached(dc, top + BarTrackHeight + 2);

        var shown = _lastFrameStart > 0 && ShownTime >= _lastFrameStart - 1e-4 ? Duration : Math.Min(ShownTime, Duration);
        var headX = XAt(shown);
        dc.DrawRoundedRectangle(PlayedBrush, null, new Rect(TrackLeft, top, Math.Max(0, headX - TrackLeft), BarTrackHeight), 3, 3);

        if (_zoom is { } z)
        {
            var (x0, x1) = (XAt(z.Start), XAt(z.End));
            var bandTop = top - FlagHeight - 1;
            var flag = _zoomColor is { } hex ? LineBrushFor(hex) : ZoomBrush;
            if (_zoomColor == null)
                dc.DrawRectangle(ZoomFillBrush, null, new Rect(x0, bandTop, Math.Max(1, x1 - x0), top + BarTrackHeight + 3 - bandTop));
            // Banderitas hacia fuera: aunque el tramo mida menos de un píxel, las dos se pueden agarrar.
            dc.DrawRectangle(flag, null, new Rect(x0 - 1, bandTop, 2, top + BarTrackHeight + 3 - bandTop));
            dc.DrawRectangle(flag, null, new Rect(x1 - 1, bandTop, 2, top + BarTrackHeight + 3 - bandTop));
            dc.DrawRectangle(flag, null, new Rect(x0 - FlagWidth, bandTop, FlagWidth, FlagHeight));
            dc.DrawRectangle(flag, null, new Rect(x1, bandTop, FlagWidth, FlagHeight));
        }

        dc.DrawEllipse(HeadBrush, null, new Point(headX, top + BarTrackHeight / 2), 6, 6);
    }

    // Lo que hay bajo el ratón en la franja de encima de la pista: una banderita o la banda.
    private Grab GrabAt(Point point)
    {
        if (_zoom is not { } z || point.Y > TrackTop || point.Y < TrackTop - FlagHeight - 4)
            return Grab.None;
        var (x0, x1) = (XAt(z.Start), XAt(z.End));
        if (point.X >= x0 - FlagWidth - 2 && point.X <= x0 + 1)
            return Grab.Start;
        if (point.X >= x1 - 1 && point.X <= x1 + FlagWidth + 2)
            return Grab.End;
        // El centro solo mueve el tramo entero si el ajuste lo permite; si no, es como pulsar la pista.
        return MoveWholeRegions && point.X > x0 && point.X < x1 ? Grab.Move : Grab.None;
    }

    /// <summary>Para las autopruebas: punto en el centro de la banda del tramo, por encima de la pista.</summary>
    internal Point ZoomBandCenter() =>
        _zoom is { } z ? new Point((XAt(z.Start) + XAt(z.End)) / 2, TrackTop - FlagHeight / 2) : default;

    /// <summary>Para las autopruebas: punto sobre la banderita del inicio o del fin del tramo.</summary>
    internal Point ZoomFlagPoint(bool end) =>
        _zoom is { } z ? new Point(end ? XAt(z.End) + FlagWidth / 2 : XAt(z.Start) - FlagWidth / 2, TrackTop - FlagHeight / 2) : default;

    protected override Cursor CursorAt(Point point) => GrabAt(point) switch
    {
        Grab.Start or Grab.End => Cursors.SizeWE,
        Grab.Move => Cursors.SizeAll,
        _ => Cursors.Hand,
    };

    protected override bool BeginGrab(Point point)
    {
        _grab = GrabAt(point);
        if (_grab == Grab.None)
            return false;
        _grabOffset = TimeAt(point.X) - _zoom!.Value.Start;
        return true;
    }

    protected override void MoveGrab(double x)
    {
        if (_zoom == null)
            return;
        var t = TimeAt(x);
        switch (_grab)
        {
            case Grab.Start:
                ZoomDragged?.Invoke(BookmarkPart.Start, t);
                break;
            case Grab.End:
                ZoomDragged?.Invoke(BookmarkPart.End, t);
                break;
            default:
                ZoomDragged?.Invoke(BookmarkPart.Body, t - _grabOffset);
                break;
        }
    }

    protected override void EndGrab()
    {
        _grab = Grab.None;
        ZoomDragEnded?.Invoke();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Shift))
        {
            ZoomWheel?.Invoke(TimeAt(e.GetPosition(this).X), e.Delta);
        }
        else
        {
            // Las ruedas finas (touchpads) mandan fracciones de muesca: se acumulan.
            _wheel += e.Delta;
            var notches = _wheel / 120;
            _wheel -= notches * 120;
            if (notches != 0)
                WheelSeekRequested?.Invoke(notches, modifiers);
        }
        e.Handled = true;
    }
}
