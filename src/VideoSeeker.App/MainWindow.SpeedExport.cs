using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VideoSeeker.App;

/// <summary>
/// Exportar un clip a otra velocidad (sobre todo a cámara lenta, para mandar algo muy rápido que se vea bien): «A otra
/// velocidad…» en el submenú de exportar abre un cuadro con la velocidad (la de reproducción por defecto), la calidad
/// y lo que durará. El cuadro del clip de los dos vídeos lleva la misma fila de velocidad.
/// </summary>
public partial class MainWindow
{
    private static readonly double[] ExportSpeeds = [0.1, 0.25, 0.5, 1, 2, 4];
    private const double MinExportSpeed = 0.1, MaxExportSpeed = 20;

    /// <summary>El último cuadro de exportar a otra velocidad (autoprueba): la ventana, el campo de la velocidad y la duración.</summary>
    internal (Window Window, TextBox Speed, TextBlock Length)? SpeedExportDialog { get; private set; }

    /// <summary>La velocidad con la que se abre el cuadro: la de reproducción si no es 1× (sin signo); si no, la dada.</summary>
    private double InitialExportSpeed(double atNormal)
    {
        var rate = Math.Abs(_nav.Rate);
        return rate != 1 ? Math.Clamp(rate, MinExportSpeed, MaxExportSpeed) : atNormal;
    }

    /// <summary>Si lo que se oye está silenciado (A, sin contar cuando se calla para que se oiga B).</summary>
    private bool ExportMuted => _store.Settings.Mute && !_aMutedForB;

    private static string ExportSpeedText(double speed) => speed.ToString("0.##", Loc.Culture);

    private static double? ParseSpeed(string text)
    {
        text = text.Trim().TrimEnd('×', 'x', 'X').Trim();
        var ok = double.TryParse(text, NumberStyles.Float, Loc.Culture, out var value)
            || double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        return ok && value >= MinExportSpeed - 1e-9 && value <= MaxExportSpeed + 1e-9 ? Math.Round(value, 3) : null;
    }

    /// <summary>
    /// «Velocidad [0,1×] [0,25×] [0,5×] [1×] [2×] [4×] [ 0,5 ]×»: los botones rellenan el campo y el campo admite
    /// cualquiera entre 0,1× y 20×. Devuelve la fila, el campo y la velocidad elegida (null si no vale); changed avisa
    /// de cada cambio.
    /// </summary>
    private (FrameworkElement Row, TextBox Box, Func<double?> Speed) BuildSpeedRow(double initial, Action changed)
    {
        var muted = (Brush)FindResource("FgMuted");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        row.Children.Add(new TextBlock { Text = S.ExportSpeed, Foreground = muted, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var box = new TextBox
        {
            Text = ExportSpeedText(initial), Width = 54, Style = (Style)FindResource("DarkTextBox"), VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Right, Margin = new Thickness(6, 0, 0, 0),
        };
        var buttons = new List<(RadioButton Button, double Speed)>();
        var updating = false;
        foreach (var speed in ExportSpeeds)
        {
            var button = new RadioButton { Content = SpeedLabel(speed), Style = (Style)FindResource("ScopeButton"), GroupName = "speed" + GetHashCode() };
            button.Checked += (_, _) =>
            {
                if (updating)
                    return;
                box.Text = ExportSpeedText(speed);
            };
            buttons.Add((button, speed));
            row.Children.Add(button);
        }
        void Sync()
        {
            var value = ParseSpeed(box.Text);
            updating = true;
            foreach (var (button, speed) in buttons)
                button.IsChecked = value is { } v && Math.Abs(v - speed) < 1e-9;
            updating = false;
            box.BorderBrush = value != null ? (Brush)FindResource("Line") : (Brush)FindResource("Error");
            box.ToolTip = value != null ? null : S.ExportSpeedRange;
            changed();
        }
        box.TextChanged += (_, _) => Sync();
        row.Children.Add(box);
        row.Children.Add(new TextBlock { Text = "×", Foreground = muted, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0) });
        Sync();
        return (row, box, () => ParseSpeed(box.Text));
    }

    private static string LengthText(double seconds) =>
        seconds < 60 ? $"{seconds.ToString("0.0", Loc.Culture)} s" : TimeFormat.Format(seconds, withMilliseconds: false);

    /// <summary>«A otra velocidad…»: el cuadro y, al aceptarlo, a la cola.</summary>
    private void AskSpeedExport(string video, double start, double end, string? name)
    {
        var (window, chosen) = BuildSpeedExportDialog(start, end, name);
        window.ShowDialog();
        SpeedExportDialog = null;
        if (chosen() is { } choice)
            ExportClip(video, start, end, name, choice.Preset, null, choice.Speed);
    }

    /// <summary>
    /// Qué se exporta, la velocidad, la calidad (alta o ligero: hay que recodificar) y lo que durará el clip (y pesará,
    /// el ligero). Devuelve la ventana sin mostrar y lo elegido (null si se cancela).
    /// </summary>
    private (Window Window, Func<(ClipPreset Preset, double Speed)?> Chosen) BuildSpeedExportDialog(double start, double end, string? name)
    {
        var fg = (Brush)FindResource("Fg");
        var muted = (Brush)FindResource("FgMuted");
        var window = new Window
        {
            Title = S.ExportAtSpeedTitle, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            UseLayoutRounding = true, Background = (Brush)FindResource("Bg"), MinWidth = 380,
        };
        (ClipPreset, double)? result = null;
        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 12) };
        body.Children.Add(new TextBlock
        {
            Text = S.ExportWhat(RangeLabel(start, end, name)), Foreground = fg, FontWeight = FontWeights.SemiBold, FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 520, Margin = new Thickness(0, 0, 0, 6),
        });

        var length = new TextBlock { Foreground = muted, FontSize = 12.5, Margin = new Thickness(0, 10, 0, 0) };
        var export = new Button { Content = S.ExportBothStart, MinWidth = 88, IsDefault = true, Style = (Style)FindResource("AccentButton") };
        RadioButton? light = null;
        Func<double?> speed = () => null;
        void Update()
        {
            if (speed() is not { } s)
            {
                length.Text = S.ExportSpeedRange;
                export.IsEnabled = false;
                return;
            }
            export.IsEnabled = true;
            var seconds = (end - start) / s;
            var text = S.ExportSpeedLength(LengthText(seconds));
            if (light?.IsChecked == true)
            {
                var megabytes = seconds * _store.Settings.LightClipMegabytesPerSecond;
                text += $" · ≈ {megabytes.ToString(megabytes < 10 ? "0.#" : "0", Loc.Culture)} {S.MegabytesUnit}";
            }
            length.Text = text;
        }
        var (row, box, value) = BuildSpeedRow(InitialExportSpeed(atNormal: 0.5), Update);
        speed = value;
        body.Children.Add(row);

        var quality = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        quality.Children.Add(new TextBlock { Text = S.ExportBothQuality, Foreground = muted, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var exact = new RadioButton { Content = S.ExportBothExact, ToolTip = S.ExactOriginalTip, Style = (Style)FindResource("ScopeButton"), GroupName = "speedquality" };
        light = new RadioButton
        {
            Content = S.LightClipMenu(_store.Settings.LightClipMegabytesPerSecond.ToString("0.##", Loc.Culture)), ToolTip = S.LightClipMenuTip,
            Style = (Style)FindResource("ScopeButton"), GroupName = "speedquality", IsChecked = true,
        };
        exact.Checked += (_, _) => Update();
        light.Checked += (_, _) => Update();
        quality.Children.Add(exact);
        quality.Children.Add(light);
        body.Children.Add(quality);
        body.Children.Add(length);
        if (ExportMuted)
            body.Children.Add(new TextBlock { Text = S.ExportSpeedNoSound, Foreground = muted, FontSize = 12.5, Margin = new Thickness(0, 2, 0, 0) });
        Update();

        export.Click += (_, _) =>
        {
            if (speed() is not { } s)
                return;
            result = (exact.IsChecked == true ? ClipPreset.Exact : ClipPreset.Light, s);
            window.Close();
        };
        window.Content = DialogWithButtons(body, export, () => window.Close());
        window.SourceInitialized += (_, _) => Dialog.UseDarkTitleBar(window);
        window.Loaded += (_, _) => export.Focus();
        SpeedExportDialog = (window, box, length);
        return (window, () => result);
    }

    /// <summary>El cuerpo con el pie de Exportar / Cancelar, como en el cuadro del clip de los dos vídeos.</summary>
    private FrameworkElement DialogWithButtons(FrameworkElement body, Button export, Action close)
    {
        var cancel = new Button { Content = S.Cancel, MinWidth = 88, IsCancel = true, Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource("DarkButton") };
        cancel.Click += (_, _) => close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(export);
        buttons.Children.Add(cancel);
        var footer = new Border
        {
            Padding = new Thickness(20, 12, 20, 12), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = (Brush)FindResource("Line"),
            Background = (Brush)FindResource("Panel"), Child = buttons,
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        return new DockPanel { Background = (Brush)FindResource("Bg"), Children = { footer, body } };
    }
}
