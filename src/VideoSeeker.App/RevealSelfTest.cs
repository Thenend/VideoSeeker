using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using VideoSeeker.App.Controls;

namespace VideoSeeker.App;

/// <summary>
/// Autoprueba de las animaciones del panel de bookmarks y de la fila del zoom (Reveal), dentro de la de bookmarks:
/// con ellas puestas, la imagen (la ventana nativa) cambia de tamaño a cada frame, siempre hacia el mismo lado, y
/// acaba donde estaría sin animar. Mide también cuánto tarda cada paso, por si cambiar de tamaño la imagen da tirones.
/// </summary>
public partial class MainWindow
{
    private async Task TestRevealAnimations(Action<string> log, Action<bool, string> check)
    {
        var animate = Reveal.Animate;
        // Lo que estaba seleccionado o ampliado vuelve al terminar, para el resto de la prueba de bookmarks.
        var (selected, zoomed) = (_selectedBookmark, _zoomRange);
        Reveal.Animate = true;
        try
        {
            if (PanelOpen)
                ShowPanel(false);
            CloseZone();
            PanelReveal.Finish();
            ZoomReveal.Finish();
            await Task.Delay(100);
            var fullWidth = _surface.Placement.W;
            var fullHeight = _surface.Placement.H;

            // Panel: se abre poco a poco y la imagen se estrecha a la vez.
            var open = await SampleReveal(PanelReveal, () => ShowPanel(true));
            var panelWidth = (int)Math.Round(BookmarkPanel.ActualWidth * VisualTreeHelper.GetDpi(this).DpiScaleX);
            Report(log, check, "panel al abrirse", open, s => s.W, fullWidth, fullWidth - panelWidth, decreasing: true);
            check(PanelOpen && BookmarkPanel.IsVisible && PanelReveal.Progress == 1, "el panel queda abierto del todo");

            var close = await SampleReveal(PanelReveal, () => ShowPanel(false));
            Report(log, check, "panel al cerrarse", close, s => s.W, fullWidth - panelWidth, fullWidth, decreasing: false);
            check(!PanelOpen && !BookmarkPanel.IsVisible, "el panel queda cerrado (Collapsed)");

            // Dar la vuelta a medio camino: se cierra desde donde estaba, sin saltar.
            // No a la mitad del tiempo: frena al final y a esas alturas ya casi ha llegado (y un await puede volver tarde,
            // con la animación acabada). En el mismo frame en que la imagen lleva un tercio del camino.
            var middle = 0;
            var reversed = new TaskCompletionSource();
            void ReverseMidway(object? sender, EventArgs e)
            {
                var width = _surface.Placement.W;
                if (PanelReveal.Animating && width > fullWidth - panelWidth / 3)
                    return;
                CompositionTarget.Rendering -= ReverseMidway;
                middle = width;
                ShowPanel(false);
                reversed.TrySetResult();
            }
            ShowPanel(true);
            CompositionTarget.Rendering += ReverseMidway;
            await Task.WhenAny(reversed.Task, Task.Delay(2000));
            CompositionTarget.Rendering -= ReverseMidway;
            check(middle > fullWidth - panelWidth && middle < fullWidth, $"a medio abrir, la imagen mide {middle} px de {fullWidth}");
            await Task.Delay(Reveal.Duration + TimeSpan.FromMilliseconds(150));
            check(!PanelOpen && !BookmarkPanel.IsVisible && _surface.Placement.W == fullWidth,
                $"y si se cierra entonces, vuelve a cerrarse ({_surface.Placement.W} px)");

            // Fila del zoom: se abre en altura y la imagen se hace más baja.
            var zone = (Math.Max(0, _position - 2), Math.Min(_duration, _position + 2));
            var zoomOpen = await SampleReveal(ZoomReveal, () => SetFreeZoom(zone));
            var rowHeight = (int)Math.Round(ZoomReveal.ActualHeight * VisualTreeHelper.GetDpi(this).DpiScaleY);
            Report(log, check, "fila del zoom al abrirse", zoomOpen, s => s.H, fullHeight, fullHeight - rowHeight, decreasing: true);
            var zoomClose = await SampleReveal(ZoomReveal, CloseZone);
            Report(log, check, "fila del zoom al cerrarse", zoomClose, s => s.H, fullHeight - rowHeight, fullHeight, decreasing: false);
            check(!ZoomReveal.IsOpen && !ZoomRow.IsVisible, "la fila del zoom queda cerrada (Collapsed)");

            // Capturas a mitad de camino, más despacio para que dé tiempo (lo que se ve en pantalla, con la imagen).
            var duration = Reveal.Duration;
            Reveal.Duration = TimeSpan.FromSeconds(2);
            try
            {
                Activate();
                await Task.Delay(400);
                ShowPanel(true);
                await Task.Delay(700);
                check(CaptureAnimation("selftest-animacion-panel.png", log), "captura del panel a medio abrir");
                PanelReveal.Finish();
                SetFreeZoom(zone);
                await Task.Delay(700);
                check(CaptureAnimation("selftest-animacion-zoom.png", log), "captura de la fila del zoom a medio abrir");
            }
            finally
            {
                Reveal.Duration = duration;
            }
            ZoomReveal.Finish();
            CloseZone();
            ShowPanel(false);
            ZoomReveal.Finish();
            PanelReveal.Finish();

            // Sin animar (el ajuste quitado), al momento.
            Reveal.Animate = false;
            ShowPanel(true);
            check(PanelReveal.Progress == 1 && !PanelReveal.Animating, "con las animaciones quitadas, el panel se abre de golpe");
            ShowPanel(false);
            check(!BookmarkPanel.IsVisible, "y se cierra de golpe");
        }
        finally
        {
            Reveal.Animate = animate;
            if (selected != null && _bookmarks?.Find(selected) != null)
                Select(selected);
            else if (zoomed != null)
                SetFreeZoom(zoomed);
        }
    }

    // Lo que se ve en pantalla; si otro programa tapa la ventana (Windows no deja ponerla delante si no tiene el foco),
    // lo que pinta la ventana, que para ver la imagen a medio cambiar de tamaño vale igual.
    private bool CaptureAnimation(string name, Action<string> log)
    {
        var path = Path.Combine(_store.Folder, name);
        if (ForeignWindowOver(VisibleBounds()) is not { } foreign)
            return CaptureWindow(path, log);
        log($"      la tapa otro programa ({foreign}): se guarda lo que pinta la ventana");
        return CaptureWindowContent(path, log);
    }

    // Lo que mide la ventana nativa en cada frame mientras dura la animación, y cuánto pasa entre frames.
    private async Task<List<((int W, int H) Size, double Ms)>> SampleReveal(Reveal reveal, Action action)
    {
        var samples = new List<((int W, int H) Size, double Ms)>();
        var clock = Stopwatch.StartNew();
        var last = 0.0;
        void OnFrame(object? sender, EventArgs e)
        {
            var now = clock.Elapsed.TotalMilliseconds;
            samples.Add(((_surface.Placement.W, _surface.Placement.H), now - last));
            last = now;
        }
        CompositionTarget.Rendering += OnFrame;
        action();
        var limit = Stopwatch.StartNew();
        while (reveal.Animating && limit.ElapsedMilliseconds < 3000)
            await Task.Delay(10);
        await Task.Delay(100);
        CompositionTarget.Rendering -= OnFrame;
        return samples;
    }

    private static void Report(Action<string> log, Action<bool, string> check, string what,
        List<((int W, int H) Size, double Ms)> samples, Func<(int W, int H), int> pick, int from, int to, bool decreasing)
    {
        var values = samples.Select(s => pick(s.Size)).ToList();
        var steps = values.Distinct().Count(v => Math.Abs(v - from) > 1 && Math.Abs(v - to) > 1);
        var monotonic = values.Zip(values.Skip(1)).All(p => decreasing ? p.Second <= p.First : p.Second >= p.First);
        // El primero incluye lo que hace al abrir (llenar la lista del panel...), antes de empezar a moverse.
        var slowest = samples.Skip(1).Select(s => s.Ms).DefaultIfEmpty(0).Max();
        log($"      {what}: {string.Join(" ", values.Distinct())}; hasta el primer frame {samples.FirstOrDefault().Ms:0} ms, " +
            $"el más lento después {slowest:0} ms, {samples.Count} frames");
        check(steps >= 4 && monotonic && Math.Abs(values.LastOrDefault() - to) <= 1,
            $"{what}: la imagen pasa de {from} a {to} px en {steps} pasos, siempre hacia el mismo lado (acaba en {values.LastOrDefault()})");
        check(slowest < 80, $"{what}: ningún frame tarda más de 80 ms ({slowest:0} ms)");
    }
}
