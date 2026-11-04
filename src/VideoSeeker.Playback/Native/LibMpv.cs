using System.Runtime.InteropServices;

namespace VideoSeeker.Playback.Native;

// Subconjunto de la API C de libmpv (client.h) que usamos.
internal static partial class LibMpv
{
    private const string Dll = "libmpv-2.dll";

    [LibraryImport(Dll)]
    internal static partial nint mpv_create();

    [LibraryImport(Dll)]
    internal static partial int mpv_initialize(nint ctx);

    [LibraryImport(Dll)]
    internal static partial void mpv_terminate_destroy(nint ctx);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int mpv_set_option_string(nint ctx, string name, string data);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int mpv_set_property_string(nint ctx, string name, string data);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint mpv_get_property_string(nint ctx, string name);

    [LibraryImport(Dll, EntryPoint = "mpv_get_property", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int mpv_get_property_double(nint ctx, string name, MpvFormat format, out double data);

    [LibraryImport(Dll)]
    internal static partial int mpv_command(nint ctx, nint[] args);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int mpv_observe_property(nint ctx, ulong replyUserdata, string name, MpvFormat format);

    [LibraryImport(Dll)]
    internal static partial nint mpv_wait_event(nint ctx, double timeout);

    [LibraryImport(Dll)]
    internal static partial void mpv_wakeup(nint ctx);

    [LibraryImport(Dll)]
    internal static partial void mpv_free(nint data);

    [LibraryImport(Dll)]
    internal static partial nint mpv_error_string(int error);
}

internal enum MpvFormat
{
    None = 0,
    String = 1,
    OsdString = 2,
    Flag = 3,
    Int64 = 4,
    Double = 5,
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEventNative
{
    public MpvEventId EventId;
    public int Error;
    public ulong ReplyUserdata;
    public nint Data;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEventPropertyNative
{
    public nint Name;
    public MpvFormat Format;
    public nint Data;
}
