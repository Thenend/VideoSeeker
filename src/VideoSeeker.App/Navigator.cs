using System.Diagnostics;
using System.Windows.Media;
using VideoSeeker.Inspect;
using VideoSeeker.Playback;

namespace VideoSeeker.App;

/// <summary>
/// Decide quién pone la imagen: mpv mientras se reproduce o busca, o el motor de inspección cuando se va
/// frame a frame por una zona ya precargada. Todo se llama desde el hilo de la interfaz.
/// </summary>
public sealed class Navigator(MpvPlayer player)
{
    private InspectionEngine? _engine;
    private double _mpvPosition;
    private bool _paused;
    // Cada frame que pinta el motor sube _shownSeq. Un seek de mpv que debe ocultarlo guarda el valor
    // que había al pedirlo: si entretanto el motor pintó otro frame, ese seek ya no lo oculta.
    private int _shownSeq;
    private int? _hideOnRestartSeq;
    // Seeks de mpv pedidos al navegar (todos pasan por HideOnNextRestart).
    private int _navSeeks;
    private bool _resumeAfterSeek;
    // Al reanudar desde un frame del motor, se oculta cuando mpv ya ha avanzado más allá de él.
    private double? _hideWhenPast;
    private double _hideMarginFrames;
    // Cuando el usuario deja de moverse, mpv se lleva en segundo plano al frame del motor. Así, al
    // reanudar, la ventana de mpv ya tiene ese frame y no asoma uno viejo (en 4K mpv avisa de que avanza
    // antes de haber pintado).
    private int? _syncSeqInFlight;
    private double _syncTarget;
    private double? _mpvSyncedTo;
    // Arrastrando la barra: la precarga no sigue al ratón (se recolocaría a cada paso).
    private bool _dragging;
    private int _dragSeq;
    // Arrastrando la barra con el vídeo en marcha: se para mientras se arrastra (se ve el frame bajo el cursor) y al
    // soltar sigue reproduciendo desde ahí, como en YouTube o en los editores.
    private bool _resumeAfterDrag;
    // J/K/L: velocidad hacia delante de mpv y, en marcha atrás, la del reloj que va pidiendo frames.
    private const double MaxSpeed = 20;
    private double _forwardSpeed = 1;
    // Velocidad elegida (botón de velocidad o rueda sobre el vídeo): de ella parten J/L y a ella se vuelve al pausar.
    // Negativa, el play va marcha atrás. Nunca entre -0,1 y 0,1.
    public const double MinRate = 0.1, MaxRate = 20;
    private double _reverseSpeed; // 0 = no va marcha atrás
    private double _reverseOrigin;
    private (double To, System.Diagnostics.Stopwatch Since)? _reverseWrap;
    private readonly Stopwatch _reverseClock = new();
    // Arrastrando la barra en vídeos pesados: la imagen persigue al cursor solo con frames del motor, en el
    // orden en que los va sacando el decodificador, sin seeks de mpv ni keyframes sueltos de otra escena.
    private bool _chasing;
    private double _chaseTarget;
    private bool _chaseReleased;
    // Si el ratón se ha movido desde que se pulsó. Un clic sin arrastre va directo al frame: enseñar los del
    // camino haría que la imagen volviera atrás y avanzara deprisa hasta el destino.
    private bool _chaseMoved;
    private bool _resumeAfterChase;
    private readonly Stopwatch _chaseSinceRelease = new();
    // Hasta cuánto por detrás del cursor se enseñan los frames que va dejando el decodificador.
    private const double ChaseTrail = 3;
    // Tramo de la barra de zoom (se precarga entero si cabe) y si se reproduce en bucle.
    private (double Start, double End)? _region;
    private (double Start, double End)? _loop;
    // El bucle es el del vídeo entero: mpv lo hace con loop-file y no con el A-B (su B no llegaría a alcanzarse).
    private bool _loopFile;
    private (double Start, double End)? PlayerLoop => _loopFile ? null : _loop;
    /// <summary>
    /// Bucle del vídeo puesto (aparte del del tramo): fuera del tramo con bucle, lo que se pasa de un extremo del vídeo
    /// sigue por el otro. Sin él, fuera del tramo no se da la vuelta.
    /// </summary>
    public bool LoopVideo { get; set; }
    // Bucle con el tramo entero en caché: la imagen la pinta el motor con su propio reloj, sin el tirón de
    // mpv al volver al principio (tiene que decodificar desde el keyframe). mpv sigue con el audio, sin
    // pista de vídeo, y así sus seeks al principio del tramo son casi instantáneos.
    private bool _loopPlaying;
    private double _loopOrigin;
    private readonly Stopwatch _loopClock = new();

    public double Fps { get; set; } = 30;

    /// <summary>Instante del frame que se ve ahora (de mpv o del motor).</summary>
    public event Action<double>? PositionShown;

    public double CurrentTime => _engine?.ShownTime ?? _mpvPosition;
    /// <summary>Seeks de mpv terminados (para medir en la autoprueba).</summary>
    public int MpvRestarts { get; private set; }
    /// <summary>Frames pintados por el motor (para medir en la autoprueba).</summary>
    public int EngineFrames => _shownSeq;
    public bool Paused => _paused;
    /// <summary>Duración del vídeo (para que, fuera del tramo con bucle, el bucle del vídeo dé la vuelta en el vídeo entero).</summary>
    public double Duration { get; set; }
    /// <summary>
    /// Inicio del último frame: ningún seek pasa de ahí (más allá, mpv enseñaba el final un momento y la imagen volvía
    /// al último frame). 0 si aún no se sabe.
    /// </summary>
    public double LastFrameStart { get; set; }

    private double ClampTime(double time) => Math.Clamp(time, 0, LastFrameStart > 0 ? LastFrameStart : double.MaxValue);
    public bool Reversing => _reverseSpeed > 0;
    /// <summary>Velocidad de reproducción: negativa marcha atrás, 0 en pausa.</summary>
    /// <summary>Velocidad de reproducción elegida (negativa, hacia atrás); J/L la multiplican hasta la próxima pausa.</summary>
    public double Rate { get; private set; } = 1;

    public double Speed => Reversing ? -_reverseSpeed : _paused ? 0 : _forwardSpeed;

    /// <summary>Cambia la velocidad o el sentido (J/K/L).</summary>
    public event Action? SpeedChanged;

    public void AttachEngine(InspectionEngine engine, string? currentPath)
    {
        _engine = engine;
        engine.SetRegion(_region);
        if (currentPath != null)
        {
            engine.Open(currentPath);
            if (_paused)
                engine.Focus(_mpvPosition);
        }
    }

    /// <summary>Tramo de la barra de zoom: en pausa se precarga entero si cabe en la caché.</summary>
    public void SetRegion((double Start, double End)? region)
    {
        _region = region;
        _engine?.SetRegion(region);
        if (_paused && !_dragging && !Reversing)
            _engine?.Focus(CurrentTime);
    }

    /// <summary>
    /// Bucle que manda: el del tramo (Shift + R) o el del vídeo entero (R, wholeFile). mpv vuelve al principio al
    /// llegar al final; marcha atrás, al revés.
    /// </summary>
    public void SetLoop((double Start, double End)? range, bool wholeFile = false)
    {
        _loop = range;
        _loopFile = wholeFile;
        if (_loopPlaying)
        {
            // El tramo cambió: si sigue entero en caché, el reloj sigue; si no, vuelve a reproducir mpv.
            if (range is { } r && LoopCached(r))
                return;
            StopLoopPlayback();
            Resume(allowLoop: false);
            return;
        }
        player.SetLoop(PlayerLoop);
        if (range != null && !_paused && !Reversing && !_chasing)
        {
            var at = CurrentTime;
            if (at >= range.Value.Start - 0.5 / Fps && at < range.Value.End && LoopCached(range.Value))
            {
                Pause();
                StartLoopPlayback(at);
            }
        }
    }

    public bool LoopPlaying => _loopPlaying;

    private bool LoopCached((double Start, double End) range)
    {
        if (_engine is not { Ready: true } engine)
            return false;
        var half = engine.FrameDuration / 2;
        return engine.CachedRanges().Any(r => r.Start <= range.Start + half && r.End >= range.End - half);
    }

    // Empieza el bucle desde la caché en "from" (dentro del tramo). False si no se puede.
    private bool StartLoopPlayback(double from)
    {
        if (_loop is not { } loop || _engine is not { } engine || !LoopCached(loop))
            return false;
        if (from < loop.Start || from >= loop.End - engine.FrameDuration / 2)
            from = loop.Start;
        if (!engine.ShowLatest(from, engine.FrameDuration) && !engine.ShowAt(from, moveFocus: false))
            return false;
        Shown();
        _loopPlaying = true;
        _loopOrigin = engine.ShownTime ?? from;
        _loopClock.Restart();
        player.SetLoop(null);
        player.SetVideoEnabled(false);
        player.SeekAbsolute(_loopOrigin, exact: true);
        _paused = false;
        player.SetPause(false);
        CompositionTarget.Rendering += OnLoopTick;
        SpeedChanged?.Invoke();
        return true;
    }

    private void OnLoopTick(object? sender, EventArgs e)
    {
        if (_loop is not { } loop || _engine is not { } engine)
        {
            StopLoopPlayback();
            return;
        }
        var frame = engine.FrameDuration;
        var t = _loopOrigin + _loopClock.Elapsed.TotalSeconds * _forwardSpeed;
        var shown = CurrentTime;
        // Se vuelve al principio cuando ya se ha visto el último frame del tramo (o si el reloj se ha ido lejos).
        if (t >= loop.End && shown + frame * 1.02 >= loop.End || t >= loop.End + 2.5 * frame || t < loop.Start - frame)
        {
            _loopOrigin = loop.Start;
            _loopClock.Restart();
            // El audio de mpv se recoloca también (sin vídeo, el seek es inmediato).
            player.SeekAbsolute(loop.Start, exact: true);
            if (engine.ShowLatest(loop.Start, frame) || engine.ShowAt(loop.Start))
                Shown();
            else
                FallBackToMpv();
            return;
        }

        // Como en la marcha atrás: los refrescos llegan a trompicones, así que con poco retraso se pasa al frame
        // siguiente sin más (no se pierde ninguno), y solo si se acumula se salta hasta donde va el reloj.
        var target = Math.Min(t, loop.End - 1e-4);
        var lag = target - shown;
        // En el último frame del tramo se espera a la vuelta: pasar "al siguiente" sería enseñar el que ya
        // queda fuera del tramo.
        if (lag < 0.75 * frame || shown + frame * 1.02 >= loop.End)
            return;
        var ok = lag > 2.5 * frame
            ? engine.ShowLatest(target, 1.5 * frame)
            : engine.ShowStepInPlace(shown, 1) || engine.ShowLatest(target, 1.5 * frame);
        // Por si el paso cayera fuera (tramo que no acaba justo en un frame): el que se ve en "target".
        if (ok && engine.ShownTime >= loop.End - 1e-4)
            ok = engine.ShowLatest(target, 1.5 * frame);
        if (ok)
            Shown();
        else
            FallBackToMpv();
    }

    // Algo del tramo salió de la caché: sigue mpv desde aquí.
    private void FallBackToMpv()
    {
        StopLoopPlayback();
        Resume(allowLoop: false);
    }

    // Clic o arrastre dentro del tramo mientras va el bucle desde la caché: sigue reproduciendo desde ahí.
    private bool JumpInLoop(double time)
    {
        if (!_loopPlaying || _loop is not { } loop || time < loop.Start || time >= loop.End
            || !_engine!.ShowLatest(time, _engine.FrameDuration))
            return false;
        Shown();
        _loopOrigin = _engine.ShownTime ?? time;
        _loopClock.Restart();
        player.SeekAbsolute(_loopOrigin, exact: true);
        return true;
    }

    /// <summary>Para el bucle desde la caché, si iba; queda en pausa en el frame que se ve.</summary>
    private bool StopLoopPlayback()
    {
        if (!_loopPlaying)
            return false;
        _loopPlaying = false;
        CompositionTarget.Rendering -= OnLoopTick;
        _loopClock.Reset();
        _paused = true;
        player.SetPause(true);
        player.SetVideoEnabled(true);
        player.SetLoop(PlayerLoop);
        // mpv vuelve a tener vídeo en otro frame: se lleva en segundo plano al que se ve.
        _mpvSyncedTo = null;
        SyncMpvLater(_shownSeq);
        SpeedChanged?.Invoke();
        return true;
    }

    /// <summary>Antes de cargar otro vídeo; con "play" empieza a reproducirse aunque el anterior estuviera en pausa.</summary>
    public void OnFileOpening(string path, bool play)
    {
        StopMotion();
        // La pausa se quita después del loadfile (MpvPlayer.Load): al final de un vídeo, con keep-open, mpv volvería
        // a pausar enseguida si se quitara antes.
        if (play)
            _paused = false;
        SetForwardSpeed(Math.Abs(Rate));
        _hideOnRestartSeq = null;
        _resumeAfterSeek = false;
        _syncSeqInFlight = null;
        _mpvSyncedTo = null;
        _engine?.Open(path);
    }

    public void OnMpvPosition(double time)
    {
        _mpvPosition = time;
        if (_hideWhenPast is { } past && time > past + _hideMarginFrames / Fps && _engine?.Showing == true)
            HideEngine();
        if (_engine?.Showing == true)
            return;
        PositionShown?.Invoke(time);
        if (_paused && !_dragging)
            _engine?.Focus(time);
    }

    public void OnMpvPause(bool paused)
    {
        _paused = paused;
        if (paused && !_chasing)
            _engine?.Focus(_mpvPosition);
        // Las velocidades de J/L duran hasta la pausa: luego se vuelve a la elegida.
        if (paused && _forwardSpeed != Math.Abs(Rate))
            SetForwardSpeed(Math.Abs(Rate));
        SpeedChanged?.Invoke();
    }

    /// <summary>
    /// mpv terminó un seek. Aún puede no haber pintado el frame nuevo: si ocultáramos ya el del motor,
    /// asomaría un instante el frame anterior de mpv. Por eso se oculta un poco después.
    /// </summary>
    public void OnMpvRestart()
    {
        MpvRestarts++;
        if (_syncSeqInFlight is { } syncSeq)
        {
            _syncSeqInFlight = null;
            if (syncSeq == _shownSeq)
                _mpvSyncedTo = _syncTarget;
        }
        var hide = _hideOnRestartSeq == _shownSeq && _engine?.Showing == true;
        _hideOnRestartSeq = null;
        if (_resumeAfterSeek)
        {
            _resumeAfterSeek = false;
            player.SetPause(false);
            // mpv acaba de buscar este frame y puede no haberlo pintado aún: margen de unos frames.
            if (hide)
                HideWhenMpvPasses(_engine!.ShownTime!.Value, 3);
        }
        else if (hide)
        {
            HideLater(_shownSeq, 80);
        }
    }

    private async void HideLater(int seq, int milliseconds)
    {
        await Task.Delay(milliseconds);
        // Si entretanto el motor pintó otro frame, ese manda.
        if (seq == _shownSeq && _engine?.Showing == true)
            HideEngine();
    }

    private void HideWhenMpvPasses(double time, double marginFrames)
    {
        _hideWhenPast = time;
        _hideMarginFrames = marginFrames;
        HideLater(_shownSeq, 600); // por si mpv no avanzara (final del vídeo)
    }

    // Tras un rato sin moverse, sincroniza mpv con el frame del motor sin ocultar este.
    private async void SyncMpvLater(int seq)
    {
        var seeks = _navSeeks;
        await Task.Delay(250);
        // Si entretanto se mandó a mpv a otro sitio, sincronizarlo lo devolvería a este frame. Pasaba al hacer
        // clic en la barra de zoom fuera de la caché justo después de otro clic dentro: mpv llegaba en menos de
        // 250 ms y esta sincronización, aún pendiente, lo traía de vuelta.
        if (seeks != _navSeeks || seq != _shownSeq || !_paused || Reversing || _chasing || _resumeAfterSeek || _hideOnRestartSeq != null || _engine?.ShownTime is not { } t
            || _syncSeqInFlight == seq || _mpvSyncedTo == t)
            return;
        _syncSeqInFlight = seq;
        _syncTarget = t;
        player.SeekAbsolute(t, exact: true);
    }

    private void HideEngine()
    {
        _hideWhenPast = null;
        _engine?.Hide();
        PositionShown?.Invoke(_mpvPosition);
    }

    public void TogglePlay()
    {
        _resumeAfterDrag = false;
        // Marcha atrás mpv ya está en pausa: basta con parar el reloj.
        if (StopReverse() || StopLoopPlayback())
            return;
        StopChase();
        // Reanudación pendiente (mpv aún va al frame del motor): se cancela y se queda en pausa en ese frame.
        if (_resumeAfterSeek)
        {
            _resumeAfterSeek = false;
            return;
        }
        if (!_paused)
        {
            Pause();
        }
        else if (Rate < 0)
        {
            StartReverse(-Rate);
        }
        else
        {
            if (EnterLoopRange(reverse: false) == null && RestartTarget(reverse: false) is { } start)
            {
                PlayFrom(start);
                return;
            }
            Resume();
        }
    }

    /// <summary>
    /// Parado en el último frame, reproducir vuelve a empezar, como en cualquier reproductor: desde el principio del tramo
    /// de zoom si el final cae dentro de él y, si no, desde el principio del vídeo. Marcha atrás, en el primer frame, igual
    /// pero desde el final. Devuelve a dónde ir, o null si no se está en ese extremo.
    /// </summary>
    private double? RestartTarget(bool reverse)
    {
        if (LastFrameStart <= 0)
            return null;
        var half = 0.5 / Fps;
        var at = CurrentTime;
        if (reverse ? at > half : at < LastFrameStart - half)
            return null;
        var inRegion = _region is { } r && at >= r.Start - half && at < r.End;
        if (reverse)
            return ClampTime(inRegion ? Math.Max(_region!.Value.Start, _region.Value.End - half) : LastFrameStart);
        return inRegion ? _region!.Value.Start : 0;
    }

    // Reproduce desde "time" estando en pausa: de la caché si está; si no, mpv busca y reanuda al llegar (sin volver
    // antes al frame que enseñaba el motor).
    private void PlayFrom(double time)
    {
        StopMotion();
        if (_engine != null && _engine.ShowAt(time))
        {
            Shown();
            Resume();
            return;
        }
        HideOnNextRestart();
        _resumeAfterSeek = true;
        player.SeekAbsolute(time, exact: true);
        _engine?.Focus(time);
    }

    /// <summary>
    /// Con el bucle de un tramo y el frame que se ve fuera de él, reproducir empieza por el principio del tramo (marcha
    /// atrás, por su último frame), como en los editores. Devuelve a dónde se fue, o null si no hizo falta.
    /// </summary>
    private double? EnterLoopRange(bool reverse)
    {
        if (_loopFile || _loop is not { } loop)
            return null;
        var half = 0.5 / Fps;
        var at = CurrentTime;
        if (at >= loop.Start - half && at < loop.End)
            return null;
        var target = reverse ? Math.Max(loop.Start, loop.End - half) : loop.Start;
        SeekTo(target);
        return target;
    }

    // Reanuda mpv desde el frame que se ve, aunque lo esté mostrando el motor.
    private void Resume(bool allowLoop = true)
    {
        // En bucle, con el tramo entero en caché y estando dentro, lo reproduce el motor.
        // El primer frame del tramo puede empezar un poco antes que él (si el tramo no se marcó por frames).
        if (allowLoop && _loop is { } loop && CurrentTime >= loop.Start - 0.5 / Fps && CurrentTime < loop.End && StartLoopPlayback(CurrentTime))
            return;
        if (_engine?.ShownTime is { } shown && _mpvSyncedTo == shown)
        {
            // mpv ya está en este frame: basta con reanudar. Se da por reanudado ya (mpv lo confirma después), para que
            // otra pulsación enseguida (el segundo clic de un doble clic) pause en vez de volver a reanudar.
            _paused = false;
            player.SetPause(false);
            HideWhenMpvPasses(shown, 0.5);
        }
        // Si el motor está mostrando un frame y mpv sigue en el de antes, primero lo llevamos ahí.
        else if (_engine?.ShownTime is { } t)
        {
            HideOnNextRestart();
            _resumeAfterSeek = true;
            player.SeekAbsolute(t, exact: true);
        }
        else
        {
            _paused = false;
            player.SetPause(false);
        }
    }

    public void Pause()
    {
        // Si iba a reanudarse al acabar un seek, ya no.
        _resumeAfterSeek = false;
        StopMotion();
        if (_paused)
            return;
        _paused = true;
        player.SetPause(true);
    }

    /// <summary>L: reproduce hacia delante; si ya lo hace, el doble de rápido (hasta 8×).</summary>
    public void Forward()
    {
        StopChase();
        if (StopReverse() || _paused)
        {
            SetForwardSpeed(Math.Abs(Rate));
            if (EnterLoopRange(reverse: false) == null && RestartTarget(reverse: false) is { } start)
            {
                PlayFrom(start);
                return;
            }
            Resume();
        }
        else
        {
            SetForwardSpeed(Math.Min(MaxSpeed, _forwardSpeed * 2));
        }
    }

    /// <summary>J: reproduce hacia atrás; si ya lo hace, el doble de rápido (hasta 8×).</summary>
    public void Backward()
    {
        if (Reversing)
        {
            RestartReverseClock(CurrentTime);
            _reverseSpeed = Math.Min(MaxSpeed, _reverseSpeed * 2);
            SpeedChanged?.Invoke();
            return;
        }
        StartReverse(Math.Abs(Rate));
    }

    private void StartReverse(double speed)
    {
        Pause();
        var origin = EnterLoopRange(reverse: true);
        if (origin == null && RestartTarget(reverse: true) is { } end)
        {
            // En el primer frame, marcha atrás vuelve a empezar desde el final.
            ShowForReverse(end);
            origin = end;
        }
        RestartReverseClock(origin ?? CurrentTime);
        _reverseSpeed = speed;
        CompositionTarget.Rendering += OnReverseTick;
        SpeedChanged?.Invoke();
    }

    // Marcha atrás salta a "time": de la caché si está; si no, el reloj espera a que llegue la imagen de allí.
    private void ShowForReverse(double time)
    {
        if (_engine != null && _engine.ShowAt(time))
        {
            Shown();
            return;
        }
        HideOnNextRestart();
        player.ScrubTo(time);
        _engine?.Focus(time);
        _reverseWrap = (time, Stopwatch.StartNew());
    }

    /// <summary>
    /// Ir a un sitio (un bookmark) sin cambiar cómo se reproduce: en pausa sigue en pausa, reproduciendo sigue
    /// reproduciendo desde allí y en marcha atrás sigue hacia atrás desde allí (salvo en el primer frame: ahí se para).
    /// </summary>
    public void GoTo(double time)
    {
        if (!Reversing)
        {
            SeekTo(time);
            return;
        }
        var speed = _reverseSpeed;
        time = ClampTime(time);
        StopReverse();
        if (time <= 0.5 / Fps)
        {
            SeekTo(time);
            return;
        }
        ShowForReverse(time);
        RestartReverseClock(time);
        _reverseSpeed = speed;
        CompositionTarget.Rendering += OnReverseTick;
        SpeedChanged?.Invoke();
    }

    /// <summary>
    /// Pasos de la rueda: de 0,1 en 0,1 hasta 2×, de 0,25 hasta 4×, de 0,5 hasta 10× y de 1 en 1 hasta 20× (lo
    /// mismo en negativo), cayendo siempre en esos valores redondos. De 0,1 se pasa a -0,1 sin pararse en 0.
    /// </summary>
    public static double StepRate(double rate, int steps)
    {
        const double Eps = 1e-9;
        static double StepAt(double magnitude) => magnitude < 2 ? 0.1 : magnitude < 4 ? 0.25 : magnitude < 10 ? 0.5 : 1;
        for (var i = 0; i < Math.Abs(steps); i++)
        {
            var sign = rate < 0 ? -1 : 1;
            var magnitude = Math.Abs(rate);
            if (Math.Sign(steps) == sign)
            {
                // Alejándose de 0: el paso del tramo en el que está.
                var step = StepAt(magnitude);
                magnitude = Math.Floor(magnitude / step + Eps) * step + step;
            }
            else
            {
                // Acercándose a 0: el paso del tramo de debajo (de 10 se baja a 9,5, no a 9).
                var step = StepAt(magnitude - Eps);
                var lower = Math.Ceiling(magnitude / step - Eps) * step - step;
                if (lower < MinRate - Eps)
                    (magnitude, sign) = magnitude > MinRate + Eps ? (MinRate, sign) : (MinRate, -sign);
                else
                    magnitude = lower;
            }
            rate = sign * Math.Min(Math.Round(magnitude, 2), MaxRate);
        }
        return rate;
    }

    /// <summary>Cambia la velocidad elegida y, al momento, la de lo que se esté reproduciendo (también marcha atrás).</summary>
    public void SetRate(double rate)
    {
        rate = Math.Clamp(Math.Round(rate, 2), -MaxRate, MaxRate);
        if (Math.Abs(rate) < MinRate)
            rate = rate < 0 ? -MinRate : MinRate;
        if (rate == Rate)
            return;
        Rate = rate;
        SetForwardSpeed(Math.Abs(rate));
        // Lo que se esté reproduciendo sigue a la nueva velocidad y, si cambió el signo, en el otro sentido.
        if (Reversing && rate < 0)
        {
            RestartReverseClock(CurrentTime);
            _reverseSpeed = -rate;
        }
        else if (Reversing)
        {
            StopReverse();
            Resume();
        }
        else if ((!_paused || _loopPlaying) && rate < 0)
        {
            StartReverse(-rate);
        }
        SpeedChanged?.Invoke();
    }

    private void SetForwardSpeed(double speed)
    {
        if (speed == _forwardSpeed)
            return;
        if (_loopPlaying)
        {
            // El reloj del bucle sigue desde donde va, a la nueva velocidad.
            _loopOrigin = _loopOrigin + _loopClock.Elapsed.TotalSeconds * _forwardSpeed;
            _loopClock.Restart();
        }
        _forwardSpeed = speed;
        player.SetSpeed(speed);
        SpeedChanged?.Invoke();
    }

    private void RestartReverseClock(double from)
    {
        _reverseOrigin = from;
        _reverseClock.Restart();
    }

    private void StopMotion()
    {
        StopReverse();
        StopChase();
        StopLoopPlayback();
    }

    /// <summary>Para la marcha atrás, si iba; queda en pausa en el frame que se ve.</summary>
    private bool StopReverse()
    {
        if (!Reversing)
            return false;
        _reverseSpeed = 0;
        _reverseWrap = null;
        CompositionTarget.Rendering -= OnReverseTick;
        _reverseClock.Reset();
        // Como tras un paso: mpv se lleva en segundo plano a este frame para reanudar sin fogonazo.
        SyncMpvLater(_shownSeq);
        SpeedChanged?.Invoke();
        return true;
    }

    // Marcha atrás, una vez por refresco de pantalla. La imagen sale de la caché del motor, que se va
    // rellenando por detrás del foco. Si el decodificador no da abasto, el reloj espera (va más lento)
    // en vez de saltar trozos.
    private void OnReverseTick(object? sender, EventArgs e)
    {
        var frame = 1 / Fps;
        var unclamped = _reverseOrigin - _reverseClock.Elapsed.TotalSeconds * _reverseSpeed;
        var target = Math.Max(0, unclamped);
        var from = CurrentTime;

        // Tras dar la vuelta a un final que no estaba en caché: el reloj espera (como mucho 3 s) a que llegue la imagen
        // de allí; si no, seguiría desde el frame de antes del salto.
        if (_reverseWrap is { } wrap)
        {
            if (Math.Abs(from - wrap.To) > 1 && wrap.Since.ElapsedMilliseconds < 3000)
            {
                RestartReverseClock(wrap.To);
                return;
            }
            _reverseWrap = null;
        }

        // En bucle, al pasar el principio del tramo (o del vídeo) se vuelve a su final.
        // Fuera del tramo (antes de él), con el bucle del vídeo, al llegar al principio del vídeo se va al final del tramo.
        if (_loop is { } loop && unclamped < loop.Start && (from >= loop.Start - frame / 2 || unclamped <= 0 && LoopVideo))
        {
            // El último frame que empieza antes del final del tramo.
            var last = Math.Max(loop.Start, loop.End - frame / 2);
            RestartReverseClock(last);
            ShowForReverse(last);
            return;
        }

        if (_engine is { Ready: true } engine)
        {
            // Los refrescos de WPF llegan a trompicones: con poco retraso se pasa al frame anterior sin más,
            // y solo si se acumula se salta hasta donde va el reloj. Así a 1× no se pierden frames.
            var aim = from - target > 2.5 * frame ? target : Math.Max(target, from - frame);
            if (engine.ShowBackTowards(aim, from))
                Shown();
            else if (target <= 0)
            {
                StopReverse(); // principio del vídeo
                return;
            }
            else if (_region is { } region && target < region.Start - frame && from >= region.Start - frame)
            {
                // Saliendo hacia atrás del tramo de zoom sin el frame anterior en caché: el foco se lleva a donde va el
                // reloj. Si no, se quedaba en el tramo (al ir a una región, que pasa a ser la zona, marcha atrás) y solo
                // se precargaba el tramo: lo de antes no llegaba nunca.
                engine.Focus(target);
            }
            var maxLag = Math.Max(3 * frame, 0.05 * _reverseSpeed);
            var lag = CurrentTime - target;
            // El reloj espera: no se aleja más de maxLag por debajo del frame que se ve. (Restarlo lo alejaba más a cada
            // refresco y, sin frames en caché donde se ve, en un instante llegaba al principio del vídeo y se paraba.)
            if (lag > maxLag)
                _reverseOrigin += lag - maxLag;
        }
        else
        {
            // Sin motor (AVI): seeks exactos encadenados de mpv, que llegan cuando pueden.
            if (from - target >= frame / 2)
            {
                HideOnNextRestart();
                player.ScrubTo(target);
            }
            if (target <= 0)
                StopReverse();
        }
    }

    public void Step(int direction)
    {
        Pause();
        var from = CurrentTime;
        // En el último frame, adelante no hay nada más.
        if (direction > 0 && LastFrameStart > 0 && from >= LastFrameStart - 0.5 / Fps)
            return;
        if (_engine != null && _engine.ShowStep(from, direction))
        {
            Shown();
            return;
        }
        if (_engine?.Showing == true)
        {
            // El siguiente frame aún no está en caché: que lo busque mpv.
            var target = Math.Max(0, from + direction / Fps);
            HideOnNextRestart();
            player.ScrubTo(target);
            _engine.Focus(target);
        }
        else if (direction > 0)
        {
            player.FrameStep();
        }
        else
        {
            player.FrameBackStep();
        }
    }

    /// <summary>Muestra el frame del instante dado, desde la caché si está o con un seek exacto de mpv.</summary>
    public void ScrubTo(double time)
    {
        Pause();
        time = ClampTime(time);
        if (_engine != null && _engine.ShowAt(time))
        {
            Shown();
            return;
        }
        HideOnNextRestart();
        player.ScrubTo(time);
        _engine?.Focus(time);
    }

    /// <summary>
    /// Mientras se arrastra la barra: el frame exacto bajo el cursor, de la caché si está (en pausa) o con
    /// seeks exactos encadenados de mpv. No mueve la precarga, que se recolocaría a cada paso del ratón.
    /// </summary>
    public void DragTo(double time)
    {
        time = ClampTime(time);
        if (ShouldChase())
        {
            Chase(time, released: false);
            return;
        }
        // Una reanudación pendiente (mpv yendo al frame del motor) cuenta como reproducir.
        if (!_dragging)
            _resumeAfterDrag = _loopPlaying || _resumeAfterSeek || !_paused && !Reversing;
        Pause();
        _dragging = true;

        if (_engine != null && _engine.ShowAt(time, moveFocus: false))
        {
            Shown();
            return;
        }
        HideOnNextRestart();
        if (player.ExactSeekCostMs <= HeavyExactSeekMs)
        {
            player.ScrubTo(time);
        }
        else if (_engine?.NearestKeyframe(time) is { } key)
        {
            // Frame exacto caro (4K, keyframes muy separados): si el keyframe más cercano queda más cerca que
            // lo que ya se ve, se enseña ese, que sale al momento. Si no, se pide el frame exacto (encadenado:
            // llega cuando pueda) en vez de volver a un keyframe más lejano, que haría parpadear la imagen.
            player.ScrubTo(Math.Abs(key - time) + 0.5 / Fps < Math.Abs(_mpvPosition - time) ? key : time);
        }
        else
        {
            player.ScrubTo(time, exact: false);
        }
        WhenStill(++_dragSeq, time);
    }

    private const double HeavyExactSeekMs = 80;

    // Al pararse un momento: el frame exacto y, en pausa, precargar alrededor para que moverse por esa
    // zona vaya ya desde la caché.
    private async void WhenStill(int seq, double time)
    {
        await Task.Delay(120);
        if (seq != _dragSeq || !_dragging)
            return;
        if (Math.Abs(CurrentTime - time) > 0.5 / Fps)
        {
            HideOnNextRestart();
            player.ScrubTo(time);
        }
        if (_paused)
            _engine?.Focus(time);
    }

    private bool ShouldChase() =>
        _engine is { Ready: true } engine && (engine.IsHeavy || player.ExactSeekCostMs > HeavyExactSeekMs);

    // Empieza o redirige la persecución. Al soltar, sigue hasta llegar al frame del cursor y entonces, si
    // se estaba reproduciendo, reanuda desde ahí.
    private void Chase(double time, bool released)
    {
        if (!_chasing)
        {
            _resumeAfterChase = !_paused || _resumeAfterSeek;
            Pause();
            _chasing = true;
            _chaseMoved = false;
            CompositionTarget.Rendering += OnChaseTick;
        }
        else if (!released)
        {
            _chaseMoved = true;
        }
        _dragging = true;
        _chaseTarget = time;
        _chaseReleased = released;
        if (released)
            _chaseSinceRelease.Restart();
        // Arrastrando hacia delante se guardan también los frames del camino, para enseñarlos en orden.
        var from = CurrentTime;
        _engine!.Focus(time, _chaseMoved && time > from ? Math.Max(from, time - ChaseTrail) : null);
    }

    private void OnChaseTick(object? sender, EventArgs e)
    {
        var engine = _engine!;
        var target = _chaseTarget;
        var from = CurrentTime;
        if (Math.Abs(from - target) > engine.FrameDuration / 2
            && (_chaseMoved ? engine.ShowTowards(target, from) : engine.ShowAt(target, moveFocus: false)))
            Shown();
        if (!_chaseReleased)
            return;
        if (Math.Abs(CurrentTime - target) <= engine.FrameDuration / 2)
        {
            var resume = _resumeAfterChase;
            StopChase();
            if (resume)
                Resume();
            else
                SyncMpvLater(_shownSeq);
        }
        else if (_chaseSinceRelease.ElapsedMilliseconds > 4000)
        {
            // El motor no llega (por ejemplo, más allá del último frame): que lo busque mpv.
            var resume = _resumeAfterChase;
            StopChase();
            SeekAbsolute(target, exact: true);
            if (resume)
                player.SetPause(false);
        }
    }

    private bool StopChase()
    {
        if (!_chasing)
            return false;
        _chasing = false;
        _dragging = false;
        _resumeAfterChase = false;
        CompositionTarget.Rendering -= OnChaseTick;
        _engine?.Focus(CurrentTime);
        return true;
    }

    /// <summary>Posición definitiva (al soltar la barra o al hacer clic en ella).</summary>
    public void SeekTo(double time)
    {
        time = ClampTime(time);
        if (JumpInLoop(time))
            return;
        if (_chasing)
        {
            Chase(time, released: true);
            return;
        }
        // Reproduciendo el bucle desde la caché, un clic fuera del tramo sigue reproduciendo desde allí. Igual si había
        // una reanudación pendiente (mpv yendo al frame del motor, por ejemplo al cambiar el tramo del bucle con un doble
        // clic en otro bookmark): enseñar el frame de la caché y dejarla pendiente hacía que mpv reanudara por debajo,
        // con sonido, mientras la imagen se quedaba congelada en ese frame.
        var wasPlaying = _loopPlaying || _resumeAfterSeek || _dragging && _resumeAfterDrag;
        StopMotion();
        _dragging = false;
        _resumeAfterDrag = false;
        if (wasPlaying)
        {
            _resumeAfterSeek = false;
            if (_engine != null && _engine.ShowAt(time))
            {
                Shown();
                // Dentro del bucle y con el tramo en caché sigue el motor; si no, mpv desde este frame.
                Resume();
                return;
            }
            HideOnNextRestart();
            _resumeAfterSeek = true;
            player.SeekAbsolute(time, exact: true);
            return;
        }
        if (_paused && _engine != null && _engine.ShowAt(time))
        {
            Shown();
            return;
        }
        SeekAbsolute(time, exact: true);
    }

    public void SeekAbsolute(double time, bool exact)
    {
        time = ClampTime(time);
        StopMotion();
        HideOnNextRestart();
        player.SeekAbsolute(time, exact);
    }

    public void SeekRelative(double seconds)
    {
        // En bucle y dentro del tramo (o del vídeo), lo que se pasa de un extremo sigue por el otro.
        var from = CurrentTime;
        // Fuera del tramo del bucle, da la vuelta en el vídeo entero si tiene el suyo.
        var outside = _loop is { } l && (from < l.Start - 0.5 / Fps || from >= l.End);
        (double Start, double End)? wrap = !outside ? _loop : LoopVideo && Duration > 0 ? (0.0, Duration) : null;
        if (wrap is { } loop && loop.End > loop.Start && from >= loop.Start - 0.5 / Fps && from < loop.End
            && (from + seconds < loop.Start || from + seconds >= loop.End))
        {
            var length = loop.End - loop.Start;
            SeekTo(loop.Start + ((from + seconds - loop.Start) % length + length) % length);
            return;
        }
        if (LastFrameStart > 0 && from + seconds > LastFrameStart)
            SeekTo(LastFrameStart);
        else if (_engine?.ShownTime is { } t)
            SeekAbsolute(Math.Max(0, t + seconds), exact: true);
        else
            player.SeekRelative(seconds);
    }

    private void Shown()
    {
        _shownSeq++;
        _hideWhenPast = null;
        _mpvSyncedTo = null;
        SyncMpvLater(_shownSeq);
        if (_engine?.ShownTime is { } t)
            PositionShown?.Invoke(t);
    }

    private void HideOnNextRestart()
    {
        _navSeeks++;
        if (_engine?.Showing == true)
            _hideOnRestartSeq = _shownSeq;
    }
}
