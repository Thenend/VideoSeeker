using System.Globalization;
using System.IO;
using System.IO.Hashing;
using System.Text;

namespace VideoSeeker.App.Data;

/// <summary>Una región con nombre (inicio y fin). Los tiempos son instantes en segundos, no frames.</summary>
public sealed class Bookmark
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double Start { get; set; }
    /// <summary>
    /// Fin de la región (exclusivo: el inicio del frame siguiente al último). Los archivos de antes (y los escritos a
    /// mano) pueden no tenerlo: entonces es una región de un frame, que BookmarkSet completa al leerlos.
    /// </summary>
    public double? End { get; set; }
    /// <summary>Color propio; con etiqueta se pinta con el de la etiqueta (y vuelve a este si se le quita).</summary>
    public string Color { get; set; } = BookmarkColors.Default;
    /// <summary>Id de su etiqueta (de las de su colección); null, sin etiqueta.</summary>
    public string? Label { get; set; }
    /// <summary>Instante del frame elegido como miniatura; null, el del inicio.</summary>
    public double? Thumb { get; set; }

    /// <summary>Instante del frame que se usa de miniatura.</summary>
    public double ThumbTime => Thumb ?? Start;
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }

    /// <summary>El fin o, sin él (archivos de antes), el de un frame de "frameDuration" segundos.</summary>
    public double EndOr(double frameDuration) => End is { } end && end > Start + 1e-6 ? end : Start + frameDuration;

    public Bookmark Clone() => (Bookmark)MemberwiseClone();
}

/// <summary>Bookmarks de un vídeo: data\bookmarks\&lt;huella&gt;.json, o el &lt;vídeo&gt;.bookmarks.json que lo acompaña.</summary>
public sealed class BookmarkFile
{
    /// <summary>Nombre del archivo de vídeo, para reconocerlo a mano y para el panel de "todos".</summary>
    public string Video { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public List<Bookmark> Bookmarks { get; set; } = [];
    /// <summary>Ids borrados aquí: así no vuelven a entrar al mezclar el archivo compartido junto al vídeo.</summary>
    public List<string>? Deleted { get; set; }
    /// <summary>Solo al compartir: las etiquetas que usan estos bookmarks (en data\ van en el labels.json de la colección).</summary>
    public List<BookmarkLabel>? Labels { get; set; }
}

/// <summary>
/// Una colección entera para compartirla (&lt;colección&gt;.collection.json): los bookmarks de cada vídeo con su huella,
/// así encajan aunque el otro tenga los vídeos con otro nombre o en otra carpeta.
/// </summary>
public sealed class BookmarkCollectionFile
{
    public string Collection { get; set; } = "";
    public List<BookmarkLabel>? Labels { get; set; }
    public List<BookmarkFile> Videos { get; set; } = [];
}

/// <summary>Etiqueta de bookmarks ("Boss", "Puzzle", "Cinemática"…): un nombre con su color, de una colección.</summary>
public sealed class BookmarkLabel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = BookmarkColors.Default;

    public BookmarkLabel Clone() => (BookmarkLabel)MemberwiseClone();
}

/// <summary>Las etiquetas de una colección, en orden (el de Alt + 1…9): data\bookmarks\&lt;colección&gt;\labels.json.</summary>
public sealed class LabelFile
{
    public List<BookmarkLabel> Labels { get; set; } = [];
    /// <summary>La última que se puso a un bookmark: la llevan los nuevos si el ajuste lo pide.</summary>
    public string? Last { get; set; }
    /// <summary>Si lo último que se eligió fue un color suelto (sin etiqueta), ese color; entonces Last es null.</summary>
    public string? LastColor { get; set; }

    public BookmarkLabel? Find(string? id) => id == null ? null : Labels.FirstOrDefault(l => l.Id == id);
}

public static class BookmarkLabels
{
    public const string FileName = "labels.json";

    public static string PathOf(DataStore store, string collection) =>
        Path.Combine(BookmarkCollections.PathOf(store, collection), FileName);

    public static LabelFile Load(DataStore store, string collection) =>
        DataStore.LoadFile<LabelFile>(PathOf(store, collection)) ?? new LabelFile();

    /// <summary>Lee, cambia y guarda las etiquetas de una colección; devuelve el texto antes y después, para el historial.</summary>
    public static (string? Before, string After) Modify(DataStore store, string collection, Action<LabelFile> change)
    {
        var path = PathOf(store, collection);
        var before = BookmarkFiles.ReadText(path);
        var data = DataStore.LoadFile<LabelFile>(path) ?? new LabelFile();
        change(data);
        DataStore.SaveFile(path, data);
        return (before, BookmarkFiles.ReadText(path)!);
    }

    public static string NewId() => "l" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>El primer color de la paleta que no lleva ninguna etiqueta (o el que menos, si están todos).</summary>
    public static string NextColor(LabelFile file) =>
        BookmarkColors.Palette.Select(p => p.Hex)
            .OrderBy(hex => file.Labels.Count(l => string.Equals(l.Color, hex, StringComparison.OrdinalIgnoreCase)))
            .First();

    /// <summary>
    /// Mete en "file" las etiquetas que vienen de otro sitio (otra colección, un archivo compartido): las que ya están
    /// (mismo id o mismo nombre) no se tocan. Devuelve qué id usar aquí para cada id de fuera que cambia.
    /// </summary>
    public static Dictionary<string, string> Merge(LabelFile file, IEnumerable<BookmarkLabel>? incoming)
    {
        var map = new Dictionary<string, string>();
        foreach (var label in incoming ?? [])
        {
            if (string.IsNullOrWhiteSpace(label.Id) || file.Find(label.Id) != null)
                continue;
            if (file.Labels.FirstOrDefault(l => string.Equals(l.Name, label.Name, StringComparison.CurrentCultureIgnoreCase)) is { } same)
                map[label.Id] = same.Id;
            else
                file.Labels.Add(label.Clone());
        }
        return map;
    }

    /// <summary>Cambia las etiquetas de unos bookmarks según "map" (lo que devuelve Merge).</summary>
    public static void Remap(IEnumerable<Bookmark> bookmarks, Dictionary<string, string> map)
    {
        if (map.Count == 0)
            return;
        foreach (var b in bookmarks)
            if (b.Label != null && map.TryGetValue(b.Label, out var to))
                b.Label = to;
    }

    /// <summary>Las etiquetas de la colección que llevan esos bookmarks, para compartirlas con ellos; null si ninguna.</summary>
    public static List<BookmarkLabel>? UsedBy(LabelFile file, IEnumerable<Bookmark> bookmarks)
    {
        var used = bookmarks.Select(b => b.Label).Where(id => id != null).ToHashSet();
        var labels = file.Labels.Where(l => used.Contains(l.Id)).ToList();
        return labels.Count > 0 ? labels : null;
    }

    /// <summary>
    /// Las etiquetas de "from" que llevan los bookmarks, metidas en las de "to" (al moverlos de colección). Cambia la
    /// etiqueta de los bookmarks si en "to" ya hay una con ese nombre. Devuelve el cambio en el archivo, o null si no hay.
    /// </summary>
    public static BookmarkHistory.Entry? CopyTo(DataStore store, string from, string to, List<Bookmark> bookmarks)
    {
        if (UsedBy(Load(store, from), bookmarks) is not { } used)
            return null;
        var map = new Dictionary<string, string>();
        var (before, after) = Modify(store, to, file => map = Merge(file, used));
        Remap(bookmarks, map);
        return new(PathOf(store, to), before, after);
    }
}

public static class BookmarkColors
{
    public const string Default = "#E5539B";

    /// <summary>Paleta del menú: no repite el ámbar del tramo, el azul de lo reproducido ni el verde de la caché.</summary>
    public static (string Name, string Hex)[] Palette =>
    [
        (S.ColorPink, "#E5539B"), (S.ColorRed, "#E5484D"), (S.ColorOrange, "#F76B15"),
        (S.ColorViolet, "#8E6CEF"), (S.ColorCyan, "#2EC8D6"), (S.ColorLime, "#A8D84E"), (S.ColorBrown, "#B07A52"), (S.ColorGray, "#8B8D98"),
        (S.ColorWhite, "#E6E8EB"),
    ];
}

/// <summary>
/// Los bookmarks del vídeo abierto, ordenados por inicio. Cada cambio se guarda al momento en
/// data\bookmarks\&lt;colección&gt; y queda apuntado en el historial de deshacer de la app; al abrir se mezcla el
/// archivo compartido junto al vídeo.
/// </summary>
public sealed class BookmarkSet
{
    private readonly DataStore _store;
    private readonly BookmarkHistory _history;
    private readonly string _file;
    private BookmarkFile _data;
    // Arrastrando: cómo estaba el archivo al empezar, para apuntar el arrastre entero como un solo paso.
    private (bool Active, string? Before, string Label) _edit;
    // Inicio del frame siguiente al que empieza en un instante (con el índice del vídeo o su fps).
    private readonly Func<double, double> _frameEnd;
    // Los que en el archivo no tienen fin (de antes de que todo fuera región): se les pone el de un frame en memoria y
    // se recalcula si cambia la rejilla de frames; el archivo no se reescribe hasta que se edite algo.
    private readonly HashSet<string> _openEnds = [];

    public string Fingerprint { get; }
    public string VideoPath { get; }
    /// <summary>Colección a la que pertenecen (cada una tiene sus propios bookmarks de cada vídeo).</summary>
    public string Collection { get; }
    /// <summary>Ruta completa del archivo de estos bookmarks.</summary>
    public string FilePath => Path.Combine(_store.Folder, _file);
    public string VideoName => _data.Video;

    /// <summary>Algo cambió (también al deshacer).</summary>
    public event Action? Changed;

    public BookmarkSet(DataStore store, BookmarkHistory history, string collection, string fingerprint, string videoPath, Func<double, double> frameEnd)
    {
        _store = store;
        _history = history;
        _frameEnd = frameEnd;
        Fingerprint = fingerprint;
        VideoPath = videoPath;
        Collection = collection;
        _file = Path.Combine("bookmarks", collection, fingerprint + ".json");
        _data = store.Load<BookmarkFile>(_file);
        _data.Fingerprint = fingerprint;
        _data.Video = Path.GetFileName(videoPath);
        var merged = MergeSidecar() | Normalize();
        ResolveOpenEnds();
        // Mezclar el compartido sí se guarda, pero sin completar los fines: solo leer no cuenta como editar.
        if (merged)
            Save(keepOpenEnds: true);
    }

    /// <summary>
    /// La rejilla de frames cambió (llegó el fps o el índice del vídeo): los que no tenían fin en el archivo vuelven a
    /// medir un frame exacto.
    /// </summary>
    public void RefreshOpenEnds()
    {
        var changed = false;
        foreach (var b in _data.Bookmarks.Where(b => _openEnds.Contains(b.Id)))
        {
            var end = Round(_frameEnd(b.Start));
            if (end > b.Start && end != b.End)
            {
                b.End = end;
                changed = true;
            }
        }
        if (changed)
            Changed?.Invoke();
    }

    // Sin fin (o con uno que no pasa del inicio): región de un frame.
    private void ResolveOpenEnds()
    {
        _openEnds.Clear();
        foreach (var b in _data.Bookmarks.Where(b => b.End is not { } e || e <= b.Start + 1e-4))
        {
            _openEnds.Add(b.Id);
            b.End = Round(OneFrameEnd(b.Start));
        }
    }

    private double OneFrameEnd(double start)
    {
        var end = _frameEnd(start);
        return end > start + 1e-4 ? end : start + 1 / 30.0;
    }

    public IReadOnlyList<Bookmark> Items => _data.Bookmarks;

    public Bookmark? Find(string? id) => id == null ? null : _data.Bookmarks.FirstOrDefault(b => b.Id == id);

    /// <summary>&lt;vídeo&gt;.bookmarks.json (sin la extensión del vídeo); al leer también vale &lt;vídeo.mkv&gt;.bookmarks.json.</summary>
    public static string SidecarPath(string videoPath) =>
        Path.Combine(Path.GetDirectoryName(videoPath) ?? "", Path.GetFileNameWithoutExtension(videoPath) + ".bookmarks.json");

    public string NextName() => S.DefaultBookmarkName(_data.Bookmarks.Count + 1);

    /// <summary>Sin "end" (o si no pasa del inicio), región de un frame.</summary>
    public Bookmark Add(string name, double start, double? end, string? color = null, string? label = null)
    {
        var before = DiskText();
        var now = DateTime.Now;
        var bookmark = new Bookmark
        {
            Id = Guid.NewGuid().ToString("N")[..12], Name = name, Start = Round(start),
            End = Round(end is { } e && e > start + 1e-4 ? e : OneFrameEnd(start)),
            Color = color ?? BookmarkColors.Default, Label = label, Created = now, Modified = now,
        };
        _data.Bookmarks.Add(bookmark);
        Commit(S.StepAdd(name), before);
        return bookmark;
    }

    /// <summary>
    /// Cambia un bookmark. Con "dragging" es un paso de un arrastre: no se guarda hasta EndEdit, que lo apunta
    /// entero como un solo paso (o, sin BeginEdit, lo suma al paso anterior: el nombre de uno recién creado).
    /// </summary>
    public void Update(string id, Action<Bookmark> change, bool dragging = false, Func<string, string>? label = null)
    {
        if (Find(id) is not { } bookmark)
            return;
        var before = dragging ? null : DiskText();
        change(bookmark);
        bookmark.Start = Round(Math.Max(0, bookmark.Start));
        // Nunca menos de un frame.
        bookmark.End = Round(bookmark.End is { } e && e > bookmark.Start + 1e-4 ? e : OneFrameEnd(bookmark.Start));
        bookmark.Modified = DateTime.Now;
        _openEnds.Remove(id);
        if (dragging)
        {
            Normalize();
            Changed?.Invoke();
            return;
        }
        Commit((label ?? (name => S.StepEdit(name)))(bookmark.Name), before);
    }

    /// <summary>Empieza un arrastre: deshacerlo vuelve a como estaba ahora.</summary>
    public void BeginEdit(string label) => _edit = (true, DiskText(), label);

    public void EndEdit()
    {
        Save();
        if (_edit.Active)
            _history.Record(_edit.Label, _data.Video, [new(FilePath, _edit.Before, DiskText())]);
        else
            _history.AmendLast(FilePath, DiskText());
        _edit = default;
    }

    public void Remove(string id) => RemoveMany([id]);

    /// <summary>Borra varios en un solo paso de deshacer.</summary>
    public void RemoveMany(IReadOnlyCollection<string> ids)
    {
        var removing = _data.Bookmarks.Where(b => ids.Contains(b.Id)).ToList();
        if (removing.Count == 0)
            return;
        var before = DiskText();
        BookmarkFiles.RemoveIds(_data, ids);
        Commit(removing.Count == 1 ? S.StepDelete(removing[0].Name) : S.StepDeleteMany(removing.Count), before);
    }

    /// <summary>
    /// Pasa los bookmarks a otra colección (el mismo vídeo allí), en un solo paso que al deshacerse los devuelve
    /// aquí y los quita de allí.
    /// </summary>
    public void MoveTo(IReadOnlyCollection<string> ids, string collection)
    {
        var moving = _data.Bookmarks.Where(b => ids.Contains(b.Id)).ToList();
        if (moving.Count == 0 || collection == Collection)
            return;
        var target = Path.Combine(BookmarkCollections.PathOf(_store, collection), Fingerprint + ".json");
        // Sus etiquetas van con ellos (o pasan a la de allí con el mismo nombre).
        var copies = moving.Select(b => b.Clone()).ToList();
        var labels = BookmarkLabels.CopyTo(_store, Collection, collection, copies);
        var (targetBefore, targetAfter) = BookmarkFiles.Modify(target, Fingerprint, _data.Video, there => BookmarkFiles.AddCopies(there, copies));
        var before = DiskText();
        // Aquí no se apuntan como borrados: si el compartido junto al vídeo los trae, que puedan volver.
        _data.Bookmarks.RemoveAll(b => moving.Contains(b));
        Normalize();
        Save();
        _history.Record(moving.Count == 1 ? S.StepMoveTo(moving[0].Name, collection) : S.StepMoveManyTo(moving.Count, collection),
            _data.Video, [new(FilePath, before, DiskText()), new(target, targetBefore, targetAfter), .. labels is { } l ? [l] : Array.Empty<BookmarkHistory.Entry>()]);
        Changed?.Invoke();
    }

    /// <summary>Vuelve a leer el archivo (tras deshacer, o tras cambiarlo desde el panel con otros vídeos).</summary>
    public void Reload()
    {
        _data = DataStore.LoadFile<BookmarkFile>(FilePath) ?? new BookmarkFile();
        _data.Fingerprint = Fingerprint;
        _data.Video = Path.GetFileName(VideoPath);
        Normalize();
        ResolveOpenEnds();
        Changed?.Invoke();
    }

    /// <summary>Escribe el &lt;vídeo&gt;.bookmarks.json junto al vídeo, para compartirlo. Devuelve su ruta.</summary>
    public string Export()
    {
        var path = SidecarPath(VideoPath);
        DataStore.SaveFile(path, new BookmarkFile
        {
            Video = _data.Video, Fingerprint = Fingerprint, Bookmarks = _data.Bookmarks,
            Labels = BookmarkLabels.UsedBy(BookmarkLabels.Load(_store, Collection), _data.Bookmarks),
        });
        // La copia de seguridad que deja el guardado no hace falta junto al vídeo.
        try { File.Delete(path + ".bak"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return path;
    }

    private string? DiskText() => BookmarkFiles.ReadText(FilePath);

    private void Commit(string label, string? before)
    {
        Normalize();
        Save();
        _history.Record(label, _data.Video, [new(FilePath, before, DiskText())]);
        Changed?.Invoke();
    }

    // Al editar se guardan todos con su fin (normalizados); con "keepOpenEnds" (al mezclar el compartido sin editar
    // nada), los que no lo tenían se quedan sin él en el archivo.
    private void Save(bool keepOpenEnds = false)
    {
        var open = keepOpenEnds ? _data.Bookmarks.Where(b => _openEnds.Contains(b.Id)).Select(b => (b, b.End)).ToList() : [];
        try
        {
            foreach (var (b, _) in open)
                b.End = null;
            _store.Save(_file, _data);
            if (!keepOpenEnds)
                _openEnds.Clear();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Se reintenta en el siguiente cambio; los bookmarks siguen en memoria.
        }
        finally
        {
            foreach (var (b, end) in open)
                b.End = end;
        }
    }

    // Por inicio y, a igual inicio, primero el más corto.
    private bool Normalize()
    {
        var sorted = _data.Bookmarks.OrderBy(b => b.Start).ThenBy(b => b.End ?? b.Start).ToList();
        var changed = !sorted.SequenceEqual(_data.Bookmarks);
        _data.Bookmarks = sorted;
        return changed;
    }

    // Mezcla el archivo compartido: entra lo nuevo, se actualiza lo que allí es más reciente y no vuelve lo
    // que se borró aquí. Los escritos a mano sin id reciben uno fijo (de su contenido) para no duplicarse.
    private bool MergeSidecar()
    {
        var path = SidecarPath(VideoPath);
        var shared = DataStore.LoadFile<BookmarkFile>(path) ?? DataStore.LoadFile<BookmarkFile>(VideoPath + ".bookmarks.json");
        if (shared == null)
            return false;
        ImportLabels(_store, Collection, shared);
        return BookmarkFiles.Merge(_data, shared.Bookmarks) > 0;
    }

    /// <summary>
    /// Las etiquetas que trae un archivo compartido entran en las de la colección (las que ya hay con su nombre se
    /// reutilizan) y sus bookmarks pasan a usar las de aquí. Devuelve el cambio en el archivo de etiquetas, o null.
    /// </summary>
    public static BookmarkHistory.Entry? ImportLabels(DataStore store, string collection, BookmarkFile shared)
    {
        if (shared.Labels is not { Count: > 0 } labels)
            return null;
        var map = new Dictionary<string, string>();
        var (before, after) = BookmarkLabels.Modify(store, collection, file => map = BookmarkLabels.Merge(file, labels));
        BookmarkLabels.Remap(shared.Bookmarks, map);
        return before == after ? null : new(BookmarkLabels.PathOf(store, collection), before, after);
    }

    internal static string ContentId(Bookmark b)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{b.Name}|{b.Start:0.000}|{b.End:0.000}");
        return "s" + Convert.ToHexStringLower(XxHash64.Hash(Encoding.UTF8.GetBytes(text)))[..11];
    }

    private static double Round(double seconds) => Math.Round(seconds, 6);

    private static bool IsLabelFile(string path) =>
        string.Equals(Path.GetFileName(path), BookmarkLabels.FileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Todos los archivos de una colección, para buscar en todos sus vídeos.</summary>
    public static IEnumerable<BookmarkFile> LoadAll(DataStore store, string collection)
    {
        var folder = BookmarkCollections.PathOf(store, collection);
        if (!Directory.Exists(folder))
            yield break;
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            if (!IsLabelFile(file) && DataStore.LoadFile<BookmarkFile>(file) is { } data)
            {
                if (string.IsNullOrEmpty(data.Fingerprint))
                    data.Fingerprint = Path.GetFileNameWithoutExtension(file);
                yield return data;
            }
    }
}

/// <summary>
/// Colecciones de bookmarks ("General", "Trabajo", "Personal"…): una carpeta cada una dentro de data\bookmarks,
/// con un archivo por vídeo. Un mismo vídeo puede tener bookmarks distintos en cada colección.
/// </summary>
public static class BookmarkCollections
{
    public const string Default = "General";

    public static string Root(DataStore store) => Path.Combine(store.Folder, "bookmarks");

    public static string PathOf(DataStore store, string collection) => Path.Combine(Root(store), collection);

    /// <summary>Antes de las colecciones los archivos iban sueltos en data\bookmarks: pasan a "General".</summary>
    public static void Migrate(DataStore store)
    {
        var root = Root(store);
        if (!Directory.Exists(root))
            return;
        var loose = Directory.GetFiles(root, "*.json*");
        if (loose.Length == 0)
            return;
        var target = PathOf(store, Default);
        Directory.CreateDirectory(target);
        foreach (var file in loose)
        {
            var destination = Path.Combine(target, Path.GetFileName(file));
            if (!File.Exists(destination))
                File.Move(file, destination);
        }
    }

    /// <summary>Las colecciones que hay (siempre al menos la activa), por orden alfabético.</summary>
    public static List<string> List(DataStore store, string active)
    {
        var names = Directory.Exists(Root(store))
            ? Directory.GetDirectories(Root(store)).Select(d => Path.GetFileName(d)!).ToList()
            : [];
        if (!names.Contains(active, StringComparer.OrdinalIgnoreCase))
            names.Add(active);
        return [.. names.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Cuántos vídeos y bookmarks tiene una colección.</summary>
    public static (int Videos, int Bookmarks) Count(DataStore store, string collection)
    {
        var files = BookmarkSet.LoadAll(store, collection).Where(f => f.Bookmarks.Count > 0).ToList();
        return (files.Count, files.Sum(f => f.Bookmarks.Count));
    }

    /// <summary>Nombre válido como carpeta, o null si no queda nada.</summary>
    public static string? Clean(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return cleaned.Length == 0 ? null : cleaned;
    }

    public static bool Exists(DataStore store, string name) => Directory.Exists(PathOf(store, name));

    public static void Create(DataStore store, string name) => Directory.CreateDirectory(PathOf(store, name));

    public static void Rename(DataStore store, string from, string to)
    {
        var source = PathOf(store, from);
        var target = PathOf(store, to);
        if (!Directory.Exists(source))
        {
            Directory.CreateDirectory(target);
            return;
        }
        // Solo cambian mayúsculas: Windows no deja moverla directamente.
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            var temp = source + ".renombrando";
            Directory.Move(source, temp);
            source = temp;
        }
        Directory.Move(source, target);
    }

    public static void Delete(DataStore store, string name)
    {
        var folder = PathOf(store, name);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }
}

/// <summary>Cambios hechos directamente en los archivos de bookmarks (los de vídeos que no están abiertos).</summary>
public static class BookmarkFiles
{
    /// <summary>El texto del archivo tal cual, o null si no existe.</summary>
    public static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    /// <summary>Lee, cambia y guarda un archivo; devuelve su texto antes y después, para el historial.</summary>
    public static (string? Before, string After) Modify(string path, string fingerprint, string video, Action<BookmarkFile> change)
    {
        var before = ReadText(path);
        var data = DataStore.LoadFile<BookmarkFile>(path) ?? new BookmarkFile();
        data.Fingerprint = fingerprint;
        if (video.Length > 0)
            data.Video = video;
        change(data);
        data.Bookmarks = [.. data.Bookmarks.OrderBy(b => b.Start).ThenBy(b => b.End ?? b.Start)];
        DataStore.SaveFile(path, data);
        return (before, ReadText(path)!);
    }

    /// <summary>Quita los bookmarks y los apunta como borrados (para que el compartido no los traiga otra vez).</summary>
    public static void RemoveIds(BookmarkFile data, IReadOnlyCollection<string> ids)
    {
        foreach (var b in data.Bookmarks.Where(b => ids.Contains(b.Id)).ToList())
        {
            data.Bookmarks.Remove(b);
            (data.Deleted ??= []).Add(b.Id);
        }
    }

    /// <summary>Añade copias (sustituyendo las que tengan el mismo id) y las saca de los borrados.</summary>
    public static void AddCopies(BookmarkFile data, IEnumerable<Bookmark> bookmarks)
    {
        foreach (var b in bookmarks)
        {
            data.Bookmarks.RemoveAll(x => x.Id == b.Id);
            data.Bookmarks.Add(b.Clone());
            data.Deleted?.Remove(b.Id);
        }
    }

    /// <summary>
    /// Mezcla bookmarks que vienen de otro (el archivo compartido junto al vídeo, o uno arrastrado sobre la app): entra lo
    /// nuevo, se actualiza lo que allí es más reciente y no vuelve lo que se borró aquí. Los escritos a mano sin id
    /// reciben uno fijo (de su contenido) para no duplicarse. Devuelve cuántos entraron o cambiaron.
    /// </summary>
    public static int Merge(BookmarkFile data, IEnumerable<Bookmark> incoming)
    {
        var changed = 0;
        foreach (var b in incoming)
        {
            if (string.IsNullOrWhiteSpace(b.Id))
                b.Id = BookmarkSet.ContentId(b);
            if (data.Deleted?.Contains(b.Id) == true)
                continue;
            var index = data.Bookmarks.FindIndex(x => x.Id == b.Id);
            if (index < 0)
            {
                if (b.Created == default)
                    b.Created = b.Modified = DateTime.Now;
                if (string.IsNullOrWhiteSpace(b.Color))
                    b.Color = BookmarkColors.Default;
                data.Bookmarks.Add(b);
                changed++;
            }
            else if (b.Modified > data.Bookmarks[index].Modified)
            {
                data.Bookmarks[index] = b;
                changed++;
            }
        }
        return changed;
    }

    /// <summary>Deja el archivo con ese texto; null lo borra (también su copia, que si no lo resucitaría).</summary>
    public static void WriteText(string path, string? text)
    {
        if (text == null)
        {
            File.Delete(path);
            File.Delete(path + ".bak");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }
}

/// <summary>
/// Historial de deshacer de los bookmarks de toda la app: cada paso guarda cómo estaban y cómo quedaron los archivos
/// que tocó (de cualquier vídeo y colección), así que deshacer funciona también con vídeos que ya no están abiertos.
/// </summary>
public sealed class BookmarkHistory
{
    private const int Max = 200;

    public sealed record Entry(string Path, string? Before, string? After);

    /// <summary>Un paso: qué se hizo, en qué vídeo y los archivos que cambió.</summary>
    public sealed record Step(string Label, string Video, IReadOnlyList<Entry> Entries);

    private readonly List<Step> _undo = [], _redo = [];

    public void Record(string label, string video, IReadOnlyList<Entry> entries)
    {
        if (entries.All(e => e.Before == e.After))
            return;
        _undo.Add(new Step(label, video, entries));
        if (_undo.Count > Max)
            _undo.RemoveAt(0);
        _redo.Clear();
    }

    /// <summary>Suma un cambio más al último paso (el nombre de un bookmark recién creado va con su creación).</summary>
    public void AmendLast(string path, string? after)
    {
        if (_undo.Count == 0)
            return;
        var last = _undo[^1];
        _undo[^1] = last with { Entries = [.. last.Entries.Select(e => e.Path == path ? e with { After = after } : e)] };
    }

    /// <summary>Deshace el último paso; devuelve cuál era, o null si no había.</summary>
    public Step? Undo() => Move(_undo, _redo, undo: true);

    public Step? Redo() => Move(_redo, _undo, undo: false);

    /// <summary>Al renombrar o borrar colecciones las rutas cambian: el historial ya no vale.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private static Step? Move(List<Step> from, List<Step> to, bool undo)
    {
        if (from.Count == 0)
            return null;
        var step = from[^1];
        from.RemoveAt(from.Count - 1);
        foreach (var entry in step.Entries)
            BookmarkFiles.WriteText(entry.Path, undo ? entry.Before : entry.After);
        to.Add(step);
        return step;
    }
}
