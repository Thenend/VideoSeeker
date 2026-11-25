using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VideoSeeker.App.Controls;

/// <summary>
/// Barra de zoom: un tramo corto del vídeo con precisión de frame. Cada frame es una celda (cuando caben),
/// el clic va al inicio del frame bajo el cursor y la etiqueta da su número. Rueda: un frame por muesca;
/// Ctrl + rueda acerca o aleja; Shift + rueda, o arrastrar con el botón central (como un mapa), desplaza el tramo.
/// </summary>
public sealed class ZoomBar : TimelineBar
{
    // Alto de la pista: con la tira de miniaturas (ajuste), más alta para que se vean.
    private const double PlainTrackHeight = 20, FilmTrackHeight = 40;
    // Separación mínima entre timecodes y entre marcas de frame para dibujarlas.
    private const double MinLabelSpacing = 90, MinFrameSpacing = 3;
    private static readonly double[] LabelSteps =
        [0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];
    // Con timecodes: pasos en frames por debajo del segundo.
    private static readonly int[] FrameSteps = [1, 2, 3, 5, 6, 10, 12, 15, 20, 30];

    private static readonly Brush FrameTickBrush = Frozen(Color.FromRgb(0x5A, 0x62, 0x70));
    private static readonly Brush MajorTickBrush = Frozen(Color.FromRgb(0x8A, 0x92, 0x9E));
    private static readonly Brush CurrentFrameBrush = Frozen(Color.FromArgb(0xC0, 0x4C, 0x9B, 0xFF));
    // Con miniaturas, el frame que se ve se tiñe poco y se recuadra, para no tapar su imagen.
    private static readonly Brush CurrentFrameTintBrush = Frozen(Color.FromArgb(0x40, 0x4C, 0x9B, 0xFF));
    private static readonly Pen CurrentFramePen = FrozenPen(Color.FromRgb(0x4C, 0x9B, 0xFF), 1.5);
    // Separación entre miniaturas.
    private static readonly Brush CellGapBrush = Frozen(Color.FromArgb(0xB0, 0x10, 0x12, 0x15));
    // Frames por miniatura: 1, 2, 3, 4, 6, 8, 12, 16, 24... (de uno a otro, ×1,5 o ×1,33: la imagen casi no se recorta).
    private static readonly int[] CellSteps = BuildCellSteps();
    // La miniatura que tapa el hueco mientras llega la buena: atenuada.
    private const double PlaceholderOpacity = 0.45;

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
        UpdateHeight();
    }

    // Etiqueta, pista, línea de la caché y franja de bookmarks.
    private void UpdateHeight() => Height = LabelHeight + 2 + BarTrackHeight + 5 + LaneHeight + 2;

    private ZoomFilmstrip? _filmstrip;

    /// <summary>De dónde salen las miniaturas de la tira; null sin tira (ajuste desactivado).</summary>
    public ZoomFilmstrip? Filmstrip
    {
        get => _filmstrip;
        set
        {
            if (_filmstrip == value)
                return;
            if (_filmstrip != null)
                _filmstrip.Changed -= InvalidateVisual;
            _filmstrip = value;
            if (_filmstrip != null)
                _filmstrip.Changed += InvalidateVisual;
            UpdateHeight();
            InvalidateVisual();
        }
    }

    private double BarTrackHeight => _filmstrip != null ? FilmTrackHeight : PlainTrackHeight;

    private static int[] BuildCellSteps()
    {
        var steps = new List<int> { 1 };
        for (var n = 2; n < 50_000_000; n *= 2)
        {
            steps.Add(n);
            steps.Add(n * 3 / 2);
        }
        return [.. steps];
    }

    // Animación del tramo (SetRange con "animate"): de dónde sale, adónde va y desde cuándo.
    private (double Start, double End) _animFrom, _animTo;
    private Stopwatch? _animClock;

    /// <summary>
    /// Enseña el tramo dado. Con "animate" (y las animaciones puestas, Reveal.Animate) va hasta él poco a poco, como
    /// el panel: los extremos se deslizan y la escala cambia suavemente. A medio camino, sigue desde donde está. Si el
    /// tramo nuevo queda lejos del de antes (otro bookmark en otra parte del vídeo), salta: deslizarse tanto en tan
    /// poco tiempo solo sería un borrón.
    /// </summary>
    public void SetRange(double start, double end, bool animate = false)
    {
        if (animate && Reveal.Animate && IsVisible && _end > _start && IsNear((_start, _end), (start, end)))
        {
            if (_animClock != null && _animTo == (start, end))
                return;
            if (_start == start && _end == end)
            {
                StopAnimation();
                return;
            }
            _animFrom = (_start, _end);
            _animTo = (start, end);
            if (_animClock == null)
                CompositionTarget.Rendering += OnAnimationFrame;
            _animClock = Stopwatch.StartNew();
            return;
        }
        StopAnimation();
        _start = start;
        _end = end;
        InvalidateVisual();
    }

    /// <summary>Si está yendo de un tramo a otro.</summary>
    internal bool AnimatingRange => _animClock != null;

    // Cerca: se solapan, o entre los dos hay menos de dos veces el más ancho.
    private static bool IsNear((double Start, double End) a, (double Start, double End) b)
    {
        var width = Math.Max(a.End - a.Start, b.End - b.Start);
        var gap = Math.Max(a.Start, b.Start) - Math.Min(a.End, b.End);
        return gap < 2 * width;
    }

    private void StopAnimation()
    {
        if (_animClock == null)
            return;
        CompositionTarget.Rendering -= OnAnimationFrame;
        _animClock = null;
    }

    private void OnAnimationFrame(object? sender, EventArgs e)
    {
        if (_animClock == null)
            return;
        var duration = Reveal.Duration.TotalMilliseconds;
        var t = duration <= 0 ? 1 : Math.Min(1, _animClock.Elapsed.TotalMilliseconds / duration);
        var eased = 1 - Math.Pow(1 - t, 3);
        _start = _animFrom.Start + (_animTo.Start - _animFrom.Start) * eased;
        _end = _animFrom.End + (_animTo.End - _animFrom.End) * eased;
        if (t >= 1)
        {
            StopAnimation();
            (_start, _end) = _animTo;
        }
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

    // Arrastrar con el botón central desplaza el tramo, como un mapa.
    protected override bool CanPan => true;

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
        var film = _filmstrip != null;
        if (_filmstrip is { } strip)
            DrawFilmstrip(dc, strip, left, right, top, fps / pixelsPerSecond);

        // Celda del frame que se ve.
        var shown = ShownTime;
        var current = _grid.FrameAt(shown);
        var cellStart = XAt(_grid.TimeOf(current));
        var cellEnd = XAt(_grid.TimeOf(current + 1));
        if (cellEnd - cellStart >= 2)
        {
            if (film)
                dc.DrawRectangle(CurrentFrameTintBrush, CurrentFramePen,
                    new Rect(cellStart + 0.75, top + 0.75, cellEnd - cellStart - 1.5, BarTrackHeight - 1.5));
            else
                dc.DrawRectangle(CurrentFrameBrush, null, new Rect(cellStart, top, cellEnd - cellStart, BarTrackHeight));
        }

        // Una marca por frame, si caben (sobre las miniaturas, solo abajo, para no cortarlas).
        if (frameSpacing >= MinFrameSpacing)
        {
            var tickHeight = frameSpacing >= 8 && !film ? BarTrackHeight : 6;
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
            // Cada marca como múltiplo del paso, redondeada: sumando el paso una y otra vez, 18,6 quedaba en 18,5999… y
            // salía «18.599».
            for (var k = Math.Ceiling(_start / step - 1e-9); Math.Round(k * step, 6) <= _end + 1e-9; k++)
                if (Math.Round(k * step, 6) is var t)
                    DrawTick(dc, t,TimeFormat.UseFrames ? TimeFormat.Timecode(t, fps) : TimeFormat.Format(t, withMilliseconds: step < 1), top, right);
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

    /// <summary>
    /// La tira: una miniatura cada N frames (N de CellSteps, el que deja la celda más parecida al ancho de la imagen),
    /// alineadas a los frames, cada una el frame que hay en su borde izquierdo. La imagen llena la celda recortando lo
    /// que sobra por el centro; si una celda de un frame es mucho más ancha que la imagen, se repite. Lo que falta se
    /// tapa con la más cercana ya hecha, atenuada, y se pide.
    /// </summary>
    private void DrawFilmstrip(DrawingContext dc, ZoomFilmstrip strip, double left, double right, double top, double framesPerPixel)
    {
        var height = BarTrackHeight;
        var imageWidth = height * strip.Aspect;
        var ideal = imageWidth * framesPerPixel;
        var step = CellSteps[0];
        foreach (var s in CellSteps)
            if (Math.Abs(Math.Log(s / ideal)) < Math.Abs(Math.Log(step / ideal)))
                step = s;
        var cellSeconds = step / (_grid.Fps > 0 ? _grid.Fps : 30);
        var end = Duration > 0 ? Math.Min(right, XAt(Duration)) : right;
        var times = new List<double>();
        for (var f = _grid.FrameAt(_start) / step * step; times.Count < 400; f += step)
        {
            var t = _grid.TimeOf(f);
            var x0 = XAt(t);
            if (x0 >= end)
                break;
            var x1 = Math.Min(XAt(_grid.TimeOf(f + step)), end);
            times.Add(t);
            var cell = new Rect(Math.Max(x0, left), top, Math.Max(0, x1 - Math.Max(x0, left)), height);
            if (cell.Width <= 0)
                continue;
            var image = strip.Get(t);
            var exact = image != null;
            image ??= strip.Nearest(t, cellSeconds * 4);
            if (image is { Width: > 0, Height: > 0 })
            {
                dc.PushClip(new RectangleGeometry(cell));
                if (!exact)
                    dc.PushOpacity(PlaceholderOpacity);
                var aspect = image.Width / image.Height;
                var natural = height * aspect;
                var cellWidth = x1 - x0;
                if (cellWidth > natural * 1.25)
                {
                    // Un frame muy ancho: la imagen entera, repetida.
                    for (var x = x0; x < x1; x += natural)
                        if (x + natural > left)
                            dc.DrawImage(image, new Rect(x, top, natural, height));
                }
                else
                {
                    var w = Math.Max(cellWidth, natural);
                    var h = w / aspect;
                    dc.DrawImage(image, new Rect(x0 + (cellWidth - w) / 2, top + (height - h) / 2, w, h));
                }
                if (!exact)
                    dc.Pop();
                dc.Pop();
            }
            if (x0 >= left)
                dc.DrawRectangle(CellGapBrush, null, new Rect(Math.Round(x0), top, 1, height));
        }
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var pixels = (int)Math.Clamp(Math.Ceiling(imageWidth * scale / 16) * 16, 32, 480);
        strip.Want(times, pixels);
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
