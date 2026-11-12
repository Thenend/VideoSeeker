using System.IO;
using System.Windows;
using VideoSeeker.App.Controls;
using VideoSeeker.App.Data;
using VideoSeeker.Inspect;

namespace VideoSeeker.App;

/// <summary>Ventana de ajustes (Ctrl + , o el engranaje) y aplicar lo que cambia en ella.</summary>
public partial class MainWindow
{
    private SettingsWindow? _settingsWindow;
    // Lo último que se le pasó a mpv, para no reiniciar su decodificador si no cambia.
    private bool? _appliedHardwareDecoding;
    private bool? _appliedTimesInList;

    private void OpenSettings()
    {
        if (_settingsWindow is { } open)
        {
            open.Activate();
            return;
        }
        HoverPopup.IsOpen = false;
        var window = _settingsWindow = new SettingsWindow(_store, ApplySettings, () => App.Restart(_currentPath), GpuDevice.DefaultAdapter())
        {
            Owner = this,
        };
        window.Closed += (_, _) =>
        {
            _settingsWindow = null;
            Activate();
            ReturnFocusToVideo();
        };
        window.Show();
    }

    /// <summary>Aplica los ajustes guardados (al arrancar y con cada cambio en la ventana).</summary>
    private void ApplySettings()
    {
        var s = _store.Settings;
        foreach (var bar in new TimelineBar[] { Seek, Zoom })
        {
            bar.MoveWholeRegions = s.MoveWholeRegions;
            bar.ShowCachedFrames = s.ShowCachedFrames;
        }
        if (Seek.ShowBookmarks != s.ShowBookmarkMarks)
            ApplyMarksVisibility();
        // Sin animar si en Windows están quitadas (Accesibilidad > Efectos visuales) y en las autopruebas, que miran
        // la ventana justo después de abrir o cerrar algo (la de los bookmarks las prueba aparte).
        Reveal.Animate = s.AnimatePanels && SystemParameters.ClientAreaAnimation
            && Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == null;

        // Idiomas y subtítulos: al abrir el siguiente vídeo.
        _player.AudioLanguages = Languages.MpvList(s.AudioLanguage);
        _player.SubtitleLanguages = Languages.MpvList(s.SubtitleLanguage);
        _player.SubtitlesOnOpen = s.SubtitlesOnOpen;
        // Al arrancar ya se la pasó App al inicializar mpv; después, en caliente.
        if (_appliedHardwareDecoding != null && _appliedHardwareDecoding != s.HardwareDecoding)
            _player.SetHardwareDecoding(s.HardwareDecoding);
        _appliedHardwareDecoding = s.HardwareDecoding;
        // La caché de frames: al abrir el siguiente vídeo.
        if (_engine != null)
            _engine.BudgetBytes = s.FrameCacheMegabytes * 1024L * 1024;

        if (_appliedTimesInList != null && _appliedTimesInList != s.BookmarkTimesInList)
        {
            HideRowPreview();
            RefreshRows();
        }
        _appliedTimesInList = s.BookmarkTimesInList;

        if (TimeFormat.UseFrames != s.TimeWithFrames)
        {
            TimeFormat.UseFrames = s.TimeWithFrames;
            UpdateTimeText();
            UpdateZoomUi();
            RefreshRows();
            Seek.InvalidateVisual();
            Zoom.InvalidateVisual();
        }
    }

    public static bool SettingsSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "settings";

    /// <summary>
    /// VIDEOSEEKER_SELFTEST=settings: valores por defecto de los ajustes nuevos, la ventana (captura en
    /// selftest-ajustes.png), que con "mover regiones" desactivado arrastrar el centro del tramo no lo mueve y
    /// hace scrubbing, que activado sí lo mueve, y el timecode. Deja los ajustes como estaban.
    /// </summary>
    private async Task RunSettingsSelfTest()
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

        while (_duration <= 0)
            await Task.Delay(100);
        var settings = _store.Settings;
        var saved = System.Text.Json.JsonSerializer.Serialize(settings, DataStore.Json);
        try
        {
            await Task.Delay(1000);
            var d = new Settings();
            Check(!d.MoveWholeRegions, "por defecto no se mueven las regiones enteras");
            Check(d.BookmarkPanelWidth == 330 && BookmarkPanel.Width == settings.BookmarkPanelWidth, $"panel de bookmarks: 330 px por defecto, ahora {BookmarkPanel.Width}");
            Check(d.ShowDraggedEdgeFrame, "por defecto se ve el frame del extremo que se arrastra");
            Check(d.HoverPreview, "por defecto se ve la miniatura al pasar el ratón por las barras");
            Check(!d.ShowCachedFrames, "por defecto no se ve la línea verde");
            Check(d.ShowBookmarkMarks, "por defecto se ven las marcas de los bookmarks");
            Check(d.PulseHiddenMarks, "por defecto, con las marcas ocultas, su botón late al crear un bookmark o ir a uno");
            Check(d.AnimatePanels, "por defecto el panel de bookmarks y la fila del zoom se abren animados");
            Check(d.SeekSeconds == 5 && d.SeekSecondsShift == 1 && d.SeekSecondsAlt == 30, "saltos 5 / 1 / 30 s");
            Check(d.ScrubPixelsPerFrame == 6, "scrubbing 6 px por frame");
            Check(d.WheelOnVideo == Settings.WheelSpeed, "la rueda sobre el vídeo cambia la velocidad");
            Check(d.ResumePlayback, "reanudar sí");
            Check(!d.TimeWithFrames, "tiempo con milisegundos");
            Check(d.BookmarkTimesInList, "tiempos de los bookmarks en la lista");
            Check(d.NewBookmarkColor == BookmarkColors.Default, "color de los nuevos: el rosa");
            Check(d.FrameCacheMegabytes == 0 && d.HardwareDecoding, "caché automática y decodificación por hardware");
            Check(!d.SubtitlesOnOpen && d.AudioLanguage == Languages.Windows && d.SubtitleLanguage == Languages.Windows,
                $"subtítulos al abrir no; idiomas los de Windows («{Languages.Windows}», {System.Globalization.CultureInfo.CurrentUICulture.Name})");
            var oldLanguage = System.Text.Json.JsonSerializer.Deserialize<Settings>("{\"audioLanguages\": \"\"}", DataStore.Json)!;
            Check(oldLanguage.AudioLanguage == Languages.Windows, "el «sin preferencia» guardado por las primeras versiones pasa al de Windows");
            var oldJson = System.Text.Json.JsonSerializer.Deserialize<Settings>("{\"subtitlesOnOpen\": true}", DataStore.Json)!;
            Check(!oldJson.SubtitlesOnOpen, "el «sí» guardado por la primera versión no cuenta (vuelve a desactivado)");

            // Idiomas: códigos y variantes para mpv; el texto libre de antes se reconoce o se queda sin preferencia.
            Check(Languages.Normalize("es,en") == "spa" && Languages.Normalize("jpn") == "jpn" && Languages.Normalize("klingon") == "", "idiomas antiguos en texto");
            Check(Languages.MpvList("spa").Split(',').Contains("es-419") && Languages.MpvList("") == "", $"variantes del español: {Languages.MpvList("spa")}");
            Check(Languages.All.Select(l => l.Name).SequenceEqual(Languages.All.Select(l => l.Name).Order(StringComparer.Create(new System.Globalization.CultureInfo("es-ES"), true))),
                "idiomas por orden alfabético");

            // Carpeta de datos: copia y comprobaciones en una carpeta temporal (nunca con los datos de verdad).
            var temp = Path.Combine(Path.GetTempPath(), "VideoSeeker-selftest-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var (a, b) = (Path.Combine(temp, "a"), Path.Combine(temp, "b"));
                Directory.CreateDirectory(Path.Combine(a, "bookmarks", "General"));
                File.WriteAllText(Path.Combine(a, "settings.json"), "{}");
                File.WriteAllText(Path.Combine(a, "bookmarks", "General", "x.json"), "{\"video\": \"x\"}");
                Check(DataStore.HasData(a) && !DataStore.HasData(b), "reconoce una carpeta con datos");
                DataStore.CopyData(a, b);
                Check(File.ReadAllText(Path.Combine(b, "bookmarks", "General", "x.json")).Contains("\"x\"") && File.Exists(Path.Combine(a, "settings.json")),
                    "copiar los datos a una carpeta vacía (el origen se queda)");
                File.WriteAllText(Path.Combine(b, "settings.json"), "{\"volume\": 7}");
                DataStore.CopyData(a, b);
                Check(Directory.GetFiles(Path.Combine(b, "anteriores"), "settings.json", SearchOption.AllDirectories).Length == 1
                    && File.ReadAllText(Path.Combine(b, "settings.json")) == "{}", "copiar encima de otros datos los aparta a «anteriores»");
                Check(DataStore.Nested(a, Path.Combine(a, "sub")) && !DataStore.Nested(a, b), "no deja elegir una carpeta dentro de la actual");
                // datalocation.json solo si no había uno (para no tocar una elección de Mario).
                if (DataStore.ReadLocation() == null)
                {
                    DataStore.WriteLocation(b, _store.Folder);
                    var written = DataStore.ReadLocation();
                    Check(written != null && DataStore.SamePath(written.Folder, b) && written.CopyFrom == _store.Folder, "apunta la carpeta elegida y de dónde copiar");
                    DataStore.WriteLocation(DataStore.DefaultFolder(), null);
                    Check(DataStore.ReadLocation() == null, "volver a la de por defecto quita la elección");
                }
            }
            finally
            {
                try { Directory.Delete(temp, recursive: true); } catch (IOException) { }
            }

            // Con los valores por defecto (sin tocar los demás).
            settings.MoveWholeRegions = false;
            settings.ShowCachedFrames = false;
            ApplySettings();
            Check(!Seek.ShowCachedFrames && !Zoom.ShowCachedFrames, "las barras no pintan la línea verde");

            // Arrastrar el centro de la banda del tramo: con el ajuste desactivado, no lo mueve y hace scrubbing.
            var range = (_duration * 0.4, _duration * 0.4 + Math.Min(10, _duration * 0.05));
            SetFreeZoom(range);
            await Task.Delay(300);
            UpdateLayout();
            var before = _zoomRange;
            var from = Seek.ZoomBandCenter();
            var to = new Point(from.X + Seek.ActualWidth * 0.1, from.Y);
            Seek.SimulateLeftDrag(from, to);
            await Task.Delay(500);
            var expected = _duration * (to.X - 8) / Math.Max(1, Seek.ActualWidth - 16);
            Check(_zoomRange == before, $"desactivado: el tramo no se mueve ({before?.Start:0.00}–{before?.End:0.00} → {_zoomRange?.Start:0.00}–{_zoomRange?.End:0.00})");
            Check(Math.Abs(_nav.CurrentTime - expected) < Math.Max(1, _duration * 0.01),
                $"desactivado: la imagen va a donde se suelta ({_nav.CurrentTime:0.00} s, esperado {expected:0.00} s)");

            settings.MoveWholeRegions = true;
            ApplySettings();
            from = Seek.ZoomBandCenter();
            Seek.SimulateLeftDrag(from, new Point(from.X + Seek.ActualWidth * 0.1, from.Y));
            await Task.Delay(300);
            Check(_zoomRange is { } moved && before is { } was && moved.Start > was.Start + 1, $"activado: el tramo se mueve entero ({_zoomRange?.Start:0.00}–{_zoomRange?.End:0.00})");
            settings.MoveWholeRegions = false;
            ApplySettings();

            // Arrastrar las banderitas (en pausa): con el ajuste, la imagen va al primer / último frame del tramo; sin él, no se mueve.
            _nav.Pause();
            settings.ShowDraggedEdgeFrame = true;
            SetFreeZoom(range);
            await Task.Delay(300);
            from = Seek.ZoomFlagPoint(end: false);
            Seek.SimulateLeftDrag(from, new Point(from.X - Seek.ActualWidth * 0.05, from.Y));
            // En vídeos pesados la imagen persigue al cursor por orden: tarda más en llegar.
            for (var i = 0; i < 40 && Zoom.Grid.FrameAt(_nav.CurrentTime) != Zoom.Grid.FrameAt(_zoomRange!.Value.Start); i++)
                await Task.Delay(100);
            var grid = Zoom.Grid;
            Check(_zoomRange is { } wider && wider.Start < range.Item1 - 1 && grid.FrameAt(_nav.CurrentTime) == grid.FrameAt(wider.Start),
                $"con el frame del extremo: el inicio enseña su frame ({_nav.CurrentTime:0.000} s, inicio {_zoomRange?.Start:0.000} s)");
            from = Seek.ZoomFlagPoint(end: true);
            Seek.SimulateLeftDrag(from, new Point(from.X + Seek.ActualWidth * 0.05, from.Y));
            await Task.Delay(800);
            Check(_zoomRange is { } longer && grid.FrameAt(_nav.CurrentTime) == grid.FrameAt(longer.End - 1e-4),
                $"con el frame del extremo: el fin enseña el último frame ({_nav.CurrentTime:0.000} s, fin {_zoomRange?.End:0.000} s)");
            settings.ShowDraggedEdgeFrame = false;
            SetFreeZoom(range);
            _nav.SeekTo(range.Item1 + 1);
            await Task.Delay(800);
            var still = _nav.CurrentTime;
            from = Seek.ZoomFlagPoint(end: false);
            Seek.SimulateLeftDrag(from, new Point(from.X - Seek.ActualWidth * 0.05, from.Y));
            from = Seek.ZoomFlagPoint(end: true);
            Seek.SimulateLeftDrag(from, new Point(from.X + Seek.ActualWidth * 0.05, from.Y));
            await Task.Delay(500);
            Check(_zoomRange is { } changed && changed.Start < range.Item1 - 1 && Math.Abs(_nav.CurrentTime - still) < 1e-3,
                $"sin el frame del extremo: el tramo cambia y la imagen no ({still:0.000} → {_nav.CurrentTime:0.000} s)");
            settings.ShowDraggedEdgeFrame = true;
            CloseZone();

            // Doble clic sobre la imagen en pausa: los dos clics (reanudar y pausar) dejan el mismo frame y en pausa.
            _nav.Pause();
            _nav.SeekTo(_duration * 0.6);
            await Task.Delay(1500);
            var clickFrom = _nav.CurrentTime;
            for (var i = 0; i < 3; i++)
            {
                OnVideoClicked();
                await Task.Delay(120 + 60 * i);
                UndoVideoClick();
                await Task.Delay(2000);
                Check(_nav.Paused && Math.Abs(_nav.CurrentTime - clickFrom) < 1.5 / Fps,
                    $"doble clic ({120 + 60 * i} ms): sigue en pausa en el mismo frame ({clickFrom:0.000} → {_nav.CurrentTime:0.000} s)");
            }

            // Doble clic de verdad (mensajes del ratón a la ventana de la imagen) desde una ventana pequeña: al pasar a
            // pantalla completa la ventana crece bajo el botón aún pulsado y eso no debe contar como arrastre.
            var placementBefore = (WindowState, Left, Top, Width, Height);
            WindowState = WindowState.Normal;
            (Left, Top, Width, Height) = (200, 150, 900, 600);
            await Task.Delay(1500);
            var dblFrom = _nav.CurrentTime;
            for (var i = 0; i < 2; i++)
            {
                // El clic, donde está el ratón de verdad (sin moverlo): Windows manda movimientos con su posición real al
                // cambiar la ventana de tamaño.
                GetCursorPosition(out var screen);
                var client = screen;
                ScreenToClient(_surface.Handle, ref client);
                SendMessage(_surface.Handle, 0x0201, 1, MakeLParam(client)); // WM_LBUTTONDOWN
                SendMessage(_surface.Handle, 0x0202, 0, MakeLParam(client)); // WM_LBUTTONUP
                await Task.Delay(100);
                SendMessage(_surface.Handle, 0x0203, 1, MakeLParam(client)); // WM_LBUTTONDBLCLK: pantalla completa
                await Task.Delay(800);
                client = screen;
                ScreenToClient(_surface.Handle, ref client);
                SendMessage(_surface.Handle, 0x0200, 1, MakeLParam(client)); // WM_MOUSEMOVE en el mismo punto de la pantalla
                SendMessage(_surface.Handle, 0x0202, 0, MakeLParam(client));
                await Task.Delay(2000);
                Check(_nav.Paused && Math.Abs(_nav.CurrentTime - dblFrom) < 1.5 / Fps,
                    $"doble clic real {(i == 0 ? "a pantalla completa" : "de vuelta a ventana")}: mismo frame ({dblFrom:0.000} → {_nav.CurrentTime:0.000} s)");
            }

            // Dos clics seguidos sin WM_LBUTTONDBLCLK (en pausa, Windows no los junta: el primero reanuda y la ventana del
            // motor se va detrás de la de mpv): también son doble clic, a pantalla completa y de vuelta. Con PostMessage
            // cada mensaje lleva su hora, como los del ratón.
            async Task PostClicks(int dragPixels)
            {
                GetCursorPosition(out var screen);
                var client = screen;
                ScreenToClient(_surface.Handle, ref client);
                var moved = client;
                moved.X += dragPixels;
                PostMessage(_surface.Handle, 0x0201, 1, MakeLParam(client));
                if (dragPixels != 0)
                    PostMessage(_surface.Handle, 0x0200, 1, MakeLParam(moved));
                PostMessage(_surface.Handle, 0x0202, 0, MakeLParam(client));
                await Task.Delay(100);
                PostMessage(_surface.Handle, 0x0201, 1, MakeLParam(client));
                PostMessage(_surface.Handle, 0x0202, 0, MakeLParam(client));
                await Task.Delay(1500);
            }
            for (var i = 0; i < 2; i++)
            {
                _nav.Pause();
                await Task.Delay(800);
                var pairFrom = _nav.CurrentTime;
                await PostClicks(0);
                Check((_beforeFullscreen != null) == (i == 0) && _nav.Paused && Math.Abs(_nav.CurrentTime - pairFrom) < 1.5 / Fps,
                    $"dos clics en pausa sin WM_LBUTTONDBLCLK: {(i == 0 ? "a pantalla completa" : "de vuelta a ventana")} en el mismo frame " +
                    $"({pairFrom:0.000} → {_nav.CurrentTime:0.000} s)");
            }
            // Un arrastre y un clic al momento no son doble clic.
            _nav.Pause();
            await Task.Delay(800);
            await PostClicks(60);
            Check(_beforeFullscreen == null, "arrastre y clic seguidos: no cambia la pantalla completa");
            if (_beforeFullscreen != null)
                ToggleFullscreen();
            _nav.Pause();
            (Left, Top, Width, Height) = (placementBefore.Left, placementBefore.Top, placementBefore.Width, placementBefore.Height);
            WindowState = placementBefore.WindowState;
            await Task.Delay(800);

            // Scrubbing sobre la imagen: al soltar sigue como estaba (reproduciendo o en pausa).
            foreach (var playing in new[] { true, false })
            {
                if (playing == _nav.Paused)
                    _nav.TogglePlay();
                await Task.Delay(800);
                GetCursorPosition(out var at);
                var p0 = at;
                ScreenToClient(_surface.Handle, ref p0);
                var p1 = p0;
                p1.X += 60;
                var scrubFrom = _nav.CurrentTime;
                SendMessage(_surface.Handle, 0x0201, 1, MakeLParam(p0)); // WM_LBUTTONDOWN
                SendMessage(_surface.Handle, 0x0200, 1, MakeLParam(p1)); // arrastre de 60 px: 10 frames
                await Task.Delay(300);
                var scrubbed = _nav.CurrentTime;
                SendMessage(_surface.Handle, 0x0202, 0, MakeLParam(p0)); // WM_LBUTTONUP
                await Task.Delay(1200);
                Check(_nav.Paused == !playing && Math.Abs(scrubbed - scrubFrom) > 3 / Fps,
                    $"scrubbing {(playing ? "reproduciendo" : "en pausa")}: al soltar sigue {(_nav.Paused ? "en pausa" : "reproduciendo")} ({scrubFrom:0.000} → {scrubbed:0.000} s)");
            }

            // Velocidad: la rueda sobre el vídeo de 0,1 en 0,1; se mantiene al pausar; en ámbar si no es 1×.
            settings.WheelOnVideo = Settings.WheelSpeed;
            OnVideoWheel(120);
            OnVideoWheel(60);
            OnVideoWheel(60);
            Check(Math.Abs(_nav.Rate - 1.2) < 1e-9, $"dos muescas (una en dos trozos): {_nav.Rate}×");
            // Girando la rueda en otro sitio (una barra), las muescas que se escapan a la imagen no cambian la velocidad.
            _lastWheelElsewhere = Environment.TickCount64;
            OnVideoWheel(120);
            OnVideoWheel(120);
            var strayRate = _nav.Rate;
            _lastWheelElsewhere = long.MinValue / 2;
            Check(Math.Abs(strayRate - 1.2) < 1e-9, $"rueda sobre la imagen justo después de girarla en una barra: no cambia la velocidad ({strayRate}×)");
            _nav.TogglePlay();
            await Task.Delay(800);
            _nav.Forward();
            Check(Math.Abs(_nav.Speed - 2.4) < 1e-9, $"L parte de la elegida: {_nav.Speed}×");
            _nav.Pause();
            await Task.Delay(500);
            Check(Math.Abs(_nav.Rate - 1.2) < 1e-9 && SpeedText.Text == SpeedLabel(1.2) && SpeedButton.Background != System.Windows.Media.Brushes.Transparent,
                $"al pausar vuelve a la elegida, resaltada: «{SpeedText.Text}»");
            for (var i = 0; i < 60; i++)
                OnVideoWheel(-120);
            Check(_nav.Rate == -19 && SpeedText.Text == SpeedLabel(-19), $"bajando pasa de 0,1× a −0,1× y sigue, con pasos más grandes: {SpeedText.Text}");
            (double From, int Steps, double To)[] steps = [(1, 1, 1.1), (2, 1, 2.25), (2, -1, 1.9), (4, 1, 4.5), (4, -1, 3.75), (10, 1, 11), (10, -1, 9.5),
                (20, 1, 20), (0.1, -1, -0.1), (-0.1, 1, 0.1), (-2, -1, -2.25), (1.05, -1, 1), (-20, -3, -20)];
            var wrong = steps.Where(t => Math.Abs(Navigator.StepRate(t.From, t.Steps) - t.To) > 1e-9).Select(t => $"{t.From}{t.Steps:+0;-0}→{Navigator.StepRate(t.From, t.Steps)}").ToList();
            Check(wrong.Count == 0, "pasos de la rueda: 0,1 hasta 2×, 0,25 hasta 4×, 0,5 hasta 10×, 1 hasta 20× " + string.Join(", ", wrong));
            SetRate(-3);
            _nav.SeekTo(_duration / 2);
            await Task.Delay(500);
            var reverseFrom = _nav.CurrentTime;
            _nav.TogglePlay();
            await Task.Delay(1000);
            Check(_nav.Reversing && _nav.Speed == -3 && _nav.CurrentTime < reverseFrom - 1, $"play con velocidad negativa: hacia atrás ({reverseFrom:0.00} → {_nav.CurrentTime:0.00} s)");
            SetRate(0.5);
            await Task.Delay(300);
            Check(!_nav.Reversing && _nav.Speed == 0.5, $"pasar a positiva reproduciendo: hacia delante a {_nav.Speed}×");
            _nav.Pause();
            for (var i = 0; i < 300; i++)
                OnVideoWheel(120);
            Check(_nav.Rate == 20, $"máximo {_nav.Rate}×");
            ResetRate();
            // El aviso, centrado abajo sobre la imagen.
            ShowToast("Velocidad 1,5×");
            await Task.Delay(200);
            var toastSize = ((FrameworkElement)ToastPopup.Child).ActualWidth;
            Check(ToastPopup.IsOpen && Math.Abs(ToastPopup.HorizontalOffset + toastSize / 2 - VideoArea.ActualWidth / 2) < 2
                    && ToastPopup.VerticalOffset > VideoArea.ActualHeight * 0.75,
                $"aviso centrado abajo: x {ToastPopup.HorizontalOffset:0} (ancho {toastSize:0} de {VideoArea.ActualWidth:0}), y {ToastPopup.VerticalOffset:0} de {VideoArea.ActualHeight:0}");
            SaveElementSnapshot((FrameworkElement)ToastPopup.Child, Path.Combine(_store.Folder, "selftest-aviso-video.png"));
            // Otro aviso encima no lo cierra y reabre (parpadeaba); al acabar se desvanece y se cierra.
            var reopened = false;
            void OnClosed(object? sender, EventArgs e) => reopened = true;
            ToastPopup.Closed += OnClosed;
            ShowToast("Velocidad 1,75×");
            await Task.Delay(100);
            ToastPopup.Closed -= OnClosed;
            Check(!reopened && ToastPopup.IsOpen, "cambiar el texto del aviso no lo cierra");
            await Task.Delay(1700);
            var fading = ((FrameworkElement)ToastPopup.Child).Opacity;
            await Task.Delay(500);
            Check(fading is > 0 and < 1 && !ToastPopup.IsOpen, $"se desvanece (opacidad {fading:0.00} a mitad) y se cierra");
            // Un menú abierto desde un botón de iconos no hereda su fuente (salían cuadraditos en el de pistas).
            var tracksMenu = new System.Windows.Controls.ContextMenu { PlacementTarget = TracksButton };
            tracksMenu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Audio" });
            tracksMenu.IsOpen = true;
            await Task.Delay(200);
            Check(!tracksMenu.FontFamily.Source.Contains("Icons"), $"fuente de los menús: {tracksMenu.FontFamily.Source}");
            tracksMenu.IsOpen = false;
            Log($"pistas: {string.Join(", ", _tracks.Select(t => t.Type))} → botón {(TracksButton.IsEnabled ? "activo" : "apagado")}");
            // Exportar bookmarks: el mismo aviso que los clips, con "Mostrar en la carpeta" y "Copiar".
            var exported = Path.Combine(Path.GetTempPath(), "VideoSeeker-selftest.bookmarks.json");
            File.WriteAllText(exported, "{}");
            ShowFileSavedPopup("3 bookmarks exportados", exported, "");
            await Task.Delay(300);
            Check(ClipPopup.IsOpen && ClipPopupName.Text == Path.GetFileName(exported) && (string)ClipPopupSecond.Content == "Copiar",
                $"aviso de bookmarks exportados: «{ClipPopupTitle.Text}», {ClipPopupName.Text}, botón «{ClipPopupSecond.Content}»");
            CloseClipPopup();
            File.Delete(exported);

            // Compartir: los bookmarks de un vídeo arrastrados se mezclan con él (por la huella) y una colección se añade
            // como nueva ("(2)" si ya existe); cada importación se deshace de una vez. Todo se deshace al acabar.
            if (_bookmarks is { } open)
            {
                var activeBefore = _store.Settings.BookmarkCollection;
                var countBefore = open.Items.Count;
                var shared = Path.Combine(Path.GetTempPath(), "VideoSeeker-selftest-compartir");
                Directory.CreateDirectory(shared);
                var marks = new List<Bookmark>
                {
                    new() { Id = "selftest-a", Name = "Compartido A", Start = 10, Modified = DateTime.Now },
                    new() { Id = "selftest-b", Name = "Compartido B", Start = 20, End = 25, Modified = DateTime.Now },
                };
                var single = Path.Combine(shared, "surf.bookmarks.json");
                DataStore.SaveFile(single, new BookmarkFile { Video = "otro nombre.mkv", Fingerprint = open.Fingerprint, Bookmarks = marks });
                ImportSharedFile(single);
                Check(_bookmarks.Items.Count == countBefore + 2 && _bookmarks.Find("selftest-b")?.End == 25,
                    $"bookmarks arrastrados: se añaden al vídeo por su huella ({countBefore} → {_bookmarks.Items.Count})");
                ImportSharedFile(single);
                Check(_bookmarks.Items.Count == countBefore + 2, "arrastrarlos otra vez no los duplica");
                UndoBookmarks(redo: false);
                Check(_bookmarks.Items.Count == countBefore, $"Ctrl + Z quita lo importado ({_bookmarks.Items.Count})");

                var collectionFile = Path.Combine(shared, "Selftest compartir.collection.json");
                DataStore.SaveFile(collectionFile, new BookmarkCollectionFile
                {
                    Collection = "Selftest compartir",
                    Videos =
                    [
                        new BookmarkFile { Video = "surf.mkv", Fingerprint = open.Fingerprint, Bookmarks = marks },
                        new BookmarkFile { Video = "otro.mp4", Fingerprint = "0000000000000000", Bookmarks = [new() { Id = "selftest-c", Name = "Otro", Start = 1 }] },
                    ],
                });
                ImportSharedFile(collectionFile);
                Check(_store.Settings.BookmarkCollection == "Selftest compartir" && _bookmarks!.Items.Count == 2,
                    $"colección arrastrada: se añade y pasa a ser la activa ({_store.Settings.BookmarkCollection}, {_bookmarks?.Items.Count} en este vídeo)");
                var roundTrip = Path.Combine(shared, "vuelta.collection.json");
                var written = WriteCollectionFile(roundTrip, "Selftest compartir");
                Check(written == (2, 3), $"exportar la colección: {written.Videos} vídeos y {written.Bookmarks} bookmarks");
                ImportSharedFile(roundTrip);
                Check(_store.Settings.BookmarkCollection == "Selftest compartir (2)", $"con el mismo nombre se llama «{_store.Settings.BookmarkCollection}»");
                UndoBookmarks(redo: false);
                UndoBookmarks(redo: false);
                Check(_store.Settings.BookmarkCollection == activeBefore && !BookmarkCollections.Exists(_store, "Selftest compartir")
                        && !BookmarkCollections.Exists(_store, "Selftest compartir (2)") && _bookmarks!.Items.Count == countBefore,
                    $"Ctrl + Z quita las colecciones importadas y vuelve a «{_store.Settings.BookmarkCollection}»");
                Directory.Delete(shared, recursive: true);

                // Región seleccionada, bien visible en las barras, con las banderitas de la zona de su color (captura en
                // selftest-region.png); se deshace al acabar.
                var region = _bookmarks!.Add("Región de prueba", _duration * 0.3, _duration * 0.45);
                _bookmarks.Add("Dentro", _duration * 0.37, null, BookmarkColors.Palette[4].Hex);
                Select(region.Id);
                await Task.Delay(300);
                Check(_zoomRange is { } rz && Math.Abs(rz.Start - region.Start) < 1e-3 && Math.Abs(rz.End - EndOf(region)) < 1e-3
                        && Seek.ZoomColor == ColorOf(region) && ZoneName.Text == region.Name,
                    "la región seleccionada es la zona: banderitas de su color y su nombre en la fila del zoom");
                SaveVisualSnapshot(Seek, Path.Combine(_store.Folder, "selftest-region.png"));
                // Y con un color oscuro (de etiqueta propia): región larga y de un frame, que se pinta con el triángulo
                // (selftest-region-dark*.png).
                var dark = _bookmarks.Add("Oscura", _duration * 0.55, _duration * 0.7, "#4A2E1C");
                var darkPin = _bookmarks.Add("Oscuro", _duration * 0.85, null, "#4A2E1C");
                Select(dark.Id);
                await Task.Delay(300);
                SaveVisualSnapshot(Seek, Path.Combine(_store.Folder, "selftest-region-dark.png"));
                Select(darkPin.Id);
                await Task.Delay(300);
                Check(Seek.IsTiny(darkPin) || _duration < 60, $"la región de un frame se pinta con el triángulo en la barra principal ({_duration:0} s)");
                SaveVisualSnapshot(Seek, Path.Combine(_store.Folder, "selftest-region-darkpin.png"));
                CloseZone();
                for (var i = 0; i < 4; i++)
                    UndoBookmarks(redo: false);
            }
            // El menú de velocidades, oscuro (captura en selftest-menu.png).
            var speedMenu = ShowSpeedMenu();
            await Task.Delay(400);
            SaveElementSnapshot(speedMenu, Path.Combine(_store.Folder, "selftest-menu.png"));
            speedMenu.IsOpen = false;
            Check(_nav.Rate == 1 && SpeedText.Text == "1×" && SpeedButton.Background == System.Windows.Media.Brushes.Transparent, "a 1×, sin resaltar");
            settings.WheelOnVideo = Settings.WheelVolume;
            var volume = settings.Volume;
            OnVideoWheel(volume >= 100 ? -120 : 120);
            await Task.Delay(500);
            Check(settings.Volume == Math.Clamp(Math.Round(volume / 5) * 5 + (volume >= 100 ? -5 : 5), 0, 100) && _nav.Rate == 1, $"con el ajuste de volumen, cambia el volumen ({volume} → {settings.Volume}) y no la velocidad");
            _player.SetVolume(volume);
            settings.WheelOnVideo = Settings.WheelSpeed;

            // Bucle del vídeo (R), sin tramo: el vídeo entero vuelve al principio al acabar.
            _loopVideo = _loopRange = false;
            ApplyLoop();
            ToggleVideoLoop();
            Check(_loopVideo && _loopFile && settings.Loop && !settings.LoopRange, "bucle del vídeo: el del vídeo entero, y se recuerda");
            _nav.SeekTo(Math.Max(0, _duration - 1));
            await Task.Delay(500);
            _nav.TogglePlay();
            await Task.Delay(2500);
            Check(_nav.CurrentTime < 2, $"el bucle del vídeo vuelve al principio ({_nav.CurrentTime:0.00} s)");
            // Con el bucle del vídeo, todo da la vuelta: saltos, frame a frame y marcha atrás.
            _nav.Pause();
            await Task.Delay(300);
            _nav.SeekTo(_duration - 2);
            await Task.Delay(500);
            _nav.SeekRelative(5);
            await Task.Delay(600);
            Check(_nav.CurrentTime is > 2 and < 4, $"bucle: +5 s cerca del final sigue por el principio ({_nav.CurrentTime:0.00} s)");
            _nav.SeekTo(0);
            await Task.Delay(500);
            StepFrame(-1);
            await Task.Delay(600);
            var lastFrame = LastFrame;
            Check(Zoom.Grid.FrameAt(_nav.CurrentTime) == lastFrame, $"bucle: frame atrás desde el primero va al último ({Zoom.Grid.FrameAt(_nav.CurrentTime)} de {lastFrame})");
            StepFrame(1);
            await Task.Delay(600);
            Check(Zoom.Grid.FrameAt(_nav.CurrentTime) == 0, $"bucle: frame adelante desde el último va al primero ({Zoom.Grid.FrameAt(_nav.CurrentTime)})");
            _nav.SeekTo(0.5);
            await Task.Delay(500);
            _nav.Backward();
            await Task.Delay(1500);
            Check(_nav.CurrentTime > _duration - 3, $"bucle: marcha atrás desde el principio sigue por el final ({_nav.CurrentTime:0.00} s de {_duration:0.00})");
            _nav.Pause();
            SetFreeZoom(range);
            Check(!RangeLooping && _loopFile && ReferenceEquals(RangeLoopButton.Foreground, FindResource("Fg")), "al ampliar un tramo sin su bucle, sigue el del vídeo");
            // Sin el bucle del tramo, reproducir dentro de él sigue más allá de su final.
            _nav.SeekTo(Math.Max(range.Item1, range.Item2 - 0.3));
            await Task.Delay(600);
            _nav.TogglePlay();
            await Task.Delay(1500);
            Check(_nav.CurrentTime > range.Item2, $"sin bucle del tramo, se sigue más allá de su final ({_nav.CurrentTime:0.00} s, tramo hasta {range.Item2:0.00})");
            _nav.Pause();
            await Task.Delay(300);
            // Bucle del tramo (Shift + R): estando fuera, empieza por su principio.
            ToggleRangeLoop();
            await Task.Delay(600);
            Check(RangeLooping && settings.LoopRange && settings.Loop && ReferenceEquals(RangeLoopButton.Foreground, FindResource("Accent"))
                && _nav.CurrentTime >= range.Item1 - 0.1 && _nav.CurrentTime < range.Item2,
                $"bucle del tramo: se recuerda y, desde fuera, va a su principio ({_nav.CurrentTime:0.00} s)");
            // Fuera del tramo: el play empieza por el tramo, y moverse frame a frame da la vuelta en el vídeo entero.
            _nav.SeekTo(range.Item2 + 20);
            await Task.Delay(600);
            _nav.TogglePlay();
            await Task.Delay(1000);
            Check(_nav.CurrentTime >= range.Item1 && _nav.CurrentTime < range.Item2, $"bucle del tramo: play desde fuera empieza por el tramo ({_nav.CurrentTime:0.00} s, tramo {range.Item1:0.00}–{range.Item2:0.00})");
            _nav.SeekTo(_duration - 1);
            await Task.Delay(3000);
            // Con los dos bucles, el del vídeo vuelve a empezar (o, si mpv se quedara al final, sigue por el tramo).
            Check(!_nav.Paused && (_nav.CurrentTime < 2.5 || _nav.CurrentTime >= range.Item1 && _nav.CurrentTime < range.Item2),
                $"los dos bucles: reproduciendo desde después del tramo, al llegar al final sigue ({_nav.CurrentTime:0.00} s)");
            _nav.Pause();
            await Task.Delay(300);
            _nav.SeekTo(0);
            await Task.Delay(500);
            StepFrame(-1);
            await Task.Delay(600);
            Check(Zoom.Grid.FrameAt(_nav.CurrentTime) == lastFrame, $"los dos bucles: fuera del tramo, frame atrás desde el primero va al último del vídeo ({Zoom.Grid.FrameAt(_nav.CurrentTime)})");
            // Solo con el bucle del tramo: fuera de él no se da la vuelta; dentro, sí.
            ToggleVideoLoop();
            Check(!_loopFile && RangeLooping, "quitar el bucle del vídeo deja el del tramo");
            _nav.SeekTo(0);
            await Task.Delay(500);
            StepFrame(-1);
            await Task.Delay(600);
            Check(Zoom.Grid.FrameAt(_nav.CurrentTime) == 0, $"solo bucle del tramo: fuera de él, frame atrás desde el primero se queda ({Zoom.Grid.FrameAt(_nav.CurrentTime)})");
            var (rangeFirst, rangeLast) = (Zoom.Grid.FrameAt(_zoomRange!.Value.Start), Zoom.Grid.FrameAt(_zoomRange.Value.End - 1e-4));
            _nav.SeekTo(Zoom.Grid.TimeOf(rangeFirst));
            await Task.Delay(500);
            StepFrame(-1);
            await Task.Delay(600);
            Check(Zoom.Grid.FrameAt(_nav.CurrentTime) == rangeLast, $"bucle del tramo: frame atrás desde su primero va a su último ({Zoom.Grid.FrameAt(_nav.CurrentTime)} de {rangeLast})");
            ToggleRangeLoop();
            CloseZone();
            Check(!_loopVideo && !_loopRange && !_loopFile && !settings.Loop && !settings.LoopRange, "quitar los dos bucles");
            _nav.Pause();
            UpdateLayout();
            SaveWindowSnapshot(this, Path.Combine(_store.Folder, "selftest-controles.png"));

            // Pantalla completa sin interfaz: los controles salen de la ventana y solo flotan encima al pedirlos.
            settings.PinFullscreenControls = false;
            UpdatePinButton();
            ToggleFullscreen();
            await Task.Delay(1000);
            Check(_floating && ControlsPopup.Child == ControlsHost && !RootGrid.Children.Contains(ControlsHost) && (!ControlsPopup.IsOpen || IsMouseInControlsZone())
                    && Math.Abs(VideoArea.ActualHeight - RootGrid.ActualHeight) < 1 && (PinButton.IsVisible == false || IsMouseInControlsZone()),
                $"pantalla completa: sin controles, la imagen ocupa todo ({VideoArea.ActualHeight:0} de {RootGrid.ActualHeight:0})");
            ShowFloatingControls();
            _mouseLeftControls = DateTime.UtcNow.AddSeconds(4); // que no se escondan durante la captura
            await Task.Delay(300);
            Check(ControlsPopup.IsOpen && Math.Abs(ControlsPopup.VerticalOffset + ControlsHost.ActualHeight - RootGrid.ActualHeight) < 2 && PinButton.IsVisible,
                $"los controles flotan abajo (y {ControlsPopup.VerticalOffset:0} + {ControlsHost.ActualHeight:0} de {RootGrid.ActualHeight:0}), con la chincheta");
            File.WriteAllText(Path.Combine(_store.Folder, "selftest-overlay.flag"), "");
            await Task.Delay(2500);
            File.Delete(Path.Combine(_store.Folder, "selftest-overlay.flag"));
            await Task.Delay(3000);
            Check(!ControlsPopup.IsOpen || IsMouseInControlsZone(), "se esconden solos con el ratón fuera de su franja");
            TogglePinControls();
            await Task.Delay(300);
            Check(!_floating && RootGrid.Children.Contains(ControlsHost) && settings.PinFullscreenControls && VideoArea.ActualHeight < RootGrid.ActualHeight - 50,
                "con la chincheta, fijos como en ventana");
            TogglePinControls();
            ToggleFullscreen();
            await Task.Delay(800);
            Check(!_floating && RootGrid.Children.Contains(ControlsHost) && !PinButton.IsVisible, "al salir, todo en su sitio y sin chincheta");

            // Posición respecto al tramo ampliado, entre corchetes junto al frame: dentro, antes ("−") y después ("+").
            {
                var zg = Zoom.Grid;
                var first = zg.FrameAt(_duration * 0.5);
                SetFreeZoom((zg.TimeOf(first), zg.TimeOf(first + 48)));
                _nav.SeekTo(zg.TimeOf(first + 24));
                await Task.Delay(800);
                Check(RangePosText.Text.StartsWith("[" + (zg.TimeOf(first + 24) - zg.TimeOf(first)).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))
                        && RangePosText.Text.Contains(" / " + (zg.TimeOf(first + 48) - zg.TimeOf(first)).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " s")
                        && RangePosText.Text.EndsWith("frame 25/48]"), $"dentro del tramo: «{RangePosText.Text}»");
                _nav.SeekTo(zg.TimeOf(first - 10));
                await Task.Delay(800);
                Check(RangePosText.Text.StartsWith("[−") && RangePosText.Text.EndsWith("−10 frames]"), $"antes del tramo: «{RangePosText.Text}»");
                _nav.SeekTo(zg.TimeOf(first + 100));
                await Task.Delay(800);
                Check(RangePosText.Text.StartsWith("[+") && RangePosText.Text.EndsWith("+53 frames]"), $"después del tramo: «{RangePosText.Text}»");
                CloseZone();
                Check(RangePosText.Text == "", "sin tramo, nada");
            }

            // Final del vídeo: en el último frame el tiempo actual y el total son iguales (con milisegundos).
            _nav.Pause();
            GoToLastFrame();
            await Task.Delay(1200);
            var parts = TimeText.Text.Split(" / ");
            Check(Math.Abs(_position - _lastFrameStart) < 1e-3 && parts.Length == 2 && parts[0] == parts[1] && parts[1].Contains('.'),
                $"último frame: «{TimeText.Text}» (empieza en {_lastFrameStart:0.000}, duración {_duration:0.000})");
            // Insistir hacia delante (→, frame siguiente) se queda en el último frame, sin enseñar el final un momento.
            var lastSeen = new List<double>();
            void Track(double t) => lastSeen.Add(t);
            _nav.PositionShown += Track;
            _nav.SeekRelative(5);
            await Task.Delay(800);
            StepFrame(1);
            await Task.Delay(500);
            _nav.PositionShown -= Track;
            Check(Math.Abs(_position - _lastFrameStart) < 1e-3 && lastSeen.All(t => t <= _lastFrameStart + 1e-3) && _nav.Paused,
                $"→ y frame siguiente en el último frame: se queda ({_position:0.000}, visto hasta {(lastSeen.Count > 0 ? lastSeen.Max() : 0):0.000})");

            // Timecode.
            Check(TimeFormat.Timecode(3723.5, 24) == "01:02:03:12", $"timecode a 24 fps: {TimeFormat.Timecode(3723.5, 24)}");
            Check(TimeFormat.Timecode(30 / 29.97, 29.97) == "00:00:01:00", $"timecode a 29,97 fps, frame 30: {TimeFormat.Timecode(30 / 29.97, 29.97)}");
            Check(TimeFormat.Timecode(29 / 29.97, 29.97) == "00:00:00:29", $"timecode a 29,97 fps, frame 29: {TimeFormat.Timecode(29 / 29.97, 29.97)}");
            settings.TimeWithFrames = true;
            ApplySettings();
            Check(TimeText.Text.Split(' ')[0].Count(c => c == ':') == 3, $"contador con frames: {TimeText.Text}");
            settings.TimeWithFrames = false;
            ApplySettings();
            Check(TimeText.Text.Contains('.'), $"contador con milisegundos: {TimeText.Text}");

            // La ventana.
            OpenSettings();
            await Task.Delay(800);
            var window = _settingsWindow!;
            var area = Dialog.WorkAreaOf(this);
            Check(window.MaxHeight == Math.Max(window.MinHeight, area.Height - 60) && window.Top >= area.Top - 1 && window.Top + window.ActualHeight <= area.Bottom + 1,
                $"la ventana cabe en su pantalla ({window.Top:0}–{window.Top + window.ActualHeight:0} de {area.Top:0}–{area.Bottom:0}, alto máximo {window.MaxHeight:0})");
            Check(window.IsVisible && window.MoveRegionsCheck.IsChecked == false && window.CachedLineCheck.IsChecked == false
                    && window.EdgeFrameCheck.IsChecked == true,
                "la ventana abre con las casillas como los ajustes");
            // El tooltip, solo en el nombre de la opción: ni en el control ni en el resto de la fila.
            var rows = new FrameworkElement[] { window.MoveRegionsCheck, window.ScrubSlider, window.SubLangCombo, window.CacheSlider }
                .Select(c => (Control: c, Row: (System.Windows.Controls.DockPanel)c.Parent)).ToList();
            Check(rows.All(r => r.Control.ToolTip == null && r.Row.ToolTip == null
                    && r.Row.Children.OfType<System.Windows.Controls.TextBlock>().Last().ToolTip is string { Length: > 0 }),
                "tooltips solo en el nombre de la opción");
            // Ctrl + Shift + B se ve en la ventana al momento.
            ToggleMarks();
            Check(window.MarksCheck.IsChecked == settings.ShowBookmarkMarks, "las marcas, sincronizadas con Ctrl + Shift + B");
            ToggleMarks();
            window.MarksCheck.IsChecked = !window.MarksCheck.IsChecked;
            Check(Seek.ShowBookmarks == settings.ShowBookmarkMarks && settings.ShowBookmarkMarks == window.MarksCheck.IsChecked,
                "y al revés: la casilla cambia las marcas");
            window.MarksCheck.IsChecked = !window.MarksCheck.IsChecked;
            window.SeekBox.Text = "7,5";
            Check(settings.SeekSeconds == 7.5, $"«7,5» en el salto: {settings.SeekSeconds}");
            window.SeekBox.Text = "abc";
            Check(settings.SeekSeconds == 7.5, "un salto no válido no cambia nada");
            window.SeekBox.Text = "5";
            window.SubLangCombo.SelectedIndex = window.SubLangCombo.Items.Count - 1;
            Check(settings.SubtitleLanguages == Languages.All[^1].Code, $"elegir un idioma de subtítulos guarda su código: {settings.SubtitleLanguages}");
            window.SubLangCombo.SelectedIndex = 0;
            Check(settings.SubtitleLanguages == "" && settings.SubtitleLanguage == "", "«Sin preferencia»");
            // De vuelta al de Windows, como por defecto (para la captura).
            settings.SubtitleLanguages = null;
            window.LoadValues();
            window.ScrubSlider.Value = 9;
            Check(settings.ScrubPixelsPerFrame == 9 && window.ScrubText.Text.StartsWith('9'), $"slider del scrubbing: {window.ScrubText.Text}");
            window.ScrubSlider.Value = 6;
            // La rueda sobre el vídeo: dos opciones como «Formato del tiempo» (capturas en selftest-ajustes-rueda.png
            // y selftest-ajustes-tiempo.png, para compararlas).
            Check(window.WheelSpeedRadio.IsChecked == true && window.WheelVolumeRadio.IsChecked == false, "rueda: abre en Velocidad, como el ajuste");
            window.WheelVolumeRadio.IsChecked = true;
            Check(settings.WheelOnVideo == Settings.WheelVolume && window.WheelSpeedRadio.IsChecked == false, "rueda: Volumen guarda «volume»");
            window.WheelSpeedRadio.IsChecked = true;
            Check(settings.WheelOnVideo == Settings.WheelSpeed, "rueda: Velocidad guarda «speed»");
            await Task.Delay(200);
            SaveVisualSnapshot(CardOf(window.WheelSpeedRadio), Path.Combine(_store.Folder, "selftest-ajustes-rueda.png"));
            SaveVisualSnapshot(CardOf(window.TimeMsRadio), Path.Combine(_store.Folder, "selftest-ajustes-tiempo.png"));
            window.CacheSlider.Value = 4000;
            Check(settings.FrameCacheMegabytes % 256 == 0 && settings.FrameCacheMegabytes >= 3840, $"slider de la caché en pasos de 256 MB: {settings.FrameCacheMegabytes} MB, {window.CacheText.Text}");
            var recommended = (GpuDevice.DefaultAdapter()?.RecommendedCacheBytes ?? AdapterInfo.FallbackCacheBytes) / (1024 * 1024);
            window.CacheSlider.Value = recommended;
            Check(settings.FrameCacheMegabytes == 0, $"en el recomendado ({recommended} MB) la caché vuelve a automática");
            const long gb = 1024L * 1024 * 1024;
            foreach (var (card, megabytes) in new[]
            {
                (new AdapterInfo("2 GB", 2 * gb, 8 * gb), 1024L), (new AdapterInfo("1 GB", gb, 8 * gb), 512L),
                (new AdapterInfo("12 GB", 12 * gb, 16 * gb), 3072L), (new AdapterInfo("3 GB", 3 * gb, 8 * gb), 1536L),
                (new AdapterInfo("Intel, 16 GB de RAM", 128L << 20, 8 * gb), 2048L), (new AdapterInfo("Intel, 8 GB de RAM", 128L << 20, 4 * gb), 1024L),
            })
                Check(card.RecommendedCacheBytes == megabytes << 20, $"caché recomendada con {card.Name}: {card.RecommendedCacheBytes >> 20} MB");
            Log($"caché: {window.CacheText.Text} (máximo {window.CacheSlider.Maximum} MB)");
            window.Scroller.ScrollToTop();
            await Task.Delay(300);
            SaveWindowSnapshot(window, Path.Combine(_store.Folder, "selftest-ajustes.png"));
            // El aviso oscuro (el de Restablecer), sin esperar respuesta: el botón elegido queda en Tag.
            var dialog = Dialog.Create(window, "Se volverán a poner los valores por defecto de todos los ajustes.", "Restablecer ajustes", "Restablecer", "Cancelar");
            dialog.Show();
            await Task.Delay(400);
            // El foco, antes de abrir el selector de color (que le quita la activación al aviso). El foco lógico del aviso:
            // el de teclado depende de qué ventana del escritorio esté activa.
            var buttons = ((System.Windows.Controls.Panel)((System.Windows.Controls.Border)((System.Windows.Controls.DockPanel)dialog.Content).Children[0]).Child)
                .Children.OfType<System.Windows.Controls.Button>().ToList();
            Check(buttons.Count == 2 && buttons[0].IsDefault && System.Windows.Input.FocusManager.GetFocusedElement(dialog) == buttons[0],
                "aviso oscuro: Restablecer es el botón principal y tiene el foco");
            SaveWindowSnapshot(dialog, Path.Combine(_store.Folder, "selftest-aviso.png"));
            // Otro color: el selector (captura en selftest-color.png), el código y el aviso si se parece a los de las barras.
            var picker = ColorPicker.Create(window, "#2E8B57", _ => { });
            picker.Show();
            await Task.Delay(400);
            SaveWindowSnapshot(picker, Path.Combine(_store.Folder, "selftest-color.png"));
            picker.Close();
            Check(ColorPicker.Parse("#abc") is { R: 0xAA, G: 0xBB, B: 0xCC } && ColorPicker.Parse("12345") == null, "otro color: lee #RGB y #RRGGBB");
            Check(ColorPicker.Clash(ColorPicker.Parse("#F2B530")!.Value) != null && ColorPicker.Clash(ColorPicker.Parse("#4C9BFF")!.Value) != null
                && ColorPicker.Clash(ColorPicker.Parse("#E5484D")!.Value) == null && ColorPicker.Clash(ColorPicker.Parse("#8E6CEF")!.Value) == null,
                "otro color: avisa si se parece al ámbar o al azul de las barras, no con los de la paleta");
            buttons[1].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Check(dialog.Tag is 1 && !dialog.IsVisible, "aviso oscuro: Cancelar lo cierra con esa respuesta");
            window.Close();
            await Task.Delay(200);
            Check(_settingsWindow == null, "Cerrar la ventana");
            OnSettingsClick(this, new RoutedEventArgs());
            await Task.Delay(500);
            var opened = _settingsWindow?.IsVisible == true;
            OnSettingsClick(this, new RoutedEventArgs());
            await Task.Delay(300);
            Check(opened && _settingsWindow == null, "el engranaje abre los ajustes y, otra vez, los cierra");
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            // Los ajustes de Mario, como estaban.
            if (System.Text.Json.JsonSerializer.Deserialize<Settings>(saved, DataStore.Json) is { } original)
            {
                foreach (var property in typeof(Settings).GetProperties().Where(p => p.CanWrite))
                    property.SetValue(settings, property.GetValue(original));
                _store.SaveSettings();
                ApplySettings();
            }
        }
        Log(failures == 0 ? "TODO OK" : $"{failures} FALLOS");
        File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
        Close();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X, Y;
    }

    private static nint MakeLParam(NativePoint p) => (nint)((p.Y << 16) | (p.X & 0xFFFF));

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetCursorPos")]
    private static extern bool GetCursorPosition(out NativePoint point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ScreenToClient(nint hwnd, ref NativePoint point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);

    // Un elemento dentro de la ventana, tal cual se ve (con fondo oscuro detrás).
    private static void SaveVisualSnapshot(FrameworkElement element, string path)
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(element);
        var size = new Size(element.ActualWidth, element.ActualHeight);
        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle((System.Windows.Media.Brush)Application.Current.FindResource("Bg"), null, new Rect(size));
            dc.DrawRectangle(new System.Windows.Media.VisualBrush(element), null, new Rect(size));
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(size.Width * dpi.DpiScaleX), (int)(size.Height * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>La tarjeta (Border) de ajustes que contiene el control.</summary>
    private static FrameworkElement CardOf(FrameworkElement control)
    {
        var element = control;
        while (element is not System.Windows.Controls.Border && element.Parent is FrameworkElement parent) element = parent;
        return element;
    }

    private static void SaveWindowSnapshot(Window window, string path) => SaveElementSnapshot((FrameworkElement)window.Content, path);

    private static void SaveElementSnapshot(FrameworkElement root, string path)
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(root);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX),
            (int)(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
