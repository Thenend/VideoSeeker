using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace VideoSeeker.App.Controls;

/// <summary>
/// Ventana Win32 hija donde pinta mpv (se le pasa como "wid"), colocada encima de un elemento WPF
/// que hace de hueco. A diferencia de HwndHost, se puede crear antes de mostrar la ventana, así que
/// mpv empieza a cargar el vídeo mientras WPF todavía monta la interfaz.
/// mpv deja pasar el ratón (input-cursor-passthrough), por eso los clics llegan aquí.
/// Un clic sin mover el ratón es Clicked; arrastrar en horizontal genera Drag* (scrubbing), con cualquiera de
/// los dos botones. Con el derecho, además, se marca un tramo (Mark*) desde el frame en que se pulsó.
/// </summary>
public sealed partial class VideoSurface : IDisposable
{
    private const string ClassName = "VideoSeekerSurface";
    private const uint CS_DBLCLKS = 0x0008;
    private const int WS_CHILD = 0x40000000, WS_CLIPCHILDREN = 0x02000000, WS_CLIPSIBLINGS = 0x04000000;
    private const int WM_SETCURSOR = 0x0020, WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202,
        WM_LBUTTONDBLCLK = 0x0203, WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_RBUTTONDBLCLK = 0x0206, WM_MOUSEWHEEL = 0x020A, WM_MBUTTONDOWN = 0x0207, WM_MBUTTONDBLCLK = 0x0209,
        WM_CAPTURECHANGED = 0x0215, WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C, WM_XBUTTONDBLCLK = 0x020D;
    // Píxeles que hay que mover antes de que una pulsación cuente como arrastre y no como clic.
    private const int DragThreshold = 4;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040, SWP_HIDEWINDOW = 0x0080;
    private const int BLACK_BRUSH = 4, IDC_ARROW = 32512;
    private const int SM_CXDOUBLECLK = 36, SM_CYDOUBLECLK = 37;

    // El delegado debe vivir mientras exista la clase de ventana registrada.
    private static readonly WndProcDelegate WndProcKeepAlive = StaticWndProc;
    private static readonly Dictionary<nint, VideoSurface> Surfaces = [];
    private static bool _classRegistered;

    private readonly Window _owner;
    private readonly FrameworkElement _placeholder;
    private (int X, int Y, int W, int H, bool Visible) _lastPlacement;
    // Dónde se pulsó, en coordenadas de pantalla: si la ventana cambia de tamaño o de sitio con el botón pulsado (el
    // doble clic que pasa a pantalla completa), el ratón no se ha movido y no hay arrastre.
    private (int X, int Y)? _press;
    private int _dragDx;
    private bool _dragging, _ignoreNextUp;
    // Cuándo (hora del mensaje) y dónde (pantalla) empezó la pulsación actual y el último clic izquierdo sin arrastre.
    // Windows no junta los dos clics en WM_LBUTTONDBLCLK si entre ellos cambia la ventana de debajo del cursor (en
    // pausa, el primer clic reanuda y la del motor se va detrás de la de mpv): el doble clic se reconoce aquí.
    private (int Time, (int X, int Y) Point) _pressStart;
    private (int Time, (int X, int Y) Point)? _lastClick;

    public nint Handle { get; private set; }
    /// <summary>Dónde está ahora la ventana nativa, en píxeles físicos dentro de la de la app (para las autopruebas).</summary>
    public (int X, int Y, int W, int H) Placement => (_lastPlacement.X, _lastPlacement.Y, _lastPlacement.W, _lastPlacement.H);

    public event Action? Clicked;
    public event Action? DoubleClicked;
    public event Action? RightClicked;
    /// <summary>Rueda del ratón sobre la imagen (positivo hacia arriba, 120 por muesca).</summary>
    public event Action<int>? Wheel;
    /// <summary>Clic con el botón central (la rueda) sobre la imagen.</summary>
    public event Action? MiddleClicked;
    /// <summary>Botón lateral del ratón: true el de adelante, false el de atrás.</summary>
    public event Action<bool>? SideButton;

    /// <summary>Esconde el cursor sobre la imagen (pantalla completa con el ratón quieto); vuelve al moverlo.</summary>
    public bool CursorHidden
    {
        get => _cursorHidden;
        set
        {
            if (value == _cursorHidden)
                return;
            _cursorHidden = value;
            if (value)
                SetCursor(0);
        }
    }

    private bool _cursorHidden;
    public event Action? DragStarted;
    /// <summary>Desplazamiento horizontal acumulado desde el inicio del arrastre, en píxeles físicos.</summary>
    public event Action<int>? DragMoved;
    public event Action? DragEnded;
    /// <summary>Se pulsa el derecho (solo o con el izquierdo ya pulsado): empieza a marcarse un tramo en el frame que se ve.</summary>
    public event Action? MarkStarted;
    /// <summary>Se suelta el derecho (o el izquierdo): true para crear el tramo, false si se canceló.</summary>
    public event Action<bool>? MarkEnded;
    private bool _marking;
    private bool _leftDown, _rightDown;
    // Si en esta pulsación se marcó un tramo, soltar el izquierdo no cuenta como clic (no pausa ni reanuda).
    private bool _markedInPress;
    // Si en esta pulsación se usó el izquierdo: entonces soltar el derecho crea el tramo aunque no se arrastrara.
    private bool _leftInPress;
    /// <summary>La ventana nativa cambió de tamaño o posición.</summary>
    public event Action? Resized;

    public VideoSurface(Window owner, FrameworkElement placeholder)
    {
        _owner = owner;
        _placeholder = placeholder;
        placeholder.SizeChanged += (_, _) => Reposition();
        placeholder.IsVisibleChanged += (_, _) => Reposition();
        placeholder.LayoutUpdated += (_, _) => Reposition();
    }

    /// <summary>Crea la ventana nativa (y la de la ventana WPF si aún no existe).</summary>
    public nint Create()
    {
        if (Handle != 0)
            return Handle;
        var parent = new WindowInteropHelper(_owner).EnsureHandle();
        RegisterClassOnce();
        Handle = CreateWindowExW(0, ClassName, "", WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
            0, 0, 1, 1, parent, 0, GetModuleHandleW(null), 0);
        if (Handle == 0)
            throw new InvalidOperationException($"CreateWindowEx falló: {Marshal.GetLastWin32Error()}");
        Surfaces[Handle] = this;
        Reposition();
        return Handle;
    }

    private void Reposition()
    {
        if (Handle == 0)
            return;
        if (!_placeholder.IsVisible || PresentationSource.FromVisual(_placeholder) == null)
        {
            if (_lastPlacement.Visible)
                SetWindowPos(Handle, 0, 0, 0, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_HIDEWINDOW);
            _lastPlacement = default;
            return;
        }
        // LayoutUpdated salta con cualquier cambio de la ventana: solo movemos si cambia algo.
        var dpi = VisualTreeHelper.GetDpi(_placeholder);
        var origin = _placeholder.TransformToAncestor(_owner).Transform(new Point(0, 0));
        var placement = ((int)Math.Round(origin.X * dpi.DpiScaleX), (int)Math.Round(origin.Y * dpi.DpiScaleY),
            (int)Math.Round(_placeholder.ActualWidth * dpi.DpiScaleX), (int)Math.Round(_placeholder.ActualHeight * dpi.DpiScaleY), true);
        if (placement == _lastPlacement)
            return;
        _lastPlacement = placement;
        SetWindowPos(Handle, 0, placement.Item1, placement.Item2, placement.Item3, placement.Item4,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        Resized?.Invoke();
    }

    private static void RegisterClassOnce()
    {
        if (_classRegistered)
            return;
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = CS_DBLCLKS,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProcKeepAlive),
            hInstance = GetModuleHandleW(null),
            hCursor = LoadCursorW(0, IDC_ARROW),
            hbrBackground = GetStockObject(BLACK_BRUSH),
            lpszClassName = Marshal.StringToHGlobalUni(ClassName),
        };
        if (RegisterClassExW(ref wc) == 0)
            throw new InvalidOperationException($"RegisterClassEx falló: {Marshal.GetLastWin32Error()}");
        _classRegistered = true;
    }

    private static nint StaticWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (Surfaces.TryGetValue(hwnd, out var surface))
        {
            switch (msg)
            {
                case WM_LBUTTONDOWN when surface._press == null && surface.IsSecondClick(ToScreen(hwnd, PointFrom(lParam))):
                    surface.OnDoubleClick(hwnd, PointFrom(lParam));
                    return 0;
                case WM_LBUTTONDOWN:
                    surface._leftDown = true;
                    if (surface._press == null)
                        surface.BeginPress(hwnd, PointFrom(lParam));
                    surface._leftInPress = true;
                    return 0;
                case WM_MOUSEMOVE when surface._press is { } press:
                    surface.OnPressedMove(press, ToScreen(hwnd, PointFrom(lParam)));
                    return 0;
                case WM_LBUTTONUP when surface._leftDown:
                    surface._leftDown = false;
                    // Con el derecho aún pulsado, el arrastre (y el tramo) siguen.
                    if (surface._rightDown)
                        return 0;
                    surface.EndMark(commit: true);
                    surface.EndPress(clicked: !surface._dragging && !surface._ignoreNextUp && !surface._markedInPress);
                    ReleaseCapture();
                    return 0;
                case WM_CAPTURECHANGED:
                    surface._leftDown = surface._rightDown = false;
                    surface.EndMark(commit: false);
                    surface.EndPress(clicked: false);
                    return 0;
                case WM_RBUTTONDOWN or WM_RBUTTONDBLCLK:
                    surface._lastClick = null;
                    surface._rightDown = true;
                    if (surface._press == null)
                        surface.BeginPress(hwnd, PointFrom(lParam));
                    if (!surface._marking)
                    {
                        surface._marking = surface._markedInPress = true;
                        surface.MarkStarted?.Invoke();
                    }
                    return 0;
                case WM_RBUTTONUP when surface._rightDown:
                {
                    surface._rightDown = false;
                    // Un clic derecho sin arrastrar ni usar el izquierdo no crea un tramo de un frame.
                    var commit = surface._dragging || surface._leftInPress;
                    surface.EndMark(commit);
                    // Con el izquierdo aún pulsado se sigue arrastrando como siempre, ya sin marcar.
                    if (!surface._leftDown)
                    {
                        var wasDragging = surface._dragging;
                        surface.EndPress(clicked: false);
                        ReleaseCapture();
                        if (!wasDragging && !commit)
                            surface.RightClicked?.Invoke();
                    }
                    return 0;
                }
                case WM_LBUTTONDBLCLK:
                    surface.OnDoubleClick(hwnd, PointFrom(lParam));
                    return 0;
                case WM_MBUTTONDOWN or WM_MBUTTONDBLCLK:
                    surface.MiddleClicked?.Invoke();
                    return 0;
                // Pulsados seguidos llegan como doble clic: cada uno cuenta. Al soltar se devuelve TRUE para que
                // Windows no lo repita como WM_APPCOMMAND.
                case WM_XBUTTONDOWN or WM_XBUTTONDBLCLK:
                    surface.SideButton?.Invoke(((wParam >> 16) & 0xFFFF) == 2);
                    return 1;
                case WM_XBUTTONUP:
                    return 1;
                case WM_MOUSEWHEEL:
                    surface.Wheel?.Invoke((short)((wParam >> 16) & 0xFFFF));
                    return 0;
                case WM_SETCURSOR when surface._dragging || surface._cursorHidden:
                    SetCursor(0);
                    return 1;
            }
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static (int X, int Y) PointFrom(nint lParam) => ((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));

    private static (int X, int Y) ToScreen(nint hwnd, (int X, int Y) point)
    {
        var p = new POINT { X = point.X, Y = point.Y };
        ClientToScreen(hwnd, ref p);
        return (p.X, p.Y);
    }

    // Durante el arrastre el cursor se oculta y se devuelve al punto de inicio tras cada movimiento:
    // así se puede seguir arrastrando sin límite aunque el ratón llegue al borde de la pantalla.
    private void OnPressedMove((int X, int Y) press, (int X, int Y) point)
    {
        if (point == press)
            return; // el movimiento sintético que provoca SetCursorPos
        var dx = point.X - press.X;
        if (!_dragging)
        {
            if (Math.Abs(dx) < DragThreshold)
                return;
            _dragging = true;
            _dragDx = 0;
            SetCursor(0);
            DragStarted?.Invoke();
        }
        _dragDx += dx;
        SetCursorPos(press.X, press.Y);
        DragMoved?.Invoke(_dragDx);
    }

    private void OnDoubleClick(nint hwnd, (int X, int Y) point)
    {
        // Tras el doble clic llega otro WM_LBUTTONUP que no debe contar como clic.
        _lastClick = null;
        _leftDown = true;
        BeginPress(hwnd, point);
        _leftInPress = true;
        _ignoreNextUp = true;
        DoubleClicked?.Invoke();
    }

    // Como Windows: pulsar dentro del tiempo de doble clic y del rectángulo de doble clic (centrado en el primero).
    private bool IsSecondClick((int X, int Y) point)
    {
        if (_lastClick is not { } last)
            return false;
        return (uint)(GetMessageTime() - last.Time) <= GetDoubleClickTime()
            && Math.Abs(point.X - last.Point.X) <= GetSystemMetrics(SM_CXDOUBLECLK) / 2
            && Math.Abs(point.Y - last.Point.Y) <= GetSystemMetrics(SM_CYDOUBLECLK) / 2;
    }

    private void BeginPress(nint hwnd, (int X, int Y) point)
    {
        _press = ToScreen(hwnd, point);
        _pressStart = (GetMessageTime(), _press.Value);
        _markedInPress = _leftInPress = false;
        SetCapture(hwnd);
    }

    private void EndMark(bool commit)
    {
        if (!_marking)
            return;
        _marking = false;
        MarkEnded?.Invoke(commit);
    }

    private void EndPress(bool clicked)
    {
        if (_press == null)
            return;
        var wasDragging = _dragging;
        _press = null;
        if (wasDragging)
            SetCursor(LoadCursorW(0, IDC_ARROW));
        _dragging = false;
        _ignoreNextUp = false;
        _lastClick = clicked && !wasDragging ? _pressStart : null;
        if (wasDragging)
            DragEnded?.Invoke();
        else if (clicked)
            Clicked?.Invoke();
    }

    public void Dispose()
    {
        if (Handle == 0)
            return;
        Surfaces.Remove(Handle);
        DestroyWindow(Handle);
        Handle = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public nint lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground, lpszMenuName, lpszClassName, hIconSm;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial ushort RegisterClassExW(ref WNDCLASSEXW wc);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowExW(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    private static partial nint SetCapture(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    [LibraryImport("user32.dll")]
    private static partial nint SetCursor(nint cursor);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(nint hwnd, ref POINT point);

    [LibraryImport("user32.dll")]
    private static partial int GetMessageTime();

    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    private static partial nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial nint LoadCursorW(nint instance, nint cursorName);

    [LibraryImport("gdi32.dll")]
    private static partial nint GetStockObject(int obj);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandleW(string? moduleName);
}
