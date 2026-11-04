using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VideoSeeker.App;

/// <summary>
/// "Otro color…": ventanita oscura para elegir un color propio (cuadro de tono claro/oscuro, tira de tonos y el código
/// #RRGGBB). Si se parece a los colores de las barras (ámbar del tramo, azul de lo reproducido, verde de la caché) avisa,
/// pero deja usarlo.
/// </summary>
public static class ColorPicker
{
    private static (string Name, Color Color)[] Reserved =>
    [
        (S.ReservedRangeAmber, Color.FromRgb(0xF0, 0xB4, 0x29)),
        (S.ReservedPlayedBlue, Color.FromRgb(0x4C, 0x9B, 0xFF)),
        (S.ReservedCacheGreen, Color.FromRgb(0x3F, 0xB9, 0x50)),
    ];

    /// <summary>El color elegido (#RRGGBB), o null si se cancela.</summary>
    public static string? Pick(Window owner, string initial)
    {
        string? result = null;
        Create(owner, initial, hex => result = hex).ShowDialog();
        return result;
    }

    /// <summary>La ventana sin mostrar (las autopruebas la abren sin esperar); "chosen" recibe el color al aceptarlo.</summary>
    internal static Window Create(Window owner, string initial, Action<string> chosen)
    {
        var res = Application.Current;
        Brush R(string key) => (Brush)res.FindResource(key);
        var start = Parse(initial) ?? Colors.Red;
        var (h, s, v) = ToHsv(start);

        const double Width = 260, Height = 170;
        var hueFill = new SolidColorBrush();
        var square = new Grid { Width = Width, Height = Height, Cursor = Cursors.Cross, ClipToBounds = true };
        square.Children.Add(new Rectangle { Fill = hueFill });
        square.Children.Add(new Rectangle { Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) });
        square.Children.Add(new Rectangle { Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) });
        var marker = new Ellipse { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
        var markerCanvas = new Canvas { IsHitTestVisible = false };
        markerCanvas.Children.Add(marker);
        square.Children.Add(markerCanvas);

        var hues = new GradientStopCollection();
        for (var i = 0; i <= 6; i++)
            hues.Add(new GradientStop(FromHsv(i * 60 % 360, 1, 1), i / 6.0));
        var hueStrip = new Grid { Width = Width, Height = 16, Cursor = Cursors.Hand, Margin = new Thickness(0, 10, 0, 0) };
        hueStrip.Children.Add(new Rectangle { Fill = new LinearGradientBrush(hues, 0), RadiusX = 3, RadiusY = 3 });
        var hueMarker = new Rectangle { Width = 4, Height = 20, Stroke = Brushes.White, StrokeThickness = 1.5, Fill = Brushes.Transparent, IsHitTestVisible = false };
        var hueCanvas = new Canvas { IsHitTestVisible = false };
        hueCanvas.Children.Add(hueMarker);
        hueStrip.Children.Add(hueCanvas);

        var preview = new Border { Width = 34, Height = 26, CornerRadius = new CornerRadius(4), BorderBrush = R("Line"), BorderThickness = new Thickness(1) };
        var hexBox = new TextBox { Width = 100, Margin = new Thickness(10, 0, 0, 0), Style = (Style)res.FindResource("DarkTextBox"), VerticalAlignment = VerticalAlignment.Center };
        var hint = new TextBlock { Foreground = R("FgMuted"), FontSize = 11.5, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        row.Children.Add(preview);
        row.Children.Add(hexBox);
        row.Children.Add(hint);

        var updating = false;
        void Refresh(bool fromText = false)
        {
            updating = true;
            var color = FromHsv(h, s, v);
            hueFill.Color = FromHsv(h, 1, 1);
            Canvas.SetLeft(marker, s * Width - 6);
            Canvas.SetTop(marker, (1 - v) * Height - 6);
            marker.Stroke = v > 0.6 && s < 0.4 ? Brushes.Black : Brushes.White;
            Canvas.SetLeft(hueMarker, h / 360 * Width - 2);
            Canvas.SetTop(hueMarker, -2);
            preview.Background = new SolidColorBrush(color);
            if (!fromText)
            {
                hexBox.Text = Hex(color);
                hint.Text = "";
            }
            updating = false;
        }

        void PickSquare(Point p)
        {
            s = Math.Clamp(p.X / Width, 0, 1);
            v = Math.Clamp(1 - p.Y / Height, 0, 1);
            Refresh();
        }
        void PickHue(Point p)
        {
            h = Math.Clamp(p.X / Width, 0, 1) * 359.9;
            Refresh();
        }
        square.MouseLeftButtonDown += (_, e) => { square.CaptureMouse(); PickSquare(e.GetPosition(square)); };
        square.MouseMove += (_, e) => { if (square.IsMouseCaptured) PickSquare(e.GetPosition(square)); };
        square.MouseLeftButtonUp += (_, _) => square.ReleaseMouseCapture();
        hueStrip.MouseLeftButtonDown += (_, e) => { hueStrip.CaptureMouse(); PickHue(e.GetPosition(hueStrip)); };
        hueStrip.MouseMove += (_, e) => { if (hueStrip.IsMouseCaptured) PickHue(e.GetPosition(hueStrip)); };
        hueStrip.MouseLeftButtonUp += (_, _) => hueStrip.ReleaseMouseCapture();
        hexBox.TextChanged += (_, _) =>
        {
            if (updating)
                return;
            if (Parse(hexBox.Text) is { } typed)
            {
                (h, s, v) = ToHsv(typed);
                hint.Text = "";
                Refresh(fromText: true);
            }
            else
            {
                hint.Text = "#RRGGBB";
            }
        };

        var window = new Window
        {
            Title = S.OtherColorTitle, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            UseLayoutRounding = true, Background = R("Bg"),
        };
        var ok = new Button { Content = S.UseThisColor, MinWidth = 88, IsDefault = true, Style = (Style)res.FindResource("AccentButton") };
        var cancel = new Button { Content = S.Cancel, MinWidth = 88, Margin = new Thickness(8, 0, 0, 0), IsCancel = true, Style = (Style)res.FindResource("DarkButton") };
        ok.Click += (_, _) =>
        {
            var color = Parse(hexBox.Text) ?? FromHsv(h, s, v);
            if (Clash(color) is { } clash && !Dialog.Confirm(window,
                    S.ColorClash(clash), S.UseIt))
                return;
            chosen(Hex(color));
            window.Close();
        };
        cancel.Click += (_, _) => window.Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var body = new StackPanel { Margin = new Thickness(20, 18, 20, 18) };
        body.Children.Add(new Border { Child = square, CornerRadius = new CornerRadius(3), ClipToBounds = true });
        body.Children.Add(hueStrip);
        body.Children.Add(row);
        var footer = new Border
        {
            Padding = new Thickness(20, 12, 20, 12), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = R("Line"),
            Background = R("Panel"), Child = buttons,
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        window.Content = new DockPanel { Background = R("Bg"), Children = { footer, body } };
        window.SourceInitialized += (_, _) => Dialog.UseDarkTitleBar(window);
        window.Loaded += (_, _) => { hexBox.Focus(); hexBox.SelectAll(); };
        Refresh();
        return window;
    }

    /// <summary>Nombre del color de las barras al que se parece demasiado, o null.</summary>
    public static string? Clash(Color c)
    {
        foreach (var (name, reserved) in Reserved)
        {
            // Distancia "redmean": se acerca bastante a cómo vemos la diferencia sin pasar a Lab.
            var r = (c.R + reserved.R) / 2.0;
            double dr = c.R - reserved.R, dg = c.G - reserved.G, db = c.B - reserved.B;
            var d = Math.Sqrt((2 + r / 256) * dr * dr + 4 * dg * dg + (2 + (255 - r) / 256) * db * db);
            if (d < 90)
                return name;
        }
        return null;
    }

    public static Color? Parse(string text)
    {
        var t = text.Trim().TrimStart('#');
        if (t.Length == 3)
            t = string.Concat(t.Select(ch => $"{ch}{ch}"));
        return t.Length == 6 && uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var n)
            ? Color.FromRgb((byte)(n >> 16), (byte)(n >> 8), (byte)n)
            : null;
    }

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = (h % 360) switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    private static (double H, double S, double V) ToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;
        var h = d == 0 ? 0 : max == r ? 60 * ((g - b) / d % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        return (h < 0 ? h + 360 : h, max == 0 ? 0 : d / max, max);
    }
}
