using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

/// <summary>
/// Compartir bookmarks con otra persona: exportar la colección entera (desde "Todos los vídeos") y, arrastrando sobre la
/// app, importar los bookmarks de un vídeo (&lt;vídeo&gt;.bookmarks.json, que se mezclan en la colección activa con su vídeo,
/// reconocido por la huella) o una colección (&lt;colección&gt;.collection.json, que se añade como colección nueva). Cada
/// importación es un solo paso de Ctrl + Z.
/// </summary>
public partial class MainWindow
{
    // Colecciones creadas al importar y la que estaba activa antes: si se deshace la importación, se quitan.
    private readonly Dictionary<string, string> _importedCollections = new(StringComparer.OrdinalIgnoreCase);

    private void ExportCollection()
    {
        var collection = _store.Settings.BookmarkCollection;
        if (!BookmarkSet.LoadAll(_store, collection).Any(f => f.Bookmarks.Count > 0))
        {
            ShowToast($"La colección «{collection}» no tiene bookmarks");
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "Exportar la colección",
            FileName = collection + ".collection.json",
            Filter = "Colección de bookmarks|*.collection.json",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != true)
            return;
        var path = dialog.FileName;
        try
        {
            var (videos, count) = WriteCollectionFile(path, collection);
            ShowFileSavedPopup($"Colección «{collection}» exportada", path,
                $" · {videos} {(videos == 1 ? "vídeo" : "vídeos")}, {count} {(count == 1 ? "bookmark" : "bookmarks")}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Dialog.Inform(this, $"No se pudo guardar la colección:\n{e.Message}");
        }
    }

    /// <summary>Escribe la colección en un archivo para compartirla; devuelve cuántos vídeos y bookmarks lleva.</summary>
    private (int Videos, int Bookmarks) WriteCollectionFile(string path, string collection)
    {
        var videos = BookmarkSet.LoadAll(_store, collection).Where(f => f.Bookmarks.Count > 0).ToList();
        DataStore.SaveFile(path, new BookmarkCollectionFile
        {
            Collection = collection,
            Labels = BookmarkLabels.Load(_store, collection).Labels is { Count: > 0 } labels ? labels : null,
            // Los borrados solo sirven aquí (para no volver a mezclar lo quitado).
            Videos = [.. videos.Select(f => new BookmarkFile { Video = f.Video, Fingerprint = f.Fingerprint, Bookmarks = f.Bookmarks })],
        });
        try { File.Delete(path + ".bak"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return (videos.Count, videos.Sum(f => f.Bookmarks.Count));
    }

    // Menú de colecciones → "Importar colección o bookmarks…": para quien no sepa que se pueden arrastrar.
    private void ShowImportDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar una colección o los bookmarks de un vídeo",
            Filter = "Colecciones y bookmarks|*.collection.json;*.bookmarks.json|Todos los JSON|*.json",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) == true)
            foreach (var file in dialog.FileNames)
                ImportSharedFile(file);
    }

    /// <summary>Un archivo .json arrastrado sobre la app: una colección o los bookmarks de un vídeo.</summary>
    private void ImportSharedFile(string path)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var root = json.RootElement;
            bool Has(string name) => root.ValueKind == JsonValueKind.Object
                && root.EnumerateObject().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (Has("videos"))
                ImportCollection(path);
            else if (Has("bookmarks"))
                ImportVideoBookmarks(path);
            else
                ShowToast($"«{Path.GetFileName(path)}» no es un archivo de bookmarks");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            ShowToast($"No se pudo leer «{Path.GetFileName(path)}»: {e.Message}");
        }
    }

    // Los bookmarks de un vídeo: a la colección activa, con su vídeo (esté abierto o no).
    private void ImportVideoBookmarks(string path)
    {
        if (DataStore.LoadFile<BookmarkFile>(path) is not { } incoming || incoming.Bookmarks.Count == 0)
        {
            ShowToast($"«{Path.GetFileName(path)}» no tiene bookmarks");
            return;
        }
        // Uno escrito a mano puede no traer huella: entonces es del vídeo abierto.
        var fingerprint = string.IsNullOrEmpty(incoming.Fingerprint) ? _bookmarks?.Fingerprint : incoming.Fingerprint;
        if (fingerprint == null)
        {
            ShowToast("No se sabe de qué vídeo son: abre el vídeo y vuelve a arrastrarlos");
            return;
        }
        var collection = _store.Settings.BookmarkCollection;
        var file = Path.Combine(BookmarkCollections.PathOf(_store, collection), fingerprint + ".json");
        var video = incoming.Video.Length > 0 ? incoming.Video : _bookmarks?.VideoName ?? "";
        var added = 0;
        try
        {
            CloseNaming(commit: true);
            // Sus etiquetas, a las de la colección (las que ya hay con el mismo nombre se reutilizan).
            var labels = BookmarkSet.ImportLabels(_store, collection, incoming);
            var (before, after) = BookmarkFiles.Modify(file, fingerprint, File.Exists(file) ? "" : video,
                data => added = BookmarkFiles.Merge(data, incoming.Bookmarks));
            _history.Record("importar bookmarks", video, [new(file, before, after), .. labels is { } l ? [l] : Array.Empty<BookmarkHistory.Entry>()]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast($"No se pudieron importar: {e.Message}");
            return;
        }
        AfterFilesChanged([file]);
        var thisVideo = fingerprint == _bookmarks?.Fingerprint;
        ShowToast(added == 0 ? "Ya tenías estos bookmarks"
            : (added == 1 ? "1 bookmark añadido" : $"{added} bookmarks añadidos")
              + (thisVideo ? " a este vídeo" : $" a «{video}» en la colección «{collection}»"));
    }

    // Una colección: se añade como nueva (con "(2)" si ya hay una con ese nombre) y pasa a ser la activa.
    private void ImportCollection(string path)
    {
        if (DataStore.LoadFile<BookmarkCollectionFile>(path) is not { } incoming
            || incoming.Videos.Where(v => !string.IsNullOrEmpty(v.Fingerprint)).ToList() is not { Count: > 0 } videos)
        {
            ShowToast($"«{Path.GetFileName(path)}» no tiene bookmarks");
            return;
        }
        var baseName = BookmarkCollections.Clean(incoming.Collection) ?? "Importada";
        var name = baseName;
        for (var i = 2; BookmarkCollections.Exists(_store, name); i++)
            name = $"{baseName} ({i})";
        var entries = new List<BookmarkHistory.Entry>();
        try
        {
            CloseNaming(commit: true);
            BookmarkCollections.Create(_store, name);
            if (incoming.Labels is { Count: > 0 } labels)
            {
                var (before, after) = BookmarkLabels.Modify(_store, name, f => f.Labels = labels);
                entries.Add(new(BookmarkLabels.PathOf(_store, name), before, after));
            }
            foreach (var video in videos)
            {
                var file = Path.Combine(BookmarkCollections.PathOf(_store, name), video.Fingerprint + ".json");
                foreach (var b in video.Bookmarks.Where(b => string.IsNullOrWhiteSpace(b.Id)))
                    b.Id = BookmarkSet.ContentId(b);
                DataStore.SaveFile(file, new BookmarkFile { Video = video.Video, Fingerprint = video.Fingerprint, Bookmarks = video.Bookmarks });
                entries.Add(new(file, null, BookmarkFiles.ReadText(file)));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast($"No se pudo importar la colección: {e.Message}");
            return;
        }
        _history.Record($"importar la colección «{name}»", "", entries);
        _importedCollections[name] = _store.Settings.BookmarkCollection;
        SetCollection(name, announce: false, showAll: true);
        if (BookmarkPanel.Visibility != Visibility.Visible)
            ShowPanel(true);
        var count = videos.Sum(v => v.Bookmarks.Count);
        ShowToast($"Colección «{name}» añadida: {videos.Count} {(videos.Count == 1 ? "vídeo" : "vídeos")}, {count} {(count == 1 ? "bookmark" : "bookmarks")}");
    }

    // Tras deshacer: una colección importada que se ha quedado vacía se quita (y vuelve la que estaba activa).
    private void RemoveUndoneImports()
    {
        foreach (var (name, previous) in _importedCollections.ToList())
        {
            var folder = BookmarkCollections.PathOf(_store, name);
            if (Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.json")
                    .Any(f => !string.Equals(Path.GetFileName(f), BookmarkLabels.FileName, StringComparison.OrdinalIgnoreCase)))
                continue;
            _importedCollections.Remove(name);
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
            catch (IOException) { }
            if (string.Equals(_store.Settings.BookmarkCollection, name, StringComparison.OrdinalIgnoreCase))
                SetCollection(BookmarkCollections.Exists(_store, previous) ? previous : BookmarkCollections.Default, announce: false);
        }
    }
}
