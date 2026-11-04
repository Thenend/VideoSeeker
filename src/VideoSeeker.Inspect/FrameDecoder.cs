using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Vortice.Direct3D11;
using ID3D11Texture2D = Vortice.Direct3D11.ID3D11Texture2D;
using Vortice.Direct3D;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace VideoSeeker.Inspect;

/// <summary>
/// Decodificador FFmpeg de la pista de vídeo de un archivo. Usa D3D11VA cuando el códec lo admite,
/// y deja cada frame en una textura propia de la GPU (copia GPU a GPU, sin pasar por la CPU).
/// No es thread-safe: cada hilo de precarga tiene el suyo.
/// </summary>
public sealed unsafe class FrameDecoder : IDisposable
{
    // El delegado debe vivir mientras se use el códec.
    private static readonly AVCodecContext_get_format GetFormatKeepAlive = ChooseFormat;

    private readonly GpuDevice _gpu;
    private AVFormatContext* _format;
    private AVCodecContext* _codec;
    private AVFrame* _frame;
    private AVPacket* _packet;
    private SwsContext* _sws;
    private readonly int _stream;
    private readonly AVRational _timeBase;
    private readonly double _startOffset;

    /// <summary>Nombre corto del contenedor según FFmpeg ("avi", "matroska,webm", "mov,mp4,…").</summary>
    public string ContainerName { get; }
    public double Fps { get; }
    public double Duration { get; }
    public int Width { get; }
    public int Height { get; }

    public FrameDecoder(string path, GpuDevice gpu)
    {
        _gpu = gpu;
        var format = (AVFormatContext*)null;
        FFmpegLib.Check(ffmpeg.avformat_open_input(&format, path, null, null), "avformat_open_input");
        _format = format;
        FFmpegLib.Check(ffmpeg.avformat_find_stream_info(_format, null), "avformat_find_stream_info");

        AVCodec* decoder = null;
        _stream = FFmpegLib.Check(ffmpeg.av_find_best_stream(_format, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0), "av_find_best_stream");
        var stream = _format->streams[_stream];
        // libdav1d no admite D3D11VA; el decodificador AV1 nativo de FFmpeg sí.
        if (stream->codecpar->codec_id == AVCodecID.AV_CODEC_ID_AV1 && ffmpeg.avcodec_find_decoder_by_name("av1") is var native && native != null)
            decoder = native;

        _codec = ffmpeg.avcodec_alloc_context3(decoder);
        FFmpegLib.Check(ffmpeg.avcodec_parameters_to_context(_codec, stream->codecpar), "avcodec_parameters_to_context");
        _codec->hw_device_ctx = gpu.NewHwDeviceRef();
        _codec->get_format = GetFormatKeepAlive;
        _codec->thread_count = 0;
        FFmpegLib.Check(ffmpeg.avcodec_open2(_codec, decoder, null), "avcodec_open2");

        _frame = ffmpeg.av_frame_alloc();
        _packet = ffmpeg.av_packet_alloc();
        _timeBase = stream->time_base;
        // mpv cuenta el tiempo desde el inicio del archivo: restamos el mismo desfase.
        _startOffset = _format->start_time == ffmpeg.AV_NOPTS_VALUE ? 0 : _format->start_time / (double)ffmpeg.AV_TIME_BASE;

        var rate = stream->avg_frame_rate.num > 0 ? stream->avg_frame_rate : stream->r_frame_rate;
        Fps = rate.num > 0 && rate.den > 0 ? ffmpeg.av_q2d(rate) : 30;
        Duration = _format->duration > 0 ? _format->duration / (double)ffmpeg.AV_TIME_BASE : 0;
        ContainerName = Marshal.PtrToStringAnsi((nint)_format->iformat->name) ?? "";
        Width = _codec->width;
        Height = _codec->height;
    }

    public double FrameDuration => 1 / Fps;

    /// <summary>Se coloca en el keyframe anterior (o igual) al instante dado.</summary>
    public void SeekKeyframe(double time)
    {
        var ts = (long)Math.Floor((time + _startOffset) / ffmpeg.av_q2d(_timeBase));
        ffmpeg.av_seek_frame(_format, _stream, ts, ffmpeg.AVSEEK_FLAG_BACKWARD);
        ffmpeg.avcodec_flush_buffers(_codec);
    }

    /// <summary>
    /// Instantes de los keyframes según el índice del contenedor (en MKV se lee al hacer el primer seek).
    /// Vacío si el archivo no tiene índice.
    /// </summary>
    public double[] KeyframeTimes()
    {
        var stream = _format->streams[_stream];
        var count = ffmpeg.avformat_index_get_entries_count(stream);
        var times = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = ffmpeg.avformat_index_get_entry(stream, i);
            // En C es un campo de bits: "flags" son los 2 bits bajos.
            if ((entry->flags2_size30 & ffmpeg.AVINDEX_KEYFRAME) != 0 && entry->timestamp != ffmpeg.AV_NOPTS_VALUE)
                times.Add(ToTime(entry->timestamp));
        }
        times.Sort();
        return [.. times];
    }

    /// <summary>Instante del keyframe donde empezaría a decodificar para llegar a "time", sin decodificar nada.</summary>
    public double? KeyframeAtOrBefore(double time)
    {
        SeekKeyframe(time);
        while (ffmpeg.av_read_frame(_format, _packet) >= 0)
        {
            try
            {
                if (_packet->stream_index != _stream)
                    continue;
                var ts = _packet->pts != ffmpeg.AV_NOPTS_VALUE ? _packet->pts : _packet->dts;
                return ts == ffmpeg.AV_NOPTS_VALUE ? null : ToTime(ts);
            }
            finally
            {
                ffmpeg.av_packet_unref(_packet);
            }
        }
        return null;
    }

    /// <summary>Decodifica el siguiente frame. Devuelve su instante, o null al final del archivo.</summary>
    public double? Next()
    {
        while (true)
        {
            var result = ffmpeg.avcodec_receive_frame(_codec, _frame);
            if (result == 0)
            {
                var ts = _frame->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE ? _frame->best_effort_timestamp : _frame->pts;
                return ts == ffmpeg.AV_NOPTS_VALUE ? null : ToTime(ts);
            }
            if (result == ffmpeg.AVERROR_EOF)
                return null;
            if (result != ffmpeg.AVERROR(ffmpeg.EAGAIN))
                FFmpegLib.Check(result, "avcodec_receive_frame");

            if (ffmpeg.av_read_frame(_format, _packet) < 0)
            {
                ffmpeg.avcodec_send_packet(_codec, null); // vaciar lo que quede dentro del decodificador
                continue;
            }
            if (_packet->stream_index == _stream)
                ffmpeg.avcodec_send_packet(_codec, _packet);
            ffmpeg.av_packet_unref(_packet);
        }
    }

    public long CurrentPts => _frame->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE ? _frame->best_effort_timestamp : _frame->pts;

    /// <summary>Copia el último frame decodificado a una textura propia de la GPU.</summary>
    public CachedFrame Capture(double time)
    {
        var sar = _frame->sample_aspect_ratio.num > 0 ? ffmpeg.av_q2d(_frame->sample_aspect_ratio) : 1;
        // NV12/P010 exigen dimensiones pares.
        var width = _frame->width & ~1;
        var height = _frame->height & ~1;
        var aspect = width * sar / height;

        return _frame->format == (int)AVPixelFormat.AV_PIX_FMT_D3D11
            ? CaptureHardware(time, width, height, aspect)
            : CaptureSoftware(time, width, height, aspect);
    }

    private CachedFrame CaptureHardware(double time, int width, int height, double aspect)
    {
        var srcPointer = (nint)_frame->data[0];
        var srcIndex = (uint)(nint)_frame->data[1];
        Marshal.AddRef(srcPointer); // el wrapper hará Release al liberarse
        using var source = new ID3D11Texture2D(srcPointer);
        var format = source.Description.Format;
        var tenBit = format == Format.P010;

        var texture = _gpu.Device.CreateTexture2D(new Texture2DDescription(format, (uint)width, (uint)height, 1, 1, BindFlags.ShaderResource));
        ID3D11ShaderResourceView? luma = null;
        ID3D11ShaderResourceView chroma;
        try
        {
            _gpu.Context.CopySubresourceRegion(texture, 0, 0, 0, 0, source, srcIndex, new Box(0, 0, 0, width, height, 1));
            luma = _gpu.Device.CreateShaderResourceView(texture,
                new ShaderResourceViewDescription(texture, ShaderResourceViewDimension.Texture2D, tenBit ? Format.R16_UNorm : Format.R8_UNorm));
            chroma = _gpu.Device.CreateShaderResourceView(texture,
                new ShaderResourceViewDescription(texture, ShaderResourceViewDimension.Texture2D, tenBit ? Format.R16G16_UNorm : Format.R8G8_UNorm));
        }
        catch
        {
            // Sin memoria para las vistas: que no se quede la textura ocupando la tarjeta.
            luma?.Dispose();
            texture.Dispose();
            throw;
        }

        return new CachedFrame
        {
            Pts = CurrentPts, Time = time, Width = width, Height = height, DisplayAspect = aspect,
            Layout = FrameLayout.BiPlanar, Color = ReadColor(tenBit ? 10 : 8),
            Bytes = (long)(width * height * 1.5 * (tenBit ? 2 : 1)),
            Texture = texture, Plane0 = luma, Plane1 = chroma,
        };
    }

    // Códecs antiguos sin D3D11VA: se convierten a BGRA en CPU. Son de baja resolución, así que es barato.
    private CachedFrame CaptureSoftware(double time, int width, int height, double aspect)
    {
        _sws = ffmpeg.sws_getCachedContext(_sws, _frame->width, _frame->height, (AVPixelFormat)_frame->format,
            width, height, AVPixelFormat.AV_PIX_FMT_BGRA, (int)SwsFlags.SWS_BILINEAR, null, null, null);
        var stride = width * 4;
        var pixels = new byte[stride * height];
        fixed (byte* dst = pixels)
        {
            var dstData = new byte*[] { dst, null, null, null };
            var dstStride = new[] { stride, 0, 0, 0 };
            ffmpeg.sws_scale(_sws, _frame->data.ToArray(), _frame->linesize.ToArray(), 0, _frame->height, dstData, dstStride);

            var texture = _gpu.Device.CreateTexture2D(
                new Texture2DDescription(Format.B8G8R8A8_UNorm, (uint)width, (uint)height, 1, 1, BindFlags.ShaderResource, ResourceUsage.Immutable),
                new SubresourceData(dst, (uint)stride));
            var view = _gpu.Device.CreateShaderResourceView(texture);
            return new CachedFrame
            {
                Pts = CurrentPts, Time = time, Width = width, Height = height, DisplayAspect = aspect,
                Layout = FrameLayout.Bgra, Color = new ColorInfo(ColorMatrix.Bt601, true, TransferCurve.Sdr, 8),
                Bytes = pixels.Length, Texture = texture, Plane0 = view,
            };
        }
    }

    private ColorInfo ReadColor(int bitDepth)
    {
        var matrix = _frame->colorspace switch
        {
            AVColorSpace.AVCOL_SPC_BT709 => ColorMatrix.Bt709,
            AVColorSpace.AVCOL_SPC_BT2020_NCL or AVColorSpace.AVCOL_SPC_BT2020_CL => ColorMatrix.Bt2020,
            AVColorSpace.AVCOL_SPC_BT470BG or AVColorSpace.AVCOL_SPC_SMPTE170M => ColorMatrix.Bt601,
            // Sin dato: lo habitual (y lo que supone mpv) es 709 en HD y 601 en SD.
            _ => _frame->height >= 720 ? ColorMatrix.Bt709 : ColorMatrix.Bt601,
        };
        var transfer = _frame->color_trc switch
        {
            AVColorTransferCharacteristic.AVCOL_TRC_SMPTE2084 => TransferCurve.Pq,
            AVColorTransferCharacteristic.AVCOL_TRC_ARIB_STD_B67 => TransferCurve.Hlg,
            _ => TransferCurve.Sdr,
        };
        return new ColorInfo(matrix, _frame->color_range == AVColorRange.AVCOL_RANGE_JPEG, transfer, bitDepth);
    }

    private double ToTime(long ts) => ts * ffmpeg.av_q2d(_timeBase) - _startOffset;

    private static AVPixelFormat ChooseFormat(AVCodecContext* context, AVPixelFormat* formats)
    {
        var fallback = AVPixelFormat.AV_PIX_FMT_NONE;
        for (var p = formats; *p != AVPixelFormat.AV_PIX_FMT_NONE; p++)
        {
            if (*p == AVPixelFormat.AV_PIX_FMT_D3D11)
                return *p;
            if (fallback == AVPixelFormat.AV_PIX_FMT_NONE && (ffmpeg.av_pix_fmt_desc_get(*p)->flags & ffmpeg.AV_PIX_FMT_FLAG_HWACCEL) == 0)
                fallback = *p;
        }
        return fallback;
    }

    public void Dispose()
    {
        var frame = _frame;
        ffmpeg.av_frame_free(&frame);
        var packet = _packet;
        ffmpeg.av_packet_free(&packet);
        var codec = _codec;
        ffmpeg.avcodec_free_context(&codec);
        var format = _format;
        ffmpeg.avformat_close_input(&format);
        ffmpeg.sws_freeContext(_sws);
        _frame = null;
        _packet = null;
        _codec = null;
        _format = null;
        _sws = null;
    }
}
