using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace VideoSeeker.App;

/// <summary>
/// Pantalla completa sin interfaz: las barras y los botones salen de la ventana y flotan sobre la parte de abajo de la
/// imagen (ControlsPopup: la imagen es una ventana nativa y WPF no puede pintar encima) solo mientras el ratón está
/// en esa franja, y se esconden 1,5 s después de salir. El cursor se esconde con el ratón quieto sobre la imagen. Con
/// la chincheta (o Tab) se quedan fijos, como en ventana. El panel de bookmarks sigue como estuviera.
/// </summary>
public partial class MainWindow
{
    private static readonly TimeSpan HideControlsAfter = TimeSpan.FromSeconds(1.5), HideCursorAfter = TimeSpan.FromSeconds(2);
    private static readonly Brush FloatingBack = Frozen(Color.FromArgb(0xD8, 0x16, 0x18, 0x1C));

    private readonly DispatcherTimer _controlsTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    // Controles fuera de la ventana, en ControlsPopup (pantalla completa sin fijar).
    private bool _floating;
    private DateTime _mouseLeftControls, _mouseStill;
    private (int X, int Y) _lastCursor;
    // Menús contextuales abiertos (el de velocidad, el de pistas…): mientras tanto los controles no se esconden.
    private int _openMenus;

    private void SetupFullscreenControls()
    {
        _controlsTimer.Tick += (_, _) => OnControlsTick();
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((_, _) => _openMenus++));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.ClosedEvent,
            new RoutedEventHandler((_, _) => _openMenus = Math.Max(0, _openMenus - 1)));
        // Los popups quedan por encima de las demás ventanas: al cambiar a otra app, se esconden.
        Deactivated += (_, _) =>
        {
            ControlsPopup.IsOpen = false;
            _surface.CursorHidden = false;
        };
        UpdatePinButton();
    }

    /// <summary>
    /// Sin las animaciones de Windows al maximizar y restaurar mientras se entra o sale de pantalla completa: desde una
    /// ventana maximizada se encadenaban dos (restaurar y maximizar) y la imagen se veía crecer de más y encogerse.
    /// Vuelven al rato, para cuando el usuario maximiza o minimiza.
    /// </summary>
    private void SuspendWindowAnimations()
    {
        const int TransitionsForceDisabled = 3;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var on = 1;
        DwmSetWindowAttribute(hwnd, TransitionsForceDisabled, ref on, sizeof(int));
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var off = 0;
            DwmSetWindowAttribute(hwnd, TransitionsForceDisabled, ref off, sizeof(int));
        };
        timer.Start();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    private void OnPinClick(object sender, RoutedEventArgs e) => TogglePinControls();

    private void TogglePinControls()
    {
        _store.Settings.PinFullscreenControls = !_store.Settings.PinFullscreenControls;
        _store.SaveSettings();
        UpdatePinButton();
        ApplyControlsMode();
        ShowToast(_store.Settings.PinFullscreenControls ? "Controles fijos" : "Controles ocultos: lleva el ratón abajo para verlos");
    }

    private void UpdatePinButton()
    {
        var pinned = _store.Settings.PinFullscreenControls;
        PinButton.Content = pinned ? "" : "";
        PinButton.Foreground = (Brush)FindResource(pinned ? "Accent" : "Fg");
        PinButton.ToolTip = pinned ? "Controles fijos (Tab)" : "Fijar los controles (Tab)";
    }

    // Controles en su sitio (ventana, o fijos) o flotando sobre la imagen.
    private void ApplyControlsMode()
    {
        var fullscreen = _beforeFullscreen != null;
        PinButton.Visibility = fullscreen ? Visibility.Visible : Visibility.Collapsed;
        var floating = fullscreen && !_store.Settings.PinFullscreenControls;
        if (floating == _floating)
            return;
        _floating = floating;
        if (floating)
        {
            RootGrid.Children.Remove(ControlsHost);
            ControlsHost.Background = FloatingBack;
            ControlsPopup.Child = ControlsHost;
            _mouseLeftControls = _mouseStill = DateTime.UtcNow;
            _controlsTimer.Start();
        }
        else
        {
            _controlsTimer.Stop();
            ControlsPopup.IsOpen = false;
            ControlsPopup.Child = null;
            ControlsHost.Background = null;
            ControlsHost.Width = double.NaN;
            RootGrid.Children.Add(ControlsHost);
            _surface.CursorHidden = false;
        }
    }

    private void ShowFloatingControls()
    {
        // Todo el ancho de la ventana y pegados abajo; se recolocan a cada paso (puede abrirse la barra de zoom).
        ControlsHost.Width = RootGrid.ActualWidth;
        ControlsHost.Measure(new Size(RootGrid.ActualWidth, double.PositiveInfinity));
        ControlsPopup.PlacementTarget = RootGrid;
        ControlsPopup.HorizontalOffset = 0;
        ControlsPopup.VerticalOffset = RootGrid.ActualHeight - ControlsHost.DesiredSize.Height;
        ControlsPopup.IsOpen = true;
    }

    private void OnControlsTick()
    {
        if (!IsActive || RootGrid.ActualHeight <= 0 || !GetCursorPos(out var cursor))
            return;
        var now = DateTime.UtcNow;
        var topLeft = RootGrid.PointToScreen(new Point(0, 0));
        var bottomRight = RootGrid.PointToScreen(new Point(RootGrid.ActualWidth, RootGrid.ActualHeight));
        var scale = (bottomRight.Y - topLeft.Y) / RootGrid.ActualHeight;
        ControlsHost.Measure(new Size(RootGrid.ActualWidth, double.PositiveInfinity));
        var zoneTop = bottomRight.Y - ControlsHost.DesiredSize.Height * scale;
        var inWindow = cursor.X >= topLeft.X && cursor.X < bottomRight.X && cursor.Y >= topLeft.Y && cursor.Y < bottomRight.Y;

        // Se quedan mientras el ratón está en su franja, se arrastra algo de ellos, hay un menú abierto o se está
        // poniendo nombre a un bookmark (su cuadro sale encima de las barras).
        if ((inWindow && cursor.Y >= zoneTop) || Mouse.Captured != null || _openMenus > 0 || NamePopup.IsOpen)
        {
            _mouseLeftControls = now;
            ShowFloatingControls();
        }
        else if (ControlsPopup.IsOpen && now - _mouseLeftControls > HideControlsAfter)
        {
            ControlsPopup.IsOpen = false;
            HoverPopup.IsOpen = false;
        }

        // El cursor, escondido con el ratón quieto sobre la imagen.
        if ((cursor.X, cursor.Y) != _lastCursor)
        {
            _lastCursor = (cursor.X, cursor.Y);
            _mouseStill = now;
            _surface.CursorHidden = false;
        }
        else if (!ControlsPopup.IsOpen && inWindow && now - _mouseStill > HideCursorAfter)
        {
            _surface.CursorHidden = true;
        }
    }

    // Para la autoprueba: si el ratón de verdad está en la franja de los controles (entonces no se esconden).
    private bool IsMouseInControlsZone()
    {
        if (!GetCursorPos(out var cursor))
            return false;
        var bottom = RootGrid.PointToScreen(new Point(0, RootGrid.ActualHeight)).Y;
        return cursor.Y >= bottom - 150 && cursor.Y < bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint point);
}
