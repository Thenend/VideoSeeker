namespace VideoSeeker.App.Data;

/// <summary>
/// Idiomas que se pueden elegir como preferidos para el audio y los subtítulos. En settings.json se guarda el
/// código (ISO 639-2, p. ej. "spa"); al abrir un vídeo se pasan a mpv todas las variantes con las que puede venir
/// marcada una pista (639-2 bibliográfico y terminológico, 639-1 y con región).
/// </summary>
public static class Languages
{
    public sealed record Language(string Code, string Name, string[] Tags);

    /// <summary>Por orden alfabético del nombre.</summary>
    public static readonly Language[] All =
    [
        new("ger", "Alemán", ["ger", "deu", "de", "de-DE", "de-AT", "de-CH"]),
        new("ara", "Árabe", ["ara", "ar"]),
        new("cat", "Catalán", ["cat", "ca", "ca-ES"]),
        new("chi", "Chino", ["chi", "zho", "zh", "zh-CN", "zh-Hans", "zh-TW", "zh-Hant", "zh-HK", "cmn", "yue"]),
        new("kor", "Coreano", ["kor", "ko", "ko-KR"]),
        new("dan", "Danés", ["dan", "da", "da-DK"]),
        new("spa", "Español", ["spa", "es", "es-ES", "es-419", "es-MX", "es-AR", "es-US"]),
        new("baq", "Euskera", ["baq", "eus", "eu", "eu-ES"]),
        new("fin", "Finés", ["fin", "fi", "fi-FI"]),
        new("fre", "Francés", ["fre", "fra", "fr", "fr-FR", "fr-CA", "fr-BE", "fr-CH"]),
        new("glg", "Gallego", ["glg", "gl", "gl-ES"]),
        new("gre", "Griego", ["gre", "ell", "el", "el-GR"]),
        new("heb", "Hebreo", ["heb", "he", "iw", "he-IL"]),
        new("hin", "Hindi", ["hin", "hi", "hi-IN"]),
        new("eng", "Inglés", ["eng", "en", "en-US", "en-GB", "en-AU", "en-CA"]),
        new("ita", "Italiano", ["ita", "it", "it-IT"]),
        new("jpn", "Japonés", ["jpn", "ja", "ja-JP"]),
        new("dut", "Neerlandés", ["dut", "nld", "nl", "nl-NL", "nl-BE"]),
        new("nor", "Noruego", ["nor", "nob", "nno", "no", "nb", "nn", "nb-NO"]),
        new("pol", "Polaco", ["pol", "pl", "pl-PL"]),
        new("por", "Portugués", ["por", "pt", "pt-PT", "pt-BR"]),
        new("rus", "Ruso", ["rus", "ru", "ru-RU"]),
        new("swe", "Sueco", ["swe", "sv", "sv-SE"]),
        new("tha", "Tailandés", ["tha", "th", "th-TH"]),
        new("tur", "Turco", ["tur", "tr", "tr-TR"]),
        new("ukr", "Ucraniano", ["ukr", "uk", "uk-UA"]),
        new("vie", "Vietnamita", ["vie", "vi", "vi-VN"]),
    ];

    /// <summary>El idioma de Windows del usuario, si está en la lista; si no, sin preferencia ("").</summary>
    public static string Windows { get; } = Normalize(System.Globalization.CultureInfo.CurrentUICulture.ThreeLetterISOLanguageName + ","
        + System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static Language? Find(string? code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// El código guardado; si es texto de una versión anterior (p. ej. "es,en"), el idioma de su primera
    /// variante reconocida, y si no se reconoce ninguna, sin preferencia ("").
    /// </summary>
    public static string Normalize(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return "";
        foreach (var tag in stored.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (All.FirstOrDefault(l => l.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) is { } language)
                return language.Code;
        return "";
    }

    /// <summary>Lista de variantes para alang / slang de mpv; vacía sin preferencia.</summary>
    public static string MpvList(string? code) => Find(code) is { } language ? string.Join(",", language.Tags) : "";
}
