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
/// propia cadena (<see cref="ComparePane"/>). B va a la derecha, a la izquierda, arriba o abajo (donde se soltó al
/// arrastrarlo, o lo elegido en el menú; se recuerda). Encima de cada imagen, su nombre y su frame; B lleva además el candado, el desfase,
/// la × para cerrar y una barra fina. Sueltos, el teclado mueve el activo (borde azul) y la rueda y el arrastre, la
/// imagen bajo el ratón; bloqueados (<see cref="CompareSync"/>), todo lo que mueve A mueve B.
/// </summary>
public partial class MainWindow
{
    private const double CompareHeaderHeight = 24, CompareBarHeight = 14, CompareGap = 4;

    private ComparePane? _compare;
    private CompareSync? _sync;
    // El panel activo (el que mueve el teclado sueltos): B o A.
    private bool _compareActiveB;
    private bool _compareStacked, _compareBFirst;
    // El primer clic sobre la imagen inactiva solo la activó: el doble clic no tiene nada que deshacer.
    private bool _clickOnlyActivated;
    private (bool Paused, double Time)? _beforeClickB;
    // Arrastre sobre la imagen de B (sueltos).
    private double _bScrubResume;
    private int _bScrubStartFrame;
    private long _bScrubLast, _bScrubShift;
    // Las zonas «Comparar» mientras se arrastra un vídeo sobre la ventana, y dónde se soltó (null: en el centro, abrir).
    private long _lastCompareDragOver;
    private string? _dropPlacement;

    private bool _compareUiBuilt;
    private Border _cmpHeaderA = null!, _cmpFrameA = null!, _cmpFrameB = null!;
    private Grid _cmpDropOverlay = null!;
    private readonly List<(Border Zone, string? Placement)> _cmpDropZones = [];
    // Lo que se ocultó mientras se arrastra (la imagen es una ventana nativa: las zonas no se pueden pintar encima).
    private Visibility _dropHiddenA, _dropHiddenB;
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
        _surface.DragStarted += () =>
        {
            SetCompareActive(false);
            UpdateCompareHold();
        };
        // Alt pulsado o soltado: el candado se abre o se vuelve a cerrar al momento (y cada 100 ms, por si se soltó fuera).
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.System && e.SystemKey is Key.LeftAlt or Key.RightAlt)
                UpdateCompareHold();
        };
        PreviewKeyUp += (_, e) =>
        {
            if ((e.Key == Key.System ? e.SystemKey : e.Key) is Key.LeftAlt or Key.RightAlt)
                UpdateCompareHold();
        };
        PreviewMouseUp += (_, _) => Dispatcher.BeginInvoke(UpdateCompareHold);
        VideoGrid.SizeChanged += (_, _) =>
        {
            if (_compare != null)
                ApplyCompareLayout();
        };
        BuildDropZones();
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
    /// vídeos. Si ya se comparaba, B pasa a ser este (y se sueltan). Con "placement", B va a ese lado (y se recuerda).
    /// </summary>
    private async void OpenCompare(string path, double start, string? placement = null)
    {
        if (placement != null && _currentPath != null)
            SetComparePlacement(placement);
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
            _sync.Ticking += UpdateCompareHold;
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
                if (_sync is { Following: true })
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
        // Dónde va B: a la derecha (por defecto), a la izquierda, arriba o abajo.
        var placement = _store.Settings.ComparePlacement;
        var stacked = placement is "top" or "bottom";
        var bFirst = placement is "left" or "top";
        if (!fresh && stacked == _compareStacked && bFirst == _compareBFirst)
            return;
        (_compareStacked, _compareBFirst) = (stacked, bFirst);
        VideoGrid.ColumnDefinitions.Clear();
        VideoGrid.RowDefinitions.Clear();
        for (var i = 0; i < 2; i++)
            if (stacked)
                VideoGrid.RowDefinitions.Add(new RowDefinition());
            else
                VideoGrid.ColumnDefinitions.Add(new ColumnDefinition());
        var (cellA, cellB) = bFirst ? (1, 0) : (0, 1);
        foreach (var element in new UIElement[] { VideoArea, EmptyHint, _cmpHeaderA, _cmpFrameA })
        {
            Grid.SetColumn(element, stacked ? 0 : cellA);
            Grid.SetRow(element, stacked ? cellA : 0);
        }
        Grid.SetColumn(_cmpCellB, stacked ? 0 : cellB);
        Grid.SetRow(_cmpCellB, stacked ? cellB : 0);
        // Las dos imágenes del mismo tamaño: A deja abajo el sitio de los controles de B, y el hueco entre las dos se
        // reparte (cada una, la mitad del lado que da a la otra).
        var half = CompareGap / 2;
        Thickness Toward(bool towardsFirst) => stacked
            ? towardsFirst ? new Thickness(0, half, 0, 0) : new Thickness(0, 0, 0, half)
            : towardsFirst ? new Thickness(half, 0, 0, 0) : new Thickness(0, 0, half, 0);
        var aSide = Toward(towardsFirst: bFirst);
        _cmpHeaderA.Margin = new Thickness(aSide.Left, aSide.Top, aSide.Right, 0);
        _cmpFrameA.Margin = aSide;
        VideoArea.Margin = new Thickness(1 + aSide.Left, CompareHeaderHeight + aSide.Top, 1 + aSide.Right, CompareBottomHeight + 1 + aSide.Bottom);
        _cmpCellB.Margin = Toward(towardsFirst: !bFirst);
    }

    // Lo que ocupan bajo la imagen de B su barra fina (y sus controles): A deja lo mismo, para que midan igual.
    private static double CompareBottomHeight => CompareBarHeight;

    /// <summary>Dónde va B ("right", "left", "top", "bottom"): se recuerda y se aplica ya si se está comparando.</summary>
    private void SetComparePlacement(string placement)
    {
        if (_store.Settings.ComparePlacement == placement)
            return;
        _store.Settings.ComparePlacement = placement;
        _store.SaveSettings();
        if (_compare != null)
            ApplyCompareLayout();
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

    private void OnComparePositionA(double time)
    {
        UpdateCompareHeaderA();
        if (_sync is { Held: true })
            _cmpOffset.Text = CompareOffsetLabel();
    }

    private void OnComparePositionB(double time)
    {
        if (_compare == null)
            return;
        _cmpBar.Position = time;
        UpdateCompareHeaderB();
        if (_sync is { Held: true })
            _cmpOffset.Text = CompareOffsetLabel();
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
        // Con Alt pulsado se ve abierto y el desfase, el de ahora y apagado.
        var locked = sync.Following;
        _cmpOffset.Foreground = sync.Held ? (Brush)FindResource("FgMuted") : CompareActiveBrush;
        _cmpLockIcon.Data = (Geometry)FindResource(locked ? "LockClosedIcon" : "LockOpenIcon");
        _cmpLock.Foreground = locked ? CompareActiveBrush : (Brush)FindResource("Fg");
        _cmpLock.ToolTip = locked ? S.CompareUnlockTip : S.CompareLockTip;
        _cmpOffset.Visibility = sync.Locked ? Visibility.Visible : Visibility.Collapsed;
        _cmpOffset.Text = sync.Locked ? CompareOffsetLabel() : "";
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
        var offset = _sync?.CurrentOffset ?? 0;
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
        if (_sync is { Following: true })
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
        if (_sync is { Following: true })
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
        UpdateCompareHold();
        if (_sync is { Following: true })
        {
            OnSurfaceDragStarted();
            return;
        }
        if (_compare is not { } pane)
            return;
        _bScrubResume = pane.Nav.Speed;
        pane.Nav.Pause();
        _bScrubStartFrame = pane.ShownFrame;
        _bScrubLast = 0;
        _bScrubShift = 0;
        _sync?.Forget();
    }

    // Sueltos, como en A pero sin bucles: se para en el primer y el último frame de B.
    private void OnCompareDragMoved(int dx)
    {
        if (_sync is { Following: true })
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
        if (_sync is { Following: true })
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
        if (_compare is not { Ready: true } pane || _sync is { Following: true } || _store.Settings.WheelOnVideo == Data.Settings.WheelVolume)
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
        UpdateCompareHold();
        if (sync.Following)
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
        // Alt + , / Alt + .: un frame solo el activo (bloqueados, el desfase cambia en lo mismo).
        if (modifiers == ModifierKeys.Alt && key is Key.OemComma or Key.OemPeriod)
        {
            if (_compareActiveB)
                sync.NudgeB(key == Key.OemPeriod ? 1 : -1);
            else
                sync.NudgeA(key == Key.OemPeriod ? 1 : -1);
            return true;
        }
        // Con Alt pulsado el candado está abierto, pero Alt + ← / → sigue siendo el salto largo de los dos juntos.
        if (sync.Held && modifiers.HasFlag(ModifierKeys.Alt) && key is Key.Left or Key.Right)
        {
            var jump = SeekStep(modifiers);
            SeekRelativeWithCompare(key == Key.Left ? -jump : jump);
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

    // ---- Alt: el candado se abre mientras está pulsado ----

    /// <summary>Para la autoprueba: Alt pulsado (true / false) sin teclado de verdad.</summary>
    private bool? _testAlt;

    private bool CompareAltDown => _testAlt ?? (IsActive && GetAsyncKeyState(0x12 /* VK_MENU */) < 0);

    /// <summary>
    /// Bloqueados, mientras Alt está pulsado el candado se abre (cada arrastre mueve solo su vídeo); al soltarlo se
    /// vuelve a bloquear con el desfase nuevo. Si se suelta con un botón pulsado, el arrastre sigue suelto hasta soltarlo.
    /// </summary>
    private void UpdateCompareHold()
    {
        if (_sync is not { Locked: true } sync)
            return;
        if (CompareAltDown)
        {
            if (sync.Held)
                return;
            sync.Hold();
            // Soltar Alt no abre el menú de la ventana (como tras Alt + rueda).
            _altUsedWithWheel = true;
        }
        else if (sync.Held && !MouseButtonHeld && !_cmpBar.IsMouseCaptured)
        {
            sync.Release();
        }
    }

    /// <summary>Salto relativo de A (← → y rueda en la barra); con Alt pulsado comparando, también de B (los dos juntos).</summary>
    private void SeekRelativeWithCompare(double seconds)
    {
        _nav.SeekRelative(seconds);
        if (_sync is { Held: true } && _compare is { Ready: true } pane)
            pane.Nav.SeekRelative(seconds);
    }

    // ---- Menús ----

    /// <summary>Al final del menú de la imagen: comparar con otro vídeo o con otra parte de este y, comparando, el candado y cerrar.</summary>
    private void AddCompareMenuItems(ItemsControl menu)
    {
        menu.Items.Add(new Separator());
        var other = WithIcon(new MenuItem { Header = S.CompareWithOtherVideo, InputGestureText = $"{S.KeyCtrl}+{S.KeyShift}+O" }, "CompareMenuIcon");
        other.Click += (_, _) => ShowCompareDialog();
        menu.Items.Add(other);
        var same = WithIcon(new MenuItem { Header = S.CompareWithThisVideo }, "CompareSameIcon");
        same.Click += (_, _) => CompareWithCurrentFrame();
        menu.Items.Add(same);
        if (_compare == null || _sync == null)
            return;
        var locked = WithIcon(new MenuItem { Header = S.CompareLockMenu, InputGestureText = $"{S.KeyCtrl}+L", IsEnabled = _compare.Ready },
            _sync.Locked ? "LockClosedIcon" : "LockOpenIcon");
        locked.Click += (_, _) => ToggleCompareLock();
        menu.Items.Add(locked);
        var place = WithIcon(new MenuItem { Header = S.ComparePlacementMenu }, "CompareMenuIcon");
        foreach (var (key, header) in new[] { ("left", S.ComparePlaceLeft), ("right", S.ComparePlaceRight), ("top", S.ComparePlaceTop), ("bottom", S.ComparePlaceBottom) })
        {
            var option = new MenuItem { Header = header, IsCheckable = true, IsChecked = _store.Settings.ComparePlacement == key };
            option.Click += (_, _) => SetComparePlacement(key);
            place.Items.Add(option);
        }
        menu.Items.Add(place);
        var close = WithIcon(new MenuItem { Header = S.CloseCompare }, "CompareCloseIcon");
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

    // ---- Arrastrar un vídeo sobre la ventana: las zonas «Comparar» ----

    /// <summary>
    /// Mientras se arrastra un vídeo con otro abierto, en lugar de la imagen salen cinco zonas: una en cada borde
    /// («Comparar», con B a ese lado) y la del centro («Abrir», como siempre). La imagen es una ventana nativa y no se
    /// puede pintar encima: se oculta mientras dura el arrastre.
    /// </summary>
    private void BuildDropZones()
    {
        _cmpDropOverlay = new Grid { Visibility = Visibility.Collapsed, Background = Brushes.Black };
        var edge = new GridLength(0.24, GridUnitType.Star);
        _cmpDropOverlay.ColumnDefinitions.Add(new ColumnDefinition { Width = edge });
        _cmpDropOverlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.52, GridUnitType.Star) });
        _cmpDropOverlay.ColumnDefinitions.Add(new ColumnDefinition { Width = edge });
        _cmpDropOverlay.RowDefinitions.Add(new RowDefinition { Height = edge });
        _cmpDropOverlay.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.52, GridUnitType.Star) });
        _cmpDropOverlay.RowDefinitions.Add(new RowDefinition { Height = edge });
        Add("left", S.ComparePlaceLeft, 0, 0, 1, 3);
        Add("right", S.ComparePlaceRight, 2, 0, 1, 3);
        Add("top", S.ComparePlaceTop, 1, 0, 1, 1);
        Add("bottom", S.ComparePlaceBottom, 1, 2, 1, 1);
        Add(null, S.CompareDropOpenDetail, 1, 1, 1, 1);
        Grid.SetRow(_cmpDropOverlay, 0);
        Grid.SetColumn(_cmpDropOverlay, 0);
        Panel.SetZIndex(_cmpDropOverlay, 5);
        RootGrid.Children.Add(_cmpDropOverlay);

        void Add(string? placement, string detail, int column, int row, int columns, int rows)
        {
            var accent = (Brush)FindResource("Accent");
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(8) };
            if (placement != null)
                content.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = (Geometry)FindResource("CompareMenuIcon"), Stroke = accent, StrokeThickness = 1.4, Width = 19, Height = 17,
                    LayoutTransform = new ScaleTransform(1.5, 1.5), HorizontalAlignment = HorizontalAlignment.Center, StrokeLineJoin = PenLineJoin.Round,
                    Margin = new Thickness(0, 0, 0, 6),
                });
            content.Children.Add(new TextBlock
            {
                Text = placement != null ? S.CompareDropHint : S.CompareDropOpen, Foreground = placement != null ? accent : (Brush)FindResource("Fg"),
                FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            });
            content.Children.Add(new TextBlock
            {
                Text = detail, Foreground = (Brush)FindResource("FgMuted"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
            });
            var zone = new Border
            {
                Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Margin = new Thickness(4), Child = content,
            };
            Grid.SetColumn(zone, column);
            Grid.SetRow(zone, row);
            Grid.SetColumnSpan(zone, columns);
            Grid.SetRowSpan(zone, rows);
            _cmpDropOverlay.Children.Add(zone);
            _cmpDropZones.Add((zone, placement));
        }
    }

    // La zona bajo el ratón: (true, lado) en un borde, (true, null) en el centro, (false, _) fuera de las zonas.
    private (bool Over, string? Placement) DropZoneAt(DragEventArgs e)
    {
        if (!_cmpDropOverlay.IsVisible)
            return (false, null);
        foreach (var (zone, placement) in _cmpDropZones)
        {
            var p = e.GetPosition(zone);
            if (p.X >= 0 && p.X <= zone.ActualWidth && p.Y >= 0 && p.Y <= zone.ActualHeight)
                return (true, placement);
        }
        return (false, null);
    }

    private void ShowDropZones(bool show, DragEventArgs? e = null)
    {
        if (show != _cmpDropOverlay.IsVisible)
        {
            if (show)
            {
                (_dropHiddenA, _dropHiddenB) = (VideoArea.Visibility, _compare?.Placeholder.Visibility ?? Visibility.Visible);
                VideoArea.Visibility = Visibility.Hidden;
                if (_compare != null)
                    _compare.Placeholder.Visibility = Visibility.Hidden;
            }
            else
            {
                VideoArea.Visibility = _dropHiddenA;
                if (_compare != null)
                    _compare.Placeholder.Visibility = _dropHiddenB;
            }
            _cmpDropOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
        if (!show)
            return;
        var (over, placement) = e != null ? DropZoneAt(e) : (false, null);
        foreach (var (zone, p) in _cmpDropZones)
        {
            var hot = over && p == placement;
            zone.Background = hot ? DropStripHoverBrush : (Brush)FindResource("Panel");
            zone.BorderBrush = hot ? (Brush)FindResource("Accent") : (Brush)FindResource("Line");
        }
    }

    private void OnCompareDragOver(object sender, DragEventArgs e)
    {
        _lastCompareDragOver = Environment.TickCount64;
        var video = _currentPath != null && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files
            && !files.All(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        if (!video)
        {
            if (_cmpDropOverlay.IsVisible)
                ShowDropZones(false);
            return;
        }
        ShowDropZones(true, e);
    }

    // Al pasar de un elemento a otro también llega DragLeave: solo se quitan si no sigue llegando DragOver.
    private async void OnCompareDragLeave(object sender, DragEventArgs e)
    {
        var at = Environment.TickCount64;
        await Task.Delay(150);
        if (_lastCompareDragOver <= at && _cmpDropOverlay.IsVisible)
            ShowDropZones(false);
    }

    private void OnComparePreviewDrop(object sender, DragEventArgs e)
    {
        var (over, placement) = DropZoneAt(e);
        _dropPlacement = over ? placement : null;
        if (_cmpDropOverlay.IsVisible)
            ShowDropZones(false);
    }

    /// <summary>Soltado en una zona de un borde: se abre como B a ese lado. False si no (se abre como siempre).</summary>
    private bool DropOnCompareStrip(DragEventArgs e, string file)
    {
        if (_dropPlacement is not { } placement)
            return false;
        _dropPlacement = null;
        OpenCompare(file, 0, placement);
        return true;
    }
}
