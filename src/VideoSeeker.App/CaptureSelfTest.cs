using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

/// <summary>
/// Capturas para la tienda (VIDEOSEEKER_SELFTEST=capturas, con «Sprite Fright», de la Blender Foundation, CC-BY): monta
/// unas escenas con la ventana en 1920 × 1080 (contando la barra de título) y guarda lo que se ve en pantalla de cada
/// una en selftest-captura-NN-nombre.png (el script las deja en resultados\capturas\&lt;idioma&gt;). Los bookmarks y las
/// etiquetas son datos de prueba (en el idioma de la interfaz) en una colección propia de los datos de prueba, que se
/// borra al empezar. Si encima de la ventana hay algo de otro programa, no guarda esa captura (sería un fallo).
/// </summary>
public partial class MainWindow
{
    public static bool CaptureSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "capturas";

    private const string CaptureCollection = "Sprite Fright";

    // Lo que escribiría alguien que estudia la animación de la película, en cada idioma.
    private sealed record CaptureTexts(string[] Labels, string[] Bookmarks, string NewName);

    private static CaptureTexts CaptureTextsFor(string language) => language switch
    {
        "en" => new(["Animation", "Expression", "Review"],
            ["shot 3 – glance at the snail", "left foot contact", "bat swing arc", "shot 12 – eye line", "throw anticipation",
             "spray – check timing", "sprite walk cycle", "check Rex's expression", "lip sync", "elder close-up"],
            "group reaction"),
        "fr" => new(["Animation", "Expression", "À revoir"],
            ["plan 3 – regard vers l'escargot", "contact pied gauche", "arc de la batte", "plan 12 – regard", "anticipation du lancer",
             "spray – vérifier le timing", "cycle de marche sprite", "revoir l'expression de Rex", "synchro labiale", "gros plan de l'ancien"],
            "réaction du groupe"),
        "de" => new(["Animation", "Mimik", "Prüfen"],
            ["Einst. 3 – Blick zur Schnecke", "Kontakt linker Fuß", "Schlagbogen", "Einst. 12 – Blick", "Wurf-Antizipation",
             "Spray – Timing prüfen", "Sprite-Laufzyklus", "Rex' Mimik prüfen", "Lippensynchronität", "Nahaufnahme Ältester"],
            "Gruppenreaktion"),
        "it" => new(["Animazione", "Espressione", "Da rivedere"],
            ["inquadratura 3 – sguardo alla lumaca", "contatto piede sx", "arco della mazza", "inquadratura 12 – sguardo",
             "anticipazione lancio", "spray – controllare timing", "ciclo camminata sprite", "rivedere espressione Rex",
             "sincronizzazione labiale", "primo piano anziano"],
            "reazione del gruppo"),
        "pt-BR" => new(["Animação", "Expressão", "Revisar"],
            ["plano 3 – olhar para o caracol", "contato pé esq.", "arco do taco", "plano 12 – olhar", "antecipação do arremesso",
             "spray – revisar timing", "ciclo de caminhada sprite", "revisar expressão do Rex", "sincronia labial", "close do ancião"],
            "reação do grupo"),
        _ => new(["Animación", "Expresión", "Revisar"],
            ["toma 3 – mirada al caracol", "contacto pie izq.", "arco del bate", "toma 12 – mirada", "anticipación lanzamiento",
             "spray – revisar timing", "ciclo de paso sprite", "revisar expresión Rex", "sincronía labial", "primer plano anciano"],
            "reacción del grupo"),
    };

    // Inicio, fin y etiqueta (índice en Labels; -1, sin etiqueta y con color propio) de cada bookmark, en segundos.
    private static readonly (double Start, double End, int Label)[] CaptureBookmarks =
    [
        (16.0, 19.5, 1), (25.0, 28.0, 0), (30.0, 33.5, -1), (64.0, 67.5, 1), (83.0, 85.5, 0),
        (86.0, 88.0, 2), (168.0, 171.5, 0), (180.0, 184.5, 2), (211.0, 214.5, 2), (232.0, 236.0, 1),
    ];

    private async Task RunCaptureSelfTest()
    {
        var clock = Stopwatch.StartNew();
        var lines = new List<string>();
        var failures = 0;
        void Log(string text) => lines.Add($"{DateTime.Now:HH:mm:ss.fff} {text}");
        void Check(bool ok, string what)
        {
            Log((ok ? "OK    " : "FALLO ") + what);
            if (!ok)
                failures++;
        }
        var language = Environment.GetEnvironmentVariable("VIDEOSEEKER_UILANG") ?? "es";
        var texts = CaptureTextsFor(language);
        string Png(string name) => Path.Combine(_store.Folder, $"selftest-captura-{name}.png");

        try
        {
            // Solo con los datos de prueba: la colección se borra y se vuelve a crear.
            if (!Path.GetFullPath(_store.Folder).Contains("selftest", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"la carpeta de datos no es la de prueba ({_store.Folder})");
            while (_bookmarks == null || _duration <= 0)
                await Task.Delay(100);
            for (var i = 0; i < 300 && _frameIndex == null; i++)
                await Task.Delay(100);
            Check(_frameIndex != null, "índice de frames listo (número de frame exacto)");
            _nav.Pause();
            Log($"vídeo de {_duration:0.0} s a {Fps:0.###} fps; idioma {language}");

            var folder = BookmarkCollections.PathOf(_store, CaptureCollection);
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
            var palette = new[] { "#2EC8D6", "#8E6CEF", "#F76B15" };
            var labelIds = texts.Labels.Select(_ => BookmarkLabels.NewId()).ToArray();
            BookmarkLabels.Modify(_store, CaptureCollection, file =>
            {
                for (var i = 0; i < texts.Labels.Length; i++)
                    file.Labels.Add(new BookmarkLabel { Id = labelIds[i], Name = texts.Labels[i], Color = palette[i] });
            });
            SetCollection(CaptureCollection, announce: false);
            ScopeThis.IsChecked = true;
            var settings = _store.Settings;
            settings.HoverPreview = true;
            settings.ShowBookmarkMarks = true;
            settings.RepeatLastLabel = false;
            ApplySettings();
            var set = _bookmarks!;
            var grid = Zoom.Grid;
            var added = new List<Bookmark>();
            for (var i = 0; i < CaptureBookmarks.Length; i++)
            {
                var (start, end, label) = CaptureBookmarks[i];
                added.Add(set.Add(texts.Bookmarks[i], grid.Snap(start), grid.Snap(end), label < 0 ? "#A8D84E" : palette[label],
                    label < 0 ? null : labelIds[label]));
            }
            Check(set.Items.Count == CaptureBookmarks.Length, $"{set.Items.Count} bookmarks de prueba");

            PlaceForCapture(Log);
            if (BookmarkPanel.Visibility == Visibility.Visible)
                ShowPanel(false);

            async Task Show(double time, int waitMs = 2500)
            {
                _nav.SeekTo(grid.Snap(time));
                await Task.Delay(waitMs);
            }

            async Task Capture(string name)
            {
                Activate();
                await Task.Delay(400);
                await WaitUntilUncovered(Log);
                var ok = CaptureWindow(Png(name), Log);
                Check(ok, $"captura {name}");
            }

            // 01: parado en un frame, con una zona de unos 2,5 s alrededor y su bucle puesto.
            _loopVideo = false;
            _loopRange = true;
            ApplyLoop();
            UpdateLoopButtons();
            SetFreeZoom((grid.Snap(17.75), grid.Snap(20.25)));
            await Show(19.0, 4000);
            ReturnFocusToVideo();
            await Capture("01-fotograma");

            // 02: el panel con los bookmarks, uno seleccionado (es la zona).
            ShowPanel(true);
            var selected = added[3];
            Select(selected.Id);
            await Show(65.4, 4000);
            ReturnFocusToVideo();
            await Task.Delay(1500); // miniaturas del panel
            await Capture("02-bookmarks");

            // 03: el vistazo al pasar por la barra principal.
            ShowPanel(false);
            Select(null);
            CloseZone();
            await Show(165.0);
            Seek.SimulateHover(Seek.TrackPoint(grid.Snap(233.5)));
            for (var i = 0; i < 100 && !(PeekPopup.IsOpen && _peekExact && PeekImage.Source != null); i++)
                await Task.Delay(50);
            Check(PeekPopup.IsOpen && _peekExact, "vistazo abierto con su frame");
            await Task.Delay(300);
            await Capture("03-vistazo");
            Seek.SimulateLeave();

            // 04: una zona libre guardada como bookmark: el cuadro del nombre, con su etiqueta.
            ShowPanel(true);
            SetFreeZoom((grid.Snap(176.5), grid.Snap(179.75)));
            await Show(178.0);
            AddBookmark();
            for (var i = 0; i < 20 && !NamePopup.IsOpen; i++)
                await Task.Delay(100);
            await Task.Delay(300);
            var newId = _naming?.Id;
            // Como si ya se hubiera escrito el nombre y elegido la etiqueta (y el panel lo enseñara ya así).
            if (newId != null)
                set.Update(newId, b => { b.Name = texts.NewName; b.Label = labelIds[1]; });
            ChooseNamingLabel(labelIds[1]);
            NameBox.Text = texts.NewName;
            NameBox.Select(NameBox.Text.Length, 0);
            await Task.Delay(500);
            Check(NamePopup.IsOpen, "cuadro del nombre abierto");
            await Capture("04-region");
            CloseNaming(commit: false);
            if (newId != null && set.Find(newId) != null)
                set.Remove(newId);

            // 05: la ventana de ajustes sobre la principal.
            ShowPanel(false);
            Select(null);
            CloseZone();
            await Show(182.0);
            OpenSettings();
            await Task.Delay(800);
            if (_settingsWindow is { } window)
            {
                // Centrada sobre la principal y más baja que ella.
                window.SizeToContent = SizeToContent.Manual;
                window.Height = Math.Min(window.ActualHeight, ActualHeight - 140);
                window.Left = Left + (ActualWidth - window.ActualWidth) / 2;
                window.Top = Top + (ActualHeight - window.Height) / 2;
                window.Activate();
                await Task.Delay(500);
                await WaitUntilUncovered(Log);
                Check(CaptureWindow(Png("05-ajustes"), Log), "captura 05-ajustes");
                window.Close();
            }
            else
            {
                Check(false, "la ventana de ajustes se abre");
            }
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        Log($"{clock.Elapsed.TotalSeconds:0.0} s");
        Log(failures == 0 ? "TODO OK" : $"{failures} FALLOS");
        File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
        Close();
    }

    // ---- Ventana de 1920 × 1080 y captura de lo que se ve ----

    [StructLayout(LayoutKind.Sequential)]
    private struct CaptureRect
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out CaptureRect value, int size);

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int DwmSetCaptureAttribute(nint hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    private static extern bool GetCaptureWindowRect(nint hwnd, out CaptureRect rect);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    private static extern bool SetCaptureWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hwnd, nint dc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint dc, nint item);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, uint op);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint item);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint dc);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hwnd, uint command);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetCloaked(nint hwnd, int attribute, out int value, int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, System.Text.StringBuilder name, int size);

    private const int CaptureWidth = 1920, CaptureHeight = 1080;

    /// <summary>
    /// Una ventana visible de otro programa por encima de la principal que la pisa (también las transparentes, como
    /// los avisos de los programas de la tarjeta gráfica), descrita; null si no hay ninguna.
    /// </summary>
    private string? ForeignWindowOver(CaptureRect bounds)
    {
        var own = (uint)Environment.ProcessId;
        for (var hwnd = GetWindow(new WindowInteropHelper(this).Handle, 3 /* GW_HWNDPREV */); hwnd != 0; hwnd = GetWindow(hwnd, 3))
        {
            if (!IsWindowVisible(hwnd) || DwmGetCloaked(hwnd, 14 /* DWMWA_CLOAKED */, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
                continue;
            GetWindowThreadProcessId(hwnd, out var process);
            if (process == own || !GetCaptureWindowRect(hwnd, out var r))
                continue;
            if (r.Right <= bounds.Left || r.Left >= bounds.Right || r.Bottom <= bounds.Top || r.Top >= bounds.Bottom || r.Width <= 0 || r.Height <= 0)
                continue;
            var name = new System.Text.StringBuilder(256);
            GetClassName(hwnd, name, name.Capacity);
            string program;
            try { program = Process.GetProcessById((int)process).ProcessName; }
            catch (ArgumentException) { program = $"proceso {process}"; }
            return $"{program}, {name} en {r.Left},{r.Top} de {r.Width}×{r.Height}";
        }
        return null;
    }

    /// <summary>Espera (hasta 30 s) a que no haya nada de otro programa encima: los avisos se quitan solos.</summary>
    private async Task<bool> WaitUntilUncovered(Action<string> log)
    {
        string? last = null;
        for (var i = 0; i < 300; i++)
        {
            if (ForeignWindowOver(VisibleBounds()) is not { } foreign)
                return true;
            if (foreign != last)
                log($"      esperando a que se quite una ventana de otro programa: {foreign}");
            last = foreign;
            await Task.Delay(100);
        }
        return false;
    }

    // Lo que se ve de la ventana (sin los bordes invisibles para redimensionar).
    private CaptureRect VisibleBounds()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        return DwmGetWindowAttribute(hwnd, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out var r, Marshal.SizeOf<CaptureRect>()) == 0
            ? r : GetCaptureWindowRect(hwnd, out r) ? r : default;
    }

    /// <summary>Ventana normal, de 1920 × 1080 visibles con la barra de título, centrada en la pantalla principal.</summary>
    private void PlaceForCapture(Action<string> log)
    {
        WindowState = WindowState.Normal;
        var hwnd = new WindowInteropHelper(this).Handle;
        // Esquinas rectas: si no, por las redondeadas se vería lo que haya detrás.
        var square = 1; // DWMWCP_DONOTROUND
        DwmSetCaptureAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref square, sizeof(int));
        var area = SystemParameters.WorkArea; // pantalla principal (en píxeles a 96 ppp)
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var (areaWidth, areaHeight) = ((int)(area.Width * dpi.DpiScaleX), (int)(area.Height * dpi.DpiScaleY));
        var x = (int)(area.Left * dpi.DpiScaleX) + Math.Max(0, (areaWidth - CaptureWidth) / 2);
        var y = (int)(area.Top * dpi.DpiScaleY) + Math.Max(0, (areaHeight - CaptureHeight) / 2);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GetCaptureWindowRect(hwnd, out var outer);
            var visible = VisibleBounds();
            var (left, top) = (visible.Left - outer.Left, visible.Top - outer.Top);
            var (extraWidth, extraHeight) = (outer.Width - visible.Width, outer.Height - visible.Height);
            SetCaptureWindowPos(hwnd, 0, x - left, y - top, CaptureWidth + extraWidth, CaptureHeight + extraHeight, 0x0004 /* SWP_NOZORDER */);
            UpdateLayout();
        }
        var final = VisibleBounds();
        log($"ventana: {final.Width}×{final.Height} visibles en {final.Left},{final.Top} (ppp {dpi.PixelsPerInchX:0}); " +
            $"pantalla principal {areaWidth}×{areaHeight}");
        if (GetCursorPosition(out var cursor) && cursor.X >= final.Left && cursor.X < final.Right && cursor.Y >= final.Top && cursor.Y < final.Bottom)
            log("      el ratón está encima de la ventana: puede salir algo resaltado");
    }

    /// <summary>
    /// Guarda lo que se ve en pantalla en el rectángulo de la ventana (con sus menús, ventanas emergentes y la de ajustes).
    /// Antes mira que todo lo que hay encima sea de la app: si asoma otro programa, no guarda nada.
    /// </summary>
    private bool CaptureWindow(string path, Action<string> log)
    {
        var bounds = VisibleBounds();
        if (bounds.Width != CaptureWidth || bounds.Height != CaptureHeight)
            log($"      la ventana mide {bounds.Width}×{bounds.Height}, no {CaptureWidth}×{CaptureHeight}");
        if (ForeignWindowOver(bounds) is { } foreign)
        {
            log($"      encima de la ventana hay una de otro programa ({foreign}): no se guarda {Path.GetFileName(path)}");
            return false;
        }

        var screen = GetDC(0);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, bounds.Width, bounds.Height);
        var old = SelectObject(memory, bitmap);
        try
        {
            BitBlt(memory, 0, 0, bounds.Width, bounds.Height, screen, bounds.Left, bounds.Top, 0x00CC0020 | 0x40000000 /* SRCCOPY | CAPTUREBLT */);
            SelectObject(memory, old);
            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgr24, null, 0)));
            using var stream = File.Create(path);
            encoder.Save(stream);
            return true;
        }
        finally
        {
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(0, screen);
        }
    }
}
