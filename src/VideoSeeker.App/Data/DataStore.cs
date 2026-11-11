using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace VideoSeeker.App.Data;

public sealed class Settings
{
    public double Volume { get; set; } = 100;
    public bool Mute { get; set; }
    /// <summary>Memoria de la caché de frames; 0 (por defecto), la recomendada para la tarjeta.</summary>
    public int FrameCacheMegabytes { get; set; }
    /// <summary>Píxeles de arrastre sobre el vídeo por cada frame de scrubbing.</summary>
    public int ScrubPixelsPerFrame { get; set; } = 6;
    /// <summary>Marcas de los bookmarks en las barras (el panel y los atajos funcionan igual sin ellas).</summary>
    public bool ShowBookmarkMarks { get; set; } = true;
    /// <summary>Ancho del panel de bookmarks, arrastrando su borde izquierdo.</summary>
    public double BookmarkPanelWidth { get; set; } = DefaultPanelWidth;
    public const double DefaultPanelWidth = 330;
    /// <summary>Bucle del vídeo entero (R o el botón junto al play): se recuerda para los siguientes vídeos y al volver a abrir la app.</summary>
    public bool Loop { get; set; }
    /// <summary>Bucle del tramo ampliado (Shift + R o el botón junto a la X del tramo): se recuerda para los siguientes tramos.</summary>
    public bool LoopRange { get; set; }
    /// <summary>En pantalla completa, los controles se quedan fijos (chincheta o Tab) en vez de esconderse.</summary>
    public bool PinFullscreenControls { get; set; }
    /// <summary>Colección de bookmarks activa (carpeta dentro de data\bookmarks).</summary>
    public string BookmarkCollection { get; set; } = BookmarkCollections.Default;
    /// <summary>Dónde estaba la ventana al cerrarla; null la primera vez (centrada).</summary>
    public WindowBounds? Window { get; set; }

    // ---- Ventana de ajustes ----

    /// <summary>
    /// Arrastrar el centro del tramo ampliado o de la banda de una región la mueve entera. Si no, el centro se
    /// comporta como la pista (seek / scrubbing); los extremos se arrastran siempre.
    /// </summary>
    public bool MoveWholeRegions { get; set; }
    /// <summary>
    /// Al arrastrar un extremo del tramo ampliado o de un bookmark (o moverlo entero), la imagen enseña el frame de ese
    /// extremo. Si no, se queda en el frame que se veía.
    /// </summary>
    public bool ShowDraggedEdgeFrame { get; set; } = true;
    /// <summary>Al pasar el ratón por las barras, el frame que hay bajo el cursor en miniatura (sin ir a él).</summary>
    public bool HoverPreview { get; set; } = true;
    /// <summary>Línea verde de la caché de frames bajo las barras (la caché funciona igual sin ella).</summary>
    public bool ShowCachedFrames { get; set; }
    /// <summary>Con las marcas ocultas, al crear un bookmark o ir a uno, su botón late unas veces (para ver por qué no salen).</summary>
    public bool PulseHiddenMarks { get; set; } = true;
    /// <summary>Segundos que saltan ← / → (y la rueda sobre la barra principal), con Shift y con Ctrl.</summary>
    public double SeekSeconds { get; set; } = 5;
    public double SeekSecondsShift { get; set; } = 1;
    public double SeekSecondsCtrl { get; set; } = 30;
    /// <summary>Qué cambia la rueda del ratón sobre el vídeo: WheelSpeed (por defecto) o WheelVolume.</summary>
    public string WheelOnVideo { get; set; } = WheelSpeed;
    public const string WheelSpeed = "speed", WheelVolume = "volume";
    /// <summary>Abrir cada vídeo donde se dejó.</summary>
    public bool ResumePlayback { get; set; } = true;
    /// <summary>Tiempos precisos como timecode hh:mm:ss:ff en vez de con milisegundos.</summary>
    public bool TimeWithFrames { get; set; }
    /// <summary>Color de los bookmarks nuevos (de BookmarkColors.Palette).</summary>
    public string NewBookmarkColor { get; set; } = BookmarkColors.Default;
    /// <summary>Los bookmarks nuevos llevan la última etiqueta que se puso (en el cuadro del nombre se cambia o se quita).</summary>
    public bool RepeatLastLabel { get; set; } = true;
    /// <summary>
    /// Tiempos y duración de cada bookmark en las filas del panel (por defecto). Si no, van en su vista previa y el
    /// nombre tiene más líneas en la fila.
    /// </summary>
    public bool BookmarkTimesInList { get; set; } = true;
    /// <summary>
    /// Idioma preferido de audio y de subtítulos: código de Languages ("spa"); "" sin preferencia; null, el de
    /// Windows (por defecto, mientras no se elija otro). Con otro nombre en el JSON que en las primeras versiones, que
    /// guardaban "" por defecto: así esos pasan a tomar el de Windows.
    /// </summary>
    [JsonPropertyName("audioLanguage")]
    public string? AudioLanguages { get; set; }
    [JsonPropertyName("subtitleLanguage")]
    public string? SubtitleLanguages { get; set; }

    /// <summary>El idioma que se usa: el elegido o, si no se eligió, el de Windows ("" sin preferencia).</summary>
    [JsonIgnore]
    public string AudioLanguage => AudioLanguages ?? Languages.Windows;
    [JsonIgnore]
    public string SubtitleLanguage => SubtitleLanguages ?? Languages.Windows;
    /// <summary>
    /// Al abrir un vídeo con subtítulos, activar una pista (la del idioma preferido si la hay). Otro nombre en el
    /// JSON que en la primera versión, que lo activaba por defecto: así vuelve a quedar desactivado una vez.
    /// </summary>
    [JsonPropertyName("enableSubtitlesOnOpen")]
    public bool SubtitlesOnOpen { get; set; }
    /// <summary>Decodificación por hardware en mpv (hwdec=auto-safe); si no, por CPU.</summary>
    public bool HardwareDecoding { get; set; } = true;
    /// <summary>Carpeta donde se guardan los clips exportados; vacía o null, junto a su vídeo.</summary>
    public string? ClipFolder { get; set; }
    /// <summary>Copiar al portapapeles cada clip exportado, para pegarlo en un chat o en Jira.</summary>
    public bool CopyClipToClipboard { get; set; } = true;
    /// <summary>Tamaño del clip ligero, en MB por segundo (vídeo y audio juntos).</summary>
    public double LightClipMegabytesPerSecond { get; set; } = 1;

    /// <summary>
    /// Idioma de la interfaz (código de Localization.Loc.All, p. ej. "en"); null, el de Windows. Se aplica al
    /// reiniciar; no vuelve al de por defecto al restablecer los ajustes.
    /// </summary>
    public string? UiLanguage { get; set; }

    /// <summary>
    /// Vuelve a los valores por defecto lo que se cambia en la ventana de ajustes (no el volumen, la colección ni el
    /// idioma de la interfaz, que pediría reiniciar).
    /// </summary>
    public void ResetPreferences()
    {
        var d = new Settings();
        MoveWholeRegions = d.MoveWholeRegions;
        ShowDraggedEdgeFrame = d.ShowDraggedEdgeFrame;
        HoverPreview = d.HoverPreview;
        ShowCachedFrames = d.ShowCachedFrames;
        ShowBookmarkMarks = d.ShowBookmarkMarks;
        PulseHiddenMarks = d.PulseHiddenMarks;
        SeekSeconds = d.SeekSeconds;
        SeekSecondsShift = d.SeekSecondsShift;
        SeekSecondsCtrl = d.SeekSecondsCtrl;
        ScrubPixelsPerFrame = d.ScrubPixelsPerFrame;
        WheelOnVideo = d.WheelOnVideo;
        ResumePlayback = d.ResumePlayback;
        TimeWithFrames = d.TimeWithFrames;
        NewBookmarkColor = d.NewBookmarkColor;
        RepeatLastLabel = d.RepeatLastLabel;
        BookmarkTimesInList = d.BookmarkTimesInList;
        AudioLanguages = d.AudioLanguages;
        SubtitleLanguages = d.SubtitleLanguages;
        SubtitlesOnOpen = d.SubtitlesOnOpen;
        FrameCacheMegabytes = d.FrameCacheMegabytes;
        HardwareDecoding = d.HardwareDecoding;
        ClipFolder = d.ClipFolder;
        CopyClipToClipboard = d.CopyClipToClipboard;
        LightClipMegabytesPerSecond = d.LightClipMegabytesPerSecond;
    }
}

/// <summary>Rectángulo de la ventana sin maximizar, en píxeles de pantalla, y si estaba maximizada.</summary>
public sealed class WindowBounds
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Maximized { get; set; }
}

/// <summary>Lo que recordamos de cada vídeo abierto, indexado por su huella.</summary>
public sealed class VideoEntry
{
    public string Path { get; set; } = "";
    public double Duration { get; set; }
    public double LastPosition { get; set; }
    /// <summary>Fps del contenedor, para los timecodes de sus bookmarks en el panel sin abrirlo; 0 si no se sabe.</summary>
    public double Fps { get; set; }
    public DateTime LastOpened { get; set; }
}

public sealed class Library
{
    public Dictionary<string, VideoEntry> Videos { get; set; } = [];
}

/// <summary>Carpeta de datos elegida en los ajustes (datalocation.json, fuera de la carpeta de datos).</summary>
public sealed class DataLocation
{
    public string Folder { get; set; } = "";
    /// <summary>Carpeta cuyos datos hay que copiar a la nueva al arrancar (se elige con la app abierta, que sigue escribiendo en la anterior).</summary>
    public string? CopyFrom { get; set; }
}

/// <summary>
/// Datos de la app en JSON legibles: en data\ junto al exe, o en %LOCALAPPDATA%\VideoSeeker
/// si ahí no se puede escribir (por ejemplo en Program Files). Se puede elegir otra carpeta en los ajustes.
/// </summary>
public sealed class DataStore
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Código generado al compilar para nuestros archivos (más rápido al arrancar que por reflexión); por si acaso,
        // cualquier otro tipo sigue yendo por reflexión.
        TypeInfoResolver = JsonTypeInfoResolver.Combine(DataJsonContext.Default, new DefaultJsonTypeInfoResolver()),
    };

    private readonly Lock _gate = new();

    public string Folder { get; }
    public Settings Settings { get; }
    public Library Library { get; }

    public DataStore()
    {
        Folder = ResolveFolder();
        Settings = Load<Settings>("settings.json");
        if (Settings.AudioLanguages != null)
            Settings.AudioLanguages = Languages.Normalize(Settings.AudioLanguages);
        if (Settings.SubtitleLanguages != null)
            Settings.SubtitleLanguages = Languages.Normalize(Settings.SubtitleLanguages);
        // 3 GB fue el valor por defecto fijo de las primeras versiones: quien lo tiene casi seguro que no lo eligió, y
        // pasa a la recomendada para su tarjeta (con 6 GB o más, la misma).
        if (Settings.FrameCacheMegabytes == 3072)
            Settings.FrameCacheMegabytes = 0;
        Library = Load<Library>("library.json");
    }

    public void SaveSettings() => Save("settings.json", Settings);

    public void SaveLibrary() => Save("library.json", Library);

    /// <summary>
    /// Carpeta de datos sin elegir otra: data\ junto al exe o, si no se puede escribir ahí, %LOCALAPPDATA%\VideoSeeker.
    /// Instalada desde la Store, data\ en la carpeta propia del paquete (<see cref="Packaged.LocalState"/>).
    /// </summary>
    public static string DefaultFolder()
    {
        if (Packaged.LocalState is { } state)
        {
            var data = Path.Combine(state, "data");
            Directory.CreateDirectory(data);
            return data;
        }
        var besideExe = Path.Combine(AppContext.BaseDirectory, "data");
        if (IsWritable(besideExe))
            return besideExe;
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoSeeker");
        Directory.CreateDirectory(appData);
        return appData;
    }

    private static readonly string LocationBesideExe = Path.Combine(AppContext.BaseDirectory, "datalocation.json");
    // En el paquete, junto a su data\ (si no, %LOCALAPPDATA% iría a una carpeta desviada).
    private static readonly string LocationInAppData = Packaged.LocalState is { } state
        ? Path.Combine(state, "datalocation.json")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoSeeker", "datalocation.json");

    /// <summary>La carpeta elegida en los ajustes (aunque no se use hasta reiniciar); null si no se eligió otra.</summary>
    public static DataLocation? ReadLocation() =>
        (Packaged.IsPackaged ? null : LoadFile<DataLocation>(LocationBesideExe)) ?? LoadFile<DataLocation>(LocationInAppData);

    /// <summary>
    /// Elige la carpeta de datos para el próximo arranque; con "copyFrom", al arrancar se copian ahí esos datos.
    /// La de por defecto sin nada que copiar quita la elección.
    /// </summary>
    public static void WriteLocation(string folder, string? copyFrom)
    {
        foreach (var file in new[] { LocationBesideExe, LocationInAppData })
            foreach (var path in new[] { file, file + ".bak" })
                if (File.Exists(path))
                    File.Delete(path);
        if (copyFrom == null && SamePath(folder, DefaultFolder()))
            return;
        // Junto al exe si se puede escribir ahí; si no, en %LOCALAPPDATA%.
        var target = !Packaged.IsPackaged && IsWritable(AppContext.BaseDirectory) ? LocationBesideExe : LocationInAppData;
        SaveFile(target, new DataLocation { Folder = Path.GetFullPath(folder), CopyFrom = copyFrom });
    }

    public static bool SamePath(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Si una está dentro de la otra (o son la misma): no se puede copiar de una a la otra.</summary>
    public static bool Nested(string a, string b)
    {
        var (x, y) = (Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)) + "\\", Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)) + "\\");
        return x.StartsWith(y, StringComparison.OrdinalIgnoreCase) || y.StartsWith(x, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Si la carpeta tiene datos de VideoSeeker.</summary>
    public static bool HasData(string folder) =>
        File.Exists(Path.Combine(folder, "settings.json")) || File.Exists(Path.Combine(folder, "library.json"))
        || Directory.Exists(Path.Combine(folder, "bookmarks"));

    private static string ResolveFolder()
    {
        var fallback = DefaultFolder();
        if (ReadLocation() is not { Folder.Length: > 0 } location || !IsWritable(location.Folder))
            return fallback;
        if (location.CopyFrom is { } from)
        {
            try
            {
                if (Directory.Exists(from) && !Nested(from, location.Folder))
                    CopyData(from, location.Folder);
                WriteLocation(location.Folder, null);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Se sigue con los datos donde estaban; se reintentará en el próximo arranque.
                return IsWritable(from) ? from : fallback;
            }
        }
        return location.Folder;
    }

    /// <summary>
    /// Copia los datos de una carpeta a otra y comprueba que llegaron enteros. Si en el destino ya había datos,
    /// antes se apartan a anteriores\&lt;fecha&gt;, para no perder nada. El origen no se toca.
    /// </summary>
    public static void CopyData(string from, string to)
    {
        Directory.CreateDirectory(to);
        if (HasData(to))
        {
            var aside = Path.Combine(to, "anteriores", DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss"));
            Directory.CreateDirectory(aside);
            foreach (var entry in Directory.EnumerateFileSystemEntries(to))
            {
                var name = Path.GetFileName(entry);
                if (name == "anteriores")
                    continue;
                if (Directory.Exists(entry))
                    Directory.Move(entry, Path.Combine(aside, name));
                else
                    File.Move(entry, Path.Combine(aside, name));
            }
        }
        var source = Path.GetFullPath(from);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.StartsWith("anteriores" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                continue;
            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
            if (new FileInfo(target).Length != new FileInfo(file).Length)
                throw new IOException(S.DataCopyIncomplete(relative));
        }
    }

    private static bool IsWritable(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Lee un JSON de la carpeta de datos (ruta relativa) o de cualquier sitio (absoluta).</summary>
    public T Load<T>(string name) where T : class, new() => LoadFile<T>(Path.Combine(Folder, name)) ?? new T();

    /// <summary>El JSON del archivo (o de su copia .bak si está dañado); null si no hay ninguno válido.</summary>
    public static T? LoadFile<T>(string path) where T : class
    {
        foreach (var candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (File.Exists(candidate) && JsonSerializer.Deserialize<T>(File.ReadAllText(candidate), Json) is { } value)
                    return value;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                // Archivo dañado (o sin permiso): probamos la copia anterior.
            }
        }
        return null;
    }

    /// <summary>Guarda un JSON en la carpeta de datos (ruta relativa) o en cualquier sitio (absoluta).</summary>
    public void Save<T>(string name, T value)
    {
        lock (_gate)
            SaveFile(Path.Combine(Folder, name), value);
    }

    // Escritura atómica: temporal + reemplazo, conservando la versión anterior como .bak.
    public static void SaveFile<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
        if (File.Exists(path))
            File.Replace(temp, path, path + ".bak");
        else
            File.Move(temp, path);
    }
}

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(Library))]
[JsonSerializable(typeof(DataLocation))]
[JsonSerializable(typeof(BookmarkFile))]
[JsonSerializable(typeof(LabelFile))]
internal sealed partial class DataJsonContext : JsonSerializerContext;
