using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using VideoSeeker.App.Controls;
using VideoSeeker.Playback;

namespace VideoSeeker.App;

/// <summary>
/// Exportar un clip a otra velocidad (sobre todo a cámara lenta, para mandar algo muy rápido que se vea bien): «A otra
/// velocidad…» en el submenú de exportar abre un cuadro con una vista previa pequeña (un mpv propio que repite el tramo
/// a la velocidad elegida, con o sin sonido: lo mismo que saldrá), la velocidad en un slider (la de reproducción por
/// defecto), con o sin sonido, la calidad y lo que durará. El cuadro del clip de los dos vídeos lleva las mismas filas
/// de velocidad y sonido.
/// </summary>
public partial class MainWindow
{
    private const double MinExportSpeed = 0.1, MaxExportSpeed = 20;
    // El slider va a saltos por estas velocidades, espaciadas en escala logarítmica (1× cerca del centro); el campo
    // admite cualquiera hasta 20×.
    private static readonly double[] SliderSpeeds = [0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6, 0.75, 1, 1.25, 1.5, 2, 3, 4];
    private const double SliderMinSpeed = 0.1, SliderMaxSpeed = 4;

    /// <summary>
    /// El último cuadro de exportar a otra velocidad (autoprueba): la ventana, el campo y el slider de la velocidad, la
    /// duración, el reproductor de la vista previa (null en el de los dos vídeos o hasta que arranca), su play y su
    /// barra (ir a una fracción del tramo).
    /// </summary>
    internal (Window Window, TextBox Speed, Slider Slider, TextBlock Length, Func<MpvPlayer?> Preview, Action Play, Action<double> Seek)? SpeedExportDialog { get; private set; }

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

    // Del slider (logaritmo de la velocidad, siempre en una de las marcas) a la velocidad de la marca más cercana.
    private static double SliderSpeed(double position) =>
        SliderSpeeds.MinBy(s => Math.Abs(Math.Log(s) - position));

    /// <summary>Una fila de las del cuadro: el nombre a la izquierda (todos alineados) y lo que se elige.</summary>
    private void AddDialogRow(Grid grid, string label, UIElement content, double top = 10)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var row = grid.RowDefinitions.Count - 1;
        var text = new TextBlock
        {
            Text = label, Foreground = (Brush)FindResource("FgMuted"), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, top, 14, 0),
        };
        Grid.SetRow(text, row);
        grid.Children.Add(text);
        if (content is FrameworkElement element)
            element.Margin = new Thickness(0, top, 0, 0);
        Grid.SetRow(content, row);
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
    }

    private static Grid DialogRows() => new()
    {
        ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
    };

    /// <summary>
    /// La velocidad: un slider a saltos (de 0,1× a 4×, con una marca en cada velocidad posible) y, al lado, el valor,
    /// que también se puede escribir (de 0,1× a 20×). Devuelve lo que va en la fila, el campo, el slider y la velocidad elegida (null si no vale); changed avisa
    /// de cada cambio.
    /// </summary>
    private (FrameworkElement Content, TextBox Box, Slider Slider, Func<double?> Speed) BuildSpeedControl(double initial, Action changed)
    {
        var panel = new DockPanel();
        var box = new TextBox
        {
            Text = ExportSpeedText(initial), Width = 52, Style = (Style)FindResource("DarkTextBox"), VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 0, 0),
        };
        var times = new TextBlock { Text = "×", Foreground = (Brush)FindResource("FgMuted"), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0) };
        DockPanel.SetDock(times, Dock.Right);
        DockPanel.SetDock(box, Dock.Right);
        var slider = new Slider
        {
            Minimum = Math.Log(SliderMinSpeed), Maximum = Math.Log(SliderMaxSpeed), Style = (Style)FindResource("DarkSlider"),
            MinWidth = 220, VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true, IsSnapToTickEnabled = true,
            Ticks = new DoubleCollection(SliderSpeeds.Select(s => Math.Log(s))),
            Value = Math.Log(SliderSpeed(Math.Log(Math.Clamp(initial, SliderMinSpeed, SliderMaxSpeed)))),
        };
        // Las marcas, debajo de la pista: una raya por velocidad, más larga en 1×. (El pulgar mide 14: la pista empieza
        // y acaba a 7 del borde.)
        var marks = new Canvas { Height = 5, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false, Margin = new Thickness(0, 0, 0, -6) };
        void DrawMarks()
        {
            marks.Children.Clear();
            var width = slider.ActualWidth - 14;
            if (width <= 0)
                return;
            foreach (var speed in SliderSpeeds)
            {
                var x = 7 + (Math.Log(speed) - slider.Minimum) / (slider.Maximum - slider.Minimum) * width;
                var mark = new Rectangle { Width = 1, Height = speed == 1 ? 5 : 3, Fill = (Brush)FindResource("FgMuted"), Opacity = speed == 1 ? 1 : 0.5 };
                Canvas.SetLeft(mark, Math.Round(x));
                marks.Children.Add(mark);
            }
        }
        slider.SizeChanged += (_, _) => DrawMarks();
        var sliderHost = new Grid { Children = { slider, marks } };
        var updating = false;
        slider.ValueChanged += (_, _) =>
        {
            if (updating)
                return;
            updating = true;
            box.Text = ExportSpeedText(SliderSpeed(slider.Value));
            updating = false;
            changed();
        };
        box.TextChanged += (_, _) =>
        {
            var value = ParseSpeed(box.Text);
            box.BorderBrush = value != null ? (Brush)FindResource("Line") : (Brush)FindResource("Error");
            box.ToolTip = value != null ? null : S.ExportSpeedRange;
            if (!updating && value is { } v)
            {
                updating = true;
                // A la marca más cercana; el campo se queda con lo escrito.
                slider.Value = Math.Log(SliderSpeed(Math.Log(Math.Clamp(v, SliderMinSpeed, SliderMaxSpeed))));
                updating = false;
            }
            changed();
        };
        panel.Children.Add(times);
        panel.Children.Add(box);
        panel.Children.Add(sliderHost);
        return (panel, box, slider, () => ParseSpeed(box.Text));
    }

    /// <summary>«Con sonido» / «Sin sonido»: lo que va en la fila y si lleva sonido.</summary>
    private (FrameworkElement Content, Func<bool> WithSound) BuildSoundControl(bool initial, Action changed)
    {
        var group = "sound" + Guid.NewGuid().ToString("N");
        var with = new RadioButton { Content = S.ExportWithSound, Style = (Style)FindResource("ScopeButton"), GroupName = group, IsChecked = initial };
        var without = new RadioButton { Content = S.ExportWithoutSound, Style = (Style)FindResource("ScopeButton"), GroupName = group, IsChecked = !initial };
        with.Checked += (_, _) => changed();
        without.Checked += (_, _) => changed();
        return (new StackPanel { Orientation = Orientation.Horizontal, Children = { with, without } }, () => with.IsChecked == true);
    }

    private static string LengthText(double seconds) =>
        seconds < 60 ? $"{seconds.ToString("0.0", Loc.Culture)} s" : TimeFormat.Format(seconds, withMilliseconds: false);

    /// <summary>«A otra velocidad…»: el cuadro y, al aceptarlo, a la cola.</summary>
    private void AskSpeedExport(string video, double start, double end, string? name)
    {
        var (window, chosen) = BuildSpeedExportDialog(video, start, end, name);
        window.ShowDialog();
        SpeedExportDialog = null;
        if (chosen() is { } choice)
            ExportClip(video, start, end, name, choice.Preset, null, choice.Speed, !choice.WithSound);
    }

    /// <summary>
    /// La vista previa, la velocidad, el sonido, la calidad (alta o ligero: hay que recodificar) y lo que durará el clip
    /// (y pesará, el ligero). Devuelve la ventana sin mostrar y lo elegido (null si se cancela).
    /// </summary>
    private (Window Window, Func<(ClipPreset Preset, double Speed, bool WithSound)?> Chosen) BuildSpeedExportDialog(string video, double start, double end, string? name)
    {
        var fg = (Brush)FindResource("Fg");
        var muted = (Brush)FindResource("FgMuted");
        var window = new Window
        {
            Title = S.ExportAtSpeedTitle, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            UseLayoutRounding = true, Background = (Brush)FindResource("Bg"),
        };
        (ClipPreset, double, bool)? result = null;
        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 12), Width = 448 };
        body.Children.Add(new TextBlock
        {
            Text = S.ExportWhat(RangeLabel(start, end, name)), Foreground = fg, FontWeight = FontWeights.SemiBold, FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 10),
        });

        // La vista previa: el hueco (mpv pinta encima en su ventana nativa), una línea con por dónde va y el play.
        var screen = new Border { Width = 448, Height = 252, Background = Brushes.Black };
        MpvPlayer? preview = null;
        // La barra del tramo: por dónde va y, con clic o arrastrando, a otro momento (la zona de clic es más alta que la raya).
        var progress = new Rectangle { Height = 3, Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Fill = (Brush)FindResource("Accent") };
        var line = new Border { Height = 3, Background = (Brush)FindResource("Line"), Child = progress, VerticalAlignment = VerticalAlignment.Top };
        var track = new Grid { Height = 12, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, Children = { line } };
        void Seek(double fraction)
        {
            fraction = Math.Clamp(fraction, 0, 1);
            progress.Width = track.ActualWidth * fraction;
            // Sin llegar al final exacto: ahí el bucle volvería al principio.
            preview?.ScrubTo(start + fraction * Math.Max(0, end - start - 1e-3));
        }
        track.MouseLeftButtonDown += (_, e) =>
        {
            track.CaptureMouse();
            Seek(e.GetPosition(track).X / Math.Max(1, track.ActualWidth));
            e.Handled = true;
        };
        track.MouseMove += (_, e) =>
        {
            if (track.IsMouseCaptured)
                Seek(e.GetPosition(track).X / Math.Max(1, track.ActualWidth));
        };
        track.MouseLeftButtonUp += (_, _) => track.ReleaseMouseCapture();
        var play = new Button
        {
            Style = (Style)FindResource("ToolButton"), FontFamily = (FontFamily)FindResource("Icons"), FontSize = 16, Content = GlyphPlay,
            ToolTip = S.ExportPreviewTip, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -6, 0, 0),
        };
        body.Children.Add(screen);
        body.Children.Add(track);
        body.Children.Add(play);

        var length = new TextBlock { Foreground = muted, FontSize = 12.5, Margin = new Thickness(0, 12, 0, 0) };
        var export = new Button { Content = S.ExportBothStart, MinWidth = 88, IsDefault = true, Style = (Style)FindResource("AccentButton") };
        RadioButton? light = null;
        Func<double?> speed = () => null;
        Func<bool> withSound = () => true;
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
            preview?.SetSpeed(s);
            preview?.SetMute(!withSound());
        }

        var rows = DialogRows();
        var (speedControl, box, slider, speedValue) = BuildSpeedControl(InitialExportSpeed(atNormal: 0.5), Update);
        speed = speedValue;
        AddDialogRow(rows, S.ExportSpeed, speedControl, top: 6);
        var (soundControl, soundValue) = BuildSoundControl(!ExportMuted, Update);
        withSound = soundValue;
        AddDialogRow(rows, S.ExportSound, soundControl);
        var exact = new RadioButton { Content = S.ExportBothExact, ToolTip = S.ExactOriginalTip, Style = (Style)FindResource("ScopeButton"), GroupName = "speedquality" };
        light = new RadioButton
        {
            Content = S.LightClipMenu(_store.Settings.LightClipMegabytesPerSecond.ToString("0.##", Loc.Culture)), ToolTip = S.LightClipMenuTip,
            Style = (Style)FindResource("ScopeButton"), GroupName = "speedquality", IsChecked = true,
        };
        exact.Checked += (_, _) => Update();
        light.Checked += (_, _) => Update();
        AddDialogRow(rows, S.ExportBothQuality, new StackPanel { Orientation = Orientation.Horizontal, Children = { exact, light } });
        body.Children.Add(rows);
        body.Children.Add(length);
        Update();

        // El reproductor de la vista previa: el tramo en bucle, a la velocidad y con el sonido elegidos. Al ponerlo en
        // marcha se para el vídeo principal (y vuelve a reproducirse al cerrar si iba).
        var surface = new VideoSurface(window, screen);
        var resumeMain = false;
        var closed = false;
        var previewPaused = true;
        void TogglePreview()
        {
            if (preview == null)
                return;
            var paused = previewPaused;
            // Playing cuenta también el bucle del tramo, que pinta el motor con mpv en pausa.
            if (paused && _nav.Playing)
            {
                resumeMain = !_nav.Reversing;
                _nav.Pause();
            }
            preview.SetPause(!paused);
        }
        play.Click += (_, _) => TogglePreview();
        surface.Clicked += TogglePreview;
        if (File.Exists(video))
        {
            var settings = _store.Settings;
            var player = new MpvPlayer { SubtitlesOnOpen = false };
            var ready = Task.Run(() => player.Initialize(settings.Volume, mute: true, settings.HardwareDecoding, Path.Combine(_store.Folder, "cache", "mpv"), secondary: true));
            window.Loaded += async (_, _) =>
            {
                try
                {
                    await ready;
                }
                catch (Exception)
                {
                    return;
                }
                if (closed)
                    return;
                surface.Create();
                player.SetWindow(surface.Handle);
                player.PauseChanged += p => Dispatcher.BeginInvoke(() =>
                {
                    previewPaused = p;
                    play.Content = p ? GlyphPlay : GlyphPause;
                });
                player.PositionChanged += t => Dispatcher.BeginInvoke(() =>
                {
                    if (!track.IsMouseCaptured)
                        progress.Width = track.ActualWidth * Math.Clamp((t - start) / Math.Max(1e-3, end - start), 0, 1);
                });
                player.Start();
                player.Load(video, start, play: false);
                player.SetLoop((start, end));
                preview = player;
                Update();
            };
            window.Closed += (_, _) =>
            {
                closed = true;
                preview = null;
                surface.Dispose();
                // mpv tarda un poco en cerrarse: en segundo plano, sin esperar.
                _ = Task.Run(async () =>
                {
                    try { await ready; } catch (Exception) { }
                    player.Dispose();
                });
                if (resumeMain && !_nav.Playing)
                    _nav.TogglePlay();
            };
        }

        export.Click += (_, _) =>
        {
            if (speed() is not { } s)
                return;
            result = (exact.IsChecked == true ? ClipPreset.Exact : ClipPreset.Light, s, withSound());
            window.Close();
        };
        window.Content = DialogWithButtons(body, export, () => window.Close());
        window.SourceInitialized += (_, _) => Dialog.UseDarkTitleBar(window);
        window.Loaded += (_, _) => export.Focus();
        SpeedExportDialog = (window, box, slider, length, () => preview, TogglePreview, Seek);
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
