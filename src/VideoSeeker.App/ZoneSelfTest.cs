using System.IO;
using System.Windows;
using System.Windows.Input;
using VideoSeeker.App.Controls;
using VideoSeeker.App.Data;

namespace VideoSeeker.App;

/// <summary>
/// Prueba automática de la zona ampliada unificada con la región seleccionada (VIDEOSEEKER_SELFTEST=zona): seleccionar
/// una región la hace la zona; I/O y las banderitas cambian la región (y Ctrl + Z lo deshace con la zona siguiéndola);
/// Ctrl + rueda y Z la sueltan sin cambiarla; Esc cierra y deselecciona; borrarla deja la zona libre; los extremos que
/// se cruzan se intercambian con el otro de ancla; la barra de zoom no se reencuadra mientras se arrastra en ella; un
/// bookmark antiguo sin fin se lee como región de un frame. Deja los bookmarks del vídeo como estaban, escribe
/// selftest.log y cierra.
/// </summary>
public partial class MainWindow
{
    public static bool ZoneSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "zona";

    private async Task RunZoneSelfTest()
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
        static bool Near(double a, double b) => Math.Abs(a - b) < 1e-3;
        bool ZoneIs(Bookmark b) => _zoomRange is { } z && Near(z.Start, b.Start) && Near(z.End, EndOf(b));
        string Show(Bookmark b) => $"{b.Start:0.000}–{EndOf(b):0.000}";
        string ShowZone() => _zoomRange is { } z ? $"{z.Start:0.000}–{z.End:0.000}" : "sin zona";

        while (_bookmarks == null || _duration <= 0)
            await Task.Delay(100);
        // Con el índice de frames la rejilla es la exacta (y no cambia a mitad de la prueba).
        for (var i = 0; i < 300 && _frameIndex == null; i++)
            await Task.Delay(100);
        var collection = _store.Settings.BookmarkCollection;
        var file = Path.Combine(BookmarkCollections.PathOf(_store, collection), _bookmarks.Fingerprint + ".json");
        var saved = File.Exists(file) ? File.ReadAllText(file) : null;
        var (fingerprint, videoPath) = (_bookmarks.Fingerprint, _bookmarks.VideoPath);
        try
        {
            await Task.Delay(1000);
            var set = _bookmarks;
            var grid = Zoom.Grid;
            Log($"vídeo de {_duration:0.0} s a {Fps:0.###} fps, índice {(_frameIndex != null ? "listo" : "sin terminar")}");
            CloseZone();

            // B sin zona: región de un frame en el frame actual, seleccionada y convertida en la zona.
            var f0 = grid.FrameAt(_duration * 0.4);
            var t0 = grid.TimeOf(f0);
            _nav.ScrubTo(t0);
            await Task.Delay(400);
            AddBookmark();
            CloseNaming(commit: false);
            var a = set.Find(_selectedBookmark)!;
            Check(a != null && Near(a.Start, t0) && Near(EndOf(a), grid.TimeOf(f0 + 1)), $"B sin zona crea una región de un frame ({(a != null ? Show(a) : "nada")})");
            Check(_zoomBookmark == a!.Id && ZoneIs(a), $"y queda seleccionada como zona ({ShowZone()})");
            Check(ZoneName.Visibility == Visibility.Visible && ZoneName.Text == a.Name && Seek.ZoomColor == ColorOf(a),
                "la fila del zoom enseña su nombre y las banderitas toman su color");

            // O fija el fin; Ctrl + Z lo deshace y la zona la sigue (también al rehacer).
            _nav.ScrubTo(grid.TimeOf(f0 + 10));
            await Task.Delay(400);
            MarkLastFrame();
            a = set.Find(a.Id)!;
            Check(Near(EndOf(a), grid.TimeOf(f0 + 11)) && ZoneIs(a), $"O cambia el fin de la región y la zona la sigue ({Show(a)})");
            UndoBookmarks(redo: false);
            a = set.Find(a.Id)!;
            Check(Near(EndOf(a), grid.TimeOf(f0 + 1)) && ZoneIs(a) && _zoomBookmark == a.Id, $"Ctrl + Z lo deshace y la zona la sigue ({ShowZone()})");
            UndoBookmarks(redo: true);
            a = set.Find(a.Id)!;
            Check(Near(EndOf(a), grid.TimeOf(f0 + 11)) && ZoneIs(a), "Ctrl + Y lo rehace, también la zona");

            // I cambia el inicio.
            _nav.ScrubTo(grid.TimeOf(f0 - 5));
            await Task.Delay(400);
            MarkFirstFrame();
            a = set.Find(a.Id)!;
            Check(Near(a.Start, grid.TimeOf(f0 - 5)) && Near(EndOf(a), grid.TimeOf(f0 + 11)) && ZoneIs(a), $"I cambia el inicio de la región ({Show(a)})");
            UndoBookmarks(redo: false);
            a = set.Find(a.Id)!;

            // La banderita del fin en la barra principal cambia la región: todo el arrastre es un paso de deshacer.
            UpdateLayout();
            await Task.Delay(300);
            var before = (Start: a.Start, End: EndOf(a));
            var flag = Seek.ZoomFlagPoint(end: true);
            Seek.SimulateLeftDrag(flag, new Point(flag.X + Seek.ActualWidth * 0.05, flag.Y));
            await Task.Delay(500);
            a = set.Find(a.Id)!;
            Check(EndOf(a) > before.End + 0.5 && Near(a.Start, before.Start) && ZoneIs(a) && _zoomBookmark == a.Id,
                $"la banderita del fin cambia la región y la zona ({Show(a)})");
            UndoBookmarks(redo: false);
            a = set.Find(a.Id)!;
            Check(Near(a.Start, before.Start) && Near(EndOf(a), before.End) && ZoneIs(a), $"un Ctrl + Z deshace el arrastre entero ({Show(a)})");
            // La del inicio, más allá del fin: se intercambian y el último frame queda de ancla.
            flag = Seek.ZoomFlagPoint(end: false);
            Seek.SimulateLeftDrag(flag, new Point(flag.X + Seek.ActualWidth * 0.05, flag.Y));
            await Task.Delay(500);
            a = set.Find(a.Id)!;
            Check(Near(a.Start, grid.TimeOf(f0 + 10)) && EndOf(a) > before.End + 0.5 && ZoneIs(a),
                $"la banderita del inicio llevada más allá del fin se intercambia: el último frame sigue dentro ({Show(a)})");
            UndoBookmarks(redo: false);
            a = set.Find(a.Id)!;

            // Extremos de una región que se cruzan (en el panel, las barras o las banderitas, el mismo cálculo).
            var f1 = grid.FrameAt(_duration * 0.6);
            var t1 = grid.TimeOf(f1);
            AddBookmarkAt(t1);
            CloseNaming(commit: false);
            var b = set.Find(_selectedBookmark)!;
            Check(Near(b.Start, t1) && Near(EndOf(b), grid.TimeOf(f1 + 1)) && ZoneIs(b), "«Añadir bookmark aquí» crea una región de un frame (y es la zona)");
            OnBookmarkPressed(b, BookmarkPart.End);
            OnBookmarkDragged(b, BookmarkPart.End, t1 + 1);
            OnBookmarkDragged(b, BookmarkPart.End, t1 - 1);
            OnBookmarkReleased(b, moved: true);
            b = set.Find(b.Id)!;
            Check(Near(b.Start, grid.TimeOf(grid.FrameAt(t1 - 1 + 0.5 / Fps))) && Near(EndOf(b), grid.TimeOf(f1 + 1)) && ZoneIs(b),
                $"el fin llevado antes del inicio la alarga hacia atrás y su frame sigue dentro ({Show(b)})");
            UndoBookmarks(redo: false);
            b = set.Find(b.Id)!;
            OnBookmarkDragged(b, BookmarkPart.End, t1);
            OnBookmarkReleased(b, moved: true);
            b = set.Find(b.Id)!;
            Check(Near(b.Start, t1) && Near(EndOf(b), grid.TimeOf(f1 + 1)), $"nunca menos de un frame ({Show(b)})");
            OnBookmarkDragged(b, BookmarkPart.End, t1 + 1);
            OnBookmarkReleased(b, moved: true);
            b = set.Find(b.Id)!;
            var last = grid.FrameAt(EndOf(b) - 1e-4);
            OnBookmarkDragged(b, BookmarkPart.Start, t1 + 2);
            OnBookmarkReleased(b, moved: true);
            b = set.Find(b.Id)!;
            Check(Near(b.Start, grid.TimeOf(last)) && Near(EndOf(b), grid.TimeOf(grid.FrameAt(t1 + 2) + 1)),
                $"el inicio llevado más allá del fin se intercambia y el último frame sigue dentro ({Show(b)})");
            UndoBookmarks(redo: false);
            UndoBookmarks(redo: false);
            b = set.Find(b.Id)!;
            Check(Near(b.Start, t1) && Near(EndOf(b), grid.TimeOf(f1 + 1)) && ZoneIs(b), "deshecho, vuelve a un frame con la zona");

            // En la barra principal, una región de un frame se pinta con el triángulo, que agarra su fin.
            UpdateLayout();
            if (Seek.IsTiny(b))
            {
                var pin = Seek.LanePoint(b.Start);
                Seek.SimulateLeftDrag(pin, new Point(pin.X - Seek.ActualWidth * 0.05, pin.Y));
                await Task.Delay(300);
                b = set.Find(b.Id)!;
                Check(b.Start < t1 - 0.5 && Near(EndOf(b), grid.TimeOf(f1 + 1)),
                    $"arrastrar hacia atrás el triángulo de una región diminuta la alarga hacia atrás ({Show(b)})");
                UndoBookmarks(redo: false);
                b = set.Find(b.Id)!;
            }
            else
            {
                Log("(vídeo corto: una región de un frame no se pinta con el triángulo; no se prueba)");
            }

            // Arrastrando el fin dentro de la barra de zoom, la barra no se reencuadra hasta soltar; la zona sí sigue en vivo.
            set.Update(b.Id, x => x.End = grid.TimeOf(f1 + 48), label: "alargar");
            b = set.Find(b.Id)!;
            Select(b.Id);
            UpdateLayout();
            await Task.Delay(300);
            var view = Zoom.View;
            Check(Near(view.Start, b.Start) && Near(view.End, EndOf(b)), $"la barra de zoom enseña la región ({view.Start:0.000}–{view.End:0.000})");
            var grip = Zoom.LanePoint(EndOf(b));
            grip.X -= 1;
            var (frozen, live) = (false, false);
            Zoom.SimulateLeftDrag(grip, new Point(grip.X - Zoom.ActualWidth * 0.3, grip.Y), beforeRelease: () =>
            {
                var now = set.Find(b.Id)!;
                frozen = Near(Zoom.View.Start, view.Start) && Near(Zoom.View.End, view.End) && EndOf(now) < view.End - 0.1;
                live = ZoneIs(now);
            });
            await Task.Delay(300);
            b = set.Find(b.Id)!;
            Check(frozen, "arrastrando el fin en la barra de zoom, la barra no cambia de escala");
            Check(live, "mientras, la zona sigue a la región (en la barra principal)");
            Check(Near(Zoom.View.Start, b.Start) && Near(Zoom.View.End, EndOf(b)) && EndOf(b) < view.End - 0.1,
                $"al soltar, la barra de zoom se reencuadra a la región ({Zoom.View.Start:0.000}–{Zoom.View.End:0.000})");
            UndoBookmarks(redo: false);
            b = set.Find(b.Id)!;

            // Sacando el fin por la derecha de la barra de zoom, avanza solo y la barra se aleja para enseñar la región
            // entera (el inicio, quieto); al volver dentro se para; todo el arrastre es un paso de deshacer. Y el inicio por
            // la izquierda, igual.
            Select(b.Id);
            UpdateLayout();
            await Task.Delay(300);
            var grow0 = (Start: b.Start, End: EndOf(b));
            var endGrip = Zoom.LanePoint(grow0.End);
            endGrip.X -= 1;
            Zoom.SimulateLeftDown(endGrip);
            Zoom.SimulateLeftMove(new Point(endGrip.X + 6, endGrip.Y));
            Zoom.SimulateLeftMove(new Point(endGrip.X + 80, endGrip.Y));
            await Task.Delay(1000);
            var grown = set.Find(b.Id)!;
            Check(Zoom.AutoExtending && EndOf(grown) > grow0.End + 0.2 && Near(grown.Start, grow0.Start)
                    && Near(Zoom.View.Start, grown.Start) && Near(Zoom.View.End, EndOf(grown)) && ZoneIs(grown),
                $"sacando el fin por la derecha de la barra de zoom avanza solo y la barra enseña la región entera ({Show(grown)})");
            Zoom.SimulateLeftMove(new Point(endGrip.X - 20, endGrip.Y));
            var stoppedAt = EndOf(set.Find(b.Id)!);
            await Task.Delay(400);
            Check(!Zoom.AutoExtending && Near(EndOf(set.Find(b.Id)!), stoppedAt), "al volver dentro de la barra se para");
            Zoom.SimulateLeftUp();
            UndoBookmarks(redo: false);
            b = set.Find(b.Id)!;
            Check(Near(b.Start, grow0.Start) && Near(EndOf(b), grow0.End) && ZoneIs(b), $"un Ctrl + Z lo deshace entero ({Show(b)})");
            UpdateLayout();
            await Task.Delay(300);
            var startGrip = Zoom.LanePoint(grow0.Start);
            startGrip.X += 1;
            Zoom.SimulateLeftDown(startGrip);
            Zoom.SimulateLeftMove(new Point(startGrip.X - 6, startGrip.Y));
            Zoom.SimulateLeftMove(new Point(startGrip.X - 80, startGrip.Y));
            await Task.Delay(1000);
            grown = set.Find(b.Id)!;
            Check(grown.Start < grow0.Start - 0.2 && Near(EndOf(grown), grow0.End) && Near(Zoom.View.Start, grown.Start) && Near(Zoom.View.End, EndOf(grown)),
                $"y el inicio por la izquierda ({Show(grown)})");
            Zoom.SimulateLeftUp();
            UndoBookmarks(redo: false);
            b = set.Find(b.Id)!;

            // Clic en una región: seleccionada y es la zona.
            CloseZone();
            OnBookmarkPressed(a, BookmarkPart.Start);
            OnBookmarkReleased(a, moved: false);
            Check(_selectedBookmark == a.Id && ZoneIs(a), $"clic en una región: seleccionada y es la zona ({ShowZone()})");

            // Ctrl + rueda y Z la sueltan sin cambiarla.
            before = (a.Start, EndOf(a));
            OnZoomWheel(a.Start, -120);
            a = set.Find(a.Id)!;
            Check(_zoomBookmark == null && _selectedBookmark == null && _zoomRange is { } wide && wide.End - wide.Start > before.End - before.Start + 1e-3
                    && Near(a.Start, before.Start) && Near(EndOf(a), before.End) && ZoneName.Visibility != Visibility.Visible && Seek.ZoomColor == null,
                $"Ctrl + rueda suelta la región sin cambiarla: zona libre {ShowZone()}, región {Show(a)}");
            Select(a.Id);
            ZoomAround(_nav.CurrentTime);
            a = set.Find(a.Id)!;
            Check(_zoomBookmark == null && _zoomRange != null && Near(a.Start, before.Start) && Near(EndOf(a), before.End), "Z también");
            // Con la zona libre, I / O cambian solo la zona.
            var free = _zoomRange!.Value;
            _nav.ScrubTo(grid.TimeOf(grid.FrameAt(free.End) - 3));
            await Task.Delay(400);
            MarkLastFrame();
            a = set.Find(a.Id)!;
            Check(_zoomRange is { } z2 && z2.End < free.End && Near(a.Start, before.Start) && Near(EndOf(a), before.End), "con la zona libre, O cambia la zona y no la región");

            // B con la zona libre: región con ella, que pasa a ser la zona.
            SetFreeZoom((t1 + 3, t1 + 4.5));
            var freeZone = _zoomRange!.Value;
            AddBookmark();
            CloseNaming(commit: false);
            var c = set.Find(_selectedBookmark)!;
            Check(c != null && Near(c.Start, freeZone.Start) && Near(EndOf(c), freeZone.End) && ZoneIs(c), "B con la zona libre crea una región con ella");

            // Esc cierra la zona y deselecciona.
            Select(a.Id);
            ReturnFocusToVideo();
            OnPreviewKeyDown(this, new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, Key.Escape) { RoutedEvent = PreviewKeyDownEvent });
            Check(_zoomRange == null && _selectedBookmark == null && ZoomRow.Visibility != Visibility.Visible, "Esc cierra la zona y deselecciona");

            // Borrar la región deja la zona libre con los mismos límites.
            Select(c!.Id);
            var zone = _zoomRange!.Value;
            DeleteSelected();
            Check(set.Find(c.Id) == null && _zoomBookmark == null && _zoomRange is { } kept && Near(kept.Start, zone.Start) && Near(kept.End, zone.End)
                    && ZoneName.Visibility != Visibility.Visible, $"borrar la región deja la zona libre con los mismos límites ({ShowZone()})");
            UndoBookmarks(redo: false);
            Check(set.Find(c.Id) != null && _zoomBookmark == null, "Ctrl + Z la recupera (la zona sigue libre)");
            // Y deshacer su creación, igual.
            Select(c.Id);
            CloseZone();

            // Ctrl + M: la zona (con la región, su nombre).
            Select(a.Id);
            Check(ExportTarget() is { } target && target.Name == a.Name && Near(target.Start, a.Start) && Near(target.End, EndOf(a)), "Ctrl + M exporta la región que es la zona, con su nombre");
            CloseZone();

            // Un bookmark antiguo sin fin (de antes de que todo fuera región) se lee como región de un frame, sin reescribir el archivo.
            var f2 = grid.FrameAt(_duration * 0.2);
            DataStore.SaveFile(file, new BookmarkFile
            {
                Video = Path.GetFileName(videoPath), Fingerprint = fingerprint,
                Bookmarks = [new Bookmark { Id = "antiguo1", Name = "Antiguo", Start = grid.TimeOf(f2), Created = DateTime.Now, Modified = DateTime.Now }],
            });
            var text = File.ReadAllText(file);
            LoadBookmarks(fingerprint, videoPath);
            var old = _bookmarks!.Find("antiguo1");
            Check(old != null && Near(EndOf(old), grid.TimeOf(f2 + 1)) && File.ReadAllText(file) == text,
                $"un bookmark antiguo sin fin se lee como región de un frame y el archivo no se toca ({(old != null ? Show(old) : "no está")})");
            JumpTo(old!);
            Check(ZoneIs(old!), "y se puede seleccionar como zona");
            _bookmarks.Update("antiguo1", x => x.Name = "Antiguo editado", label: "renombrar");
            var written = DataStore.LoadFile<BookmarkFile>(file)?.Bookmarks.SingleOrDefault();
            Check(written?.End is { } end && Near(end, grid.TimeOf(f2 + 1)), "al editar algo se guarda con su fin");
            CloseZone();
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            File.Delete(file + ".bak");
            if (saved != null)
                File.WriteAllText(file, saved);
            else
                File.Delete(file);
            LoadBookmarks(fingerprint, videoPath);
            Log(failures == 0 ? "TODO BIEN" : $"{failures} FALLOS");
            File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
            Close();
        }
    }
}
