using FFmpeg.AutoGen;

namespace VideoSeeker.Inspect;

/// <summary>
/// Instante de cada frame de la pista de vídeo, en orden, para dar el número de frame exacto aunque el
/// vídeo tenga fps variable o no empiece en cero. Se construye leyendo los paquetes sin decodificar nada
/// (en un vídeo de 9 GB, unos 15 s) y se guarda en disco para no repetirlo.
/// </summary>
public sealed unsafe class FrameIndex
{
    private const uint Magic = 0x49465356; // "VSFI"
    private const int Version = 2; // la 2 añade los keyframes

    private readonly double[] _times;

    private FrameIndex(double[] times, double[] keyframes)
    {
        _times = times;
        Keyframes = keyframes;
    }

    public int Count => _times.Length;

    /// <summary>Instantes de los keyframes, en orden (el índice del contenedor no siempre está completo).</summary>
    public double[] Keyframes { get; }

    /// <summary>Número (desde 0) del frame más cercano al instante dado.</summary>
    public int FrameNumber(double time)
    {
        if (_times.Length == 0)
            return 0;
        var i = Array.BinarySearch(_times, time);
        if (i >= 0)
            return i;
        i = ~i;
        if (i == 0)
            return 0;
        if (i == _times.Length)
            return _times.Length - 1;
        return time - _times[i - 1] <= _times[i] - time ? i - 1 : i;
    }

    /// <summary>Número del frame que se está viendo en el instante dado: el último que empieza en o antes de él.</summary>
    public int FrameAtOrBefore(double time)
    {
        if (_times.Length == 0)
            return 0;
        var i = Array.BinarySearch(_times, time);
        if (i < 0)
            i = ~i - 1;
        return Math.Clamp(i, 0, _times.Length - 1);
    }

    /// <summary>Instante en que empieza el frame dado (se limita al primero y al último).</summary>
    public double TimeOf(int frame) => _times.Length == 0 ? 0 : _times[Math.Clamp(frame, 0, _times.Length - 1)];

    /// <summary>Lee todos los paquetes de vídeo del archivo. Los instantes son como los de mpv (desde el inicio).</summary>
    public static FrameIndex Build(string path, CancellationToken token)
    {
        var format = (AVFormatContext*)null;
        FFmpegLib.Check(ffmpeg.avformat_open_input(&format, path, null, null), "avformat_open_input");
        var packet = ffmpeg.av_packet_alloc();
        try
        {
            FFmpegLib.Check(ffmpeg.avformat_find_stream_info(format, null), "avformat_find_stream_info");
            var video = FFmpegLib.Check(ffmpeg.av_find_best_stream(format, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0), "av_find_best_stream");
            // Las demás pistas ni se leen.
            for (var s = 0; s < format->nb_streams; s++)
                if (s != video)
                    format->streams[s]->discard = AVDiscard.AVDISCARD_ALL;

            var timeBase = ffmpeg.av_q2d(format->streams[video]->time_base);
            // El mismo desfase que restan FrameDecoder y mpv.
            var startOffset = format->start_time == ffmpeg.AV_NOPTS_VALUE ? 0 : format->start_time / (double)ffmpeg.AV_TIME_BASE;
            var timestamps = new List<long>();
            var keyTimestamps = new List<long>();
            while (ffmpeg.av_read_frame(format, packet) >= 0)
            {
                if (packet->stream_index == video && (packet->flags & ffmpeg.AV_PKT_FLAG_DISCARD) == 0)
                {
                    var ts = packet->pts != ffmpeg.AV_NOPTS_VALUE ? packet->pts : packet->dts;
                    if (ts != ffmpeg.AV_NOPTS_VALUE)
                    {
                        timestamps.Add(ts);
                        if ((packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0)
                            keyTimestamps.Add(ts);
                    }
                }
                ffmpeg.av_packet_unref(packet);
                token.ThrowIfCancellationRequested();
            }

            // Los paquetes van en orden de decodificación: los frames se numeran en orden de presentación.
            timestamps.Sort();
            var times = new List<double>(timestamps.Count);
            for (var i = 0; i < timestamps.Count; i++)
                if (i == 0 || timestamps[i] != timestamps[i - 1])
                    times.Add(timestamps[i] * timeBase - startOffset);
            keyTimestamps.Sort();
            return new FrameIndex([.. times], [.. keyTimestamps.Distinct().Select(k => k * timeBase - startOffset)]);
        }
        finally
        {
            ffmpeg.av_packet_free(&packet);
            ffmpeg.avformat_close_input(&format);
        }
    }

    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = file + ".tmp";
        using (var writer = new BinaryWriter(File.Create(temp)))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(_times.Length);
            foreach (var t in _times)
                writer.Write(t);
            writer.Write(Keyframes.Length);
            foreach (var t in Keyframes)
                writer.Write(t);
        }
        File.Move(temp, file, overwrite: true);
    }

    /// <summary>El índice guardado, o null si no existe o no es válido.</summary>
    public static FrameIndex? Load(string file)
    {
        try
        {
            using var reader = new BinaryReader(File.OpenRead(file));
            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version)
                return null;
            var count = reader.ReadInt32();
            if (count < 0 || count > (reader.BaseStream.Length - 12) / sizeof(double))
                return null;
            var times = new double[count];
            for (var i = 0; i < count; i++)
                times[i] = reader.ReadDouble();
            var keyCount = reader.ReadInt32();
            if (keyCount < 0 || keyCount > count)
                return null;
            var keyframes = new double[keyCount];
            for (var i = 0; i < keyCount; i++)
                keyframes[i] = reader.ReadDouble();
            return new FrameIndex(times, keyframes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
