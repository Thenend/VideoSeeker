using FFmpeg.AutoGen;

namespace VideoSeeker.Inspect;

/// <summary>Lo que hace falta saber de la pista de vídeo para exportar un tramo con la misma calidad.</summary>
/// <param name="VideoCodec">Nombre de FFmpeg: h264, hevc, av1, vp9…</param>
/// <param name="VideoBitrate">Bits por segundo del vídeo en ese tramo (0 si no se pudo medir).</param>
public sealed record ClipSource(string VideoCodec, int Width, int Height, int BitDepth, double Fps, long VideoBitrate);

public static unsafe class ClipProbe
{
    /// <summary>
    /// Mide el bitrate real del vídeo entre start y end (instantes como los de mpv) sumando el tamaño de sus
    /// paquetes, sin decodificar nada. Si no hay paquetes, usa el que declara el archivo.
    /// </summary>
    public static ClipSource Measure(string path, double start, double end, CancellationToken token)
    {
        var format = (AVFormatContext*)null;
        FFmpegLib.Check(ffmpeg.avformat_open_input(&format, path, null, null), "avformat_open_input");
        var packet = ffmpeg.av_packet_alloc();
        try
        {
            FFmpegLib.Check(ffmpeg.avformat_find_stream_info(format, null), "avformat_find_stream_info");
            var video = FFmpegLib.Check(ffmpeg.av_find_best_stream(format, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0), "av_find_best_stream");
            for (var s = 0; s < format->nb_streams; s++)
                if (s != video)
                    format->streams[s]->discard = AVDiscard.AVDISCARD_ALL;
            var stream = format->streams[video];
            var par = stream->codecpar;
            var desc = ffmpeg.av_pix_fmt_desc_get((AVPixelFormat)par->format);
            var depth = desc != null ? desc->comp[0].depth : 8;
            var rate = stream->avg_frame_rate.num > 0 ? stream->avg_frame_rate : stream->r_frame_rate;
            var fps = rate.num > 0 && rate.den > 0 ? ffmpeg.av_q2d(rate) : 0;

            var timeBase = ffmpeg.av_q2d(stream->time_base);
            var startOffset = format->start_time == ffmpeg.AV_NOPTS_VALUE ? 0 : format->start_time / (double)ffmpeg.AV_TIME_BASE;
            ffmpeg.av_seek_frame(format, -1, (long)((start + startOffset) * ffmpeg.AV_TIME_BASE), ffmpeg.AVSEEK_FLAG_BACKWARD);
            long bytes = 0;
            while (ffmpeg.av_read_frame(format, packet) >= 0)
            {
                try
                {
                    if (packet->stream_index != video)
                        continue;
                    var ts = packet->pts != ffmpeg.AV_NOPTS_VALUE ? packet->pts : packet->dts;
                    if (ts == ffmpeg.AV_NOPTS_VALUE)
                        continue;
                    var t = ts * timeBase - startOffset;
                    // Los paquetes van en orden de decodificación: con frames B, alguno del final llega algo tarde.
                    if (t > end + 2)
                        break;
                    if (t >= start && t < end)
                        bytes += packet->size;
                }
                finally
                {
                    ffmpeg.av_packet_unref(packet);
                }
                token.ThrowIfCancellationRequested();
            }

            var bitrate = bytes > 0 && end > start ? (long)(bytes * 8 / (end - start)) : par->bit_rate > 0 ? par->bit_rate : format->bit_rate;
            return new ClipSource(ffmpeg.avcodec_get_name(par->codec_id), par->width, par->height, depth, fps, Math.Max(0, bitrate));
        }
        finally
        {
            ffmpeg.av_packet_free(&packet);
            ffmpeg.avformat_close_input(&format);
        }
    }

    /// <summary>
    /// El último keyframe en o antes del instante, leyendo los paquetes desde donde cae la búsqueda (que puede
    /// ser bastante antes: en MKV con subtítulos, FFmpeg retrocede para no perder los que ya se veían). null si no
    /// hay ninguno.
    /// </summary>
    public static double? KeyframeAtOrBefore(string path, double time, CancellationToken token)
    {
        var format = (AVFormatContext*)null;
        FFmpegLib.Check(ffmpeg.avformat_open_input(&format, path, null, null), "avformat_open_input");
        var packet = ffmpeg.av_packet_alloc();
        try
        {
            FFmpegLib.Check(ffmpeg.avformat_find_stream_info(format, null), "avformat_find_stream_info");
            var video = FFmpegLib.Check(ffmpeg.av_find_best_stream(format, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0), "av_find_best_stream");
            for (var s = 0; s < format->nb_streams; s++)
                if (s != video)
                    format->streams[s]->discard = AVDiscard.AVDISCARD_ALL;
            var timeBase = ffmpeg.av_q2d(format->streams[video]->time_base);
            var startOffset = format->start_time == ffmpeg.AV_NOPTS_VALUE ? 0 : format->start_time / (double)ffmpeg.AV_TIME_BASE;
            ffmpeg.av_seek_frame(format, -1, (long)((time + startOffset) * ffmpeg.AV_TIME_BASE), ffmpeg.AVSEEK_FLAG_BACKWARD);
            double? found = null;
            while (ffmpeg.av_read_frame(format, packet) >= 0)
            {
                try
                {
                    var ts = packet->pts != ffmpeg.AV_NOPTS_VALUE ? packet->pts : packet->dts;
                    if (packet->stream_index != video || ts == ffmpeg.AV_NOPTS_VALUE)
                        continue;
                    var t = ts * timeBase - startOffset;
                    if (t > time + 1e-4)
                    {
                        // Los keyframes llegan en orden; pasado el instante, ya no hay otro antes.
                        if ((packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0)
                            break;
                        continue;
                    }
                    if ((packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0)
                        found = t;
                }
                finally
                {
                    ffmpeg.av_packet_unref(packet);
                }
                token.ThrowIfCancellationRequested();
            }
            return found;
        }
        finally
        {
            ffmpeg.av_packet_free(&packet);
            ffmpeg.avformat_close_input(&format);
        }
    }
}
