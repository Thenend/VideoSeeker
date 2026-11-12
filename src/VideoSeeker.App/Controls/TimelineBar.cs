using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VideoSeeker.App.Data;

namespace VideoSeeker.App.Controls;

/// <summary>Qué parte de un bookmark hay bajo el ratón: su inicio, su fin o el cuerpo de la región.</summary>
public enum BookmarkPart { Start, End, Body }

/// <summary>
/// Base de las dos líneas de tiempo (la principal y la de zoom): muestra un tramo del vídeo, pide seeks al
/// hacer clic o arrastrar con el botón izquierdo y, con el derecho, selecciona un tramo arrastrando o pide
/// el menú contextual si no se arrastra. Debajo de la pista, una franja con los bookmarks (todos son regiones): una
/// banda con un triángulo en el inicio y una marca en el fin; las que miden muy pocos píxeles, solo el triángulo,
/// que agarra su fin. Se pulsan para ir a ellos y se arrastran para cambiarlos.
/// </summary>
public abstract class TimelineBar : FrameworkElement
{
    protected const double SidePadding = 8, LabelHeight = 18;
    private const double SelectThreshold = 4;
    protected const double LaneHeight = 9;
    private const double PinHalfWidth = 4.5, PinHeight = 7;
    // Por debajo de este ancho en píxeles una región se pinta solo con el triángulo (si no, no se vería ni se podría pinchar).
    private const double TinyWidth = 6;
    private static readonly TimeSpan DragSeekInterval = TimeSpan.FromMilliseconds(15);

    protected static readonly Brush TrackBrush = Frozen(Color.FromRgb(0x3A, 0x3F, 0x47));
    protected static readonly Brush CachedBrush = Frozen(Color.FromRgb(0x3F, 0xB9, 0x50));
    protected static readonly Brush PlayedBrush = Frozen(Color.FromRgb(0x4C, 0x9B, 0xFF));
    protected static readonly Brush HeadBrush = Frozen(Colors.White);
    protected static readonly Brush HoverBrush = Frozen(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
    protected static readonly Brush LabelBrush = Frozen(Color.FromRgb(0xE6, 0xE8, 0xEB));
    protected static readonly Brush MutedBrush = Frozen(Color.FromRgb(0x9A, 0xA0, 0xA8));
    protected static readonly Brush LabelBackBrush = Frozen(Color.FromArgb(0xE0, 0x10, 0x12, 0x15));
    protected static readonly Brush ZoomBrush = Frozen(Color.FromRgb(0xF0, 0xB4, 0x29));
    protected static readonly Brush ZoomFillBrush = Frozen(Color.FromArgb(0x55, 0xF0, 0xB4, 0x29));
    protected static readonly Typeface LabelFace = new("Segoe UI");
    // Contorno doble del seleccionado, como los anillos de foco: oscuro pegado a la forma y claro por fuera. Destaca
    // igual con un color oscuro (marrón, etiquetas propias) que con uno claro (blanco), y sobre el fondo oscuro.
    private static readonly Color HaloLight = Color.FromRgb(0xE6, 0xE8, 0xEB); // el blanco de los textos
    private static readonly Color HaloDark = Color.FromRgb(0x0E, 0x0F, 0x12);
    private static readonly Brush HaloLightBrush = Frozen(HaloLight);
    private static readonly Brush HaloDarkBrush = Frozen(HaloDark);
    private static readonly Pen HaloLightPen = FrozenPen(HaloLight, 4, PenLineJoin.Round);
    private static readonly Pen HaloDarkPen = FrozenPen(HaloDark, 2, PenLineJoin.Round);
    private static readonly Dictionary<string, (Brush Solid, Brush Faded)> BookmarkBrushes = [];

    private double _duration, _position;
    private IReadOnlyList<(double Start, double End)> _cached = [];
    private DateTime _lastDragSeek;
    // Dónde se pulsó el botón izquierdo; hasta moverse unos píxeles, es un clic y no un arrastre.
    private double _pressX;
    private bool _dragMoved;
    private const double DragThreshold = 3;
    // Botón derecho: dónde se pulsó y, si ya se está arrastrando, hasta dónde va la selección.
    private double? _selectFromX, _selectToX;
    private bool _grabbing;
    // Botón derecho pulsado mientras se arrastra con el izquierdo: se está marcando un tramo.
    private bool _marking;
    private double? _markAnchor;
    // Y al revés: seleccionando con el derecho se pulsa el izquierdo; la imagen sigue al extremo de la selección.
    private bool _selectFollow;
    private double _selectFromY;
    private IReadOnlyList<Bookmark> _bookmarks = [];
    private string? _selectedBookmark;
    // Bookmark pulsado con el izquierdo: qué parte, dónde y, para mover la región entera, a qué distancia de su inicio.
    private (string Id, BookmarkPart Part, double PressX, double Offset)? _bookmarkPress;
    private bool _bookmarkMoved;
    // Pulsada la banda de una región sin poder moverla entera: hasta arrastrar es un clic en el bookmark; al
    // arrastrar pasa a ser un arrastre normal de la pista.
    private Bookmark? _bodyPress;
    private string? _hoveredBookmark;
    // Arrastrando con el botón central (la barra de zoom): dónde se pulsó y qué tramo enseñaba la barra.
    private (double X, double Start, double End)? _panPress;

    // ---- Avance solo al sacar el extremo que se arrastra por un lado de la barra (solo la de zoom) ----
    // Más allá de AutoDeadZone píxeles del borde de la pista, el extremo avanza solo hacia fuera, a una velocidad que
    // crece de forma exponencial con la distancia: AutoSlowFrames frames por segundo nada más pasar la zona muerta y
    // AutoFastSeconds segundos de vídeo por segundo a AutoFastDistance píxeles (y no más).
    private const double AutoDeadZone = 4;
    private const double AutoSlowFrames = 4;
    private const double AutoFastSeconds = 8;
    private const double AutoFastDistance = 180;
    // Hacia qué lado avanza (-1, 0 o 1), dónde está el ratón y por qué instante va el extremo.
    private int _autoDirection;
    private double _autoX, _autoTime;
    private readonly System.Diagnostics.Stopwatch _autoClock = new();

    /// <summary>Si sacar el extremo que se arrastra por un lado de la pista lo hace avanzar solo (la barra de zoom).</summary>
    protected virtual bool AutoExtendEdges => false;

    /// <summary>Fps del vídeo, para la velocidad lenta del avance solo.</summary>
    protected virtual double FramesPerSecond => 30;

    /// <summary>Mientras el extremo que se arrastra avanza solo porque el ratón está fuera de la pista.</summary>
    public bool AutoExtending => _autoDirection != 0;

    /// <summary>Si arrastrar con el botón central desplaza la vista, como un mapa (la barra de zoom).</summary>
    protected virtual bool CanPan => false;

    /// <summary>Mientras se arrastra con el botón central para desplazar la vista.</summary>
    public bool Panning => _panPress != null;

    protected double? HoverX { get; private set; }
    /// <summary>Instante bajo el cursor mientras se arrastra con el botón izquierdo.</summary>
    protected double? DragTime { get; private set; }

    /// <summary>Petición de seek: segundos y si es la definitiva (al soltar) o una intermedia del arrastre.</summary>
    public event Action<double, bool>? SeekRequested;
    /// <summary>Tramo seleccionado arrastrando con el botón derecho.</summary>
    public event Action<double, double>? RangeSelected;
    /// <summary>Clic derecho sin arrastrar: menú contextual para ese instante.</summary>
    public event Action<double>? MenuRequested;
    /// <summary>Arrastrando con el izquierdo se pulsa el derecho: empieza a marcarse un tramo en el frame que se ve.</summary>
    public event Action? MarkStarted;
    /// <summary>Se suelta el derecho (o el izquierdo): true para crear el tramo, false si se canceló.</summary>
    public event Action<bool>? MarkEnded;
    /// <summary>Se pulsa un bookmark con el izquierdo (para seleccionarlo e ir a él).</summary>
    public event Action<Bookmark, BookmarkPart>? BookmarkPressed;
    /// <summary>Se arrastra una parte de un bookmark: el instante nuevo (sin ajustar a frame) del inicio, del fin o, con Body, del inicio de la región movida.</summary>
    public event Action<Bookmark, BookmarkPart, double>? BookmarkDragged;
    /// <summary>Se suelta el bookmark pulsado: true si se arrastró.</summary>
    public event Action<Bookmark, bool>? BookmarkReleased;
    /// <summary>Clic derecho sobre un bookmark.</summary>
    public event Action<Bookmark>? BookmarkMenuRequested;
    /// <summary>El ratón pasa por encima de un bookmark (null al salir), con la x de su marca (o del centro de la región) en la barra.</summary>
    public event Action<Bookmark?, double>? BookmarkHovered;
    /// <summary>
    /// El ratón pasa por la barra sin pulsar nada ni estar sobre un bookmark: el instante bajo el cursor (el de su frame en
    /// la barra de zoom) y su x, para enseñar ese frame en miniatura. Null al salir, al pulsar o al llegar a un bookmark.
    /// </summary>
    public event Action<double?, double>? PeekRequested;
    /// <summary>
    /// Arrastrando con el botón central (CanPan): el tramo que debe enseñar la barra para que lo pulsado siga bajo el
    /// cursor (mismo ancho; quien lo recibe lo ajusta al vídeo).
    /// </summary>
    public event Action<double, double>? PanDragged;

    protected TimelineBar()
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
        set { _position = value; if (DragTime == null) InvalidateVisual(); }
    }

    /// <summary>Tramos ya precargados por el motor de inspección; se dibujan como una línea bajo la barra.</summary>
    public IReadOnlyList<(double Start, double End)> CachedRanges
    {
        get => _cached;
        set { _cached = value; InvalidateVisual(); }
    }

    /// <summary>Inicio del tramo que se está marcando (lo pone quien recibe MarkStarted); null si no se marca.</summary>
    public double? MarkAnchor
    {
        get => _markAnchor;
        set { _markAnchor = value; InvalidateVisual(); }
    }

    /// <summary>Bookmarks del vídeo, ordenados por inicio.</summary>
    public IReadOnlyList<Bookmark> Bookmarks
    {
        get => _bookmarks;
        set { _bookmarks = value; InvalidateVisual(); }
    }

    /// <summary>Si se ven (y se pueden pulsar) las marcas de los bookmarks. La franja conserva su sitio para que nada se mueva.</summary>
    public bool ShowBookmarks
    {
        get => _showBookmarks;
        set { _showBookmarks = value; InvalidateVisual(); }
    }

    private bool _showBookmarks = true;

    /// <summary>Color con el que se pinta cada bookmark (el de su etiqueta, si la tiene); sin él, el suyo.</summary>
    public Func<Bookmark, string>? ColorOf { get; set; }

    private string ColorFor(Bookmark b) => ColorOf?.Invoke(b) ?? b.Color;

    /// <summary>
    /// Si arrastrar el centro de una región (la banda de un bookmark, o el tramo ampliado en la principal) la mueve
    /// entera. Si no, arrastrarlo es como arrastrar la pista; los extremos se arrastran siempre.
    /// </summary>
    public bool MoveWholeRegions { get; set; }

    /// <summary>Si se dibuja la línea verde de lo precargado.</summary>
    public bool ShowCachedFrames
    {
        get => _showCached;
        set { _showCached = value; InvalidateVisual(); }
    }

    private bool _showCached = true;

    /// <summary>Id del bookmark seleccionado: se dibuja resaltado y con el asa de su fin.</summary>
    public string? SelectedBookmark
    {
        get => _selectedBookmark;
        set { _selectedBookmark = value; InvalidateVisual(); }
    }

    /// <summary>Posición x de un instante en la barra (para colocar ventanas encima).</summary>
    public double XOf(double time) => XAt(time);

    /// <summary>
    /// Bookmark que se está editando arrastrándolo sin seleccionarlo (uno que no es la zona): se resalta como el
    /// seleccionado mientras dura el arrastre.
    /// </summary>
    public string? EditingBookmark
    {
        get => _editingBookmark;
        set { _editingBookmark = value; InvalidateVisual(); }
    }

    private string? _editingBookmark;

    /// <summary>Si se está pulsando (sin soltar) un bookmark de esta barra.</summary>
    public bool HoldingBookmark => _bookmarkPress != null;

    /// <summary>Si la región mide tan pocos píxeles que se pinta solo con el triángulo (que agarra su fin).</summary>
    public bool IsTiny(Bookmark b) => XAt(EndOf(b)) - XAt(b.Start) < TinyWidth;

    private static double EndOf(Bookmark b) => b.End is { } e && e > b.Start ? e : b.Start;

    /// <summary>Tramo del vídeo que ocupa la barra de lado a lado.</summary>
    protected abstract double ViewStart { get; }
    protected abstract double ViewEnd { get; }
    protected abstract double TrackTop { get; }
    protected abstract double TrackHeight { get; }
    /// <summary>Arriba de la franja de bookmarks (bajo la pista y la línea de la caché).</summary>
    protected double LaneTop => TrackTop + TrackHeight + 5;

    protected double TrackLeft => SidePadding;
    protected double TrackWidth => Math.Max(1, ActualWidth - 2 * SidePadding);
    protected double ShownTime => DragTime ?? _position;

    protected double TimeAt(double x)
    {
        var span = ViewEnd - ViewStart;
        return span <= 0 ? ViewStart : ViewStart + Math.Clamp((x - TrackLeft) / TrackWidth, 0, 1) * span;
    }

    /// <summary>Posición x de un instante; fuera del tramo visible queda fuera de la pista.</summary>
    protected double XAt(double time)
    {
        var span = ViewEnd - ViewStart;
        return TrackLeft + (span <= 0 ? 0 : (time - ViewStart) / span * TrackWidth);
    }

    /// <summary>Instante al que va un clic (la barra de zoom lo ajusta al inicio del frame).</summary>
    protected virtual double SnapTime(double time) => time;

    protected abstract string HoverLabel(double time);

    /// <summary>Pinta la barra; la base añade encima la selección y la etiqueta del cursor.</summary>
    protected abstract void RenderBar(DrawingContext dc);

    protected override void OnRender(DrawingContext dc)
    {
        // Fondo transparente para que toda la superficie reciba el ratón.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        RenderBar(dc);
        DrawBookmarks(dc);

        if (_markAnchor is { } anchor)
        {
            var x0 = Math.Clamp(Math.Min(XAt(anchor), XAt(ShownTime)), TrackLeft, TrackLeft + TrackWidth);
            var x1 = Math.Clamp(Math.Max(XAt(anchor), XAt(ShownTime)), TrackLeft, TrackLeft + TrackWidth);
            dc.DrawRectangle(ZoomFillBrush, null, new Rect(x0, TrackTop - 4, Math.Max(1, x1 - x0), TrackHeight + 8));
            dc.DrawRectangle(ZoomBrush, null, new Rect(XAt(anchor) - 1, TrackTop - 4, 2, TrackHeight + 8));
        }

        if (_selectFromX is { } from && _selectToX is { } to)
        {
            var x0 = Math.Clamp(Math.Min(from, to), TrackLeft, TrackLeft + TrackWidth);
            var x1 = Math.Clamp(Math.Max(from, to), TrackLeft, TrackLeft + TrackWidth);
            dc.DrawRectangle(ZoomFillBrush, null, new Rect(x0, TrackTop - 4, x1 - x0, TrackHeight + 8));
            dc.DrawRectangle(ZoomBrush, null, new Rect(x0 - 1, TrackTop - 4, 2, TrackHeight + 8));
            dc.DrawRectangle(ZoomBrush, null, new Rect(x1 - 1, TrackTop - 4, 2, TrackHeight + 8));
            DrawLabel(dc, $"{HoverLabel(TimeAt(x0))} – {HoverLabel(TimeAt(x1))}", (x0 + x1) / 2);
        }
        else if (HoverX is { } hx && _duration > 0)
        {
            dc.DrawRectangle(HoverBrush, null, new Rect(Math.Round(hx), TrackTop - 3, 1, TrackHeight + 6));
            DrawLabel(dc, HoverLabel(TimeAt(hx)), hx);
        }
        RefreshPeekAfterRender();
    }

    private void DrawBookmarks(DrawingContext dc)
    {
        if (!_showBookmarks || _bookmarks.Count == 0 || ViewEnd <= ViewStart)
            return;
        var left = TrackLeft - PinHalfWidth - 2;
        var right = TrackLeft + TrackWidth + PinHalfWidth + 2;
        var lane = LaneTop;
        // Lo que cae fuera del tramo visible (en la barra de zoom) no se pinta encima de los botones de al lado.
        dc.PushClip(new RectangleGeometry(new Rect(left, 0, right - left, ActualHeight)));
        // Resaltados: el seleccionado y el que se está editando sin seleccionarlo (arrastrando uno que no es la zona).
        bool Highlighted(Bookmark b) => b.Id == _selectedBookmark || b.Id == _editingBookmark;
        var highlighted = _bookmarks.Where(Highlighted).ToList();
        // Sus bandas, debajo de todo: los bookmarks que caen dentro se siguen viendo.
        foreach (var region in highlighted.Where(r => !IsTiny(r)))
            DrawSelectedBand(dc, region, lane, left, right);
        foreach (var b in _bookmarks)
            if (!Highlighted(b))
                DrawBookmark(dc, b, lane, left, right, false);
        foreach (var b in highlighted)
            DrawBookmark(dc, b, lane, left, right, true); // encima de los demás
        dc.Pop();
    }

    private void DrawBookmark(DrawingContext dc, Bookmark b, double lane, double left, double right, bool selected)
    {
        var (solid, faded) = BrushesFor(ColorFor(b));
        var x0 = XAt(b.Start);
        var x1 = XAt(EndOf(b));
        if (x1 < left || x0 > right)
            return;
        // Muy corta: solo el triángulo, como un instante, para que se vea y se pueda pinchar.
        var tiny = x1 - x0 < TinyWidth;
        if (!tiny)
        {
            var a = Math.Max(x0, left);
            var c = Math.Min(x1, right);
            // La seleccionada ya tiene su banda (DrawSelectedBand), por debajo de los demás.
            if (!selected)
                dc.DrawRectangle(faded, null, new Rect(a, lane + 2, Math.Max(1, c - a), 4));
            if (x1 <= right)
            {
                var end = new Rect(x1 - 1.5, lane, 3, LaneHeight - 1);
                if (selected)
                    DrawHalo(dc, end);
                dc.DrawRectangle(solid, null, end);
            }
        }
        if (selected)
        {
            // Línea fina sobre la pista para ver dónde cae exactamente (aclarada si el color es oscuro, que sobre la pista
            // gris no se vería).
            var line = LineBrushFor(ColorFor(b));
            if (x0 >= TrackLeft)
                dc.DrawRectangle(line, null, new Rect(Math.Round(x0), TrackTop, 1, lane - TrackTop));
            if (!tiny && x1 <= TrackLeft + TrackWidth)
                dc.DrawRectangle(line, null, new Rect(Math.Round(x1), TrackTop, 1, lane - TrackTop));
        }
        if (x0 >= left && x0 <= right)
        {
            var pin = new StreamGeometry();
            using (var g = pin.Open())
            {
                g.BeginFigure(new Point(x0, lane), true, true);
                g.LineTo(new Point(x0 + PinHalfWidth, lane + PinHeight), true, false);
                g.LineTo(new Point(x0 - PinHalfWidth, lane + PinHeight), true, false);
            }
            pin.Freeze();
            if (selected)
            {
                // Los trazos van centrados en el borde: el relleno tapa su mitad de dentro y queda 1 px oscuro y 1 px claro.
                dc.DrawGeometry(null, HaloLightPen, pin);
                dc.DrawGeometry(null, HaloDarkPen, pin);
            }
            dc.DrawGeometry(solid, null, pin);
        }
    }

    // Región seleccionada: su banda dentro del contorno doble, que ocupa toda la franja. Va por debajo de los demás
    // bookmarks, así que los que caen dentro se siguen viendo.
    private void DrawSelectedBand(DrawingContext dc, Bookmark b, double lane, double left, double right)
    {
        var a = Math.Max(XAt(b.Start), left);
        var c = Math.Min(XAt(EndOf(b)), right);
        if (c <= a)
            return;
        var band = new Rect(a, lane + 2, c - a, LaneHeight - 4);
        DrawHalo(dc, band);
        dc.DrawRectangle(SelectedBandFor(ColorFor(b)), null, band);
    }

    // 1 px oscuro y 1 px claro alrededor del rectángulo (se pinta antes que él).
    private static void DrawHalo(DrawingContext dc, Rect rect)
    {
        var outer = rect;
        outer.Inflate(2, 2);
        dc.DrawRectangle(HaloLightBrush, null, outer);
        var inner = rect;
        inner.Inflate(1, 1);
        dc.DrawRectangle(HaloDarkBrush, null, inner);
    }

    private static readonly Dictionary<string, Brush> LineBrushes = [];

    // El color del bookmark, mezclado con blanco si es oscuro, hasta que se vea sobre la pista (y como texto).
    internal static Brush LineBrushFor(string hex)
    {
        if (LineBrushes.TryGetValue(hex, out var brush))
            return brush;
        var color = ((SolidColorBrush)BrushesFor(hex).Solid).Color;
        var luma = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
        const double Min = 0.55;
        if (luma < Min)
        {
            var t = (Min - luma) / (1 - luma);
            byte Mix(byte v) => (byte)Math.Round(v + (255 - v) * t);
            color = Color.FromRgb(Mix(color.R), Mix(color.G), Mix(color.B));
        }
        return LineBrushes[hex] = Frozen(color);
    }

    private static readonly Dictionary<string, Brush> SelectedBands = [];

    // Algo más opaco que el de las demás regiones, pero sin llegar al color pleno (que deslumbraba).
    private static Brush SelectedBandFor(string hex)
    {
        if (SelectedBands.TryGetValue(hex, out var brush))
            return brush;
        var color = ((SolidColorBrush)BrushesFor(hex).Solid).Color;
        return SelectedBands[hex] = Frozen(Color.FromArgb(0xC8, color.R, color.G, color.B));
    }

    protected static (Brush Solid, Brush Faded) BrushesFor(string hex)
    {
        if (BookmarkBrushes.TryGetValue(hex, out var brushes))
            return brushes;
        Color color;
        try { color = (Color)ColorConverter.ConvertFromString(hex); }
        catch (FormatException) { color = (Color)ColorConverter.ConvertFromString(BookmarkColors.Default); }
        return BookmarkBrushes[hex] = (Frozen(color), Frozen(Color.FromArgb(0xA0, color.R, color.G, color.B)));
    }

    /// <summary>El bookmark (y su parte) bajo el punto, si está en la franja de bookmarks.</summary>
    private (Bookmark Bookmark, BookmarkPart Part)? HitBookmark(Point p)
    {
        if (!_showBookmarks || _bookmarks.Count == 0 || p.Y < LaneTop - 3 || p.Y > LaneTop + LaneHeight + 3 || ViewEnd <= ViewStart)
            return null;
        if (p.X < TrackLeft - PinHalfWidth - 2 || p.X > TrackLeft + TrackWidth + PinHalfWidth + 2)
            return null;
        // Primero el fin del seleccionado.
        if (_bookmarks.FirstOrDefault(b => b.Id == _selectedBookmark) is { } sel && !IsTiny(sel) && Math.Abs(p.X - XAt(EndOf(sel))) <= 4)
            return (sel, BookmarkPart.End);
        // La marca de inicio o de fin más cercana; a igual distancia, la del seleccionado. El triángulo de una región
        // muy corta agarra su fin: arrastrándolo hacia atrás, crece hacia atrás (el inicio hace de ancla).
        Bookmark? nearest = null;
        var nearestPart = BookmarkPart.Start;
        var best = PinHalfWidth + 1;
        foreach (var b in _bookmarks)
        {
            var tiny = IsTiny(b);
            var d = Math.Abs(p.X - XAt(b.Start));
            if (d < best || (d == best && b.Id == _selectedBookmark))
                (nearest, nearestPart, best) = (b, tiny ? BookmarkPart.End : BookmarkPart.Start, d);
            if (!tiny)
            {
                d = Math.Abs(p.X - XAt(EndOf(b))) + 0.5; // en empate con un inicio, gana el inicio
                if (d <= 4.5 && d < best)
                    (nearest, nearestPart, best) = (b, BookmarkPart.End, d);
            }
        }
        if (nearest != null)
            return (nearest, nearestPart);
        // Dentro de una región: la más corta que lo contiene.
        var region = _bookmarks.Where(b => !IsTiny(b) && p.X >= XAt(b.Start) && p.X <= XAt(EndOf(b)))
            .OrderBy(b => EndOf(b) - b.Start).FirstOrDefault();
        return region != null ? (region, BookmarkPart.Body) : null;
    }

    protected void DrawCached(DrawingContext dc, double y)
    {
        if (!_showCached)
            return;
        foreach (var (start, end) in _cached)
        {
            var x0 = Math.Max(TrackLeft, XAt(start));
            var x1 = Math.Min(TrackLeft + TrackWidth, XAt(end));
            if (x1 > x0 - 1)
                dc.DrawRectangle(CachedBrush, null, new Rect(x0, y, Math.Max(1, x1 - x0), 2));
        }
    }

    protected FormattedText Text(string text, double size, Brush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFace, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected void DrawLabel(DrawingContext dc, string text, double centerX)
    {
        var ft = Text(text, 11.5, LabelBrush);
        var width = ft.Width + 10;
        var x = Math.Clamp(centerX - width / 2, 0, Math.Max(0, ActualWidth - width));
        dc.DrawRoundedRectangle(LabelBackBrush, null, new Rect(x, 0, width, LabelHeight - 2), 3, 3);
        dc.DrawText(ft, new Point(x + 5, (LabelHeight - 2 - ft.Height) / 2));
    }

    /// <summary>
    /// Arrastres propios de cada barra (las asas del tramo ampliado en la principal). True si empieza uno:
    /// entonces la base le pasa los movimientos en vez de pedir seeks.
    /// </summary>
    protected virtual bool BeginGrab(Point point) => false;
    protected virtual void MoveGrab(double x) { }
    protected virtual void EndGrab() { }
    /// <summary>Cursor según lo que hay bajo el ratón.</summary>
    protected virtual Cursor CursorAt(Point point) => Cursors.Hand;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (LeftDown(e.GetPosition(this), e.ClickCount))
            e.Handled = true;
    }

    private bool LeftDown(Point point, int clickCount)
    {
        if (_panPress != null)
            return true;
        SetPeek(null);
        if (_selectFromX != null)
        {
            if (_duration <= 0)
                return false;
            _selectToX ??= point.X;
            _selectFollow = true;
            SendDragSeek(SnapTime(TimeAt(point.X)), force: true);
            return true;
        }
        if (_duration <= 0)
            return false;
        if (HitBookmark(point) is { } hit)
        {
            if (hit.Part == BookmarkPart.Body && !MoveWholeRegions)
            {
                // No se puede mover entera: hasta saber si es un clic o un arrastre de la pista, nada.
                _bodyPress = hit.Bookmark;
                _pressX = point.X;
                _dragMoved = false;
                CaptureMouse();
                return true;
            }
            _bookmarkPress = (hit.Bookmark.Id, hit.Part, point.X, TimeAt(point.X) - hit.Bookmark.Start);
            _bookmarkMoved = false;
            CaptureMouse();
            BookmarkPressed?.Invoke(hit.Bookmark, hit.Part);
            return true;
        }
        if (BeginGrab(point))
        {
            _grabbing = true;
            CaptureMouse();
            return true;
        }
        CaptureMouse();
        _pressX = point.X;
        _dragMoved = false;
        DragTo(point.X, force: true);
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        // Con el botón central simulado, el ratón de verdad no cuenta (al capturarlo, WPF manda un movimiento con su
        // posición real, que desplazaba la vista).
        if (!_simulatingPan)
            MouseMoved(e.GetPosition(this), e.LeftButton == MouseButtonState.Pressed, e.RightButton == MouseButtonState.Pressed);
    }

    private void MouseMoved(Point point, bool left, bool right)
    {
        HoverX = point.X;
        double? peekX = null;
        if (_panPress != null)
        {
            PanTo(point.X);
        }
        else if (_bodyPress != null)
        {
            // Arrastrando el centro de la banda: como en la pista, la imagen sigue al cursor.
            if (left && Math.Abs(point.X - _pressX) >= DragThreshold)
            {
                _bodyPress = null;
                SetHovered(null);
                _dragMoved = true;
                DragTo(point.X, force: true);
            }
        }
        else if (_bookmarkPress is { } press)
        {
            if (left && (_bookmarkMoved || Math.Abs(point.X - press.PressX) >= DragThreshold)
                && _bookmarks.FirstOrDefault(b => b.Id == press.Id) is { } bookmark)
            {
                _bookmarkMoved = true;
                // Un extremo llevado por fuera de la pista avanza solo (OnAutoExtend); al volver dentro, sigue al ratón.
                // Solo con la región que es la zona (la seleccionada): mover la vista por otra cambiaría la zona. Con otra,
                // el extremo se queda en el borde de la barra.
                var outside = AutoExtendEdges && press.Part != BookmarkPart.Body && press.Id == _selectedBookmark
                    ? point.X < TrackLeft - AutoDeadZone ? -1 : point.X > TrackLeft + TrackWidth + AutoDeadZone ? 1 : 0
                    : 0;
                _autoX = point.X;
                if (outside != 0)
                {
                    if (outside != _autoDirection)
                        StartAutoExtend(outside);
                }
                else
                {
                    StopAutoExtend();
                    var t = TimeAt(point.X);
                    BookmarkDragged?.Invoke(bookmark, press.Part, press.Part == BookmarkPart.Body ? t - press.Offset : t);
                }
            }
        }
        else if (_grabbing)
        {
            MoveGrab(point.X);
        }
        else if (DragTime != null && left)
        {
            if (_dragMoved || Math.Abs(point.X - _pressX) >= DragThreshold)
            {
                _dragMoved = true;
                DragTo(point.X, force: false);
            }
        }
        else if (_selectFromX is { } from && right)
        {
            if (_selectToX != null || Math.Abs(point.X - from) >= SelectThreshold)
                _selectToX = point.X;
            if (_selectFollow && left)
                SendDragSeek(SnapTime(TimeAt(point.X)), force: false);
        }
        else
        {
            var hit = HitBookmark(point);
            Cursor = hit switch
            {
                // Lo que pasará al arrastrar: mover un extremo o la región entera.
                { Part: BookmarkPart.Start or BookmarkPart.End } => Cursors.SizeWE,
                { Part: BookmarkPart.Body } when MoveWholeRegions => Cursors.SizeAll,
                { Part: BookmarkPart.Body } => Cursors.Hand,
                _ => CursorAt(point),
            };
            SetHovered(hit?.Bookmark);
            // Con la barra capturando el ratón (pulsada, arrastrando) no: al capturar, WPF manda un movimiento propio.
            if (hit == null && !left && !right && !IsMouseCaptured)
                peekX = point.X;
        }
        SetPeek(peekX);
        InvalidateVisual();
    }

    private double? _peekTime;
    private bool _peekRefreshQueued;

    private void SetPeek(double? x)
    {
        double? time = x is { } px && Duration > 0 && ViewEnd > ViewStart ? SnapTime(TimeAt(px)) : null;
        if (time == null && _peekTime == null)
            return;
        _peekTime = time;
        PeekRequested?.Invoke(time, x ?? 0);
    }

    // Con el ratón quieto la vista puede cambiar debajo (rueda en la barra de zoom, la zona que se mueve): el frame del
    // vistazo se pone al día después de pintar.
    private void RefreshPeekAfterRender()
    {
        if (_peekTime is not { } shown || HoverX is not { } hx || _peekRefreshQueued || SnapTime(TimeAt(hx)) == shown)
            return;
        _peekRefreshQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _peekRefreshQueued = false;
            if (_peekTime != null && HoverX is { } x)
                SetPeek(x);
        });
    }

    /// <summary>Para las autopruebas: un arrastre con el botón izquierdo de un punto a otro, por los mismos caminos que el ratón.</summary>
    /// <param name="beforeRelease">Se llama con el botón aún pulsado, al final del recorrido (para mirar cómo va).</param>
    internal void SimulateLeftDrag(Point from, Point to, int steps = 8, Action? beforeRelease = null)
    {
        LeftDown(from, 1);
        for (var i = 1; i <= steps; i++)
            MouseMoved(new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps), left: true, right: false);
        beforeRelease?.Invoke();
        LeftUp();
    }

    // ---- Desplazar la vista con el botón central (la barra de zoom) ----

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle && MiddleDown(e.GetPosition(this)))
            e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle && MiddleUp(e.GetPosition(this)))
            e.Handled = true;
    }

    // Solo si no se está haciendo otra cosa con los otros botones.
    private bool MiddleDown(Point point)
    {
        if (!CanPan || _duration <= 0 || ViewEnd <= ViewStart || _panPress != null || DragTime != null || _grabbing
            || _bookmarkPress != null || _bodyPress != null || _selectFromX != null)
            return false;
        SetPeek(null);
        SetHovered(null);
        _panPress = (point.X, ViewStart, ViewEnd);
        Cursor = Cursors.ScrollWE;
        CaptureMouse();
        return true;
    }

    private bool MiddleUp(Point point)
    {
        if (_panPress == null)
            return false;
        _panPress = null;
        ReleaseMouseCapture();
        Cursor = CursorAt(point);
        InvalidateVisual();
        return true;
    }

    // Lo pulsado sigue bajo el cursor: arrastrar hacia la izquierda lleva a frames posteriores. En un extremo del vídeo la
    // vista no pasa de ahí; se toma como nuevo punto de partida, para que al volver responda al momento.
    private void PanTo(double x)
    {
        if (_panPress is not { } pan)
            return;
        var shift = (pan.X - x) * (pan.End - pan.Start) / TrackWidth;
        PanDragged?.Invoke(pan.Start + shift, pan.End + shift);
        if (Math.Abs(ViewStart - (pan.Start + shift)) > 1e-9)
            _panPress = (x, ViewStart, ViewEnd);
    }

    /// <summary>Para las autopruebas: pulsar, mover y soltar el botón central, por los mismos caminos que el ratón.</summary>
    internal void SimulateMiddleDown(Point point)
    {
        _simulatingPan = true;
        if (!MiddleDown(point))
            _simulatingPan = false;
    }

    internal void SimulateMiddleMove(Point point) => MouseMoved(point, left: false, right: false);

    internal void SimulateMiddleUp(Point point)
    {
        MiddleUp(point);
        _simulatingPan = false;
    }

    private bool _simulatingPan;

    private void StartAutoExtend(int direction)
    {
        if (_autoDirection == 0)
            CompositionTarget.Rendering += OnAutoExtend;
        _autoDirection = direction;
        // Empieza por el borde de la pista por el que se ha salido.
        _autoTime = direction > 0 ? ViewEnd : ViewStart;
        _autoClock.Restart();
    }

    private void StopAutoExtend()
    {
        if (_autoDirection == 0)
            return;
        _autoDirection = 0;
        CompositionTarget.Rendering -= OnAutoExtend;
        _autoClock.Stop();
    }

    // En cada frame de pintado: el extremo avanza lo que toca a la velocidad de la distancia del ratón al borde. Quien
    // recibe el arrastre lo ajusta a frames, aplica el ancla y (la barra de zoom) se reencuadra para enseñarlo.
    private void OnAutoExtend(object? sender, EventArgs e)
    {
        if (_autoDirection == 0 || _bookmarkPress is not { } press || _bookmarks.FirstOrDefault(b => b.Id == press.Id) is not { } bookmark)
        {
            StopAutoExtend();
            return;
        }
        var dt = Math.Min(0.1, _autoClock.Elapsed.TotalSeconds);
        _autoClock.Restart();
        var edge = _autoDirection > 0 ? TrackLeft + TrackWidth : TrackLeft;
        var distance = Math.Max(0, Math.Abs(_autoX - edge) - AutoDeadZone);
        var slow = AutoSlowFrames / Math.Max(1, FramesPerSecond);
        // slow · e^(distance / k), con k tal que a AutoFastDistance se llega a AutoFastSeconds.
        var k = (AutoFastDistance - AutoDeadZone) / Math.Log(AutoFastSeconds / slow);
        var speed = Math.Min(AutoFastSeconds, slow * Math.Exp(distance / k));
        var next = Math.Clamp(_autoTime + _autoDirection * speed * dt, 0, Duration);
        if (next == _autoTime)
            return;
        _autoTime = next;
        BookmarkDragged?.Invoke(bookmark, press.Part, _autoTime);
    }

    /// <summary>Para las autopruebas: pulsar, mover y soltar el botón izquierdo por separado (con tiempo entre medias).</summary>
    internal void SimulateLeftDown(Point point) => LeftDown(point, 1);

    internal void SimulateLeftMove(Point point) => MouseMoved(point, left: true, right: false);

    internal void SimulateLeftUp() => LeftUp();

    /// <summary>Para las autopruebas: el ratón pasa por un punto sin pulsar nada, y sale de la barra.</summary>
    internal void SimulateHover(Point point) => MouseMoved(point, left: false, right: false);

    internal void SimulateLeave() => OnMouseLeave(null!);

    /// <summary>Para las autopruebas: punto del centro de la pista en el instante dado.</summary>
    internal Point TrackPoint(double time) => new(XAt(time), TrackTop + TrackHeight / 2);

    /// <summary>Para las autopruebas: punto del centro de la franja de bookmarks en el instante dado.</summary>
    internal Point LanePoint(double time) => new(XAt(time), LaneTop + LaneHeight / 2);

    private void SetHovered(Bookmark? bookmark)
    {
        if (bookmark?.Id == _hoveredBookmark)
            return;
        _hoveredBookmark = bookmark?.Id;
        BookmarkHovered?.Invoke(bookmark, bookmark != null ? AnchorX(bookmark) : 0);
    }

    // Dónde colocar la miniatura: sobre la marca, o en el centro de la parte visible de una región.
    private double AnchorX(Bookmark bookmark)
    {
        var (left, right) = (TrackLeft, TrackLeft + TrackWidth);
        return (Math.Clamp(XAt(bookmark.Start), left, right) + Math.Clamp(XAt(EndOf(bookmark)), left, right)) / 2;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (LeftUp())
            e.Handled = true;
    }

    private bool LeftUp()
    {
        if (_bodyPress is { } body)
        {
            // Clic sin arrastrar en la banda: como siempre, se selecciona y se va a él.
            _bodyPress = null;
            ReleaseMouseCapture();
            BookmarkPressed?.Invoke(body, BookmarkPart.Body);
            return true;
        }
        if (_bookmarkPress is { } press)
        {
            StopAutoExtend();
            _bookmarkPress = null;
            ReleaseMouseCapture();
            if (_bookmarks.FirstOrDefault(b => b.Id == press.Id) is { } bookmark)
                BookmarkReleased?.Invoke(bookmark, _bookmarkMoved);
            return true;
        }
        if (_grabbing)
        {
            _grabbing = false;
            ReleaseMouseCapture();
            EndGrab();
            return true;
        }
        if (_selectFollow)
        {
            // Se deja de seguir; la selección con el derecho continúa.
            _selectFollow = false;
            if (_selectToX is { } x)
                SeekRequested?.Invoke(SnapTime(TimeAt(x)), true);
            return true;
        }
        if (DragTime is not { } t)
            return false;
        var marking = _marking;
        _marking = false;
        DragTime = null;
        ReleaseMouseCapture();
        _position = t;
        SeekRequested?.Invoke(t, true);
        if (marking)
            MarkEnded?.Invoke(true);
        InvalidateVisual();
        return true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        SetPeek(null);
        if (DragTime != null && !_marking)
        {
            _marking = true;
            MarkStarted?.Invoke();
            e.Handled = true;
            return;
        }
        if (_duration <= 0 || DragTime != null || _grabbing || _bookmarkPress != null || _bodyPress != null || _panPress != null)
            return;
        _selectFromY = e.GetPosition(this).Y;
        _selectFromX = e.GetPosition(this).X;
        _selectToX = null;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        if (_marking)
        {
            // El arrastre con el izquierdo sigue; el tramo se crea ya.
            _marking = false;
            MarkEnded?.Invoke(true);
            e.Handled = true;
            return;
        }
        if (_selectFromX is not { } from)
            return;
        var to = _selectToX;
        var follow = _selectFollow;
        _selectFromX = _selectToX = null;
        _selectFollow = false;
        if (!follow)
            ReleaseMouseCapture();
        InvalidateVisual();
        e.Handled = true;
        if (to is { } x)
        {
            var (start, end) = (TimeAt(Math.Min(from, x)), TimeAt(Math.Max(from, x)));
            if (end > start)
                RangeSelected?.Invoke(start, end);
            // Con el izquierdo aún pulsado se sigue arrastrando como siempre, ya sin seleccionar.
            if (follow)
            {
                _pressX = x;
                _dragMoved = true;
                DragTo(x, force: true);
            }
        }
        else if (HitBookmark(new Point(from, _selectFromY)) is { } hit)
        {
            SetHovered(null);
            BookmarkMenuRequested?.Invoke(hit.Bookmark);
        }
        else
        {
            MenuRequested?.Invoke(SnapTime(TimeAt(from)));
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_marking)
        {
            _marking = false;
            MarkEnded?.Invoke(false);
        }
        DragTime = null;
        _selectFromX = _selectToX = null;
        _selectFollow = false;
        _bodyPress = null;
        _panPress = null;
        StopAutoExtend();
        if (_bookmarkPress is { } press)
        {
            _bookmarkPress = null;
            if (_bookmarks.FirstOrDefault(b => b.Id == press.Id) is { } bookmark)
                BookmarkReleased?.Invoke(bookmark, _bookmarkMoved);
        }
        if (_grabbing)
        {
            _grabbing = false;
            EndGrab();
        }
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        HoverX = null;
        SetHovered(null);
        SetPeek(null);
        InvalidateVisual();
    }

    // Durante el arrastre limitamos un poco la frecuencia; quien las recibe encadena los seeks.
    private void DragTo(double x, bool force)
    {
        DragTime = SnapTime(TimeAt(x));
        InvalidateVisual();
        SendDragSeek(DragTime.Value, force);
    }

    private void SendDragSeek(double time, bool force)
    {
        var now = DateTime.UtcNow;
        if (force || now - _lastDragSeek >= DragSeekInterval)
        {
            _lastDragSeek = now;
            SeekRequested?.Invoke(time, false);
        }
    }

    private static Pen FrozenPen(Color color, double thickness, PenLineJoin join = PenLineJoin.Miter)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness) { LineJoin = join };
        pen.Freeze();
        return pen;
    }

    protected static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
