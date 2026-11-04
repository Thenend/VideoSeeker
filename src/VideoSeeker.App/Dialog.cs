using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace VideoSeeker.App;

/// <summary>
/// Avisos y preguntas con el estilo oscuro de la app, en lugar de MessageBox: botones con nombre (el primero es el
/// principal y responde a Enter; Esc o cerrar la ventana es "ninguno").
/// </summary>
public static class Dialog
{
    /// <summary>Aviso con un solo botón.</summary>
    public static void Inform(Window owner, string message, string title = "VideoSeeker") =>
        Choose(owner, message, title, "Aceptar");

    /// <summary>Pregunta con un botón para hacerlo y otro para no; true si se elige el primero.</summary>
    public static bool Confirm(Window owner, string message, string yes, string no = "Cancelar", string title = "VideoSeeker") =>
        Choose(owner, message, title, yes, no) == 0;

    /// <summary>Varias respuestas: el índice del botón elegido, o -1 si se cierra con Esc o la ×.</summary>
    public static int Choose(Window owner, string message, string title, params string[] buttons)
    {
        var window = Create(owner, message, title, buttons);
        window.ShowDialog();
        return window.Tag is int chosen ? chosen : -1;
    }

    /// <summary>La ventana sin mostrar (las autopruebas la abren sin esperar a la respuesta).</summary>
    internal static Window Create(Window owner, string message, string title, params string[] buttons)
    {
        var window = new Window
        {
            Title = title, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            UseLayoutRounding = true, Background = (Brush)Application.Current.FindResource("Bg"),
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        for (var i = 0; i < buttons.Length; i++)
        {
            var index = i;
            var button = new Button
            {
                Content = buttons[i], MinWidth = 88, Margin = new Thickness(8, 0, 0, 0), IsDefault = i == 0,
                Style = (Style)Application.Current.FindResource(i == 0 ? "AccentButton" : "DarkButton"),
            };
            button.Click += (_, _) => { window.Tag = index; window.Close(); };
            row.Children.Add(button);
        }
        window.Content = new DockPanel
        {
            Background = (Brush)Application.Current.FindResource("Bg"),
            Children =
            {
                new Border
                {
                    Padding = new Thickness(20, 12, 20, 12), BorderThickness = new Thickness(0, 1, 0, 0),
                    BorderBrush = (Brush)Application.Current.FindResource("Line"),
                    Background = (Brush)Application.Current.FindResource("Panel"), Child = row,
                }.Also(b => DockPanel.SetDock(b, Dock.Bottom)),
                new TextBlock
                {
                    Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 440, MinWidth = 280, FontSize = 13.5,
                    Foreground = (Brush)Application.Current.FindResource("Fg"), Margin = new Thickness(22, 20, 22, 22),
                    LineHeight = 20,
                },
            },
        };
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                window.Close();
                e.Handled = true;
            }
        };
        window.SourceInitialized += (_, _) => UseDarkTitleBar(window);
        window.Loaded += (_, _) => (row.Children[0] as Button)?.Focus();
        return window;
    }

    private static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>Zona útil (sin la barra de tareas) de la pantalla en la que está la ventana, en unidades de WPF.</summary>
    public static Rect WorkAreaOf(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */), ref info))
            return SystemParameters.WorkArea;
        var dpi = VisualTreeHelper.GetDpi(window);
        return new Rect(info.Work.Left / dpi.DpiScaleX, info.Work.Top / dpi.DpiScaleY,
            (info.Work.Right - info.Work.Left) / dpi.DpiScaleX, (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY);
    }

    /// <summary>Mueve la ventana lo justo para que quede entera dentro de la zona.</summary>
    public static void KeepInside(Window window, Rect area)
    {
        var (width, height) = (window.ActualWidth, Math.Min(window.ActualHeight, area.Height));
        window.Left = Math.Clamp(window.Left, area.Left, Math.Max(area.Left, area.Right - width));
        window.Top = Math.Clamp(window.Top, area.Top, Math.Max(area.Top, area.Bottom - height));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public MonitorRect Monitor, Work;
        public int Flags;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    /// <summary>
    /// Barra de título oscura, como el resto de la app, aunque Windows esté en modo claro o pinte las barras con el
    /// color de énfasis: modo oscuro (Windows 10 20H1 en adelante) y, en Windows 11, el fondo de la app en la barra y
    /// un borde gris. Los botones y el comportamiento siguen siendo los de Windows.
    /// </summary>
    public static void UseDarkTitleBar(Window window)
    {
        const int UseImmersiveDarkMode = 20, BorderColor = 34, CaptionColor = 35;
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var on = 1;
        DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref on, sizeof(int));
        // COLORREF: 0x00BBGGRR. Fondo #16181C y borde #3A3F47 (Bg y Line de App.xaml).
        var caption = 0x001C1816;
        DwmSetWindowAttribute(hwnd, CaptionColor, ref caption, sizeof(int));
        var border = 0x00473F3A;
        DwmSetWindowAttribute(hwnd, BorderColor, ref border, sizeof(int));
    }
}
