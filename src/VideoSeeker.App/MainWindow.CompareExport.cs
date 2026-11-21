using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoSeeker.Inspect;

namespace VideoSeeker.App;

/// <summary>
/// Exportar un clip de los dos vídeos sincronizados en un solo MP4 (<see cref="CompareClip"/>): el tramo de A (zona,
/// región o bookmark) y el mismo de B con el desfase, en su sitio. Antes de empezar, un cuadro enseña qué sale de cada
/// uno (primer y último frame, con miniaturas), lo que saldrá en negro y la calidad. Con la comparación sincronizada y
/// una zona puesta, la barra fina de B marca su tramo equivalente.
/// </summary>
public partial class MainWindow
{
    /// <summary>Lo que saldría de cada vídeo: frames de A, frames de B (null si no tiene imagen) y lo que va en negro.</summary>
    internal sealed record CompareClipPlan(double Start, double End, double BStart, int AFirst, int ALast, int? BFirst, int? BLast,
        (double From, double To)? BlackBefore, (double From, double To)? BlackAfter);

    /// <summary>El último cuadro abierto (autoprueba).</summary>
    internal (Window Window, CompareClipPlan Plan)? CompareExportDialog { get; private set; }

    /// <summary>
    /// Junto a cada «Exportar clip» de este vídeo, comparando: «Exportar clip de los dos vídeos». Sin sincronizar, apagado
    /// con lo que hay que hacer.
    /// </summary>
    private void AddCompareExportMenu(ItemsControl menu, string? video, double start, double end, string? name, bool zone)
    {
        if (_compare is not { } pane || video == null || video != _currentPath)
            return;
        var item = WithIcon(new MenuItem { Header = S.ExportBothClip, InputGestureText = zone ? $"{S.KeyCtrl}+{S.KeyShift}+M" : "" }, "ClipMenuIcon");
        if (_sync is not { Locked: true } || !pane.Ready || end <= start)
        {
            item.IsEnabled = false;
            item.ToolTip = S.ExportBothNeedsSync;
            ToolTipService.SetShowOnDisabled(item, true);
        }
        else
        {
            item.Click += (_, _) => AskCompareExport(start, end, name);
        }
        menu.Items.Add(item);
    }

    /// <summary>Ctrl + Shift + M: el clip de los dos con la zona ampliada.</summary>
    private void ShowCompareExportForZone()
    {
        if (ExportTarget() is not { } target)
        {
            ShowToast(S.ExportNeedsRange);
            return;
        }
        if (_sync is not { Locked: true } || _compare is not { Ready: true })
        {
            ShowToast(S.ExportBothNeedsSync);
            return;
        }
        AskCompareExport(target.Start, target.End, target.Name);
    }

    internal CompareClipPlan PlanCompareClip(double start, double end)
    {
        var pane = _compare!;
        var offset = _sync!.CurrentOffset;
        var gridA = Zoom.Grid;
        var gridB = pane.Grid;
        var frameB = 1 / pane.Fps;
        var (b0, b1) = (start + offset, end + offset);
        var bEnd = pane.LastFrameStart + frameB;
        var (from, to) = (Math.Max(b0, pane.FirstFrameStart), Math.Min(b1, bEnd));
        int? bFirst = null, bLast = null;
        if (to - from > frameB / 2)
            (bFirst, bLast) = (gridB.FrameAt(from + 1e-6), gridB.FrameAt(to - 1e-4));
        (double, double)? before = b0 < pane.FirstFrameStart - frameB / 2 ? (start, Math.Min(end, pane.FirstFrameStart - offset)) : null;
        (double, double)? after = b1 > bEnd + frameB / 2 ? (Math.Max(start, bEnd - offset), end) : null;
        return new CompareClipPlan(start, end, b0, gridA.FrameAt(start + 1e-6), gridA.FrameAt(end - 1e-4), bFirst, bLast, before, after);
    }

    /// <summary>El cuadro de antes de exportar; al aceptarlo, a la cola.</summary>
    private void AskCompareExport(double start, double end, string? name)
    {
        var (window, chosen) = BuildCompareExportDialog(start, end);
        window.ShowDialog();
        CompareExportDialog = null;
        if (chosen() is { } choice)
            ExportCompareClip(start, end, name, choice.Preset, choice.Speed);
    }

    internal ClipJob? ExportCompareClip(double start, double end, string? name, ClipPreset preset, double speed = 1)
    {
        if (_compare is not { } pane || _sync == null || _currentPath == null)
            return null;
        var settings = _store.Settings;
        var job = new ClipJob
        {
            Source = _currentPath, Start = start, End = end, Name = name, Preset = preset,
            AudioStream = _tracks.FirstOrDefault(t => t is { Type: "audio", Selected: true })?.FfIndex,
            Folder = settings.ClipFolder, LightMegabytesPerSecond = settings.LightClipMegabytesPerSecond, HardwareDecoding = settings.HardwareDecoding,
            // A otra velocidad, como se oye: sin sonido si los dos están silenciados.
            Speed = speed, NoAudio = speed != 1 && pane.Muted && ExportMuted,
            Compare = new CompareClip
            {
                Source = pane.Path!, Start = start + _sync.CurrentOffset, End = pane.LastFrameStart + 1 / pane.Fps,
                Stacked = _compareStacked, First = _compareBFirst, AudioFromB = !pane.Muted,
            },
        };
        var queued = _exporter.Busy;
        _exporter.Enqueue(job);
        ShowToast(queued ? S.ClipQueued : S.ExportingClip);
        return job;
    }

    /// <summary>
    /// Una fila por vídeo («A · nombre», miniaturas del primer y del último frame, «frame N (tiempo) → frame M (tiempo)» y
    /// la duración), lo que saldrá en negro, la calidad y Exportar / Cancelar. Devuelve la ventana sin mostrar y la calidad
    /// elegida (null si se cancela).
    /// </summary>
    private (Window Window, Func<(ClipPreset Preset, double Speed)?> Chosen) BuildCompareExportDialog(double start, double end)
    {
        var plan = PlanCompareClip(start, end);
        var pane = _compare!;
        var fg = (Brush)FindResource("Fg");
        var muted = (Brush)FindResource("FgMuted");
        var line = (Brush)FindResource("Line");
        var window = new Window
        {
            Title = S.ExportBothClip, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            UseLayoutRounding = true, Background = (Brush)FindResource("Bg"),
        };
        (ClipPreset, double)? result = null;
        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 12) };

        string Frame(FrameGrid grid, int frame, double fps) => $"{S.FrameNumber(frame)} ({TimeFormat.Precise(grid.TimeOf(frame), fps)})";
        string Length(int frames, double seconds) =>
            $"{S.Frames(frames)} · {seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)} s";

        void Row(string label, string? path, FrameGrid grid, double fps, int? first, int? last)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Image Thumb(int column)
            {
                var image = new Image { Width = 160, Height = 90, Stretch = Stretch.Uniform };
                var frame = new Border { Background = Brushes.Black, BorderBrush = line, BorderThickness = new Thickness(1), Child = image, Margin = new Thickness(0, 0, 6, 0) };
                Grid.SetColumn(frame, column);
                row.Children.Add(frame);
                return image;
            }
            var (left, right) = (Thumb(0), Thumb(1));
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), MaxWidth = 380 };
            text.Children.Add(new TextBlock { Text = label, Foreground = fg, FontWeight = FontWeights.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
            if (first is { } f && last is { } l)
            {
                text.Children.Add(new TextBlock { Text = $"{Frame(grid, f, fps)} → {Frame(grid, l, fps)}", Foreground = fg, FontFamily = new FontFamily("Consolas"), FontSize = 12.5, Margin = new Thickness(0, 6, 0, 0) });
                var seconds = grid.TimeOf(l + 1) - grid.TimeOf(f);
                text.Children.Add(new TextBlock { Text = Length(l - f + 1, seconds), Foreground = muted, FontFamily = new FontFamily("Consolas"), FontSize = 12.5, Margin = new Thickness(0, 2, 0, 0) });
                if (path != null)
                    LoadThumbs(path, grid.TimeOf(f), grid.TimeOf(l), left, right);
            }
            else
            {
                text.Children.Add(new TextBlock { Text = S.ExportBothNoImage, Foreground = muted, FontSize = 12.5, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
            }
            Grid.SetColumn(text, 2);
            row.Children.Add(text);
            body.Children.Add(row);
        }

        Row($"A · {Path.GetFileName(_currentPath)}", _currentPath, Zoom.Grid, Fps, plan.AFirst, plan.ALast);
        Row($"B · {Path.GetFileName(pane.Path)}", pane.Path, pane.Grid, pane.Fps, plan.BFirst, plan.BLast);
        foreach (var black in new[] { plan.BlackBefore, plan.BlackAfter })
            if (black is { } b && plan.BFirst != null)
                body.Children.Add(new TextBlock
                {
                    Text = S.ExportBothBlack(TimeFormat.Short(b.From, Fps), TimeFormat.Short(b.To, Fps)), Foreground = (Brush)FindResource("Error"),
                    FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxWidth = 720, Margin = new Thickness(0, 0, 0, 8),
                });

        // La calidad, como en el clip de un vídeo (sin la copia directa: hay que juntar las dos imágenes).
        var quality = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        quality.Children.Add(new TextBlock { Text = S.ExportBothQuality, Foreground = muted, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var exact = new RadioButton { Content = S.ExportBothExact, ToolTip = S.ExportBothExactTip, Style = (Style)FindResource("ScopeButton"), GroupName = "quality" };
        var light = new RadioButton
        {
            Content = S.LightClipMenu(_store.Settings.LightClipMegabytesPerSecond.ToString("0.##", Loc.Culture)), ToolTip = S.LightClipMenuTip,
            Style = (Style)FindResource("ScopeButton"), GroupName = "quality", IsChecked = true,
        };
        quality.Children.Add(exact);
        quality.Children.Add(light);

        // La velocidad, como en «A otra velocidad…»; aquí por defecto la normal, salvo que se reproduzca a otra.
        var export = new Button { Content = S.ExportBothStart, MinWidth = 88, IsDefault = true, Style = (Style)FindResource("AccentButton") };
        var length = new TextBlock { Foreground = muted, FontSize = 12.5, Margin = new Thickness(0, 10, 0, 0) };
        Func<double?> speed = () => 1;
        void Update()
        {
            export.IsEnabled = speed() != null;
            length.Text = speed() is { } s ? S.ExportSpeedLength(LengthText((end - start) / s)) : S.ExportSpeedRange;
        }
        var (speedRow, speedBox, value) = BuildSpeedRow(InitialExportSpeed(atNormal: 1), Update);
        speed = value;
        body.Children.Add(speedRow);
        quality.Margin = new Thickness(0, 10, 0, 0);
        body.Children.Add(quality);
        body.Children.Add(length);
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
        CompareExportDialog = (window, plan);
        SpeedExportDialog = (window, speedBox, length);
        return (window, () => result);
    }

    // Las miniaturas del primer y del último frame, decodificadas aparte (sin tocar la caché de la tarjeta).
    private async void LoadThumbs(string path, double first, double last, Image left, Image right)
    {
        var frames = await Task.Run(() =>
        {
            try
            {
                using var thumbnailer = new Thumbnailer(path);
                return new[] { thumbnailer.Grab(first, 160, CancellationToken.None), thumbnailer.Grab(last, 160, CancellationToken.None) };
            }
            catch (Exception e) when (e is FFmpegException or IOException)
            {
                return null;
            }
        });
        if (frames == null)
            return;
        foreach (var (image, frame) in new[] { (left, frames[0]), (right, frames[1]) })
            if (frame is { } f)
                image.Source = BitmapSource.Create(f.Width, f.Height, 96, 96, PixelFormats.Bgr32, null, f.Pixels, f.Width * 4);
    }

    /// <summary>Bloqueados y con una zona en A, la barra de B marca su tramo equivalente.</summary>
    private void UpdateCompareBand()
    {
        if (!_compareUiBuilt)
            return;
        _cmpBar.Band = _sync is { Locked: true } sync && _zoomRange is { } z && _compare != null
            ? (z.Start + sync.CurrentOffset, z.End + sync.CurrentOffset) : null;
    }
}
