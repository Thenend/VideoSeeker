namespace VideoSeeker.App;

public static class TimeFormat
{
    /// <summary>Tiempos precisos como timecode hh:mm:ss:ff (ajuste "Tiempo") en vez de con milisegundos.</summary>
    public static bool UseFrames { get; set; }

    /// <summary>h:mm:ss(.fff), sin la hora si es cero.</summary>
    public static string Format(double seconds, bool withMilliseconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, double.IsFinite(seconds) ? seconds : 0));
        var text = t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
        return withMilliseconds ? $"{text}.{t.Milliseconds:000}" : text;
    }

    /// <summary>h:mm:ss.f (con décimas), para donde hay poco sitio: la lista de bookmarks.</summary>
    public static string WithTenths(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, double.IsFinite(seconds) ? seconds : 0));
        return $"{Format(seconds, withMilliseconds: false)}.{t.Milliseconds / 100}";
    }

    /// <summary>Como Precise, pero con décimas en vez de milisegundos (el timecode no se acorta).</summary>
    public static string Short(double seconds, double fps) =>
        UseFrames && fps > 0 ? Timecode(seconds, fps) : WithTenths(seconds);

    /// <summary>Un instante exacto: con milisegundos o, según el ajuste y si se sabe el fps, como timecode.</summary>
    public static string Precise(double seconds, double fps) =>
        UseFrames && fps > 0 ? Timecode(seconds, fps) : Format(seconds, withMilliseconds: true);

    /// <summary>
    /// hh:mm:ss:ff, como en Premiere o DaVinci, pero con los segundos del reloj (los mismos que con milisegundos):
    /// ff es el frame dentro de ese segundo. Así no deriva en vídeos de 23,976 o 29,97 fps.
    /// </summary>
    public static string Timecode(double seconds, double fps)
    {
        var t = Math.Max(0, double.IsFinite(seconds) ? seconds : 0);
        // Un 1 % de frame de margen: un frame que empieza en 0,99999 s es el primero del segundo 1.
        t += 0.01 / fps;
        var whole = Math.Floor(t);
        var frame = Math.Min((int)((t - whole) * fps), (int)Math.Ceiling(fps) - 1);
        var s = (long)whole;
        return $"{s / 3600:00}:{s / 60 % 60:00}:{s % 60:00}:{frame.ToString(fps > 100 ? "000" : "00")}";
    }
}
