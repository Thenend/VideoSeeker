using System.Windows;

namespace VideoSeeker.App;

/// <summary>
/// Dibujo pequeño a la izquierda de una opción de menú, en la columna de la marca de verificación (como en los menús
/// de Windows): para reconocer la opción sin leerla. Es distinto de <c>MenuItem.Icon</c>, que va después de esa columna
/// y usan las muestras de color de las etiquetas, que pueden llevar además la marca.
/// </summary>
public static class MenuIcon
{
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.RegisterAttached("Glyph", typeof(object), typeof(MenuIcon), new PropertyMetadata(null));

    public static object? GetGlyph(DependencyObject element) => element.GetValue(GlyphProperty);

    public static void SetGlyph(DependencyObject element, object? value) => element.SetValue(GlyphProperty, value);
}
