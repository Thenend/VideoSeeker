namespace VideoSeeker.App.Data;

/// <summary>
/// Idiomas que se pueden elegir como preferidos para el audio y los subtítulos. En settings.json se guarda el
/// código (ISO 639-2, p. ej. "spa"); al abrir un vídeo se pasan a mpv todas las variantes con las que puede venir
/// marcada una pista (639-2 bibliográfico y terminológico, 639-1 y con región).
/// </summary>
public static class Languages
{
    /// <summary>Un idioma; su nombre, en el idioma de la interfaz (Strings.LanguageName).</summary>
    public sealed record Language(string Code, string[] Tags)
    {
        public string Name => S.LanguageName(Code);
    }

    /// <summary>Por orden alfabético del nombre en español (los ajustes los ordenan por el nombre en el idioma de la interfaz).</summary>
    public static readonly Language[] All =
    [
        new("ger", ["ger", "deu", "de", "de-DE", "de-AT", "de-CH"]),
        new("ara", ["ara", "ar"]),
        new("cat", ["cat", "ca", "ca-ES"]),
        new("chi", ["chi", "zho", "zh", "zh-CN", "zh-Hans", "zh-TW", "zh-Hant", "zh-HK", "cmn", "yue"]),
        new("kor", ["kor", "ko", "ko-KR"]),
        new("dan", ["dan", "da", "da-DK"]),
        new("spa", ["spa", "es", "es-ES", "es-419", "es-MX", "es-AR", "es-US"]),
        new("baq", ["baq", "eus", "eu", "eu-ES"]),
        new("fin", ["fin", "fi", "fi-FI"]),
        new("fre", ["fre", "fra", "fr", "fr-FR", "fr-CA", "fr-BE", "fr-CH"]),
        new("glg", ["glg", "gl", "gl-ES"]),
        new("gre", ["gre", "ell", "el", "el-GR"]),
        new("heb", ["heb", "he", "iw", "he-IL"]),
        new("hin", ["hin", "hi", "hi-IN"]),
        new("eng", ["eng", "en", "en-US", "en-GB", "en-AU", "en-CA"]),
        new("ita", ["ita", "it", "it-IT"]),
        new("jpn", ["jpn", "ja", "ja-JP"]),
        new("dut", ["dut", "nld", "nl", "nl-NL", "nl-BE"]),
        new("nor", ["nor", "nob", "nno", "no", "nb", "nn", "nb-NO"]),
        new("pol", ["pol", "pl", "pl-PL"]),
        new("por", ["por", "pt", "pt-PT", "pt-BR"]),
        new("rus", ["rus", "ru", "ru-RU"]),
        new("swe", ["swe", "sv", "sv-SE"]),
        new("tha", ["tha", "th", "th-TH"]),
        new("tur", ["tur", "tr", "tr-TR"]),
        new("ukr", ["ukr", "uk", "uk-UA"]),
        new("vie", ["vie", "vi", "vi-VN"]),
    ];

    /// <summary>El idioma de Windows del usuario, si está en la lista; si no, sin preferencia ("").</summary>
    public static string Windows { get; } = Normalize(Localization.Loc.WindowsCulture.ThreeLetterISOLanguageName + ","
        + Localization.Loc.WindowsCulture.TwoLetterISOLanguageName);

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
