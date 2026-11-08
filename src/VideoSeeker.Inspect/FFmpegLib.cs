using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace VideoSeeker.Inspect;

public sealed class FFmpegException(string message) : Exception(message);

public static unsafe class FFmpegLib
{
    private static bool _initialized;

    /// <summary>Carga las DLL de FFmpeg desde la carpeta indicada. Llamar una vez al arrancar.</summary>
    public static void Initialize(string dllFolder)
    {
        if (_initialized)
            return;
        ffmpeg.RootPath = dllFolder;
        DynamicallyLoadedBindings.Initialize();
        ffmpeg.av_log_set_level(ffmpeg.AV_LOG_ERROR);
        _initialized = true;
    }

    internal static int Check(int result, string what)
    {
        if (result < 0)
            throw new FFmpegException($"{what}: {ErrorText(result)}");
        return result;
    }

    /// <summary>
    /// Convierte un frame decodificado en CPU a BGRA de "width"×"height" en un búfer nativo propio (liberar con
    /// av_free). Nunca en un byte[]: las rutinas SIMD de swscale escriben de más al final de cada fila cuando el ancho
    /// no es múltiplo de 16 (un WMV de 520 px pisaba 32 bytes tras el array y corrompía el heap de .NET, que cerraba
    /// la aplicación). Por eso las filas van alineadas a 64 bytes y sobra un margen al final.
    /// </summary>
    internal static byte* ScaleToBgra(ref SwsContext* sws, AVFrame* frame, int width, int height, int flags, out int stride)
    {
        if (frame->width <= 0 || frame->height <= 0 || width <= 0 || height <= 0)
            throw new FFmpegException($"sws_scale: tamaño no válido {frame->width}x{frame->height} → {width}x{height}");
        sws = ffmpeg.sws_getCachedContext(sws, frame->width, frame->height, (AVPixelFormat)frame->format,
            width, height, AVPixelFormat.AV_PIX_FMT_BGRA, flags, null, null, null);
        if (sws == null)
            throw new FFmpegException($"sws_getCachedContext: formato {(AVPixelFormat)frame->format} no admitido");

        const int Align = 64, Slack = 256;
        stride = (width * 4 + Align - 1) / Align * Align;
        var size = (ulong)stride * (ulong)height + Slack;
        var buffer = (byte*)ffmpeg.av_malloc(size);
        if (buffer == null)
            throw new FFmpegException("av_malloc: sin memoria");
        var dstData = new byte*[] { buffer, null, null, null };
        var dstStride = new[] { stride, 0, 0, 0 };
        var rows = ffmpeg.sws_scale(sws, frame->data.ToArray(), frame->linesize.ToArray(), 0, frame->height, dstData, dstStride);
        if (rows <= 0)
        {
            ffmpeg.av_free(buffer);
            throw new FFmpegException($"sws_scale: {ErrorText(rows)}");
        }
        return buffer;
    }

    internal static string ErrorText(int error)
    {
        const int size = 256;
        var buffer = stackalloc byte[size];
        ffmpeg.av_strerror(error, buffer, size);
        return Marshal.PtrToStringAnsi((nint)buffer) ?? $"error {error}";
    }
}
