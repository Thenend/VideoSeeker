using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

// Exportar el tramo ampliado o una región de bookmark como clip: menú «Exportar clip» (Ctrl + M, como en
// Premiere), en segundo plano, con su progreso en una píldora de la barra de controles y en la de tareas, y un
// aviso al acabar con dónde ha quedado.
public partial class MainWindow
{
    private const string GlyphExporting = "\uE896", GlyphExported = "\uE73E", GlyphExportFailed = "\uE7BA";
    private static readonly Brush ExportingFill = new SolidColorBrush(Color.FromArgb(0x66, 0x4C, 0x9B, 0xFF));
    private static readonly Brush ExportedFill = new SolidColorBrush(Color.FromArgb(0x40, 0x3D, 0xD6, 0x8C));
    private static readonly Brush ExportFailedFill = new SolidColorBrush(Color.FromArgb(0x55, 0xE5, 0x48, 0x4D));
    private static readonly Brush ExportedGlyph = new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C));

    private readonly ClipExporter _exporter = new();
    // El último clip que terminó (o su error): la píldora lo deja a mano hasta que se quita.
    private (ClipJob Job, string? Error)? _lastClip;
    // Los clips de esta sesión, el más reciente primero.
    private readonly List<string> _recentClips = [];
    // El aviso de fin se cierra solo pasado este rato, salvo con el ratón encima.
    private readonly DispatcherTimer _clipPopupTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _clipPopupPending;

    private void InitClipExport()
    {
        _exporter.Changed += () => Dispatcher.BeginInvoke(UpdateExportButton);
        _exporter.Finished += (job, error) => Dispatcher.BeginInvoke(() => OnClipFinished(job, error));
        TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();
        ExportPill.SizeChanged += (_, _) => UpdateExportButton();
        _clipPopupTimer.Tick += (_, _) =>
        {
            if (ClipPopup.Child.IsMouseOver)
                return;
            CloseClipPopup();
        };
        // Es una ventana aparte, por encima de todo: con la app en segundo plano se esconde y vuelve al volver.
        Deactivated += (_, _) =>
        {
            if (ClipPopup.IsOpen)
            {
                ClipPopup.IsOpen = false;
                _clipPopupPending = true;
            }
        };
        Activated += (_, _) =>
        {
            if (_clipPopupPending && _clipPopupTimer.IsEnabled)
                PlaceClipPopup();
            _clipPopupPending = false;
        };
        LocationChanged += (_, _) => { if (ClipPopup.IsOpen) PlaceClipPopup(); };
        SizeChanged += (_, _) => { if (ClipPopup.IsOpen) PlaceClipPopup(); };
    }

    /// <summary>El submenú «Exportar clip» con las tres maneras, para un tramo de un vídeo.</summary>
    // Con la claqueta. "header" cambia el texto (en el menú de la imagen dice de qué es el clip); Ctrl + M solo si exporta la zona.
    private void AddExportMenu(ItemsControl menu, string? video, double start, double end, string? name, string? header = null, bool zone = true)
    {
        var export = WithIcon(new MenuItem { Header = header ?? S.ExportClip, InputGestureText = zone ? $"{S.KeyCtrl}+M" : "" }, "ClipMenuIcon");
        if (video == null || !File.Exists(video) || end <= start)
        {
            export.IsEnabled = false;
            menu.Items.Add(export);
            return;
        }
        AddExportItems(export, video, start, end, name);
        menu.Items.Add(export);
    }

    private void AddExportItems(ItemsControl menu, string video, double start, double end, string? name)
    {
        var keyframe = video == _currentPath ? KeyframeAtOrBefore(start) : null;
        var exact = new MenuItem { Header = S.ExactOriginal, ToolTip = S.ExactOriginalTip };
        exact.Click += (_, _) => ExportClip(video, start, end, name, ClipPreset.Exact, null);
        menu.Items.Add(exact);
        var copy = new MenuItem { Header = S.CopyOriginal(CopyStartText(start, keyframe)), ToolTip = S.CopyOriginalTip };
        copy.Click += (_, _) => ExportClip(video, start, end, name, ClipPreset.Copy, keyframe);
        menu.Items.Add(copy);
        var light = new MenuItem
        {
            Header = S.LightClipMenu(_store.Settings.LightClipMegabytesPerSecond.ToString("0.##", Loc.Culture)),
            ToolTip = S.LightClipMenuTip,
        };
        light.Click += (_, _) => ExportClip(video, start, end, name, ClipPreset.Light, null);
        menu.Items.Add(light);
    }

    // Cuánto antes empieza la copia directa, que solo puede empezar en un keyframe.
    private string CopyStartText(double start, double? keyframe)
    {
        if (keyframe is not { } k)
            return S.StartsAtPreviousKeyframe;
        var early = start - k;
        var frame = Fps > 0 ? 1 / Fps : 0.04;
        if (early < frame / 2)
            return S.ExactCut;
        if (early >= 1)
            return S.StartsSecondsEarlier(early.ToString("0.#", Loc.Culture));
        var frames = (int)Math.Round(early / frame);
        return S.StartsFramesEarlier(frames);
    }

    // El keyframe en o antes del instante, según el índice propio del vídeo (null mientras se construye).
    private double? KeyframeAtOrBefore(double time)
    {
        if (_frameIndex is not { Keyframes: { Length: > 0 } keys })
            return null;
        var i = Array.BinarySearch(keys, time + 1e-4);
        if (i < 0)
            i = ~i - 1;
        return i >= 0 ? keys[i] : null;
    }

    /// <summary>Ctrl + M: el menú de exportar para la zona ampliada (libre o la región seleccionada).</summary>
    private void ShowExportMenu()
    {
        if (ExportTarget() is not { } target)
        {
            ShowToast(S.ExportNeedsRange);
            return;
        }
        var menu = new ContextMenu { PlacementTarget = VideoArea, Placement = PlacementMode.MousePoint };
        menu.Items.Add(new MenuItem { Header = S.ExportWhat(RangeLabel(target.Start, target.End, target.Name)), IsEnabled = false });
        AddExportItems(menu, target.Video, target.Start, target.End, target.Name);
        menu.IsOpen = true;
    }

    // La zona ampliada; si es una región, el clip se llama como ella.
    private (string Video, double Start, double End, string? Name)? ExportTarget()
    {
        if (_currentPath == null)
            return null;
        if (ZoneBookmark is { } region)
            return (_currentPath, region.Start, EndOf(region), region.Name);
        return _zoomRange is { } z ? (_currentPath, z.Start, z.End, null) : null;
    }

    private string RangeLabel(double start, double end, string? name) =>
        name is { Length: > 0 } ? S.QuotedName(name) : $"{TimeFormat.Short(start, Fps)} – {TimeFormat.Short(end, Fps)}";

    private void ExportClip(string video, double start, double end, string? name, ClipPreset preset, double? keyframe)
    {
        var settings = _store.Settings;
        var job = new ClipJob
        {
            Source = video, Start = start, End = end, Name = name, Preset = preset, Keyframe = keyframe,
            // La pista de audio que se está oyendo, si es de este vídeo; si no, la primera.
            AudioStream = video == _currentPath ? _tracks.FirstOrDefault(t => t is { Type: "audio", Selected: true })?.FfIndex : null,
            Folder = settings.ClipFolder, LightMegabytesPerSecond = settings.LightClipMegabytesPerSecond,
            HardwareDecoding = settings.HardwareDecoding,
        };
        var queued = _exporter.Busy;
        _exporter.Enqueue(job);
        ShowToast(queued ? S.ClipQueued : S.ExportingClip);
    }

    private void OnClipFinished(ClipJob job, string? error)
    {
        if (job.Cancelled)
        {
            ShowToast(S.ExportCancelled);
            UpdateExportButton();
            return;
        }
        _lastClip = (job, error);
        if (error == null && job.Output != null)
        {
            _recentClips.Remove(job.Output);
            _recentClips.Insert(0, job.Output);
            if (_recentClips.Count > 8)
                _recentClips.RemoveAt(_recentClips.Count - 1);
        }
        UpdateExportButton();
        var copied = error == null && _store.Settings.CopyClipToClipboard && CopyToClipboard(job.Output!);
        ShowClipPopup(job, error, copied);
    }

    // Como archivo, igual que al copiarlo en el Explorador: se pega tal cual en un chat, en Jira o en una carpeta.
    private static bool CopyToClipboard(string path)
    {
        try
        {
            Clipboard.SetFileDropList(new StringCollection { path });
            return true;
        }
        catch (Exception e) when (e is COMException or ExternalException)
        {
            return false;
        }
    }

    private static void RevealInExplorer(string path) => System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");

    private static void OpenFile(string path) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });

    private static string Megabytes(string path)
    {
        var size = new FileInfo(path).Length / (1024.0 * 1024);
        return $"{size.ToString(size < 10 ? "0.#" : "0", Loc.Culture)} {S.MegabytesUnit}";
    }

    // ---- Aviso al acabar ----

    private void ShowClipPopup(ClipJob job, string? error, bool copied)
    {
        HideToast();
        var ok = error == null;
        ClipPopupGlyph.Text = ok ? GlyphExported : GlyphExportFailed;
        ClipPopupGlyph.Foreground = ok ? ExportedGlyph : (Brush)FindResource("Error");
        ClipPopupTitle.Text = ok ? S.ClipSaved : S.ClipFailed;
        ClipPopupName.Text = Path.GetFileName(job.Output) ?? ClipExporter.FileName(job);
        ClipPopupName.ToolTip = job.Output;
        ClipPopupDetail.Text = ok
            ? $"{S.InFolder(Path.GetDirectoryName(job.Output) ?? "")} · {Megabytes(job.Output!)}" + (copied ? "\n" + S.CopiedForChat : "")
            : error;
        ClipPopupButtons.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        ClipPopupSecond.Content = S.Open;
        _popupFile = (job.Output, CopyInsteadOfOpen: false);
        _clipPopupTimer.Interval = TimeSpan.FromSeconds(ok ? 15 : 30);
        _clipPopupTimer.Stop();
        _clipPopupTimer.Start();
        PlaceClipPopup();
    }

    // Abajo a la derecha de la imagen, por encima de los controles si flotan sobre ella.
    private void PlaceClipPopup()
    {
        var child = (FrameworkElement)ClipPopup.Child;
        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var height = VideoArea.ActualHeight - (ControlsPopup.IsOpen ? ControlsHost.ActualHeight : 0);
        ClipPopup.HorizontalOffset = Math.Max(0, VideoArea.ActualWidth - child.DesiredSize.Width - 16);
        ClipPopup.VerticalOffset = Math.Max(0, height - child.DesiredSize.Height - 16);
        ClipPopup.IsOpen = true;
        // Un Popup abierto no sigue a la ventana: si ya estaba abierto, se le obliga a recolocarse.
        var offset = ClipPopup.HorizontalOffset;
        ClipPopup.HorizontalOffset = offset + 1;
        ClipPopup.HorizontalOffset = offset;
    }

    private void CloseClipPopup()
    {
        _clipPopupTimer.Stop();
        _clipPopupPending = false;
        ClipPopup.IsOpen = false;
    }

    private void OnClipPopupCloseClick(object sender, RoutedEventArgs e) => CloseClipPopup();

    // Archivo del aviso (el clip, o los bookmarks exportados) y si su segundo botón copia en vez de abrir.
    private (string? Path, bool CopyInsteadOfOpen) _popupFile;

    /// <summary>El mismo aviso que al exportar un clip, para otro archivo guardado (los bookmarks junto al vídeo).</summary>
    private void ShowFileSavedPopup(string title, string path, string detail)
    {
        HideToast();
        ClipPopupGlyph.Text = GlyphExported;
        ClipPopupGlyph.Foreground = ExportedGlyph;
        ClipPopupTitle.Text = title;
        ClipPopupName.Text = Path.GetFileName(path);
        ClipPopupName.ToolTip = path;
        ClipPopupDetail.Text = S.InFolder(Path.GetDirectoryName(path) ?? "") + detail;
        ClipPopupButtons.Visibility = Visibility.Visible;
        ClipPopupSecond.Content = S.Copy;
        _popupFile = (path, CopyInsteadOfOpen: true);
        _clipPopupTimer.Interval = TimeSpan.FromSeconds(15);
        _clipPopupTimer.Stop();
        _clipPopupTimer.Start();
        PlaceClipPopup();
    }

    private void OnClipRevealClick(object sender, RoutedEventArgs e)
    {
        if (_popupFile.Path is { } path && File.Exists(path))
            RevealInExplorer(path);
        CloseClipPopup();
    }

    private void OnClipOpenClick(object sender, RoutedEventArgs e)
    {
        if (_popupFile.Path is { } path && File.Exists(path))
        {
            if (!_popupFile.CopyInsteadOfOpen)
                OpenFile(path);
            else
                ShowToast(CopyToClipboard(path) ? S.CopiedForFolder : S.CopyFailed);
        }
        CloseClipPopup();
    }

    // ---- Píldora de la barra de controles ----

    private void UpdateExportButton()
    {
        var current = _exporter.Current;
        var pending = _exporter.Pending;
        var taskbar = TaskbarItemInfo;
        double fill;
        if (current != null)
        {
            ExportGlyph.Text = GlyphExporting;
            ExportGlyph.Foreground = (Brush)FindResource("Fg");
            ExportText.Text = S.ExportingPercent((int)Math.Round(current.Progress * 100)) + (pending > 0 ? $" (+{pending})" : "");
            ExportFill.Background = ExportingFill;
            fill = current.Progress;
            ExportButton.ToolTip = (Path.GetFileName(current.Output) ?? S.PreparingClip)
                + (pending > 0 ? S.MoreQueued(pending) : "") + S.ClickToCancel;
            taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
            taskbar.ProgressValue = Math.Max(0.02, current.Progress);
        }
        else if (_lastClip is { } last)
        {
            var ok = last.Error == null;
            ExportGlyph.Text = ok ? GlyphExported : GlyphExportFailed;
            ExportGlyph.Foreground = ok ? ExportedGlyph : (Brush)FindResource("Error");
            ExportText.Text = ok ? S.ClipReady : S.ExportError;
            ExportFill.Background = ok ? ExportedFill : ExportFailedFill;
            fill = 1;
            ExportButton.ToolTip = ok ? Path.GetFileName(last.Job.Output) + S.ClickToGo : S.ClickToSeeWhy;
            taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
        }
        else
        {
            ExportButton.Visibility = Visibility.Collapsed;
            taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
            return;
        }
        ExportButton.Visibility = Visibility.Visible;
        ExportFill.Width = ExportPill.ActualWidth * Math.Clamp(fill, 0, 1);
    }

    private void OnExportButtonClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ExportButton, Placement = PlacementMode.Top };
        if (_exporter.Current is { } current)
        {
            var cancel = new MenuItem { Header = S.CancelClip(Path.GetFileName(current.Output) ?? S.TheClip) };
            cancel.Click += (_, _) => _exporter.CancelCurrent();
            menu.Items.Add(cancel);
            if (_exporter.Pending > 0)
            {
                var all = new MenuItem { Header = S.CancelQueuedToo(_exporter.Pending) };
                all.Click += (_, _) => _exporter.CancelAll();
                menu.Items.Add(all);
            }
            menu.IsOpen = true;
            return;
        }
        if (_lastClip is { Error: { } error } failed)
        {
            ShowClipPopup(failed.Job, error, copied: false);
            return;
        }
        if (_lastClip is { Job.Output: { } path } && File.Exists(path))
        {
            menu.Items.Add(new MenuItem { Header = $"{Path.GetFileName(path)} · {Megabytes(path)}", IsEnabled = false });
            var reveal = new MenuItem { Header = S.ShowInFolder };
            reveal.Click += (_, _) => RevealInExplorer(path);
            menu.Items.Add(reveal);
            var open = new MenuItem { Header = S.Open };
            open.Click += (_, _) => OpenFile(path);
            menu.Items.Add(open);
            var copy = new MenuItem { Header = S.CopyToClipboard };
            copy.Click += (_, _) => ShowToast(CopyToClipboard(path) ? S.ClipCopied : S.CopyFailed);
            menu.Items.Add(copy);
        }
        var older = _recentClips.Where(p => p != _lastClip?.Job.Output && File.Exists(p)).ToList();
        if (older.Count > 0)
        {
            var previous = new MenuItem { Header = S.PreviousClips };
            foreach (var clip in older)
            {
                var item = new MenuItem { Header = Path.GetFileName(clip), ToolTip = S.ShowInFolder };
                item.Click += (_, _) => RevealInExplorer(clip);
                previous.Items.Add(item);
            }
            menu.Items.Add(previous);
        }
        menu.Items.Add(new Separator());
        var hide = new MenuItem { Header = S.RemoveFromHere };
        hide.Click += (_, _) =>
        {
            _lastClip = null;
            CloseClipPopup();
            UpdateExportButton();
        };
        menu.Items.Add(hide);
        menu.IsOpen = true;
    }

    public static bool ClipSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "clips";

    /// <summary>
    /// VIDEOSEEKER_SELFTEST=clips: exporta un tramo de 3 s del vídeo abierto con las tres maneras a una carpeta
    /// temporal (sin tocar el portapapeles), comprueba el menú, el botón de progreso y los archivos, captura la barra
    /// de controles en selftest-clips.png y escribe selftest-clips.log. Deja los ajustes como estaban.
    /// </summary>
    private async Task RunClipSelfTest()
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

        // La barra de controles, sobre el fondo de la ventana (ella es transparente).
        void Capture(string name, FrameworkElement? element = null)
        {
            UpdateLayout();
            var bar = element ?? ControlsHost;
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(bar);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(bar.ActualWidth * dpi.DpiScaleX), (int)(bar.ActualHeight * dpi.DpiScaleY),
                dpi.PixelsPerInchX, dpi.PixelsPerInchY, System.Windows.Media.PixelFormats.Pbgra32);
            var visual = new System.Windows.Media.DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var area = new Rect(0, 0, bar.ActualWidth, bar.ActualHeight);
                dc.DrawRectangle(Background, null, area);
                dc.DrawRectangle(new System.Windows.Media.VisualBrush(bar), null, area);
            }
            bitmap.Render(visual);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(_store.Folder, name));
            png.Save(stream);
        }

        var settings = _store.Settings;
        var (folder, clipboard) = (settings.ClipFolder, settings.CopyClipToClipboard);
        var temp = Path.Combine(Path.GetTempPath(), "VideoSeeker-clips-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            for (var i = 0; i < 300 && (_duration <= 0 || _frameIndex == null); i++)
                await Task.Delay(100);
            Check(_frameIndex != null, "índice de frames listo");
            settings.ClipFolder = temp;
            settings.CopyClipToClipboard = false;

            var grid = Zoom.Grid;
            var first = grid.FrameAt(_duration * 0.3);
            var count = (int)Math.Round(3 * Fps);
            SetFreeZoom((grid.TimeOf(first), grid.TimeOf(first + count)));
            await Task.Delay(300);
            var menu = new ContextMenu();
            AddExportMenu(menu, _currentPath, _zoomRange!.Value.Start, _zoomRange.Value.End, null);
            var items = ((MenuItem)menu.Items[0]).Items.OfType<MenuItem>().Select(i => (string)i.Header).ToList();
            Log($"menú: {string.Join(" | ", items)}");
            Check(items.Count == 3, "tres maneras de exportar");
            Check(ExportTarget() is { } target && target.Start == _zoomRange.Value.Start, "Ctrl + M exporta el tramo ampliado");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var (start, end) = _zoomRange.Value;
            foreach (var preset in new[] { ClipPreset.Exact, ClipPreset.Copy, ClipPreset.Light })
                ExportClip(_currentPath!, start, end, null, preset, preset == ClipPreset.Copy ? KeyframeAtOrBefore(start) : null);
            var captured = false;
            while (_exporter.Busy && watch.Elapsed < TimeSpan.FromMinutes(3))
            {
                await Task.Delay(100);
                if (!captured && _exporter.Current is { Progress: > 0.2 })
                {
                    Check(ExportButton.Visibility == Visibility.Visible && ExportText.Text.StartsWith("Exportando") && ExportText.Text.Contains('%'),
                        $"el botón enseña el progreso («{ExportText.Text}»)");
                    captured = true;
                    Capture("selftest-clips-progreso.png");
                }
            }
            await Task.Delay(300);
            Log($"exportados en {watch.Elapsed.TotalSeconds:0.0} s");
            Check(ExportButton.Visibility == Visibility.Visible && ExportText.Text == "Clip listo", $"al acabar, la píldora dice «{ExportText.Text}»");
            Check(ClipPopup.IsOpen && ClipPopupName.Text.Length > 0 && ClipPopupDetail.Text.StartsWith("En "),
                $"aviso de fin: «{ClipPopupTitle.Text}» · {ClipPopupName.Text} · {ClipPopupDetail.Text.Replace('\n', ' ')}");
            Check(_recentClips.Count == 3, $"tres clips recientes ({_recentClips.Count})");
            Check(_lastClip is { Error: null }, $"sin errores {_lastClip?.Error}");
            var files = Directory.Exists(temp) ? Directory.GetFiles(temp) : [];
            foreach (var file in files)
                Log($"  {Path.GetFileName(file)}: {new FileInfo(file).Length / 1024} KB");
            Check(files.Length == 3 && files.All(f => new FileInfo(f).Length > 0), "tres archivos con contenido");
            Check(files.Count(f => f.Contains("(ligero)") && f.EndsWith(".mp4")) == 1, "el ligero es un MP4 marcado «(ligero)»");

            Capture("selftest-clips.png");
            Capture("selftest-clips-aviso.png", (FrameworkElement)ClipPopup.Child);
            CloseZone();
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            settings.ClipFolder = folder;
            settings.CopyClipToClipboard = clipboard;
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { }
            Log(failures == 0 ? "TODO OK" : $"{failures} FALLOS");
            File.WriteAllLines(Path.Combine(_store.Folder, "selftest-clips.log"), lines);
            Close();
        }
    }

    /// <summary>Al cerrar con un clip a medias: true si se puede salir (y se cancela).</summary>
    private bool ConfirmCloseWhileExporting()
    {
        if (!_exporter.Busy)
            return true;
        return Dialog.Confirm(this, S.ExportingOnExit, S.Quit, S.Wait);
    }
}
