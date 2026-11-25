using FFmpeg.AutoGen;

namespace VideoSeeker.Inspect;

/// <summary>
/// Miniaturas de un vídeo: decodifica en CPU (sin tocar la GPU ni la caché del motor) el frame que se ve en
/// un instante y lo escala a un ancho fijo en BGRA. Abrir el archivo cuesta; se reutiliza para varias.
/// No es thread-safe.
/// </summary>
public sealed unsafe class Thumbnailer : IDisposable
{
    private AVFormatContext* _format;
    private AVCodecContext* _codec;
    private AVFrame* _frame, _kept, _ahead;
    private AVPacket* _packet;
    private SwsContext* _sws;
    private readonly int _stream;
    private readonly AVRational _timeBase;
    private readonly double _startOffset;
    private readonly double _frameDuration;
    // Modo seguido (Grab con "sequential"): el decodificador se quedó justo después del instante pedido, con el primer
    // frame que ya pasaba de él guardado en _ahead. Un instante poco más adelante se saca siguiendo, sin buscar desde
    // un keyframe. NaN si no se puede seguir (otra búsqueda, cancelada, final del archivo).
    private double _lastTarget = double.NaN;
    private bool _aheadValid, _eof;
    // Hasta cuánto más adelante sale a cuenta seguir decodificando en vez de buscar (que vuelve a un keyframe).
    private const double ContinueWithin = 2.0;

    public string Path { get; }

    public Thumbnailer(string path)
    {
        Path = path;
        var format = (AVFormatContext*)null;
        FFmpegLib.Check(ffmpeg.avformat_open_input(&format, path, null, null), "avformat_open_input");
        _format = format;
        FFmpegLib.Check(ffmpeg.avformat_find_stream_info(_format, null), "avformat_find_stream_info");

        AVCodec* decoder = null;
        _stream = FFmpegLib.Check(ffmpeg.av_find_best_stream(_format, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0), "av_find_best_stream");
        for (var s = 0; s < _format->nb_streams; s++)
            if (s != _stream)
                _format->streams[s]->discard = AVDiscard.AVDISCARD_ALL;
        var stream = _format->streams[_stream];

        _codec = ffmpeg.avcodec_alloc_context3(decoder);
        FFmpegLib.Check(ffmpeg.avcodec_parameters_to_context(_codec, stream->codecpar), "avcodec_parameters_to_context");
        // Pocos hilos: va en segundo plano y no debe quitárselos a la reproducción ni al motor.
        _codec->thread_count = 4;
        FFmpegLib.Check(ffmpeg.avcodec_open2(_codec, decoder, null), "avcodec_open2");

        _frame = ffmpeg.av_frame_alloc();
        _kept = ffmpeg.av_frame_alloc();
        _ahead = ffmpeg.av_frame_alloc();
        _packet = ffmpeg.av_packet_alloc();
        _timeBase = stream->time_base;
        // Los mismos instantes que mpv y FrameDecoder: desde el inicio del archivo.
        _startOffset = _format->start_time == ffmpeg.AV_NOPTS_VALUE ? 0 : _format->start_time / (double)ffmpeg.AV_TIME_BASE;
        var rate = stream->avg_frame_rate.num > 0 ? stream->avg_frame_rate : stream->r_frame_rate;
        _frameDuration = rate.num > 0 && rate.den > 0 ? 1 / ffmpeg.av_q2d(rate) : 1 / 30.0;
    }

    /// <summary>
    /// El frame que se ve en "time", escalado a "width" de ancho (el alto sigue la proporción de pantalla).
    /// Null si no se pudo decodificar. Con "sequential" (varios instantes en orden, como la tira de la barra de zoom),
    /// uno poco después del anterior se saca siguiendo la decodificación, sin volver a buscar.
    /// </summary>
    public (byte[] Pixels, int Width, int Height)? Grab(double time, int width, CancellationToken token, bool sequential = false)
    {
        // Desde el keyframe, el último frame que empieza en o antes del instante (con medio frame de margen).
        // Los frames salen en orden de presentación; el candidato se guarda aparte porque el siguiente
        // reutiliza _frame.
        var target = time + _frameDuration / 2;
        var resume = sequential && !_eof && !double.IsNaN(_lastTarget)
            && target - _lastTarget >= _frameDuration * 0.9 && target - _lastTarget <= ContinueWithin;
        _lastTarget = double.NaN;
        if (!resume)
        {
            var ts = (long)Math.Floor((Math.Max(0, time) + _startOffset) / ffmpeg.av_q2d(_timeBase));
            ffmpeg.av_seek_frame(_format, _stream, ts, ffmpeg.AVSEEK_FLAG_BACKWARD);
            ffmpeg.avcodec_flush_buffers(_codec);
            ffmpeg.av_frame_unref(_ahead);
            _aheadValid = false;
            _eof = false;
        }

        var have = false;
        ffmpeg.av_frame_unref(_kept);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int result;
            if (_aheadValid)
            {
                // Siguiendo: el frame que ya se leyó tras el instante anterior es el primero.
                ffmpeg.av_frame_unref(_frame);
                ffmpeg.av_frame_move_ref(_frame, _ahead);
                _aheadValid = false;
                result = 0;
            }
            else
                result = ffmpeg.avcodec_receive_frame(_codec, _frame);
            if (result == 0)
            {
                var pts = _frame->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE ? _frame->best_effort_timestamp : _frame->pts;
                var frameTime = pts == ffmpeg.AV_NOPTS_VALUE ? time : pts * ffmpeg.av_q2d(_timeBase) - _startOffset;
                if (frameTime > target && have)
                {
                    if (sequential)
                    {
                        ffmpeg.av_frame_move_ref(_ahead, _frame);
                        _aheadValid = true;
                    }
                    break;
                }
                ffmpeg.av_frame_unref(_kept);
                ffmpeg.av_frame_move_ref(_kept, _frame);
                have = true;
                if (frameTime > target)
                    break; // ya el primero pasa del instante: se queda ese
                continue;
            }
            if (result == ffmpeg.AVERROR_EOF || result != ffmpeg.AVERROR(ffmpeg.EAGAIN))
                break;

            if (ffmpeg.av_read_frame(_format, _packet) < 0)
            {
                _eof = true;
                ffmpeg.avcodec_send_packet(_codec, null); // vaciar lo que quede dentro del decodificador
                continue;
            }
            if (_packet->stream_index == _stream)
                ffmpeg.avcodec_send_packet(_codec, _packet);
            ffmpeg.av_packet_unref(_packet);
        }
        if (have && sequential)
            _lastTarget = target;
        return have ? Convert(_kept, width) : null;
    }

    private (byte[] Pixels, int Width, int Height) Convert(AVFrame* frame, int width)
    {
        var sar = frame->sample_aspect_ratio.num > 0 ? ffmpeg.av_q2d(frame->sample_aspect_ratio) : 1;
        var aspect = frame->width * sar / Math.Max(1, frame->height);
        var height = Math.Max(2, (int)Math.Round(width / aspect / 2) * 2);
        // A un búfer nativo con margen y luego al array, fila a fila (ver FFmpegLib.ScaleToBgra).
        var src = FFmpegLib.ScaleToBgra(ref _sws, frame, width, height, (int)SwsFlags.SWS_AREA, out var srcStride);
        try
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            fixed (byte* dst = pixels)
                for (var y = 0; y < height; y++)
                    Buffer.MemoryCopy(src + (long)y * srcStride, dst + (long)y * stride, stride, stride);
            return (pixels, width, height);
        }
        finally
        {
            ffmpeg.av_free(src);
        }
    }

    public void Dispose()
    {
        var frame = _frame;
        ffmpeg.av_frame_free(&frame);
        var kept = _kept;
        ffmpeg.av_frame_free(&kept);
        var ahead = _ahead;
        ffmpeg.av_frame_free(&ahead);
        var packet = _packet;
        ffmpeg.av_packet_free(&packet);
        var codec = _codec;
        ffmpeg.avcodec_free_context(&codec);
        var format = _format;
        ffmpeg.avformat_close_input(&format);
        ffmpeg.sws_freeContext(_sws);
        _frame = null;
        _kept = null;
        _ahead = null;
        _packet = null;
        _codec = null;
        _format = null;
        _sws = null;
    }
}
