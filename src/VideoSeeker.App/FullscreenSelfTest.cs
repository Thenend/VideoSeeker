using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace VideoSeeker.App;

/// <summary>
/// Prueba automática de la pantalla completa (VIDEOSEEKER_SELFTEST=pantalla): al entrar y salir, desde la ventana
/// maximizada y desde una normal, la ventana no pasa por ningún tamaño intermedio (se veía encogerse a su tamaño sin
/// maximizar, con las barras a media pantalla y lo de detrás asomando, y luego crecer), acaba cubriendo el monitor
/// entero y al salir vuelve exactamente a como estaba. Apunta cada tamaño por el que pasa la ventana (los que le da
/// Windows) y la imagen en cada frame. Escribe selftest.log y cierra.
/// </summary>
public partial class MainWindow
{
    public static bool FullscreenSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "pantalla";

    private async Task RunFullscreenSelfTest()
    {
        var lines = new List<string>();
        var failures = 0;
        void Log(string text) => lines.Add($"{DateTime.Now:HH:mm:ss.fff} {text}");
        void Check(bool ok, string what)
        {
            Log((ok ? "OK    " : "FALLO ") + what);
            if (!ok)
                failures++;
        }
        try
        {
            while (_duration <= 0)
                await Task.Delay(100);
            await Task.Delay(1000);
            var hwnd = new WindowInteropHelper(this).Handle;
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            GetMonitorInfoW(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info);
            var (monitor, work) = (ToCaptureRect(info.Monitor), ToCaptureRect(info.Work));
            Log($"monitor {Show(monitor)}, área de trabajo {Show(work)}");

            // Con el panel abierto, como lo suele tener Mario.
            if (!PanelOpen)
                ShowPanel(true);
            PanelReveal.Finish();

            foreach (var start in new[] { WindowState.Maximized, WindowState.Normal })
            {
                WindowState = start;
                await Task.Delay(800);
                GetCaptureWindowRect(hwnd, out var before);
                var surfaceBefore = _surface.Placement;
                var label = start == WindowState.Maximized ? "maximizada" : "normal";
                Log($"desde la ventana {label}: {Show(before)}, imagen {surfaceBefore.W}×{surfaceBefore.H}");

                // Entrar: nunca más pequeña que como estaba, y al final, el monitor entero.
                var enter = await RecordSizes(hwnd, ToggleFullscreen);
                Log($"      al entrar pasa por: {string.Join(" → ", enter.Rects.Select(Show))}");
                Log($"      imagen en cada frame: {string.Join(" ", enter.Surface.Distinct().Select(s => $"{s.W}×{s.H}"))}");
                GetCaptureWindowRect(hwnd, out var full);
                Check(Same(full, monitor), $"desde {label}, la pantalla completa cubre el monitor ({Show(full)})");
                Check(enter.Rects.All(r => Covers(r, Clip(before, monitor))),
                    $"desde {label}, al entrar no encoge por el camino");
                Check(enter.Surface.All(s => s.W >= surfaceBefore.W && s.H >= surfaceBefore.H),
                    $"desde {label}, la imagen no encoge por el camino");

                // Salir: vuelve tal cual estaba, y desde maximizada sin pasar por la ventana normal.
                var exit = await RecordSizes(hwnd, ToggleFullscreen);
                Log($"      al salir pasa por: {string.Join(" → ", exit.Rects.Select(Show))}");
                GetCaptureWindowRect(hwnd, out var after);
                Check(Same(after, before) && WindowState == start && WindowStyle == WindowStyle.SingleBorderWindow,
                    $"desde {label}, al salir vuelve como estaba ({Show(after)}, {WindowState})");
                if (start == WindowState.Maximized)
                    Check(exit.Rects.All(r => Covers(r, Clip(before, monitor))),
                        "desde maximizada, al salir no pasa por la ventana normal");
                else
                    Check(exit.Rects.All(r => Same(r, full) || Same(r, before)),
                        "desde normal, al salir va directa a su tamaño (sin pasar por maximizada)");
                Check(PanelOpen && !PanelReveal.Animating, $"desde {label}, el panel sigue abierto y quieto");
            }
            WindowState = WindowState.Maximized;
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            Log(failures == 0 ? "TODO OK" : $"{failures} FALLOS");
            File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
            Close();
        }

        static string Show(CaptureRect r) => $"{r.Left},{r.Top} {r.Width}×{r.Height}";
        // Lo que se ve de la ventana (maximizada sobresale 8 px por cada lado, fuera del monitor) y si r lo cubre.
        static CaptureRect Clip(CaptureRect r, CaptureRect m) => new()
            { Left = Math.Max(r.Left, m.Left), Top = Math.Max(r.Top, m.Top), Right = Math.Min(r.Right, m.Right), Bottom = Math.Min(r.Bottom, m.Bottom) };
        static bool Covers(CaptureRect r, CaptureRect area) => r.Left <= area.Left && r.Top <= area.Top && r.Right >= area.Right && r.Bottom >= area.Bottom;
        static bool Same(CaptureRect a, CaptureRect b) => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    }

    private static CaptureRect ToCaptureRect(NativeRect r) => new() { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom };

    // Los tamaños que Windows da a la ventana (WM_WINDOWPOSCHANGED) y la imagen en cada frame, mientras se hace algo.
    private async Task<(List<CaptureRect> Rects, List<(int W, int H)> Surface)> RecordSizes(nint hwnd, Action action)
    {
        var rects = new List<CaptureRect>();
        var surface = new List<(int W, int H)>();
        nint Hook(nint h, int msg, nint w, nint l, ref bool handled)
        {
            const int WM_WINDOWPOSCHANGED = 0x0047, SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002;
            if (msg == WM_WINDOWPOSCHANGED)
            {
                var pos = Marshal.PtrToStructure<WindowPos>(l);
                if ((pos.Flags & (SWP_NOSIZE | SWP_NOMOVE)) != (SWP_NOSIZE | SWP_NOMOVE))
                {
                    GetCaptureWindowRect(h, out var r);
                    rects.Add(r);
                }
            }
            return 0;
        }
        // También el rectángulo en cada frame (al salir, Windows no siempre avisa del cambio de tamaño).
        void OnFrame(object? sender, EventArgs e)
        {
            surface.Add((_surface.Placement.W, _surface.Placement.H));
            GetCaptureWindowRect(hwnd, out var r);
            if (rects.Count == 0 || !rects[^1].Equals(r))
                rects.Add(r);
        }
        var source = HwndSource.FromHwnd(hwnd);
        source.AddHook(Hook);
        CompositionTarget.Rendering += OnFrame;
        try
        {
            action();
            await Task.Delay(800);
        }
        finally
        {
            CompositionTarget.Rendering -= OnFrame;
            source.RemoveHook(Hook);
        }
        return (rects, surface);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public nint Hwnd, InsertAfter;
        public int X, Y, Cx, Cy;
        public int Flags;
    }
}
