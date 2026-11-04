namespace VideoSeeker.App;

/// <summary>
/// Enlaces del pie de Ajustes: apoyar al autor y ver el código fuente.
/// Si haces un fork: te pedimos, como favor y no como obligación, que dejes <see cref="SupportUrl"/> como está
/// (ver CONTRIBUTING.md). Puedes añadir tu propio enlace al lado.
/// </summary>
internal static class AppLinks
{
    /// <summary>
    /// Página de apoyo (docs/apoyar/ publicada con GitHub Pages): ahí están los métodos de pago, que se pueden
    /// cambiar sin sacar otra versión de la app. Vacía: el botón «Apoyar» no se muestra.
    /// </summary>
    public const string SupportUrl = "https://thenend.github.io/VideoSeeker/apoyar/";

    /// <summary>Repositorio con el código fuente (la licencia GPL obliga a ofrecerlo junto al programa).</summary>
    public const string SourceUrl = "https://github.com/Thenend/VideoSeeker";
}
