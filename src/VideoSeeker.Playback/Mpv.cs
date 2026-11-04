using System.Runtime.InteropServices;
using VideoSeeker.Playback.Native;

namespace VideoSeeker.Playback;

public enum MpvEventId
{
    None = 0,
    Shutdown = 1,
    LogMessage = 2,
    GetPropertyReply = 3,
    SetPropertyReply = 4,
    CommandReply = 5,
    StartFile = 6,
    EndFile = 7,
    FileLoaded = 8,
    ClientMessage = 16,
    VideoReconfig = 17,
    AudioReconfig = 18,
    Seek = 20,
    PlaybackRestart = 21,
    PropertyChange = 22,
    QueueOverflow = 24,
    Hook = 25,
}

/// <summary>Evento de mpv ya copiado a memoria gestionada.</summary>
public readonly record struct MpvEvent(MpvEventId Id, int Error, ulong ReplyUserdata, string? PropertyName,
    double? DoubleValue, bool? FlagValue = null, string? StringValue = null);

public sealed class MpvException(string message) : Exception(message);

/// <summary>Envoltorio fino y thread-safe (como la propia libmpv) de un contexto mpv.</summary>
public sealed class Mpv : IDisposable
{
    private nint _ctx;

    public Mpv()
    {
        _ctx = LibMpv.mpv_create();
        if (_ctx == 0)
            throw new MpvException("mpv_create falló");
    }

    public void SetOption(string name, string value) =>
        Check(LibMpv.mpv_set_option_string(_ctx, name, value), $"opción {name}={value}");

    public void Initialize() => Check(LibMpv.mpv_initialize(_ctx), "mpv_initialize");

    public void SetProperty(string name, string value) =>
        Check(LibMpv.mpv_set_property_string(_ctx, name, value), $"propiedad {name}={value}");

    public string? GetString(string name)
    {
        var ptr = LibMpv.mpv_get_property_string(_ctx, name);
        if (ptr == 0)
            return null;
        try { return Marshal.PtrToStringUTF8(ptr); }
        finally { LibMpv.mpv_free(ptr); }
    }

    public double? GetDouble(string name) =>
        LibMpv.mpv_get_property_double(_ctx, name, MpvFormat.Double, out var value) >= 0 ? value : null;

    public void Command(params string[] args)
    {
        var ptrs = new nint[args.Length + 1];
        try
        {
            for (var i = 0; i < args.Length; i++)
                ptrs[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            Check(LibMpv.mpv_command(_ctx, ptrs), string.Join(' ', args));
        }
        finally
        {
            foreach (var p in ptrs)
                if (p != 0) Marshal.FreeCoTaskMem(p);
        }
    }

    public void ObserveDouble(ulong id, string name) =>
        Check(LibMpv.mpv_observe_property(_ctx, id, name, MpvFormat.Double), $"observar {name}");

    public void ObserveFlag(ulong id, string name) =>
        Check(LibMpv.mpv_observe_property(_ctx, id, name, MpvFormat.Flag), $"observar {name}");

    /// <summary>Las propiedades de tipo nodo (como track-list) llegan como JSON.</summary>
    public void ObserveString(ulong id, string name) =>
        Check(LibMpv.mpv_observe_property(_ctx, id, name, MpvFormat.String), $"observar {name}");

    /// <summary>Espera el siguiente evento; timeout en segundos (negativo = sin límite).</summary>
    public MpvEvent WaitEvent(double timeout)
    {
        var native = Marshal.PtrToStructure<MpvEventNative>(LibMpv.mpv_wait_event(_ctx, timeout));
        if (native.EventId != MpvEventId.PropertyChange || native.Data == 0)
            return new MpvEvent(native.EventId, native.Error, native.ReplyUserdata, null, null);

        var prop = Marshal.PtrToStructure<MpvEventPropertyNative>(native.Data);
        var name = Marshal.PtrToStringUTF8(prop.Name);
        if (prop.Data == 0)
            return new MpvEvent(native.EventId, native.Error, native.ReplyUserdata, name, null);
        return prop.Format switch
        {
            MpvFormat.Double => new MpvEvent(native.EventId, native.Error, native.ReplyUserdata, name, Marshal.PtrToStructure<double>(prop.Data)),
            MpvFormat.Flag => new MpvEvent(native.EventId, native.Error, native.ReplyUserdata, name, null, FlagValue: Marshal.ReadInt32(prop.Data) != 0),
            MpvFormat.String => new MpvEvent(native.EventId, native.Error, native.ReplyUserdata, name, null, StringValue: Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(prop.Data))),
            _ => new MpvEvent(native.EventId, native.Error, native.ReplyUserdata, name, null),
        };
    }

    public void Wakeup() => LibMpv.mpv_wakeup(_ctx);

    public void Dispose()
    {
        if (_ctx != 0)
        {
            LibMpv.mpv_terminate_destroy(_ctx);
            _ctx = 0;
        }
    }

    private static void Check(int error, string what)
    {
        if (error < 0)
            throw new MpvException($"{what}: {Marshal.PtrToStringUTF8(LibMpv.mpv_error_string(error))}");
    }
}
