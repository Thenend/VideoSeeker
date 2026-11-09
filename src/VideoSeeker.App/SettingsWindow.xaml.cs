using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using VideoSeeker.App.Data;
using VideoSeeker.Inspect;

namespace VideoSeeker.App;

/// <summary>
/// Ajustes (Ctrl + , o el engranaje). Ventana propia: la imagen es una ventana nativa y no se puede tapar con WPF.
/// Cada cambio se guarda en settings.json y se aplica al momento (quien la abre lo aplica en "changed"). Las
/// explicaciones van en el tooltip del nombre de cada opción.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private readonly DataStore _store;
    private readonly Action _changed;
    private readonly Action _restart;
    private readonly double? _vramMegabytes;
    // La caché que se usa con el valor automático (0 en los ajustes).
    private readonly int _recommendedCacheMegabytes;
    // Mientras se vuelcan los valores en los controles, sus eventos no son cambios del usuario.
    private bool _loading;

    /// <param name="restart">Cierra la app y la vuelve a abrir (para usar otra carpeta de datos).</param>
    public SettingsWindow(DataStore store, Action changed, Action restart, AdapterInfo? adapter)
    {
        _store = store;
        _changed = changed;
        _restart = restart;
        InitializeComponent();
        var s = store.Settings;

        if (AppLinks.SupportUrl.Length == 0) SupportButton.Visibility = Visibility.Collapsed;
        VersionText.Text = AppVersion();

        Bind(MoveRegionsCheck, v => s.MoveWholeRegions = v);
        Bind(EdgeFrameCheck, v => s.ShowDraggedEdgeFrame = v);
        Bind(HoverPreviewCheck, v => s.HoverPreview = v);
        Bind(CachedLineCheck, v => s.ShowCachedFrames = v);
        Bind(MarksCheck, v => s.ShowBookmarkMarks = v);
        BindNumber(SeekBox, v => s.SeekSeconds = v);
        BindNumber(SeekShiftBox, v => s.SeekSecondsShift = v);
        BindNumber(SeekCtrlBox, v => s.SeekSecondsCtrl = v);
        ScrubSlider.ValueChanged += (_, _) =>
        {
            ScrubText.Text = $"{ScrubSlider.Value:0} px por frame";
            Change(() => s.ScrubPixelsPerFrame = (int)Math.Round(ScrubSlider.Value));
        };
        WheelVolumeRadio.Checked += (_, _) => Change(() => s.WheelOnVideo = Settings.WheelVolume);
        WheelSpeedRadio.Checked += (_, _) => Change(() => s.WheelOnVideo = Settings.WheelSpeed);
        Bind(ResumeCheck, v => s.ResumePlayback = v);
        TimeFramesRadio.Checked += (_, _) => Change(() => s.TimeWithFrames = true);
        TimeMsRadio.Checked += (_, _) => Change(() => s.TimeWithFrames = false);
        Bind(RepeatLabelCheck, v => s.RepeatLastLabel = v);
        TimesListRadio.Checked += (_, _) => Change(() => s.BookmarkTimesInList = true);
        TimesPreviewRadio.Checked += (_, _) => Change(() => s.BookmarkTimesInList = false);
        FillLanguages(AudioLangCombo, v => s.AudioLanguages = v);
        FillLanguages(SubLangCombo, v => s.SubtitleLanguages = v);
        Bind(SubsOnOpenCheck, v => s.SubtitlesOnOpen = v);
        Bind(HwdecCheck, v => s.HardwareDecoding = v);

        // Caché: de 512 MB hasta lo que tenga la tarjeta, en pasos de 256 MB. En el recomendado se guarda 0
        // (automático), para que siga a la tarjeta si se cambia.
        _vramMegabytes = adapter?.MemoryBytes / (1024.0 * 1024);
        _recommendedCacheMegabytes = (int)((adapter?.RecommendedCacheBytes ?? AdapterInfo.FallbackCacheBytes) / (1024 * 1024));
        CacheSlider.Maximum = Math.Max(_vramMegabytes is { } vram ? Math.Floor(vram / 256) * 256 : 16384, CacheSetting(s));
        CacheSlider.ValueChanged += (_, _) =>
        {
            UpdateCacheText();
            Change(() => s.FrameCacheMegabytes = CacheMegabytes == _recommendedCacheMegabytes ? 0 : CacheMegabytes);
        };
        CacheLabel.ToolTip = "Cuanta más, más tiempo de vídeo se puede recorrer frame a frame al instante. Se aplica al abrir el siguiente vídeo.";
        Bind(ClipClipboardCheck, v => s.CopyClipToClipboard = v);
        LightClipSlider.ValueChanged += (_, _) =>
        {
            UpdateLightClipText();
            Change(() => s.LightClipMegabytesPerSecond = Math.Round(LightClipSlider.Value * 4) / 4);
        };

        LoadValues();
        UpdateFolderUi();
        UnregisterButton.IsEnabled = FileAssociations.IsRegistered;
        if (Packaged.IsPackaged)
        {
            // Instalada desde la Store los tipos de vídeo vienen con la app y se quitan al desinstalarla.
            UnregisterButton.Visibility = Visibility.Collapsed;
            DefaultFolderButton.ToolTip = "La carpeta de la app en Windows. Se borra al desinstalarla.";
        }
        PreviewKeyDown += OnKeyDown;
        SourceInitialized += (_, _) =>
        {
            Dialog.UseDarkTitleBar(this);
            // Como mucho el alto de la pantalla donde está la app (no la principal), y entera dentro de ella.
            MaxHeight = Dialog.WorkAreaOf(Owner ?? this).Height - 40;
        };
        Loaded += (_, _) => Dialog.KeepInside(this, Dialog.WorkAreaOf(Owner ?? this));
    }

    /// <summary>Vuelca los ajustes guardados en los controles (también si cambian por fuera, p. ej. con Ctrl + Shift + B).</summary>
    public void LoadValues()
    {
        var s = _store.Settings;
        _loading = true;
        try
        {
            MoveRegionsCheck.IsChecked = s.MoveWholeRegions;
            EdgeFrameCheck.IsChecked = s.ShowDraggedEdgeFrame;
            HoverPreviewCheck.IsChecked = s.HoverPreview;
            CachedLineCheck.IsChecked = s.ShowCachedFrames;
            MarksCheck.IsChecked = s.ShowBookmarkMarks;
            SetNumber(SeekBox, s.SeekSeconds);
            SetNumber(SeekShiftBox, s.SeekSecondsShift);
            SetNumber(SeekCtrlBox, s.SeekSecondsCtrl);
            ScrubSlider.Value = Math.Clamp(s.ScrubPixelsPerFrame, 1, 20);
            ScrubText.Text = $"{ScrubSlider.Value:0} px por frame";
            (s.WheelOnVideo == Settings.WheelVolume ? WheelVolumeRadio : WheelSpeedRadio).IsChecked = true;
            ResumeCheck.IsChecked = s.ResumePlayback;
            (s.TimeWithFrames ? TimeFramesRadio : TimeMsRadio).IsChecked = true;
            BuildColorSwatches();
            RepeatLabelCheck.IsChecked = s.RepeatLastLabel;
            (s.BookmarkTimesInList ? TimesListRadio : TimesPreviewRadio).IsChecked = true;
            SelectLanguage(AudioLangCombo, s.AudioLanguage);
            SelectLanguage(SubLangCombo, s.SubtitleLanguage);
            SubsOnOpenCheck.IsChecked = s.SubtitlesOnOpen;
            CacheSlider.Value = CacheSetting(s);
            HwdecCheck.IsChecked = s.HardwareDecoding;
            ClipClipboardCheck.IsChecked = s.CopyClipToClipboard;
            LightClipSlider.Value = Math.Clamp(s.LightClipMegabytesPerSecond, LightClipSlider.Minimum, LightClipSlider.Maximum);
        }
        finally
        {
            _loading = false;
        }
        UpdateCacheText();
        UpdateLightClipText();
        UpdateClipFolderUi();
    }

    // ---- Clips ----

    private void UpdateLightClipText() => LightClipText.Text = $"{LightClipSlider.Value.ToString("0.##", Spanish)} MB/s";

    private void UpdateClipFolderUi()
    {
        var folder = _store.Settings.ClipFolder;
        ClipFolderText.Text = folder is { Length: > 0 } ? folder : "Junto a cada vídeo";
        ClipFolderText.ToolTip = folder;
        ClipFolderBesideButton.IsEnabled = folder is { Length: > 0 };
    }

    private void OnChangeClipFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Carpeta para los clips exportados" };
        if (_store.Settings.ClipFolder is { Length: > 0 } current && Directory.Exists(current))
            dialog.InitialDirectory = current;
        if (dialog.ShowDialog(this) != true)
            return;
        Change(() => _store.Settings.ClipFolder = dialog.FolderName);
        UpdateClipFolderUi();
    }

    private void OnClipFolderBesideClick(object sender, RoutedEventArgs e)
    {
        Change(() => _store.Settings.ClipFolder = null);
        UpdateClipFolderUi();
    }

    // Solo los de la paleta: un color concreto se le pone a una etiqueta.
    private void BuildColorSwatches()
    {
        var s = _store.Settings;
        ColorPanel.Children.Clear();
        if (!BookmarkColors.Palette.Any(c => Same(c.Hex, s.NewBookmarkColor)))
            s.NewBookmarkColor = BookmarkColors.Default;
        foreach (var (name, hex) in BookmarkColors.Palette)
        {
            var swatch = new RadioButton
            {
                Style = (Style)FindResource("Swatch"), GroupName = "Color", Tag = hex, ToolTip = name,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), IsChecked = Same(hex, s.NewBookmarkColor),
            };
            swatch.Checked += (_, _) => Change(() => s.NewBookmarkColor = hex);
            ColorPanel.Children.Add(swatch);
        }
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void Change(Action apply)
    {
        if (_loading)
            return;
        apply();
        _store.SaveSettings();
        _changed();
    }

    private void Bind(CheckBox box, Action<bool> set)
    {
        box.Checked += (_, _) => Change(() => set(true));
        box.Unchecked += (_, _) => Change(() => set(false));
    }

    // "Sin preferencia" y los idiomas por su nombre; se guarda el código.
    private void FillLanguages(ComboBox combo, Action<string> set)
    {
        combo.Items.Add(new ComboBoxItem { Content = "Sin preferencia", Tag = "" });
        foreach (var language in Languages.All)
            combo.Items.Add(new ComboBoxItem { Content = language.Name, Tag = language.Code });
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem { Tag: string code })
                Change(() => set(code));
        };
    }

    private static void SelectLanguage(ComboBox combo, string code) =>
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => string.Equals((string)i.Tag, code, StringComparison.OrdinalIgnoreCase))
            ?? combo.Items[0];

    // Un número válido se aplica al escribirlo; uno no válido se pinta en rojo y no cambia nada.
    private void BindNumber(TextBox box, Action<double> set)
    {
        const double min = 0.04, max = 3600;
        box.TextChanged += (_, _) =>
        {
            var valid = TryParse(box.Text, out var value) && value >= min && value <= max;
            box.Foreground = (Brush)FindResource(valid ? "Fg" : "Error");
            box.ToolTip = valid ? null : "Entre 0,04 y 3600 segundos";
            if (valid)
                Change(() => set(value));
        };
        // Al salir de un campo no válido, vuelve a lo guardado.
        box.LostKeyboardFocus += (_, _) =>
        {
            if (!TryParse(box.Text, out var value) || value < min || value > max)
                LoadValues();
        };
    }

    // Con coma (español) o con punto.
    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, Spanish, out value) && !text.Contains('.')
        || double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static void SetNumber(TextBox box, double value) => box.Text = value.ToString("0.##", Spanish);

    private int CacheSetting(Settings s) => s.FrameCacheMegabytes > 0 ? s.FrameCacheMegabytes : _recommendedCacheMegabytes;

    // En pasos de 256 MB también si el valor no viene del ratón.
    private int CacheMegabytes => 512 + (int)Math.Round((CacheSlider.Value - 512) / 256) * 256;

    private void UpdateCacheText()
    {
        var mb = CacheMegabytes;
        var over = _vramMegabytes is { } vram && mb > vram * 0.9;
        CacheText.Text = _vramMegabytes is { } total ? $"{Number(mb, "0.##")} de {Number(total, "0.#")} GB" : GigaBytes(mb);
        CacheText.Foreground = (Brush)FindResource(over ? "Error" : "FgMuted");
        CacheSlider.ToolTip = over ? "Más del 90 % de la memoria de la tarjeta: puede faltar para otros programas." : null;
    }

    private static string Number(double megabytes, string format) => (megabytes / 1024).ToString(format, Spanish);

    private static string GigaBytes(double megabytes) =>
        megabytes >= 1024 ? $"{(megabytes / 1024).ToString("0.#", Spanish)} GB" : $"{megabytes:0} MB";

    // ---- Carpeta de datos ----

    private void UpdateFolderUi()
    {
        DataFolderText.Text = _store.Folder;
        DataFolderText.ToolTip = _store.Folder;
        // Elegida, pero se usará al reiniciar.
        var location = DataStore.ReadLocation();
        var next = location?.Folder is { Length: > 0 } folder ? folder : DataStore.DefaultFolder();
        if (!DataStore.SamePath(next, _store.Folder))
        {
            PendingFolderText.Text = $"Al reiniciar se usará {next}" + (location?.CopyFrom != null ? ", con una copia de los datos actuales." : ".");
            PendingFolderText.Visibility = Visibility.Visible;
        }
        else
        {
            PendingFolderText.Visibility = Visibility.Collapsed;
        }
        DefaultFolderButton.IsEnabled = !DataStore.SamePath(next, DataStore.DefaultFolder());
    }

    private void OnChangeDataFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Carpeta para los datos de VideoSeeker" };
        if (Path.GetDirectoryName(_store.Folder) is { } parent && Directory.Exists(parent))
            dialog.InitialDirectory = parent;
        if (dialog.ShowDialog(this) == true)
            ChooseDataFolder(dialog.FolderName);
    }

    private void OnDefaultDataFolderClick(object sender, RoutedEventArgs e) => ChooseDataFolder(DataStore.DefaultFolder());

    /// <summary>
    /// Elige otra carpeta de datos para el próximo arranque, preguntando qué hacer con los datos. Nunca se borra
    /// nada: los datos se copian (al arrancar, cuando esta instancia ya no escribe) y la carpeta de antes se queda.
    /// </summary>
    private void ChooseDataFolder(string target)
    {
        const string caption = "Carpeta de datos";
        var current = _store.Folder;
        try
        {
            if (DataStore.SamePath(target, current))
            {
                // Vuelta a la actual: se anula lo que estuviera pendiente.
                DataStore.WriteLocation(current, null);
                UpdateFolderUi();
                return;
            }
            if (DataStore.Nested(target, current))
            {
                Dialog.Inform(this, "La carpeta de datos no puede estar dentro de la actual ni contenerla.", caption);
                return;
            }
            Directory.CreateDirectory(target);
            var probe = Path.Combine(target, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);

            string? copyFrom;
            if (DataStore.HasData(target))
            {
                // 0: usar los de allí; 1: copiar los actuales encima (los de allí se apartan).
                var answer = Dialog.Choose(this,
                    $"{target}\n\nya tiene datos de VideoSeeker. Si copias allí los actuales, los que había se guardan en su subcarpeta «anteriores».",
                    caption, "Usar esos datos", "Copiar los actuales", "Cancelar");
                if (answer is < 0 or 2)
                    return;
                copyFrom = answer == 1 ? current : null;
            }
            else
            {
                var answer = Dialog.Choose(this,
                    $"¿Llevar tus datos (ajustes, bookmarks, posición de cada vídeo) a\n{target}?\n\nLa carpeta actual se queda como está.",
                    caption, "Llevar los datos", "Empezar vacía", "Cancelar");
                if (answer is < 0 or 2)
                    return;
                copyFrom = answer == 0 ? current : null;
            }
            DataStore.WriteLocation(target, copyFrom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialog.Inform(this, $"No se puede usar esa carpeta:\n{ex.Message}", caption);
            return;
        }
        UpdateFolderUi();
        if (Dialog.Confirm(this, "La nueva carpeta de datos se usará al reiniciar VideoSeeker.", "Reiniciar ahora", "Más tarde", caption))
            _restart();
    }

    private void OnOpenDataFolderClick(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_store.Folder}\"") { UseShellExecute = true });

    // ---- Apoyar y código fuente ----

    private void OnSupportClick(object sender, RoutedEventArgs e) => OpenLink(AppLinks.SupportUrl);

    private void OnSourceClick(object sender, RoutedEventArgs e) => OpenLink(AppLinks.SourceUrl);

    private static void OpenLink(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    /// <summary>La versión que pone make-portable.ps1 (fecha y commit), sin el hash que añade .NET.</summary>
    private static string AppVersion()
    {
        var info = typeof(SettingsWindow).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion;
        if (string.IsNullOrEmpty(info)) return "";
        int plus = info.IndexOf('+');
        return "Versión " + (plus > 0 ? info[..plus] : info);
    }

    // ---- Abrir con doble clic ----

    private void OnChooseInWindowsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            FileAssociations.Register();
            UnregisterButton.IsEnabled = true;
            FileAssociations.OpenWindowsSettings();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or System.ComponentModel.Win32Exception)
        {
            Dialog.Inform(this, $"No se pudieron registrar los tipos de vídeo:\n{ex.Message}", "Abrir con doble clic");
        }
    }

    private void OnUnregisterClick(object sender, RoutedEventArgs e)
    {
        try
        {
            FileAssociations.Unregister();
            UnregisterButton.IsEnabled = false;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Dialog.Inform(this, $"No se pudo quitar el registro:\n{ex.Message}", "Abrir con doble clic");
        }
    }

    // ---- Ventana ----

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Esc o Ctrl + , (el mismo atajo que la abre) la cierran; con un desplegable abierto, Esc solo lo cierra.
        if ((e.Key == Key.Escape && !AudioLangCombo.IsDropDownOpen && !SubLangCombo.IsDropDownOpen)
            || (e.Key == Key.OemComma && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)))
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (!Dialog.Confirm(this, "Se volverán a poner los valores por defecto de todos los ajustes.", "Restablecer", title: "Restablecer ajustes"))
            return;
        _store.Settings.ResetPreferences();
        _store.SaveSettings();
        LoadValues();
        _changed();
        // La carpeta de datos también, con las mismas preguntas que su botón (qué hacer con los datos, reiniciar).
        var next = DataStore.ReadLocation()?.Folder is { Length: > 0 } chosen ? chosen : DataStore.DefaultFolder();
        if (!DataStore.SamePath(next, DataStore.DefaultFolder()) || !DataStore.SamePath(_store.Folder, DataStore.DefaultFolder()))
            ChooseDataFolder(DataStore.DefaultFolder());
    }
}
