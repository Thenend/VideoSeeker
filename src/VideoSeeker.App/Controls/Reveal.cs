using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VideoSeeker.App.Controls;

/// <summary>
/// Abre y cierra a su hijo animando el sitio que ocupa (el ancho o el alto), para que lo de al lado (la imagen, que es
/// una ventana nativa y no se puede deslizar ni tapar) se mueva poco a poco hasta su sitio en vez de saltar.
/// El hijo se mide y se coloca siempre a su tamaño entero y se recorta: con <see cref="Slide"/> va pegado al borde que
/// se mueve (entra deslizándose); sin él se queda quieto y el borde lo va descubriendo.
/// Lo que vale es <see cref="IsOpen"/>: mientras se cierra, el hijo aún se ve; al acabar pasa a Collapsed.
/// </summary>
public sealed class Reveal : Decorator
{
    /// <summary>Si se anima (el ajuste, las animaciones de Windows y las autopruebas, que lo quieren al momento).</summary>
    public static bool Animate { get; set; } = true;
    public static TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Horizontal: anima el ancho (el sitio está a la derecha, crece hacia la izquierda). Vertical: el alto.</summary>
    public Orientation Orientation { get; set; } = Orientation.Horizontal;
    /// <summary>El hijo va pegado al borde que se mueve (izquierdo o de arriba) y entra deslizándose.</summary>
    public bool Slide { get; set; }
    /// <summary>Además, aparece y desaparece fundiéndose.</summary>
    public bool Fade { get; set; }

    public bool IsOpen { get; private set; }
    /// <summary>Si está a medio abrir o cerrar.</summary>
    public bool Animating => _clock != null;
    /// <summary>Cuánto se ve, de 0 a 1.</summary>
    public double Progress => _progress;

    private double _progress, _from, _to;
    private Stopwatch? _clock;
    private TimeSpan _duration;

    public Reveal()
    {
        ClipToBounds = true;
    }

    // Al empezar manda lo que diga el hijo en el XAML.
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        IsOpen = Child?.Visibility == Visibility.Visible;
        _progress = IsOpen ? 1 : 0;
        Opacity = Fade ? _progress : 1;
        IsHitTestVisible = IsOpen;
    }

    /// <summary>Abre o cierra; a medio camino, da la vuelta desde donde está. Sin animar (o sin ventana), al momento.</summary>
    public void SetOpen(bool open, bool animate = true)
    {
        if (Child == null)
            return;
        if (open == IsOpen)
        {
            if (!animate)
                Finish();
            return;
        }
        IsOpen = open;
        // Mientras se cierra se ve pero ya no se puede tocar.
        IsHitTestVisible = open;
        var target = open ? 1.0 : 0.0;
        if (open)
            Child.Visibility = Visibility.Visible;
        if (!animate || !Animate || !IsVisible || PresentationSource.FromVisual(this) == null)
        {
            Stop();
            Apply(target);
            return;
        }
        _from = _progress;
        _to = target;
        // Lo que falte, a la misma velocidad: dar la vuelta a medio camino tarda lo que se había recorrido.
        _duration = TimeSpan.FromTicks((long)(Duration.Ticks * Math.Abs(_to - _from)));
        if (_clock == null)
            CompositionTarget.Rendering += OnRendering;
        _clock = Stopwatch.StartNew();
    }

    /// <summary>Acaba ya la animación que hubiera.</summary>
    public void Finish()
    {
        if (_clock == null)
            return;
        Stop();
        Apply(_to);
    }

    private void Stop()
    {
        if (_clock == null)
            return;
        CompositionTarget.Rendering -= OnRendering;
        _clock = null;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_clock == null)
            return;
        var t = _duration.Ticks <= 0 ? 1 : Math.Min(1, _clock.Elapsed.Ticks / (double)_duration.Ticks);
        // Rápido al principio y frenando al final (cúbica), como los paneles de Windows.
        var eased = 1 - Math.Pow(1 - t, 3);
        if (t >= 1)
        {
            Stop();
            Apply(_to);
        }
        else
        {
            Apply(_from + (_to - _from) * eased);
        }
    }

    private void Apply(double progress)
    {
        _progress = progress;
        Opacity = Fade ? progress : 1;
        if (progress <= 0 && !IsOpen && Child != null)
            Child.Visibility = Visibility.Collapsed;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child == null)
            return default;
        Child.Measure(constraint);
        var size = Child.DesiredSize;
        return Orientation == Orientation.Horizontal
            ? new Size(Math.Round(size.Width * _progress), size.Height)
            : new Size(size.Width, Math.Round(size.Height * _progress));
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child == null)
            return arrangeSize;
        var size = Child.DesiredSize;
        Rect rect;
        if (Orientation == Orientation.Horizontal)
        {
            var width = Math.Max(size.Width, arrangeSize.Width);
            rect = new Rect(Slide ? 0 : arrangeSize.Width - width, 0, width, arrangeSize.Height);
        }
        else
        {
            var height = Math.Max(size.Height, arrangeSize.Height);
            rect = new Rect(0, Slide ? 0 : arrangeSize.Height - height, arrangeSize.Width, height);
        }
        Child.Arrange(rect);
        return arrangeSize;
    }
}
