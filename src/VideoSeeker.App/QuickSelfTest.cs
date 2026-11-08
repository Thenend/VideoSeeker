using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

/// <summary>
/// Prueba rápida (VIDEOSEEKER_SELFTEST=rapida), unos segundos: para cambios de textos, tooltips, menús o la ventana
/// de ajustes, que no tocan cómo funciona nada. Comprueba que arranca y enseña el vídeo, abre la ventana de ajustes y
/// los menús principales (vídeo, bookmark, velocidad, colección) y el cuadro del nombre, y en todo ello: que no hay
/// textos cortados, que ningún texto nombra otros programas, y que no hay errores de enlace de datos. Guarda una
/// captura de cada cosa (selftest-rapida-*.png) y todos los textos y tooltips en selftest-rapida-textos.txt, para
/// buscar ahí el texto cambiado. Escribe selftest.log y cierra. No deja nada cambiado.
/// </summary>
public partial class MainWindow
{
    public static bool QuickSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "rapida";

    // Errores de enlace de datos de WPF (se apuntan desde el arranque, antes de crear la ventana).
    private static readonly List<string> BindingErrors = [];

    private sealed class BindingErrorListener : TraceListener
    {
        public override void Write(string? message) { }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                lock (BindingErrors)
                    BindingErrors.Add(message.Trim());
        }
    }

    /// <summary>Para la prueba rápida: desde aquí, los errores de enlace de datos se apuntan.</summary>
    public static void WatchBindingErrors()
    {
        PresentationTraceSources.Refresh(); // sin esto WPF solo los saca con el depurador
        PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorListener());
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
    }

    // Nombres de otros programas: la interfaz no los nombra nunca (decisión de Mario).
    private static readonly Regex OtherPrograms = new(
        @"\b(Premiere|DaVinci|Resolve|YouTube|VLC|mpv|MPC|PotPlayer|KMPlayer|QuickTime|Final Cut|After Effects|Avid|Vegas|Twitch)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private async Task RunQuickSelfTest()
    {
        var clock = Stopwatch.StartNew();
        var lines = new List<string>();
        var texts = new List<string>();
        var failures = 0;
        void Log(string text) => lines.Add($"{DateTime.Now:HH:mm:ss.fff} {text}");
        void Check(bool ok, string what)
        {
            Log((ok ? "OK    " : "FALLO ") + what);
            if (!ok)
                failures++;
        }
        string Png(string name) => Path.Combine(_store.Folder, $"selftest-rapida-{name}.png");

        // Una parte de la interfaz: sus textos y tooltips a la lista, los cortados y los que nombran otros programas, fallo.
        void Inspect(string part, Visual root, bool trimmedIsFailure)
        {
            texts.Add($"## {part}");
            var (cut, trimmed, named) = (new List<string>(), new List<string>(), new List<string>());
            foreach (var (text, isCut, hasEllipsis) in TextsOf(root))
            {
                texts.Add(text);
                if (OtherPrograms.IsMatch(text))
                    named.Add(text);
                if (isCut && (trimmedIsFailure || !hasEllipsis))
                    cut.Add(text);
                else if (isCut)
                    trimmed.Add(text);
            }
            texts.Add("");
            Check(cut.Count == 0, $"{part}: ningún texto cortado{(cut.Count > 0 ? ": «" + string.Join("», «", cut) + "»" : "")}");
            Check(named.Count == 0, $"{part}: ningún texto nombra otros programas{(named.Count > 0 ? ": «" + string.Join("», «", named) + "»" : "")}");
            if (trimmed.Count > 0)
                Log($"      {part}: recortados con «…» (puede ser a propósito, como nombres largos): «{string.Join("», «", trimmed)}»");
        }

        // Un menú que se acaba de abrir: captura, sus opciones (y las de los submenús) y se cierra.
        async Task InspectMenu(string part, string png)
        {
            // Hasta 2 s: en un equipo cargado el menú tarda más en abrirse.
            ContextMenu? menu = null;
            for (var i = 0; i < 20 && menu == null; i++)
            {
                await Task.Delay(100);
                menu = OpenContextMenus().FirstOrDefault();
            }
            await Task.Delay(150); // que acabe de dibujarse antes de la captura
            Check(menu != null, $"menú {part}: se abre");
            if (menu == null)
                return;
            SaveElementSnapshot(menu, Png(png));
            Inspect($"Menú {part}", menu, trimmedIsFailure: true);
            texts.Insert(texts.Count - 1, "(opciones) " + string.Join(" | ", MenuEntries(menu.Items)));
            var named = MenuEntries(menu.Items).Where(e => OtherPrograms.IsMatch(e)).ToList();
            Check(named.Count == 0, $"menú {part}: ninguna opción nombra otros programas{(named.Count > 0 ? ": «" + string.Join("», «", named) + "»" : "")}");
            menu.IsOpen = false;
            await Task.Delay(100);
        }

        Bookmark? probe = null;
        var panelWasOpen = BookmarkPanel.Visibility == Visibility.Visible;
        try
        {
            // Arranque: el vídeo se abre, se ve y el motor de inspección arranca.
            for (var i = 0; i < 150 && (_duration <= 0 || _bookmarks == null); i++)
                await Task.Delay(100);
            Check(_duration > 0 && _bookmarks != null, $"abre el vídeo ({_duration:0.0} s, {Fps:0.##} fps)");
            for (var i = 0; i < 100 && _engine == null; i++)
                await Task.Delay(100);
            Check(_engine != null, "el motor de inspección arranca");
            if (_duration <= 0 || _bookmarks == null)
                throw new InvalidOperationException("sin vídeo no se puede seguir");
            Activate();
            _nav.Pause();
            await Task.Delay(500);

            // Ventana principal, con el panel de bookmarks abierto.
            if (!panelWasOpen)
                ToggleOpenPanel();
            await Task.Delay(300);
            UpdateLayout();
            // Con el fondo de la app debajo: la barra de controles es transparente y saldría en blanco. (El vídeo va en
            // su propia ventana nativa y sale negro.)
            if (Content is FrameworkElement main)
                SaveVisualSnapshot(main, Png("ventana"));
            Inspect("Ventana principal", this, trimmedIsFailure: false);

            // Ajustes: la ventana entera (también lo que hay que desplazar para ver).
            OpenSettings();
            await Task.Delay(800);
            var settings = _settingsWindow!;
            var area = Dialog.WorkAreaOf(this);
            Check(settings.IsVisible && settings.Top >= area.Top - 1 && settings.Top + settings.ActualHeight <= area.Bottom + 1,
                $"la ventana de ajustes se abre y cabe en la pantalla ({settings.Top:0}–{settings.Top + settings.ActualHeight:0} de {area.Top:0}–{area.Bottom:0})");
            SaveWindowSnapshot(settings, Png("ajustes"));
            if (settings.Scroller.Content is FrameworkElement all)
                SaveVisualSnapshot(all, Png("ajustes-entera"));
            Inspect("Ajustes", settings, trimmedIsFailure: true);
            settings.Close();
            await Task.Delay(200);
            Check(_settingsWindow == null, "la ventana de ajustes se cierra");

            // Menús: el del vídeo, el de un bookmark (uno de prueba, que se borra al acabar), velocidad y colección.
            ShowVideoMenu();
            await InspectMenu("del vídeo", "menu-video");
            probe = _bookmarks.Add("Prueba rápida", _duration * 0.3, _duration * 0.3 + Math.Min(2, _duration * 0.05));
            await Task.Delay(200);
            ShowBookmarkMenu(probe, Seek);
            await InspectMenu("del bookmark", "menu-bookmark");
            ShowSpeedMenu();
            await InspectMenu("de velocidad", "menu-velocidad");
            OnCollectionClick(this, new RoutedEventArgs());
            await InspectMenu("de colección", "menu-coleccion");

            // El cuadro del nombre (F2), sin cambiar nada.
            BeginRename(probe, isNew: false);
            for (var i = 0; i < 20 && !NamePopup.IsOpen; i++)
                await Task.Delay(100);
            await Task.Delay(200);
            Check(NamePopup.IsOpen, "el cuadro del nombre se abre");
            if (NamePopup.Child is FrameworkElement box)
            {
                SaveElementSnapshot(box, Png("nombre"));
                Inspect("Cuadro del nombre", box, trimmedIsFailure: true);
            }
            CloseNaming(commit: false);
            await Task.Delay(100);
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            foreach (var menu in OpenContextMenus())
                menu.IsOpen = false;
            _settingsWindow?.Close();
            if (probe != null)
                _bookmarks?.Remove(probe.Id);
            if (!panelWasOpen && BookmarkPanel.Visibility == Visibility.Visible)
                ToggleOpenPanel();
        }
        List<string> errors;
        lock (BindingErrors)
            errors = [.. BindingErrors];
        Check(errors.Count == 0, $"sin errores de enlace de datos{(errors.Count > 0 ? ":" + Environment.NewLine + string.Join(Environment.NewLine, errors) : "")}");
        Log($"{clock.Elapsed.TotalSeconds:0.0} s; capturas en selftest-rapida-*.png, textos en selftest-rapida-textos.txt");
        Log(failures == 0 ? "TODO OK" : $"{failures} FALLOS");
        File.WriteAllLines(Path.Combine(_store.Folder, "selftest-rapida-textos.txt"), texts);
        File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
        Close();
    }

    /// <summary>
    /// Textos visibles y tooltips de una parte de la interfaz, sin repetir. Un texto de una línea está cortado si no
    /// cabe en su sitio (WPF lo recorta, con «…» si lleva TextTrimming) o se sale por la derecha de la ventana.
    /// </summary>
    private static List<(string Text, bool Cut, bool Ellipsis)> TextsOf(Visual root)
    {
        var found = new List<(string, bool, bool)>();
        var seen = new HashSet<string>();
        var rootWidth = (root as FrameworkElement)?.ActualWidth ?? double.MaxValue;
        if (root is Window { Content: FrameworkElement content })
            rootWidth = content.ActualWidth;
        var stack = new Stack<DependencyObject>([root]);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            for (var i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                stack.Push(VisualTreeHelper.GetChild(node, i));
            if (node is not FrameworkElement element)
                continue;
            var tip = element.ToolTip switch
            {
                string s => s,
                TextBlock t => t.Text,
                ToolTip { Content: string s } => s,
                ToolTip { Content: TextBlock t } => t.Text,
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(tip) && seen.Add("tooltip: " + tip))
                found.Add(("tooltip: " + tip, false, false));
            if (element is not TextBlock { IsVisible: true } block || string.IsNullOrWhiteSpace(block.Text))
                continue;
            var cut = false;
            if (block.TextWrapping == TextWrapping.NoWrap && block.ActualWidth > 0 && !block.Text.Contains('\n'))
            {
                var formatted = new FormattedText(block.Text, CultureInfo.CurrentUICulture, block.FlowDirection,
                    new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize,
                    Brushes.Black, VisualTreeHelper.GetDpi(block).PixelsPerDip);
                var room = block.ActualWidth - block.Padding.Left - block.Padding.Right;
                cut = formatted.Width > room + 1;
                if (!cut && root is FrameworkElement)
                {
                    try
                    {
                        var bounds = block.TransformToAncestor(root).TransformBounds(new Rect(0, 0, block.ActualWidth, block.ActualHeight));
                        cut = bounds.Width > 0 && bounds.Right > rootWidth + 1 && bounds.Left < rootWidth;
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
            var key = block.Text.Trim();
            if (seen.Add(key) || cut)
                found.Add((key, cut, block.TextTrimming != TextTrimming.None));
        }
        return found;
    }

    /// <summary>Las opciones de un menú, con las de sus submenús («Sub ▸ opción») y su atajo entre corchetes.</summary>
    private static IEnumerable<string> MenuEntries(ItemCollection items, string prefix = "")
    {
        foreach (var item in items.OfType<MenuItem>())
        {
            var header = item.Header switch
            {
                string s => s,
                TextBlock t => t.Text,
                _ => item.Header?.ToString() ?? "",
            };
            yield return prefix + header + (string.IsNullOrEmpty(item.InputGestureText) ? "" : $" [{item.InputGestureText}]");
            foreach (var sub in MenuEntries(item.Items, prefix + header + " ▸ "))
                yield return sub;
        }
    }

    /// <summary>Los menús contextuales abiertos ahora mismo (cada uno va en su propia ventana emergente).</summary>
    private static List<ContextMenu> OpenContextMenus()
    {
        var menus = new List<ContextMenu>();
        foreach (var source in PresentationSource.CurrentSources.OfType<HwndSource>())
        {
            if (source.RootVisual == null)
                continue;
            var stack = new Stack<DependencyObject>([source.RootVisual]);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node is ContextMenu { IsOpen: true } menu)
                {
                    menus.Add(menu);
                    continue;
                }
                for (var i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                    stack.Push(VisualTreeHelper.GetChild(node, i));
            }
        }
        return menus;
    }
}
