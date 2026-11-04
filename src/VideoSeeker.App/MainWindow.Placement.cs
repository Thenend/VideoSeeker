using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

/// <summary>
/// La ventana se abre donde se cerró: mismo monitor, posición, tamaño y maximizada o no, como VLC. Se guarda el
/// rectángulo de la ventana sin maximizar (en píxeles de pantalla) y si estaba maximizada; en pantalla completa, cómo
/// estaba antes de entrar (se vuelve a abrir en ventana, en ese monitor). Si ese monitor ya no está o la ventana
/// quedaría casi fuera, se mete en el monitor más cercano.
/// </summary>
public partial class MainWindow
{
    // Lo mínimo de la ventana que tiene que verse en un monitor (y su barra de título, para poder moverla).
    private const int MinVisible = 120, TitleBarHeight = 32;

    // Sobre la ventana nativa ya creada pero aún sin mostrar (se crea pronto para arrancar mpv).
    private void RestorePlacement()
    {
        if (_store.Settings.Window is not { Width: > 0, Height: > 0 } saved)
            return;
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var rect = FitOnScreen(new NativeRect(saved.Left, saved.Top, saved.Left + saved.Width, saved.Top + saved.Height));
        WindowStartupLocation = WindowStartupLocation.Manual;
        // Dos veces: si el monitor tiene otra escala que donde se creó la ventana, la primera la cambia de escala (y
        // de tamaño); la segunda, ya en él, deja el tamaño exacto.
        for (var i = 0; i < 2; i++)
        {
            var placement = new WindowPlacementData { Length = Marshal.SizeOf<WindowPlacementData>(), ShowCmd = SW_HIDE, NormalPosition = ToWorkspace(rect) };
            SetWindowPlacement(hwnd, ref placement);
        }
        if (saved.Maximized)
            WindowState = WindowState.Maximized;
    }

    private void SavePlacement()
    {
        // Las autopruebas cambian el tamaño de la ventana: no deben quedarse como el sitio donde Mario la dejó.
        if (Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") != null)
            return;
        var hwnd = new WindowInteropHelper(this).Handle;
        var placement = new WindowPlacementData { Length = Marshal.SizeOf<WindowPlacementData>() };
        if (hwnd == 0 || !GetWindowPlacement(hwnd, ref placement))
            return;
        var rect = ToScreen(placement.NormalPosition);
        var maximized = _beforeFullscreen is { } before
            ? before.State == WindowState.Maximized
            : placement.ShowCmd == SW_SHOWMAXIMIZED || (placement.ShowCmd == SW_SHOWMINIMIZED && (placement.Flags & WPF_RESTORETOMAXIMIZED) != 0);
        _store.Settings.Window = new WindowBounds
        {
            Left = rect.Left,
            Top = rect.Top,
            Width = rect.Right - rect.Left,
            Height = rect.Bottom - rect.Top,
            Maximized = maximized,
        };
    }

    // El rectángulo tal cual si se ve bien en algún monitor; si no, metido en el área de trabajo del más cercano
    // (centrado si no se veía nada, como al desconectar ese monitor).
    private static NativeRect FitOnScreen(NativeRect rect)
    {
        var areas = WorkAreas();
        if (areas.Count == 0)
            return rect;
        var titleBar = rect with { Bottom = rect.Top + TitleBarHeight };
        foreach (var area in areas)
        {
            var visible = Intersect(rect, area);
            var title = Intersect(titleBar, area);
            if (visible.Width >= MinVisible && visible.Height >= MinVisible && title.Width >= MinVisible && title.Height > 0)
                return rect;
        }

        var monitor = MonitorFromRect(ref rect, MONITOR_DEFAULTTONULL);
        var offScreen = monitor == 0;
        if (offScreen)
            monitor = MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(monitor, ref info))
            return rect;
        var work = info.Work;
        var width = Math.Min(rect.Width, work.Width);
        var height = Math.Min(rect.Height, work.Height);
        var left = offScreen ? work.Left + (work.Width - width) / 2 : Math.Clamp(rect.Left, work.Left, work.Right - width);
        var top = offScreen ? work.Top + (work.Height - height) / 2 : Math.Clamp(rect.Top, work.Top, work.Bottom - height);
        return new NativeRect(left, top, left + width, top + height);
    }

    private static List<NativeRect> WorkAreas()
    {
        var areas = new List<NativeRect>();
        EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfoW(monitor, ref info))
                areas.Add(info.Work);
            return true;
        }, 0);
        return areas;
    }

    private static NativeRect Intersect(NativeRect a, NativeRect b) =>
        new(Math.Max(a.Left, b.Left), Math.Max(a.Top, b.Top), Math.Max(Math.Max(a.Left, b.Left), Math.Min(a.Right, b.Right)),
            Math.Max(Math.Max(a.Top, b.Top), Math.Min(a.Bottom, b.Bottom)));

    // GetWindowPlacement/SetWindowPlacement usan coordenadas del área de trabajo del monitor principal, que se
    // desplazan respecto a las de pantalla si la barra de tareas está arriba o a la izquierda.
    private static (int X, int Y) WorkspaceOffset()
    {
        var primary = MonitorFromPoint(new NativePointXY(0, 0), MONITOR_DEFAULTTOPRIMARY);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfoW(primary, ref info) ? (info.Work.Left - info.Monitor.Left, info.Work.Top - info.Monitor.Top) : (0, 0);
    }

    private static NativeRect ToWorkspace(NativeRect r)
    {
        var (x, y) = WorkspaceOffset();
        return new NativeRect(r.Left - x, r.Top - y, r.Right - x, r.Bottom - y);
    }

    private static NativeRect ToScreen(NativeRect r)
    {
        var (x, y) = WorkspaceOffset();
        return new NativeRect(r.Left + x, r.Top + y, r.Right + x, r.Bottom + y);
    }

    private const int SW_HIDE = 0, SW_SHOWMINIMIZED = 2, SW_SHOWMAXIMIZED = 3, WPF_RESTORETOMAXIMIZED = 2;
    private const int MONITOR_DEFAULTTONULL = 0, MONITOR_DEFAULTTOPRIMARY = 1, MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private record struct NativeRect(int Left, int Top, int Right, int Bottom)
    {
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePointXY(int x, int y)
    {
        public int X = x, Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacementData
    {
        public int Length, Flags, ShowCmd;
        public NativePointXY MinPosition, MaxPosition;
        public NativeRect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor, Work;
        public int Flags;
    }

    private delegate bool MonitorEnumProc(nint monitor, nint hdc, nint rect, nint data);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(nint hwnd, ref WindowPlacementData placement);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(nint hwnd, ref WindowPlacementData placement);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromRect(ref NativeRect rect, int flags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePointXY point, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc callback, nint data);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hwnd, int command);
}
