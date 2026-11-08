using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoSeeker.App.Controls;

namespace VideoSeeker.App;

/// <summary>
/// Prueba automática del vistazo (VIDEOSEEKER_SELFTEST=vistazo): pasar el ratón por las barras enseña el frame de debajo
/// en miniatura (al momento si está en la caché de frames; si no, cuánto tarda, también tras un barrido rápido), volver a un punto ya visto es inmediato, la barra de
/// zoom pide el frame bajo el cursor, al salir o pulsar se quita y con el ajuste apagado no sale. Guarda
/// selftest-vistazo.png con la miniatura, escribe selftest.log y cierra.
/// </summary>
public partial class MainWindow
{
    public static bool PeekSelfTestEnabled => Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST") == "vistazo";

    private async Task RunPeekSelfTest()
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

        while (_bookmarks == null || _duration <= 0)
            await Task.Delay(100);
        for (var i = 0; i < 300 && _frameIndex == null; i++)
            await Task.Delay(100);
        var setting = _store.Settings.HoverPreview;
        try
        {
            await Task.Delay(1000);
            Activate();
            await Task.Delay(300);
            _store.Settings.HoverPreview = true;
            Log($"vídeo de {_duration:0.0} s a {Fps:0.###} fps, ventana activa: {IsActive}");
            var grid = Zoom.Grid;

            // Hasta que se ve el frame pedido (no uno del camino); los milisegundos, o -1 si no llega.
            async Task<long> WaitExact(int timeoutMs = 10000)
            {
                var clock = Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < timeoutMs)
                {
                    if (PeekPopup.IsOpen && _peekExact && PeekImage.Source != null)
                        return clock.ElapsedMilliseconds;
                    await Task.Delay(10);
                }
                return -1;
            }

            async Task<long> HoverSeek(double fraction)
            {
                Seek.SimulateHover(Seek.TrackPoint(_duration * fraction));
                return await WaitExact();
            }

            // Lo que ya está en la caché de frames (la línea verde) sale al momento, reducido en la GPU.
            (double Start, double End)? cachedRange = null;
            for (var i = 0; i < 100 && cachedRange == null; i++)
            {
                cachedRange = _engine?.CachedRanges().Where(r => r.End - r.Start > 0.5).Select(r => ((double, double)?)r).FirstOrDefault();
                if (cachedRange == null)
                    await Task.Delay(100);
            }
            if (cachedRange is { } range)
            {
                var inside = (range.Start + range.End) / 2;
                var gpuBefore = PeekFromGpu;
                var clock = Stopwatch.StartNew();
                Seek.SimulateHover(Seek.TrackPoint(inside));
                var gpuMs = clock.Elapsed.TotalMilliseconds;
                Check(PeekPopup.IsOpen && _peekExact && PeekFromGpu == gpuBefore + 1,
                    $"en caché ({range.Start:0.00}–{range.End:0.00} s): sale de la GPU en {gpuMs:0.0} ms");
                SavePeekSnapshot(PeekPopup.Child as FrameworkElement, "selftest-vistazo-gpu.png");
                Seek.SimulateLeave();
            }
            else
            {
                Check(false, "no llegó a haber nada en la caché de frames para probar la miniatura desde la GPU");
            }

            var first = await HoverSeek(0.3);
            Check(first >= 0, $"barra principal al 30 %: se ve el frame en {first} ms (abre el archivo)");
            var second = await HoverSeek(0.7);
            Check(second >= 0, $"barra principal al 70 %: {second} ms");
            var again = await HoverSeek(0.3);
            Check(again is >= 0 and < 50, $"volver al 30 %: {again} ms (ya hecho)");
            Check(PeekBox.Width > 0 && PeekBox.Height > 0, $"recuadro de {PeekBox.Width:0}×{PeekBox.Height:0}");
            SavePeekSnapshot(PeekPopup.Child as FrameworkElement, "selftest-vistazo.png");

            // Barrido rápido de un lado a otro: el último punto llega sin esperar a todos los del camino.
            var sweep = Stopwatch.StartNew();
            for (var i = 0; i <= 60; i++)
            {
                Seek.SimulateHover(Seek.TrackPoint(_duration * (0.05 + 0.9 * i / 60)));
                await Task.Delay(8);
            }
            var sweepMs = sweep.ElapsedMilliseconds;
            var afterSweep = await WaitExact();
            Check(afterSweep >= 0, $"tras un barrido de {sweepMs} ms, el del final en {afterSweep} ms");
            var expected = grid.TimeOf(grid.FrameAt(_duration * 0.95));
            Check(_peekTime is { } pt && Math.Abs(pt - Math.Min(expected, _lastFrameStart > 0 ? _lastFrameStart : expected)) < 0.002,
                $"pide el frame bajo el cursor ({_peekTime:0.000} s, esperado {expected:0.000} s)");

            // Pulsar en la barra lo quita.
            Seek.SimulateLeftDown(Seek.TrackPoint(_duration * 0.5));
            Check(!PeekPopup.IsOpen, "al pulsar en la barra se quita");
            Seek.SimulateLeftUp();
            await Task.Delay(300);

            // Barra de zoom: un tramo de unos segundos, el ratón en medio.
            var zoneStart = grid.TimeOf(grid.FrameAt(_duration * 0.4));
            var zoneEnd = grid.TimeOf(grid.FrameAt(Math.Min(_duration, zoneStart + 4)));
            SetFreeZoom((zoneStart, zoneEnd));
            await Task.Delay(500);
            var middle = (zoneStart + zoneEnd) / 2;
            Zoom.SimulateHover(Zoom.TrackPoint(middle));
            var zoomMs = await WaitExact();
            var frame = grid.FrameAt(middle);
            Check(zoomMs >= 0, $"barra de zoom: se ve el frame en {zoomMs} ms");
            Check(_peekTime is { } zt && grid.FrameAt(zt) is var f && Math.Abs(f - frame) <= 1,
                $"barra de zoom: frame {(_peekTime is { } t ? grid.FrameAt(t) : -1)} (bajo el cursor, {frame})");
            Check(PeekPopup.PlacementTarget == Zoom, "sale sobre la barra de zoom");
            Zoom.SimulateLeave();
            Check(!PeekPopup.IsOpen, "al salir de la barra se quita");
            CloseZone();
            await Task.Delay(300);

            // Con el ajuste apagado no sale.
            _store.Settings.HoverPreview = false;
            Seek.SimulateHover(Seek.TrackPoint(_duration * 0.6));
            await Task.Delay(200);
            Check(!PeekPopup.IsOpen, "con el ajuste apagado no sale");
            Seek.SimulateLeave();
        }
        catch (Exception e)
        {
            Check(false, $"excepción: {e}");
        }
        finally
        {
            _store.Settings.HoverPreview = setting;
            Log(failures == 0 ? "TODO BIEN" : $"{failures} FALLOS");
            File.WriteAllLines(Path.Combine(_store.Folder, "selftest.log"), lines);
            Close();
        }
    }

    private void SavePeekSnapshot(FrameworkElement? element, string name)
    {
        if (element == null || element.ActualWidth <= 0)
            return;
        var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(_store.Folder, name));
        encoder.Save(stream);
    }
}
