using VideoSeeker.Inspect;

namespace VideoSeeker.App;

/// <summary>
/// Dónde empieza cada frame: con el índice del archivo si ya está, o con el fps mientras tanto.
/// </summary>
public readonly record struct FrameGrid(FrameIndex? Index, double Fps)
{
    // Un instante calculado puede quedar una millonésima antes del frame que le toca.
    private const double Epsilon = 1e-6;

    /// <summary>Número del frame que se ve en "time".</summary>
    public int FrameAt(double time) => Index is { } index
        ? index.FrameAtOrBefore(time + Epsilon)
        : Math.Max(0, (int)Math.Floor(time * Fps + Epsilon));

    public double TimeOf(int frame) => Index is { } index && frame < index.Count
        ? index.TimeOf(frame)
        : Index is { Count: > 0 } last
            ? last.TimeOf(last.Count - 1) + (frame - last.Count + 1) / Fps
            : Math.Max(0, frame) / Fps;

    /// <summary>Inicio del frame que se ve en "time".</summary>
    public double Snap(double time) => TimeOf(FrameAt(time));
}
