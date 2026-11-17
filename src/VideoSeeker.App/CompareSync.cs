using System.Diagnostics;
using System.Windows.Threading;
using VideoSeeker.Playback;

namespace VideoSeeker.App;

/// <summary>
/// El candado entre los dos vídeos al comparar. Bloqueados, B sigue a A con un desfase fijo (tiempo de B − tiempo de A,
/// el que había al bloquear): todo lo que mueve A (reproducir, J/K/L, velocidad, pasos, saltos, barras, bookmarks,
/// bucles) mueve B a "tiempo de A + desfase". No intercepta las órdenes: mira lo que hace A (lo que enseña y cómo se
/// mueve) y lleva B ahí con su propio Navigator. En pausa, B va al frame exacto que se vería en ese instante;
/// reproduciendo, cada 100 ms compara las dos posiciones y retoca la velocidad de B para que no se aleje.
/// </summary>
public sealed class CompareSync : IDisposable
{
    public enum BRange { Inside, Before, After }

    private readonly Navigator _a;
    private readonly ComparePane _b;
    private readonly Func<FrameGrid> _gridA;
    private readonly Func<double> _lastFrameA;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    // Tiempo de A con el que se calcula B mientras está en pausa tras un paso: con fps distintos, un paso puede cambiar
    // solo la imagen de B y dejar A en el mismo frame, a medio camino de él. Null: el del frame que enseña A.
    private double? _virtual;
    // B persiguiendo al cursor con DragTo mientras se arrastra la barra de A (al soltar, SeekTo).
    private bool _bDragging;
    // El último frame de B pedido en pausa (para no repetir el mismo seek a cada aviso de A) y cuándo: si al rato B no
    // está en él (mpv avanzó un frame más al pausar, o un seek se quedó por el camino), se vuelve a pedir.
    private int? _requestedFrame;
    private readonly Stopwatch _requestedAt = new();
    private const int RetryMs = 300;
    // Última posición enseñada por cada uno y cuándo: con la velocidad, se estima dónde va entre un frame y otro.
    private (double Pos, long Ticks) _lastA, _lastB;
    private double _ema;
    // En cuánto tiempo se recupera lo que B se ha alejado reproduciendo.
    private const double CatchUpSeconds = 1;
    private bool _emaValid;

    public bool Locked { get; private set; }
    /// <summary>Tiempo de B − tiempo de A.</summary>
    public double Offset { get; private set; }
    /// <summary>Dónde cae "tiempo de A + desfase" respecto a B (bloqueados; sueltos, siempre dentro).</summary>
    public BRange Range { get; private set; }
    /// <summary>Última diferencia medida reproduciendo (s; positiva, B va por delante). Para la autoprueba.</summary>
    public double LastDrift { get; private set; }
    /// <summary>Seeks hechos para alcanzar a A reproduciendo (más de 0,5 s de diferencia). Para la autoprueba.</summary>
    public int CatchUpSeeks { get; private set; }
    /// <summary>Retoque de velocidad de B ahora (1 = ninguno). Para la autoprueba.</summary>
    public double Trim => _b.Nav.SpeedTrim;

    /// <summary>Cambió el candado, el desfase o si B queda fuera.</summary>
    public event Action? StateChanged;

    private readonly MpvPlayer _playerA;
    // Lo que dice cada mpv reproduciendo, con la hora a la que lo dijo (en su propio hilo: sin lo que tarda en llegar a
    // la interfaz, que varía en uno o dos frames).
    private readonly Lock _clockGate = new();
    private (double Pos, long Ticks) _mpvA, _mpvB;

    public CompareSync(Navigator a, MpvPlayer playerA, ComparePane b, Func<FrameGrid> gridA, Func<double> lastFrameA)
    {
        _a = a;
        _playerA = playerA;
        _b = b;
        _gridA = gridA;
        _lastFrameA = lastFrameA;
        _a.PositionShown += OnAShown;
        _a.SpeedChanged += OnASpeedChanged;
        _b.Nav.PositionShown += OnBShown;
        _playerA.PositionChanged += OnMpvA;
        _b.Player.PositionChanged += OnMpvB;
        _timer.Tick += (_, _) => Tick();
    }

    private void OnMpvA(double t)
    {
        lock (_clockGate)
            _mpvA = (t, Stopwatch.GetTimestamp());
    }

    private void OnMpvB(double t)
    {
        lock (_clockGate)
            _mpvB = (t, Stopwatch.GetTimestamp());
    }

    /// <summary>El tiempo de A que manda (el del frame que enseña o, tras un paso con fps distintos, el del paso).</summary>
    public double ATime => _virtual ?? _a.CurrentTime;
    public double Target => ATime + Offset;

    public void Lock()
    {
        if (Locked || !_b.Ready)
            return;
        Offset = _b.Nav.CurrentTime - _a.CurrentTime;
        _virtual = null;
        _requestedFrame = null;
        _bDragging = false;
        _emaValid = false;
        Locked = true;
        // B deja lo que hiciera por su cuenta: a partir de ahora sigue a A.
        if (_b.Nav.Playing && !_a.Moving)
            _b.Nav.Pause();
        _timer.Start();
        Follow();
        StateChanged?.Invoke();
    }

    public void Unlock()
    {
        if (!Locked)
            return;
        Locked = false;
        _timer.Stop();
        _virtual = null;
        _b.Nav.SpeedTrim = 1;
        if (_bDragging)
        {
            _bDragging = false;
            _b.Nav.SeekTo(_b.Nav.CurrentTime);
        }
        Range = BRange.Inside;
        StateChanged?.Invoke();
    }

    public void Toggle()
    {
        if (Locked)
            Unlock();
        else
            Lock();
    }

    private void OnAShown(double time)
    {
        _lastA = (time, Stopwatch.GetTimestamp());
        if (!Locked)
            return;
        // A se fue a otro frame por otro camino (no un paso de los dos): manda ese.
        if (_virtual is { } v && _gridA().FrameAt(v) != _gridA().FrameAt(time))
            _virtual = null;
        if (!_a.Moving)
            Follow();
    }

    private void OnBShown(double time) => _lastB = (time, Stopwatch.GetTimestamp());

    private void OnASpeedChanged()
    {
        if (Locked)
            Follow();
    }

    private void Tick()
    {
        if (!Locked)
            return;
        Follow();
        if (_a.Moving && _b.Nav.Moving && Range == BRange.Inside)
            CorrectDrift();
    }

    /// <summary>Lleva B a donde debe estar según A: a su frame en pausa o reproduciendo como A.</summary>
    private void Follow()
    {
        if (!Locked || !_b.Ready)
            return;
        var target = Target;
        var frameB = 1 / _b.Fps;
        var range = target < _b.FirstFrameStart - frameB / 2 ? BRange.Before
            : target >= _b.LastFrameStart + frameB ? BRange.After
            : BRange.Inside;
        if (range != Range)
        {
            Range = range;
            _requestedFrame = null;
            StateChanged?.Invoke();
        }
        if (range != BRange.Inside)
        {
            // Fuera de B: quieto en su primer o último frame (la imagen queda oculta).
            Park(range == BRange.Before ? _b.FirstFrameStart : _b.LastFrameStart);
            return;
        }
        if (!_a.Moving)
        {
            FollowPaused(target);
            return;
        }

        _virtual = null;
        var speed = _a.PlaybackSpeed;
        if (_bDragging)
        {
            _bDragging = false;
            _b.Nav.SeekTo(_b.Grid.Snap(target));
        }
        if (_b.Nav.Rate != speed)
            _b.Nav.SetRate(speed);
        // En el último frame (o el primero, hacia atrás) B se queda quieto: reproducir ahí lo llevaría al otro extremo.
        var atEdge = speed > 0 ? target >= _b.LastFrameStart - frameB / 2 : target <= _b.FirstFrameStart + frameB / 2;
        if (atEdge)
        {
            Park(speed > 0 ? _b.LastFrameStart : _b.FirstFrameStart);
            return;
        }
        if (!_b.Nav.Playing)
        {
            var snap = _b.Grid.Snap(target);
            if (Math.Abs(_b.Nav.CurrentTime - snap) > frameB / 2)
                _b.Nav.ScrubTo(snap);
            _requestedFrame = null;
            _emaValid = false;
            _b.Nav.SpeedTrim = 1;
            _b.Nav.TogglePlay();
        }
    }

    private void Park(double time)
    {
        _b.Nav.SpeedTrim = 1;
        var grid = _b.Grid;
        var frame = grid.FrameAt(time);
        if (_b.Nav.Playing)
        {
            _b.Nav.Pause();
            _requestedFrame = null;
        }
        Request(frame);
    }

    // Lleva B en pausa al frame dado, salvo que ya esté o ya se haya pedido hace poco.
    private void Request(int frame)
    {
        var grid = _b.Grid;
        var there = grid.FrameAt(_b.Nav.CurrentTime) == frame && !_b.Nav.Moving;
        if (_requestedFrame == frame && (there || _requestedAt.ElapsedMilliseconds < RetryMs))
            return;
        _requestedFrame = frame;
        _requestedAt.Restart();
        if (!there)
            _b.Nav.ScrubTo(grid.TimeOf(frame));
    }

    private void FollowPaused(double target)
    {
        _b.Nav.SpeedTrim = 1;
        // Arrastrando la barra de A, B persigue al cursor igual que A (DragTo); al soltar, SeekTo.
        if (_a.Dragging)
        {
            _bDragging = true;
            _requestedFrame = null;
            _b.Nav.DragTo(target);
            return;
        }
        var grid = _b.Grid;
        var frame = grid.FrameAt(target);
        var time = grid.TimeOf(frame);
        if (_bDragging)
        {
            _bDragging = false;
            _requestedFrame = frame;
            _requestedAt.Restart();
            _b.Nav.SeekTo(time);
            return;
        }
        if (_b.Nav.Playing)
        {
            _b.Nav.Pause();
            _requestedFrame = null;
        }
        Request(frame);
    }

    // Dónde va de verdad: la última posición enseñada más lo que ha avanzado desde entonces (como mucho un frame).
    private static double Estimate((double Pos, long Ticks) last, double speed, double frame)
    {
        var elapsed = (Stopwatch.GetTimestamp() - last.Ticks) / (double)Stopwatch.Frequency;
        return last.Pos + Math.Sign(speed) * Math.Min(elapsed * Math.Abs(speed), frame * Math.Max(1, Math.Abs(speed)));
    }

    /// <summary>
    /// Reproduciendo: si B se aleja más de medio frame, va algo más deprisa o más despacio hasta alcanzar a A, en
    /// proporción a lo que se ha alejado (lo recupera en un segundo: a 60 fps, un 1 % con medio frame y un 2 % con uno; si
    /// se atasca 10 frames, un 17 %), hasta un 25 %; si se aleja más de 0,5 s, salta. Por debajo de un cuarto de frame
    /// no se toca.
    /// </summary>
    private void CorrectDrift()
    {
        var speed = _a.PlaybackSpeed;
        var frameA = 1 / _a.Fps;
        var frameB = 1 / _b.Fps;
        // Con mpv reproduciendo, lo que dice en su hilo; con la imagen del motor (bucle desde la caché, marcha atrás), lo
        // que se enseña.
        (double, long) a, b;
        lock (_clockGate)
            (a, b) = (_mpvA, _mpvB);
        var estA = Estimate(_a.ShowingMpv ? a : _lastA, speed, frameA);
        var estB = Estimate(_b.Nav.ShowingMpv ? b : _lastB, speed, frameB);
        var d = estB - (estA + Offset);
        LastDrift = d;
        if (Math.Abs(d) > 0.5 * Math.Max(1, Math.Abs(speed)))
        {
            CatchUpSeeks++;
            _emaValid = false;
            _b.Nav.SpeedTrim = 1;
            _b.Nav.GoTo(_b.Grid.Snap(estA + Offset + speed * 0.1));
            return;
        }
        _ema = _emaValid ? 0.7 * _ema + 0.3 * d : d;
        _emaValid = true;
        var size = Math.Abs(_ema);
        // Entre un cuarto y medio frame se deja como esté (sin cambiar a cada muestra).
        if (size < frameB / 4)
            _b.Nav.SpeedTrim = 1;
        else if (size > frameB / 2)
            _b.Nav.SpeedTrim = 1 - Math.Sign(speed) * Math.Clamp(_ema / Math.Max(1, Math.Abs(speed)) / CatchUpSeconds, -0.25, 0.25);
    }

    /// <summary>
    /// Un paso con los dos bloqueados: al siguiente (o anterior) cambio de imagen de cualquiera de los dos. Con los mismos
    /// fps y el desfase en frames enteros, un frame de cada uno. Si los cambios casi coinciden (menos de un cuarto de
    /// frame), cuentan como uno.
    /// </summary>
    public void Step(int direction)
    {
        var gridA = _gridA();
        var gridB = _b.Grid;
        var v = _virtual ?? gridA.Snap(_a.CurrentTime);
        var fa = gridA.FrameAt(v);
        var lastA = gridA.FrameAt(_lastFrameA());
        var candidates = new List<double>();
        const double Eps = 1e-6;
        if (direction > 0)
        {
            if (fa < lastA)
                candidates.Add(gridA.TimeOf(fa + 1));
            var tb = v + Offset;
            if (tb < _b.FirstFrameStart)
                candidates.Add(_b.FirstFrameStart - Offset);
            else if (tb < _b.LastFrameStart)
                candidates.Add(gridB.TimeOf(gridB.FrameAt(tb) + 1) - Offset);
        }
        else
        {
            var startA = gridA.TimeOf(fa);
            candidates.Add(startA < v - Eps ? startA : gridA.TimeOf(Math.Max(0, fa - 1)));
            var tb = v + Offset;
            if (tb > _b.LastFrameStart + 1 / _b.Fps)
                candidates.Add(_b.LastFrameStart - Offset);
            else if (tb > _b.FirstFrameStart + Eps)
            {
                var fb = gridB.FrameAt(tb);
                var startB = gridB.TimeOf(fb) - Offset;
                candidates.Add(startB < v - Eps ? startB : gridB.TimeOf(Math.Max(0, fb - 1)) - Offset);
            }
        }
        var ahead = candidates.Where(c => direction > 0 ? c > v + Eps : c < v - Eps).ToList();
        if (ahead.Count == 0)
            return;
        var next = direction > 0 ? ahead.Min() : ahead.Max();
        // Dos cambios casi a la vez: los dos en un paso.
        var close = Math.Min(1 / _a.Fps, 1 / _b.Fps) / 4;
        next = direction > 0 ? ahead.Where(c => c <= next + close).Max() : ahead.Where(c => c >= next - close).Min();
        next = Math.Clamp(next, 0, _lastFrameA() > 0 ? _lastFrameA() : double.MaxValue);

        var newFa = gridA.FrameAt(next);
        _virtual = next;
        if (newFa != fa || _a.Playing)
            _a.ScrubTo(gridA.TimeOf(newFa));
        // Si A no cambia de frame no avisa: B se lleva aquí.
        Follow();
    }

    /// <summary>Alt + , / Alt + .: solo B un frame; bloqueados, el desfase cambia en lo mismo.</summary>
    public void NudgeB(int direction)
    {
        if (!_b.Ready)
            return;
        var grid = _b.Grid;
        var fb = grid.FrameAt(_b.Nav.CurrentTime);
        var lastB = grid.FrameAt(_b.LastFrameStart);
        var to = Math.Clamp(fb + direction, 0, Math.Max(0, lastB));
        if (to == fb)
            return;
        var time = grid.TimeOf(to);
        if (Locked)
        {
            Offset += time - grid.TimeOf(fb);
            _requestedFrame = to;
            _requestedAt.Restart();
            StateChanged?.Invoke();
        }
        _b.Nav.ScrubTo(time);
    }

    /// <summary>B se movió por su cuenta (sueltos): lo pedido antes ya no cuenta.</summary>
    public void Forget() => _requestedFrame = null;

    public void Dispose()
    {
        _timer.Stop();
        _a.PositionShown -= OnAShown;
        _a.SpeedChanged -= OnASpeedChanged;
        _b.Nav.PositionShown -= OnBShown;
        _playerA.PositionChanged -= OnMpvA;
        _b.Player.PositionChanged -= OnMpvB;
    }
}
