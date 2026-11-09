using System.IO;
using System.Runtime.InteropServices;

namespace VideoSeeker.App;

/// <summary>
/// Si la app corre instalada como paquete (Microsoft Store, MSIX; ver scripts/make-msix.ps1). Ahí la carpeta del
/// programa es de solo lectura, Windows desvía a otra carpeta lo que se escribe en %LOCALAPPDATA% (el Explorador no lo
/// vería) y los tipos de vídeo los declara el manifiesto del paquete, no el registro.
/// </summary>
public static partial class Packaged
{
    /// <summary>Nombre de familia del paquete (Nombre_hash del editor); null si no es un paquete.</summary>
    public static string? FamilyName { get; } = ReadFamilyName();

    public static bool IsPackaged => FamilyName != null;

    /// <summary>Id de la aplicación en el manifiesto (Application Id en scripts/msix/AppxManifest.xml).</summary>
    public const string ApplicationId = "VideoSeeker";

    /// <summary>Identificador con que Windows conoce la app (para Aplicaciones predeterminadas).</summary>
    public static string? AppUserModelId => FamilyName is { } family ? $"{family}!{ApplicationId}" : null;

    /// <summary>
    /// La carpeta propia del paquete para los datos (LocalState, la de ApplicationData.LocalFolder): es una ruta real,
    /// sin desvío, así que el Explorador la abre bien. Windows la borra al desinstalar la app.
    /// </summary>
    public static string? LocalState => FamilyName is { } family
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", family, "LocalState")
        : null;

    private static string? ReadFamilyName()
    {
        try
        {
            uint length = 0;
            // ERROR_INSUFFICIENT_BUFFER (122) si hay paquete; APPMODEL_ERROR_NO_PACKAGE (15700) si no.
            if (GetCurrentPackageFamilyName(ref length, null) != 122 || length == 0)
                return null;
            var buffer = new char[length];
            if (GetCurrentPackageFamilyName(ref length, buffer) != 0)
                return null;
            return new string(buffer, 0, (int)length - 1);
        }
        catch (Exception e) when (e is EntryPointNotFoundException or DllNotFoundException)
        {
            return null;
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, [Out] char[]? packageFamilyName);
}
