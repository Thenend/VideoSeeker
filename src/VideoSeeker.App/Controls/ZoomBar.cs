using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VideoSeeker.App.Controls;

/// <summary>
/// Barra de zoom: un tramo corto del vídeo con precisión de frame. Cada frame es una celda (cuando caben),
/// el clic va al inicio del frame bajo el cursor y la etiqueta da su número. Rueda: un frame por muesca;
/// Ctrl + rueda acerca o aleja; Shift + rueda desplaza el tramo.
/// </summary>
public sealed class ZoomBar : TimelineBar
{
    private const double BarTrackHeight = 20;
    // Separación mínima entre timecodes y entre marcas de frame para dibujarlas.
    private const double MinLabelSpacing = 90, MinFrameSpacing = 3;
    private static readonly double[] LabelSteps =
        [0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];
    // Con timecodes: pasos en frames por debajo del segundo.
    private static readonly int[] FrameSteps = [1, 2, 3, 5, 6, 10, 12, 15, 20, 30];

    private static readonly Brush FrameTickBrush = Frozen(Color.FromRgb(0x5A, 0x62, 0x70));
    private static readonly Brush MajorTickBrush = Frozen(Color.FromRgb(0x8A, 0x92, 0x9E));
    private static readonly Brush CurrentFrameBrush = Frozen(Color.FromArgb(0xC0, 0x4C, 0x9B, 0xFF));

    private double _start, _end;
    private FrameGrid _grid = new(null, 30);
    private int _wheel;

    /// <summary>Rueda sin modificadores: frames a avanzar (negativo, retroceder).</summary>
    public event Action<int>? StepRequested;
    /// <summary>Ctrl + rueda: instante bajo el cursor y giro de la rueda (positivo acerca).</summary>
    public event Action<double, int>? ZoomWheel;
    /// <summary>Shift + rueda: segundos a desplazar el tramo.</summary>
    public event Action<double>? PanRequested;

    public ZoomBar()
    {
        // Etiqueta, pista, línea de la caché y franja de bookmarks.
        Height = LabelHeight + 2 + BarTrackHeight + 5 + LaneHeight + 2;
    }

    public void SetRange(double start, double end)
    {
        _start = start;
        _end = end;
        InvalidateVisual();
    }

    /// <summary>Tramo que enseña la barra (para las autopruebas).</summary>
    internal (double Start, double End) View => (_start, _end);

    public FrameGrid Grid
    {
        get => _grid;
        set { _grid = value; InvalidateVisual(); }
    }

    protected override double ViewStart => _start;
    protected override double ViewEnd => _end;
    protected override double TrackHeight => BarTrackHeight;
    protected override double TrackTop => LabelHeight + 2;

    // Sacar el extremo que se arrastra por un lado de la pista lo hace avanzar solo (y la barra se aleja para enseñarlo).
    protected override bool AutoExtendEdges => true;

    protected override double FramesPerSecond => _grid.Fps;

    protected override double SnapTime(double time) => Math.Min(_grid.TimeOf(FrameAt(time)), Duration);

    // Frame bajo el cursor. El borde derecho del tramo es el inicio del frame siguiente, que ya no está
    // dentro: al llevar el cursor hasta el final se queda en el último frame del tramo. El margen (0,1 ms) tiene
    // que ser mayor que el que FrameGrid suma para redondear, o volvería a caer en el siguiente.
    private int FrameAt(double time) => _grid.FrameAt(Math.Min(time, _end - 1e-4));

    protected override string HoverLabel(double time)
    {
        var frame = FrameAt(time);
        return $"{TimeFormat.Precise(_grid.TimeOf(frame), _grid.Fps)} · {S.FrameNumber(frame)}";
    }

    protected override void RenderBar(DrawingContext dc)
    {
        var top = TrackTop;
        var left = TrackLeft;
        var right = TrackLeft + TrackWidth;
        var span = _end - _start;
        dc.DrawRectangle(TrackBrush, null, new Rect(left, top, TrackWidth, BarTrackHeight));
        if (span <= 0)
            return;
        dc.PushClip(new RectangleGeometry(new Rect(left, 0, TrackWidth, ActualHeight)));

        var pixelsPerSecond = TrackWidth / span;
        var fps = _grid.Fps > 0 ? _grid.Fps : 30;
        var frameSpacing = pixelsPerSecond / fps;

        // Celda del frame que se ve.
        var shown = ShownTime;
        var current = _grid.FrameAt(shown);
        var cellStart = XAt(_grid.TimeOf(current));
        var cellEnd = XAt(_grid.TimeOf(current + 1));
        if (cellEnd - cellStart >= 2)
            dc.DrawRectangle(CurrentFrameBrush, null, new Rect(cellStart, top, cellEnd - cellStart, BarTrackHeight));

        // Una marca por frame, si caben.
        if (frameSpacing >= MinFrameSpacing)
        {
            var tickHeight = frameSpacing >= 8 ? BarTrackHeight : 6;
            var first = _grid.FrameAt(_start);
            for (var f = first; f < first + 10000; f++)
            {
                var x = XAt(_grid.TimeOf(f));
                if (x > right)
                    break;
                if (x >= left)
                    dc.DrawRectangle(FrameTickBrush, null, new Rect(Math.Round(x), top + BarTrackHeight - tickHeight, 1, tickHeight));
            }
        }

        // Timecodes en pasos redondos.
        var step = LabelSteps.FirstOrDefault(s => s * pixelsPerSecond >= MinLabelSpacing, LabelSteps[^1]);
        var frameStep = FrameSteps.FirstOrDefault(n => n < fps && n * frameSpacing >= MinLabelSpacing, 0);
        if (TimeFormat.UseFrames && step < 1 && frameStep > 0)
        {
            // Con timecodes, por debajo del segundo los pasos son de frames enteros y cada marca cae en su frame.
            for (var f = (_grid.FrameAt(_start) + frameStep - 1) / frameStep * frameStep; _grid.TimeOf(f) <= _end + 1e-9; f += frameStep)
                DrawTick(dc, _grid.TimeOf(f), TimeFormat.Timecode(_grid.TimeOf(f), fps), top, right);
        }
        else
        {
            if (TimeFormat.UseFrames)
                step = Math.Max(step, 1);
            for (var t = Math.Ceiling(_start / step - 1e-9) * step; t <= _end + 1e-9; t += step)
                DrawTick(dc, t, TimeFormat.UseFrames ? TimeFormat.Timecode(t, fps) : TimeFormat.Format(t, withMilliseconds: step < 1), top, right);
        }

        DrawCached(dc, top + BarTrackHeight + 2);

        // Cabezal al inicio del frame que se ve.
        if (shown >= _start && shown <= _end)
        {
            var x = XAt(shown);
            dc.DrawRectangle(HeadBrush, null, new Rect(x - 1, top - 3, 2, BarTrackHeight + 6));
            var head = new StreamGeometry();
            using (var g = head.Open())
            {
                g.BeginFigure(new Point(x - 5, top - 7), true, true);
                g.LineTo(new Point(x + 5, top - 7), false, false);
                g.LineTo(new Point(x, top - 1), false, false);
            }
            head.Freeze();
            dc.DrawGeometry(HeadBrush, null, head);
        }
        dc.Pop();
    }

    private void DrawTick(DrawingContext dc, double time, string text, double top, double right)
    {
        var x = Math.Round(XAt(time));
        dc.DrawRectangle(MajorTickBrush, null, new Rect(x, top - 4, 1, BarTrackHeight + 4));
        var label = Text(text, 10.5, MutedBrush);
        if (x + 3 + label.Width <= right)
            dc.DrawText(label, new Point(x + 3, top - 4 - label.Height + 1));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            ZoomWheel?.Invoke(TimeAt(e.GetPosition(this).X), e.Delta);
        }
        else if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Hacia arriba, hacia atrás en el tiempo; una muesca desplaza una quinta parte del tramo.
            PanRequested?.Invoke(-e.Delta / 120.0 * 0.2 * (_end - _start));
        }
        else
        {
            // Las ruedas finas (touchpads) mandan fracciones de muesca: se acumulan.
            _wheel += e.Delta;
            var frames = _wheel / 120;
            _wheel -= frames * 120;
            if (frames != 0)
                StepRequested?.Invoke(frames);
        }
        e.Handled = true;
    }
}
