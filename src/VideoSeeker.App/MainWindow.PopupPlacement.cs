using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace VideoSeeker.App;

/// <summary>
/// Dónde salen los menús y los cuadros flotantes. Dos cosas que pasaban con varios monitores (Mario, 11 de noviembre):
/// el menú del clic derecho en la imagen salía a veces arriba a la izquierda, y la miniatura de las barras o el cuadro
/// del nombre, cerca del borde izquierdo, salían en el monitor de al lado.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Un menú contextual nuevo que se abrirá en el puntero. No se usa <c>PlacementMode.MousePoint</c>: toma la posición
    /// que conoce WPF, y la imagen es una ventana nativa aparte que no se la da (tras arrastrar por las barras queda
    /// vieja o en 0,0 y el menú salía en la esquina de la ventana). Se lee la del sistema y se pone relativa a la ventana.
    /// </summary>
    private ContextMenu MenuAtCursor()
    {
        var at = CursorInRoot();
        return new ContextMenu { PlacementTarget = RootGrid, Placement = PlacementMode.Relative, HorizontalOffset = at.X, VerticalOffset = at.Y };
    }

    /// <summary>El puntero en coordenadas de la ventana (las de <c>RootGrid</c>), leído del sistema.</summary>
    private Point CursorInRoot()
    {
        if (GetCursorPosition(out var screen) && PresentationSource.FromVisual(RootGrid) != null)
            return RootGrid.PointFromScreen(new Point(screen.X, screen.Y));
        return Mouse.GetPosition(RootGrid);
    }

    /// <summary>
    /// La x (en la barra) donde centrar un cuadro de ese ancho que sale encima de ella, movida lo justo para que quede
    /// entero dentro de la ventana. Si se sale por un lado, Windows lo lleva entero al monitor de ese lado.
    /// </summary>
    private double KeepCenteredInWindow(FrameworkElement bar, double x, double width)
    {
        x = Math.Clamp(x, 0, bar.ActualWidth);
        if (PresentationSource.FromVisual(bar) == null)
            return x;
        var left = bar.TranslatePoint(new Point(0, 0), RootGrid).X;
        var min = width / 2 - left;
        var max = RootGrid.ActualWidth - left - width / 2;
        return Math.Clamp(x, min, Math.Max(min, max));
    }

    /// <summary>Ancho que tendrá el contenido de un popup (medido si aún no se ha mostrado).</summary>
    private static double PopupWidth(Popup popup, double fallback)
    {
        if (popup.Child is not FrameworkElement child)
            return fallback;
        if (child.ActualWidth > 0)
            return child.ActualWidth;
        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return child.DesiredSize.Width > 0 ? child.DesiredSize.Width : fallback;
    }
}
