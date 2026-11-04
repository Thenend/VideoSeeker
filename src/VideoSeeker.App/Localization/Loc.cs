using System.Globalization;
using System.Windows.Markup;

namespace VideoSeeker.App.Localization;

/// <summary>
/// Idioma de la interfaz. Se elige una vez al arrancar (App.OnStartup): el de los ajustes o, si no se eligió, el de
/// Windows; cambiarlo en los ajustes se aplica al reiniciar. Los textos están en Strings (uno por idioma, en
/// Strings.*.cs); en el código se usan con S.Nombre y en XAML con {l:Tr Nombre}.
/// </summary>
public static class Loc
{
    /// <summary>Un idioma de la interfaz: su código (el que se guarda en settings.json) y su nombre en ese idioma.</summary>
    public sealed record UiLanguage(string Code, string NativeName, Func<Strings> Create);

    /// <summary>Los idiomas disponibles, en el orden del desplegable de los ajustes.</summary>
    public static readonly UiLanguage[] All =
    [
        new("es", "Español", () => new SpanishStrings()),
        new("en", "English", () => new EnglishStrings()),
        new("fr", "Français", () => new FrenchStrings()),
        new("de", "Deutsch", () => new GermanStrings()),
        new("it", "Italiano", () => new ItalianStrings()),
        new("pt-BR", "Português (Brasil)", () => new PortugueseBrazilStrings()),
    ];

    /// <summary>Los textos del idioma elegido.</summary>
    public static Strings S { get; private set; } = new SpanishStrings();

    /// <summary>El código del idioma en uso ("es", "en", "fr", "de", "it" o "pt-BR").</summary>
    public static string Code { get; private set; } = "es";

    /// <summary>Para los números que se escriben en la interfaz (coma o punto decimal, como en ese idioma).</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>El idioma de la interfaz de Windows, guardado antes de cambiar el de la app.</summary>
    public static CultureInfo WindowsCulture { get; } = CultureInfo.CurrentUICulture;

    /// <summary>El idioma de Windows, si la app está traducida a él; si no, inglés.</summary>
    public static string WindowsLanguage { get; } = FromCulture(WindowsCulture);

    /// <summary>El idioma que corresponde a una cultura de Windows; inglés si no hay traducción a ese idioma.</summary>
    public static string FromCulture(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        // Quien tiene Windows en catalán, gallego o euskera tiene más a mano el español que el inglés.
        "es" or "ca" or "gl" or "eu" => "es",
        "fr" => "fr",
        "de" => "de",
        "it" => "it",
        "pt" => "pt-BR",
        _ => "en",
    };

    /// <summary>El idioma guardado si es uno de los disponibles; si no (null, sin elegir), el de Windows.</summary>
    public static UiLanguage Resolve(string? setting) =>
        All.FirstOrDefault(l => string.Equals(l.Code, setting, StringComparison.OrdinalIgnoreCase))
        ?? All.First(l => l.Code == WindowsLanguage);

    /// <summary>Elige el idioma de la interfaz; se llama una sola vez, al arrancar, antes de crear las ventanas.</summary>
    public static void Initialize(string? setting)
    {
        var language = Resolve(setting);
        S = language.Create();
        Code = language.Code;
        Culture = CultureInfo.GetCultureInfo(language.Code switch
        {
            "es" => "es-ES",
            "en" => "en-US",
            "fr" => "fr-FR",
            "de" => "de-DE",
            "it" => "it-IT",
            _ => language.Code,
        });
        // Los textos de .NET en ese idioma; los formatos de números y fechas siguen siendo los de Windows.
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }
}

/// <summary>
/// Un texto de la interfaz en XAML, por el nombre de su propiedad en Strings: Text="{l:Tr OpenVideo}". Se resuelve al
/// cargar la ventana; un nombre que no existe falla al abrirla.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        typeof(Strings).GetProperty(Key)?.GetValue(Loc.S) as string
        ?? throw new InvalidOperationException($"No hay ningún texto «{Key}» en Strings.");
}
