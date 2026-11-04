using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VideoSeeker.Playback.Native;

/// <summary>
/// libmpv-2.dll va en la subcarpeta mpv\ (para no llenar de DLL la carpeta del exe). .NET solo la buscaría junto
/// al programa, así que se le dice dónde está antes de la primera llamada.
/// </summary>
internal static class LibMpvLoader
{
    private const string Dll = "libmpv-2.dll";

    // CA2255 avisa de [ModuleInitializer] en una biblioteca; aquí es justo lo que hace falta (antes de cualquier llamada).
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register() => NativeLibrary.SetDllImportResolver(typeof(LibMpvLoader).Assembly, Resolve);

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Dll)
            return 0;
        var inSubfolder = Path.Combine(AppContext.BaseDirectory, "mpv", Dll);
        if (NativeLibrary.TryLoad(inSubfolder, out var handle))
            return handle;
        return 0; // Si no está ahí, la búsqueda normal (junto al exe).
    }
}
