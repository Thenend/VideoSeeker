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

    internal static string ErrorText(int error)
    {
        const int size = 256;
        var buffer = stackalloc byte[size];
        ffmpeg.av_strerror(error, buffer, size);
        return Marshal.PtrToStringAnsi((nint)buffer) ?? $"error {error}";
    }
}
