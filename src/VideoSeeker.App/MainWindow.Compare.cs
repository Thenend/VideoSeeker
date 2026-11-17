using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using VideoSeeker.App.Controls;

namespace VideoSeeker.App;

/// <summary>
/// Comparar dos vídeos: el abierto (A, el que manda: sus barras, sus bookmarks, su audio) y otro (B) a su lado, con su
/// propia cadena (<see cref="ComparePane"/>). Lado a lado o, si así se ven más grandes (vídeos anchos en una ventana
/// estrecha), uno encima del otro. Encima de cada imagen, su nombre y su frame; B lleva además el candado, el desfase,
/// la × para cerrar y una barra fina. Sueltos, el teclado mueve el activo (borde azul) y la rueda y el arrastre, la
/// imagen bajo el ratón; bloqueados (<see cref="CompareSync"/>), todo lo que mueve A mueve B.
/// </summary>
public partial class MainWindow
{
    private const double CompareHeaderHeight = 24, CompareBarHeight = 14, CompareGap = 4, CompareStripWidth = 132;

    private ComparePane? _compare;
    private CompareSync? _sync;
    // El panel activo (el que mueve el teclado sueltos): B o A.
    private bool _compareActiveB;
    private bool _compareStacked;
    // El primer clic sobre la imagen inactiva solo la activó: el doble clic no tiene nada que deshacer.
    private bool _clickOnlyActivated;
    private (bool Paused, double Time)? _beforeClickB;
    // Arrastre sobre la imagen de B (sueltos).
    private double _bScrubResume;
    private int _bScrubStartFrame;
    private long _bScrubLast, _bScrubShift;
    // La franja «Comparar» mientras se arrastra un vídeo sobre la ventana.
    private long _lastCompareDragOver;
    private bool _dropOnStrip;

    private bool _compareUiBuilt;
    private Border _cmpHeaderA = null!, _cmpFrameA = null!, _cmpFrameB = null!, _cmpDropStrip = null!, _cmpDropStripBack = null!;
    private Grid _cmpCellB = null!;
    private TextBlock _cmpNameA = null!, _cmpFrameTextA = null!, _cmpNameB = null!, _cmpFrameTextB = null!, _cmpOffset = null!, _cmpOutside = null!;
    private Button _cmpLock = null!;
    private System.Windows.Shapes.Path _cmpLockIcon = null!;
    private CompareBar _cmpBar = null!;

    private static readonly Brush CompareActiveBrush = Frozen(Color.FromRgb(0x4C, 0x9B, 0xFF));
    private static readonly Brush DropStripHoverBrush = Frozen(Color.FromRgb(0x24, 0x3A, 0x57));

    /// <summary>Lo que no depende de que haya comparación: la franja al arrastrar y que tocar la imagen de A la active.</summary>
    private void InitCompare()
    {
        DragEnter += OnCompareDragOver;
        DragOver += OnCompareDragOver;
        DragLeave += OnCompareDragLeave;
        PreviewDrop += OnComparePreviewDrop;
        _surface.DragStarted += () => SetCompareActive(false);
        VideoGrid.SizeChanged += (_, _) =>
        {
            if (_compare != null)
                ApplyCompareLayout();
        };
        BuildDropStrip();
    }

    // ---- Abrir y cerrar ----

    private void ShowCompareDialog()
    {
        if (_currentPath == null)
        {
            ShowOpenDialog();
            return;
        }
        var dialog = new OpenFileDialog { Filter = VideoFilter, Title = S.CompareOpenTitle };
        dialog.InitialDirectory = Path.GetDirectoryName(_compare?.Path ?? _currentPath);
        if (dialog.ShowDialog(this) == true)
            OpenCompare(dialog.FileName, 0);
    }

    /// <summary>B = este mismo vídeo, en el frame que se ve.</summary>
    private void CompareWithCurrentFrame()
    {
        if (_currentPath != null)
            OpenCompare(_currentPath, Zoom.Grid.Snap(_nav.CurrentTime));
    }

    /// <summary>
    /// Abre "path" como B en "start" (en pausa). Sin vídeo abierto, lo abre como siempre. No entra en el historial de
    /// vídeos. Si ya se comparaba, B pasa a ser este (y se sueltan).
    /// </summary>
    private async void OpenCompare(string path, double start)
    {
        if (_currentPath == null)
        {
            OpenByHand(path);
            return;
        }
        if (!File.Exists(path))
        {
            Dialog.Inform(this, S.FileNotFound(path));
            return;
        }
        if (_compare == null)
        {
            var pane = _compare = new ComparePane(this, Dispatcher, _store.Folder, _ffmpegReady, _store.Settings.HardwareDecoding);
            _sync = new CompareSync(_nav, _player, pane, () => Zoom.Grid, () => _lastFrameStart);
            _sync.StateChanged += UpdateCompareUi;
            pane.Changed += OnCompareChanged;
            pane.Nav.PositionShown += OnComparePositionB;
            pane.Nav.SpeedChanged += UpdateCompareHeaderB;
            _nav.PositionShown += OnComparePositionA;
            var surface = pane.Surface;
            surface.Clicked += OnCompareClicked;
            surface.DoubleClicked += () =>
            {
                if (!ClickOnlyActivated())
                    UndoCompareClick();
                ToggleFullscreen();
            };
            surface.DragStarted += OnCompareDragStarted;
            surface.DragMoved += OnCompareDragMoved;
            surface.DragEnded += OnCompareDragEnded;
            surface.Wheel += OnCompareWheel;
            surface.MiddleClicked += () =>
            {
                if (_sync is { Locked: true })
                    ResetRate();
                else if (_compare?.Ready == true)
                    SetCompareRate(1);
            };
            surface.RightClicked += () =>
            {
                SetCompareActive(true);
                ShowVideoMenu();
            };
            // La memoria de la caché de frames, mitad para cada uno (el ajuste no cambia).
            if (_engine != null)
                _engine.CacheShare = 2;
            ApplyCompareLayout();
        }
        else
        {
            _sync?.Unlock();
        }
        SetCompareActive(true);
        var same = string.Equals(Path.GetFullPath(path), Path.GetFullPath(_currentPath), StringComparison.OrdinalIgnoreCase);
        UpdateCompareUi();
        await _compare.OpenAsync(path, start, _engine, _store.Settings.FrameCacheMegabytes * 1024L * 1024, same ? _frameIndex : null);
        UpdateCompareUi();
    }

    /// <summary>La × de B o «Cerrar comparación». "immediate" al cerrar la app (sin esperar a nada).</summary>
    private void CloseCompare(bool immediate = false)
    {
        if (_compare is not { } pane)
            return;
        _sync?.Dispose();
        _sync = null;
        _nav.PositionShown -= OnComparePositionA;
        _compare = null;
        _compareActiveB = false;
        if (_engine != null)
            _engine.CacheShare = 1;
        // Fuera de la vista, la ventana nativa de B se oculta al momento; mpv y el motor se cierran después.
        ApplyCompareLayout();
        _ = pane.CloseAsync(immediate);
    }

    // Abrir otro vídeo en A: el desfase ya no significa nada.
    private void OnMainVideoOpening() => _sync?.Unlock();

    // ---- Vista ----

    private void BuildCompareUi()
    {
        if (_compareUiBuilt)
            return;
        _compareUiBuilt = true;
        var fg = (Brush)FindResource("Fg");
        var muted = (Brush)FindResource("FgMuted");
        var panel = (Brush)FindResource("Panel");
        TextBlock Name() => new() { Foreground = fg, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8, 0, 8, 0) };
        TextBlock FrameLabel() => new() { Foreground = muted, FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };

        // A: nombre y frame.
        _cmpNameA = Name();
        _cmpFrameTextA = FrameLabel();
        var dockA = new DockPanel();
        DockPanel.SetDock(_cmpFrameTextA, Dock.Right);
        dockA.Children.Add(_cmpFrameTextA);
        dockA.Children.Add(_cmpNameA);
        _cmpHeaderA = new Border { Background = panel, Height = CompareHeaderHeight, VerticalAlignment = VerticalAlignment.Top, Child = dockA };
        _cmpHeaderA.MouseLeftButtonDown += (_, _) => SetCompareActive(false);
        _cmpFrameA = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false };
        Panel.SetZIndex(_cmpFrameA, 2);

        // B: candado y desfase a la izquierda (entre las dos imágenes), nombre, frame y la ×.
        _cmpLockIcon = new System.Windows.Shapes.Path { Style = (Style)FindResource("IconPath"), Data = (Geometry)FindResource("LockOpenIcon") };
        _cmpLock = new Button { Style = (Style)FindResource("ToolButton"), Padding = new Thickness(5, 0, 5, 0), Height = CompareHeaderHeight - 2, Content = _cmpLockIcon };
        _cmpLock.Click += (_, _) => ToggleCompareLock();
        _cmpOffset = new TextBlock
        {
            Foreground = CompareActiveBrush, FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0), ToolTip = S.CompareOffsetTip, Visibility = Visibility.Collapsed, Background = Brushes.Transparent,
        };
        var close = new Button
        {
            Style = (Style)FindResource("ToolButton"), FontFamily = (FontFamily)FindResource("Icons"), FontSize = 10, Content = "",
            Padding = new Thickness(7, 0, 7, 0), Height = CompareHeaderHeight - 2, ToolTip = S.CloseCompare,
        };
        close.Click += (_, _) => CloseCompare();
        _cmpNameB = Name();
        _cmpFrameTextB = FrameLabel();
        var dockB = new DockPanel();
        DockPanel.SetDock(_cmpLock, Dock.Left);
        DockPanel.SetDock(_cmpOffset, Dock.Left);
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(_cmpFrameTextB, Dock.Right);
        dockB.Children.Add(_cmpLock);
        dockB.Children.Add(_cmpOffset);
        dockB.Children.Add(close);
        dockB.Children.Add(_cmpFrameTextB);
        dockB.Children.Add(_cmpNameB);
        var headerB = new Border { Background = panel, Height = CompareHeaderHeight, VerticalAlignment = VerticalAlignment.Top, Child = dockB };
        headerB.MouseLeftButtonDown += (_, _) => SetCompareActive(true);

        _cmpBar = new CompareBar();
        _cmpBar.SeekRequested += OnCompareBarSeek;
        var barHost = new Border { Background = panel, Height = CompareBarHeight, VerticalAlignment = VerticalAlignment.Bottom, Child = _cmpBar };
        _cmpOutside = new TextBlock
        {
            Foreground = muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        _cmpFrameB = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false };
        Panel.SetZIndex(_cmpFrameB, 2);
        _cmpCellB = new Grid { Background = Brushes.Black };
        _cmpCellB.Children.Add(headerB);
        _cmpCellB.Children.Add(_cmpOutside);
        _cmpCellB.Children.Add(barHost);
        _cmpCellB.Children.Add(_cmpFrameB);
    }

    /// <summary>Coloca A y B (o vuelve a la vista de uno solo si ya no se compara).</summary>
    private void ApplyCompareLayout()
    {
        if (_compare is not { } pane)
        {
            if (!_compareUiBuilt)
                return;
            VideoGrid.Children.Remove(_cmpHeaderA);
            VideoGrid.Children.Remove(_cmpFrameA);
            VideoGrid.Children.Remove(_cmpCellB);
            if (_cmpCellB.Children.OfType<Border>().FirstOrDefault(b => b.Tag as string == "B") is { } old)
                _cmpCellB.Children.Remove(old);
            VideoGrid.ColumnDefinitions.Clear();
            VideoGrid.RowDefinitions.Clear();
            VideoArea.Margin = new Thickness(0);
            return;
        }
        BuildCompareUi();
        var fresh = !VideoGrid.Children.Contains(_cmpCellB);
        if (fresh)
        {
            pane.Placeholder.Tag = "B";
            pane.Placeholder.Margin = new Thickness(1, CompareHeaderHeight, 1, CompareBarHeight + 1);
            if (!_cmpCellB.Children.Contains(pane.Placeholder))
                _cmpCellB.Children.Insert(1, pane.Placeholder);
            VideoGrid.Children.Add(_cmpHeaderA);
            VideoGrid.Children.Add(_cmpFrameA);
            VideoGrid.Children.Add(_cmpCellB);
        }
        var stacked = ChooseStacked(force: fresh);
        if (!fresh && stacked == _compareStacked)
            return;
        _compareStacked = stacked;
        VideoGrid.ColumnDefinitions.Clear();
        VideoGrid.RowDefinitions.Clear();
        for (var i = 0; i < 2; i++)
            if (stacked)
                VideoGrid.RowDefinitions.Add(new RowDefinition());
            else
                VideoGrid.ColumnDefinitions.Add(new ColumnDefinition());
        foreach (var element in new UIElement[] { VideoArea, EmptyHint, _cmpHeaderA, _cmpFrameA })
        {
            Grid.SetColumn(element, 0);
            Grid.SetRow(element, 0);
        }
        Grid.SetColumn(_cmpCellB, stacked ? 0 : 1);
        Grid.SetRow(_cmpCellB, stacked ? 1 : 0);
        // Las dos imágenes del mismo tamaño: A deja abajo el sitio de la barra de B, y el hueco entre las dos se reparte.
        var half = CompareGap / 2;
        var aSide = stacked ? new Thickness(0, 0, 0, half) : new Thickness(0, 0, half, 0);
        _cmpHeaderA.Margin = stacked ? new Thickness(0) : aSide;
        _cmpFrameA.Margin = aSide;
        VideoArea.Margin = new Thickness(1, CompareHeaderHeight, 1 + aSide.Right, CompareBarHeight + 1 + aSide.Bottom);
        _cmpCellB.Margin = stacked ? new Thickness(0, half, 0, 0) : new Thickness(half, 0, 0, 0);
    }

    /// <summary>
    /// Uno encima del otro si así se ve más imagen de los dos (vídeos anchos en una ventana estrecha); si no, lado a
    /// lado. Con un margen para no cambiar a cada píxel mientras se anima un panel.
    /// </summary>
    private bool ChooseStacked(bool force)
    {
        var (w, h) = (VideoGrid.ActualWidth, VideoGrid.ActualHeight);
        if (w <= 0 || h <= 0)
            return _compareStacked;
        var aspectA = _player.VideoAspect is double a && a > 0 ? a : 16 / 9.0;
        var aspectB = _compare is { Aspect: > 0 } pane ? pane.Aspect : aspectA;
        static double Fit(double width, double height, double aspect) =>
            width <= 0 || height <= 0 ? 0 : width / height > aspect ? height * height * aspect : width * width / aspect;
        var chrome = CompareHeaderHeight + CompareBarHeight + 1;
        var side = Fit(w / 2 - CompareGap, h - chrome, aspectA) + Fit(w / 2 - CompareGap, h - chrome, aspectB);
        var stack = Fit(w - 2, h / 2 - CompareGap - chrome, aspectA) + Fit(w - 2, h / 2 - CompareGap - chrome, aspectB);
        if (force)
            return stack > side;
        return _compareStacked ? stack * 1.1 >= side : stack > side * 1.1;
    }

    private void SetCompareActive(bool b)
    {
        if (_compare == null)
            b = false;
        _compareActiveB = b;
        if (!_compareUiBuilt)
            return;
        _cmpFrameA.BorderBrush = _compare != null && !b ? CompareActiveBrush : Brushes.Transparent;
        _cmpFrameB.BorderBrush = b ? CompareActiveBrush : Brushes.Transparent;
    }

    private void OnCompareChanged()
    {
        if (_compare is not { } pane)
            return;
        _cmpBar.Duration = pane.LastFrameStart > 0 ? pane.LastFrameStart : pane.Duration;
        ApplyCompareLayout();
        UpdateCompareHeaderB();
        UpdateCompareUi();
    }

    private void OnComparePositionA(double time) => UpdateCompareHeaderA();

    private void OnComparePositionB(double time)
    {
        if (_compare == null)
            return;
        _cmpBar.Position = time;
        UpdateCompareHeaderB();
    }

    private void UpdateCompareHeaderA()
    {
        if (!_compareUiBuilt || _compare == null)
            return;
        _cmpNameA.Text = $"A · {Path.GetFileName(_currentPath)}";
        _cmpNameA.ToolTip = _currentPath;
        _cmpFrameTextA.Text = FrameText.Text;
    }

    private void UpdateCompareHeaderB()
    {
        if (!_compareUiBuilt || _compare is not { } pane)
            return;
        _cmpNameB.Text = $"B · {Path.GetFileName(pane.Path)}";
        _cmpNameB.ToolTip = pane.Path;
        var t = pane.Nav.CurrentTime;
        var frame = pane.Index is { } index ? S.FrameNumber(index.FrameNumber(t))
            : pane.FpsKnown ? S.FrameNumberApprox($"{Math.Round(t * pane.Fps):0}") : "";
        // La velocidad, si no es 1× (sueltos se cambia aparte de la de A).
        var speed = pane.Nav.Speed != 0 ? pane.Nav.Speed : pane.Nav.Rate;
        _cmpFrameTextB.Text = speed != 1 ? $"{frame} · {SpeedLabel(speed)}" : frame;
    }

    /// <summary>El candado, el desfase y si B queda fuera.</summary>
    private void UpdateCompareUi()
    {
        if (!_compareUiBuilt || _compare is not { } pane || _sync is not { } sync)
            return;
        var locked = sync.Locked;
        _cmpLockIcon.Data = (Geometry)FindResource(locked ? "LockClosedIcon" : "LockOpenIcon");
        _cmpLock.Foreground = locked ? CompareActiveBrush : (Brush)FindResource("Fg");
        _cmpLock.ToolTip = locked ? S.CompareUnlockTip : S.CompareLockTip;
        _cmpOffset.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        _cmpOffset.Text = locked ? CompareOffsetLabel() : "";
        var outside = locked && sync.Range != CompareSync.BRange.Inside;
        pane.Placeholder.Visibility = outside ? Visibility.Hidden : Visibility.Visible;
        _cmpOutside.Visibility = outside ? Visibility.Visible : Visibility.Collapsed;
        _cmpOutside.Text = sync.Range == CompareSync.BRange.Before
            ? S.CompareStartsAt(TimeFormat.Short(pane.FirstFrameStart - sync.Offset, Fps))
            : S.CompareEnded;
        UpdateCompareHeaderA();
        UpdateCompareHeaderB();
    }

    /// <summary>"B +1.250 s (+75 frames)": el desfase con el formato de tiempo de los ajustes y en frames de A.</summary>
    private string CompareOffsetLabel()
    {
        var offset = _sync?.Offset ?? 0;
        var sign = offset < -1e-6 ? "−" : "+";
        var size = Math.Abs(offset);
        var time = TimeFormat.UseFrames ? TimeFormat.Timecode(size, Fps)
            : size.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " s";
        return S.CompareOffset(sign + time, sign + S.Frames((int)Math.Round(size * Fps)));
    }

    // ---- Candado ----

    private void ToggleCompareLock()
    {
        if (_sync is not { } sync || _compare is not { } pane)
            return;
        if (!sync.Locked && !pane.Ready)
            return;
        sync.Toggle();
        ShowToast(sync.Locked ? S.CompareLockedToast(CompareOffsetLabel()) : S.CompareUnlockedToast);
    }

    /// <summary>Con los dos bloqueados, ↑ ↓ , . y la rueda van al siguiente cambio de imagen de cualquiera de los dos.</summary>
    private bool CompareStep(int direction)
    {
        if (_sync is not { Locked: true } sync)
            return false;
        // En el borde de un bucle, como siempre (da la vuelta) y B sigue a A.
        var frame = Zoom.Grid.FrameAt(_nav.CurrentTime);
        if (LoopFrames(frame) is { } loop && (direction > 0 ? frame == loop.Last : frame == loop.First))
            return false;
        sync.Step(direction);
        return true;
    }

    // ---- Ratón y teclado ----

    private bool ActivateCompareA()
    {
        if (_compare == null || !_compareActiveB)
        {
            _clickOnlyActivated = false;
            return false;
        }
        SetCompareActive(false);
        _clickOnlyActivated = true;
        return true;
    }

    private bool ClickOnlyActivated()
    {
        var was = _clickOnlyActivated;
        _clickOnlyActivated = false;
        return was;
    }

    private void OnCompareClicked()
    {
        ReturnFocusToVideo();
        if (_compare is not { } pane)
            return;
        if (!_compareActiveB)
        {
            SetCompareActive(true);
            _clickOnlyActivated = true;
            return;
        }
        _clickOnlyActivated = false;
        if (_sync is { Locked: true })
        {
            _beforeClick = (_nav.Paused, _nav.CurrentTime);
            _nav.TogglePlay();
        }
        else
        {
            _beforeClickB = (pane.Nav.Paused, pane.Nav.CurrentTime);
            pane.Nav.TogglePlay();
            _sync?.Forget();
        }
    }

    // Como en A: el primer clic del doble clic pausó o reanudó; se deshace.
    private void UndoCompareClick()
    {
        if (_compare is not { } pane)
            return;
        if (_sync is { Locked: true })
        {
            _nav.TogglePlay();
            if (_beforeClick is { Paused: true } before && Math.Abs(_nav.CurrentTime - before.Time) > 0.5 / Fps)
                _nav.SeekTo(before.Time);
            _beforeClick = null;
            return;
        }
        pane.Nav.TogglePlay();
        if (_beforeClickB is { Paused: true } b && Math.Abs(pane.Nav.CurrentTime - b.Time) > 0.5 / pane.Fps)
            pane.Nav.SeekTo(b.Time);
        _beforeClickB = null;
    }

    private void OnCompareDragStarted()
    {
        SetCompareActive(true);
        ReturnFocusToVideo();
        if (_sync is { Locked: true })
        {
            OnSurfaceDragStarted();
            return;
        }
        if (_compare is not { } pane)
            return;
        _bScrubResume = pane.Nav.Speed;
        pane.Nav.Pause();
        _bScrubStartFrame = pane.Grid.FrameAt(pane.Nav.CurrentTime);
        _bScrubLast = 0;
        _bScrubShift = 0;
        _sync?.Forget();
    }

    // Sueltos, como en A pero sin bucles: se para en el primer y el último frame de B.
    private void OnCompareDragMoved(int dx)
    {
        if (_sync is { Locked: true })
        {
            OnSurfaceDragMoved(dx);
            return;
        }
        if (_compare is not { Ready: true } pane)
            return;
        var grid = pane.Grid;
        long frames = dx / Math.Max(1, _store.Settings.ScrubPixelsPerFrame) + _bScrubShift;
        var last = grid.FrameAt(pane.LastFrameStart);
        var clamped = Math.Clamp(frames, -(long)_bScrubStartFrame, Math.Max(-(long)_bScrubStartFrame, last - _bScrubStartFrame));
        _bScrubShift += clamped - frames;
        if (clamped == _bScrubLast)
            return;
        _bScrubLast = clamped;
        pane.Nav.ScrubTo(grid.TimeOf((int)(_bScrubStartFrame + clamped)));
    }

    private void OnCompareDragEnded()
    {
        if (_sync is { Locked: true })
        {
            OnSurfaceDragEnded();
            return;
        }
        if (_compare is not { } pane)
            return;
        var speed = _bScrubResume;
        _bScrubResume = 0;
        if (speed == 0 || !pane.Nav.Paused)
            return;
        if (speed < 0)
            pane.Nav.Backward();
        else
            pane.Nav.TogglePlay();
    }

    // Rueda sobre B: sueltos, su velocidad (con el ajuste de la rueda en velocidad); bloqueados o con el volumen, como en A.
    private void OnCompareWheel(int delta)
    {
        if (_compare is not { Ready: true } pane || _sync is { Locked: true } || _store.Settings.WheelOnVideo == Data.Settings.WheelVolume)
        {
            OnVideoWheel(delta);
            return;
        }
        var now = Environment.TickCount64;
        if (Keyboard.Modifiers != ModifierKeys.None || now - _lastWheelElsewhere < WheelElsewhereGraceMs)
        {
            if (now - _lastWheelElsewhere < WheelElsewhereGraceMs)
                _lastWheelElsewhere = now;
            _wheelRest = 0;
            return;
        }
        WheelSteps(delta, steps => SetCompareRate(Navigator.StepRate(pane.Nav.Rate, steps)));
    }

    private void SetCompareRate(double rate)
    {
        if (_compare is not { } pane)
            return;
        pane.Nav.SetRate(rate);
        ShowToast($"B · {S.SpeedToast(SpeedLabel(pane.Nav.Rate))}");
        UpdateCompareHeaderB();
    }

    // La barra fina de B: sueltos mueve B; bloqueados, A (y B lo sigue).
    private void OnCompareBarSeek(double time, bool final)
    {
        if (_compare is not { Ready: true } pane || _sync is not { } sync)
            return;
        SetCompareActive(true);
        if (sync.Locked)
        {
            OnTimelineSeek(Math.Clamp(time - sync.Offset, 0, _lastFrameStart > 0 ? _lastFrameStart : _duration), final);
            return;
        }
        if (final)
            pane.Nav.SeekTo(time);
        else
            pane.Nav.DragTo(time);
        sync.Forget();
    }

    /// <summary>
    /// Teclas de la comparación: Ctrl + Shift + O (abrir B), Ctrl + L (candado), Alt + , / Alt + . (solo B un frame) y,
    /// con B activo y sueltos, las de moverse (Espacio, J/K/L, flechas, , . Inicio, Fin), que van a B. True si la usó.
    /// </summary>
    private bool HandleCompareKey(Key key, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (key == Key.O && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            ShowCompareDialog();
            return true;
        }
        if (_compare is not { } pane || _sync is not { } sync)
            return false;
        if (key == Key.L && modifiers == ModifierKeys.Control)
        {
            ToggleCompareLock();
            return true;
        }
        if (modifiers == ModifierKeys.Alt && key is Key.OemComma or Key.OemPeriod)
        {
            sync.NudgeB(key == Key.OemPeriod ? 1 : -1);
            return true;
        }
        if (!_compareActiveB || sync.Locked || !pane.Ready || modifiers.HasFlag(ModifierKeys.Control))
            return false;
        var nav = pane.Nav;
        switch (key)
        {
            case Key.Space:
                nav.TogglePlay();
                break;
            case Key.K:
                if (!e.IsRepeat)
                    nav.Pause();
                break;
            case Key.J or Key.L when Keyboard.IsKeyDown(Key.K):
                nav.Step(key == Key.L ? 1 : -1);
                break;
            case Key.J:
                if (!e.IsRepeat)
                {
                    nav.Backward();
                    ShowToast($"B · {S.SpeedToast(SpeedLabel(nav.Speed))}");
                }
                break;
            case Key.L:
                if (!e.IsRepeat)
                {
                    nav.Forward();
                    if (nav.Speed != 1)
                        ShowToast($"B · {S.SpeedToast(SpeedLabel(nav.Speed))}");
                }
                break;
            case Key.Up or Key.OemPeriod:
                nav.Step(1);
                break;
            case Key.Down or Key.OemComma:
                nav.Step(-1);
                break;
            case Key.Left or Key.Right:
                var seconds = SeekStep(modifiers);
                nav.SeekRelative(key == Key.Left ? -seconds : seconds);
                break;
            case Key.Home:
                nav.SeekTo(0);
                break;
            case Key.End:
                nav.ScrubTo(pane.LastFrameStart);
                break;
            default:
                return false;
        }
        sync.Forget();
        return true;
    }

    // ---- Menús ----

    /// <summary>Al final del menú de la imagen: comparar con otro vídeo o con otra parte de este y, comparando, el candado y cerrar.</summary>
    private void AddCompareMenuItems(ItemsControl menu)
    {
        menu.Items.Add(new Separator());
        var other = WithIcon(new MenuItem { Header = S.CompareWithOtherVideo, InputGestureText = $"{S.KeyCtrl}+{S.KeyShift}+O" }, "CompareMenuIcon");
        other.Click += (_, _) => ShowCompareDialog();
        menu.Items.Add(other);
        var same = new MenuItem { Header = S.CompareWithThisVideo };
        same.Click += (_, _) => CompareWithCurrentFrame();
        menu.Items.Add(same);
        if (_compare == null || _sync == null)
            return;
        var locked = new MenuItem { Header = S.CompareLockMenu, InputGestureText = $"{S.KeyCtrl}+L", IsCheckable = true, IsChecked = _sync.Locked, IsEnabled = _compare.Ready };
        locked.Click += (_, _) => ToggleCompareLock();
        menu.Items.Add(locked);
        var close = new MenuItem { Header = S.CloseCompare };
        close.Click += (_, _) => CloseCompare();
        menu.Items.Add(close);
    }

    /// <summary>En el menú de un bookmark (de este vídeo o de otro, en el panel): abrirlo como B en su inicio.</summary>
    private void AddCompareBookmarkItem(ItemsControl menu, string? video, double start)
    {
        var exists = video != null && File.Exists(video) && _currentPath != null;
        var item = WithIcon(new MenuItem { Header = S.CompareWithBookmark, IsEnabled = exists }, "CompareMenuIcon");
        item.Click += (_, _) => OpenCompare(video!, start);
        menu.Items.Add(item);
    }

    // ---- Arrastrar un vídeo sobre la ventana: la franja «Comparar» ----

    private void BuildDropStrip()
    {
        var accent = (Brush)FindResource("Accent");
        var icon = new System.Windows.Shapes.Path
        {
            Data = (Geometry)FindResource("CompareMenuIcon"), Stroke = accent, StrokeThickness = 1.4, Width = 19, Height = 17,
            LayoutTransform = new ScaleTransform(1.6, 1.6), HorizontalAlignment = HorizontalAlignment.Center,
            StrokeLineJoin = PenLineJoin.Round,
        };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
        text.Children.Add(icon);
        text.Children.Add(new TextBlock
        {
            Text = S.CompareDropHint, Foreground = accent, FontSize = 15, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 6), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
        });
        text.Children.Add(new TextBlock
        {
            Text = S.CompareDropDetail, Foreground = (Brush)FindResource("FgMuted"), FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
        });
        _cmpDropStripBack = new Border { Background = (Brush)FindResource("Panel") };
        var grid = new Grid();
        grid.Children.Add(_cmpDropStripBack);
        grid.Children.Add(text);
        _cmpDropStrip = new Border
        {
            Width = CompareStripWidth, HorizontalAlignment = HorizontalAlignment.Right, BorderBrush = accent,
            BorderThickness = new Thickness(2, 0, 0, 0), Visibility = Visibility.Collapsed, Child = grid,
        };
        Grid.SetRow(_cmpDropStrip, 0);
        Grid.SetColumn(_cmpDropStrip, 0);
        RootGrid.Children.Add(_cmpDropStrip);
    }

    private bool IsOverDropStrip(DragEventArgs e)
    {
        if (!_cmpDropStrip.IsVisible)
            return false;
        var p = e.GetPosition(_cmpDropStrip);
        return p.X >= 0 && p.X <= _cmpDropStrip.ActualWidth && p.Y >= 0 && p.Y <= _cmpDropStrip.ActualHeight;
    }

    private void ShowDropStrip(bool show, bool hover = false)
    {
        if (show != _cmpDropStrip.IsVisible)
        {
            // La imagen es una ventana nativa: no se puede tapar. Se estrecha mientras dura el arrastre.
            VideoGrid.Margin = show ? new Thickness(0, 0, CompareStripWidth, 0) : new Thickness(0);
            _cmpDropStrip.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
        _cmpDropStripBack.Background = hover ? DropStripHoverBrush : (Brush)FindResource("Panel");
    }

    private void OnCompareDragOver(object sender, DragEventArgs e)
    {
        _lastCompareDragOver = Environment.TickCount64;
        var video = _currentPath != null && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files
            && !files.All(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        if (!video)
        {
            if (_cmpDropStrip.IsVisible)
                ShowDropStrip(false);
            return;
        }
        ShowDropStrip(true, IsOverDropStrip(e));
    }

    // Al pasar de un elemento a otro también llega DragLeave: solo se quita si no sigue llegando DragOver.
    private async void OnCompareDragLeave(object sender, DragEventArgs e)
    {
        var at = Environment.TickCount64;
        await Task.Delay(150);
        if (_lastCompareDragOver <= at && _cmpDropStrip.IsVisible)
            ShowDropStrip(false);
    }

    private void OnComparePreviewDrop(object sender, DragEventArgs e)
    {
        _dropOnStrip = IsOverDropStrip(e);
        if (_cmpDropStrip.IsVisible)
            ShowDropStrip(false);
    }

    /// <summary>Soltado sobre la franja: se abre como B. False si se soltó en otro sitio (se abre como siempre).</summary>
    private bool DropOnCompareStrip(DragEventArgs e, string file)
    {
        if (!_dropOnStrip)
            return false;
        _dropOnStrip = false;
        OpenCompare(file, 0);
        return true;
    }
}
