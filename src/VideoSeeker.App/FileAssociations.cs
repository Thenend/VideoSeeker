using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace VideoSeeker.App;

/// <summary>
/// Registro de los tipos de vídeo para el usuario actual (HKCU, sin administrador), como VLC o mpv.net: un ProgID por
/// extensión, la entrada en "Abrir con" y las Capabilities que hacen salir a VideoSeeker en Aplicaciones
/// predeterminadas. Desde Windows 8 un programa no puede ponerse como predeterminado: eso lo elige el usuario en
/// Configuración, que es adonde lleva <see cref="OpenWindowsSettings"/>.
/// Instalada desde la Store (<see cref="Packaged"/>) no se toca el registro: los tipos van en el manifiesto del
/// paquete, Windows los registra al instalar y los quita al desinstalar.
/// </summary>
public static partial class FileAssociations
{
    private const string AppName = "VideoSeeker";
    private const string Classes = @"Software\Classes";
    private const string CapabilitiesKey = @"Software\VideoSeeker\Capabilities";
    private const string RegisteredApplications = @"Software\RegisteredApplications";

    public static readonly string[] Extensions =
    [
        ".mkv", ".mp4", ".m4v", ".avi", ".mov", ".wmv", ".webm", ".ts", ".m2ts", ".mts",
        ".mpg", ".mpeg", ".flv", ".3gp", ".ogv", ".vob", ".divx", ".asf",
    ];

    // Los que salen en la página de VideoSeeker en Aplicaciones predeterminadas, para no tener que cambiar 18 uno a
    // uno. El resto queda solo en "Abrir con".
    private static readonly string[] Main = [".mkv", ".mp4", ".avi", ".mov", ".webm"];

    private static string ProgId(string extension) => $"{AppName}{extension}";

    private static string Exe => Environment.ProcessPath ?? throw new InvalidOperationException("Sin ruta del programa.");

    private static string OpenCommand(string exe) => $"\"{exe}\" \"%1\"";

    public static bool IsRegistered
    {
        get
        {
            if (Packaged.IsPackaged)
                return true;
            using var key = Registry.CurrentUser.OpenSubKey(RegisteredApplications);
            return key?.GetValue(AppName) != null;
        }
    }

    public static void Register()
    {
        if (Packaged.IsPackaged)
            return;
        var exe = Exe;
        var icon = $"\"{exe}\",0";
        using (var capabilities = Registry.CurrentUser.CreateSubKey(CapabilitiesKey))
        {
            capabilities.SetValue("ApplicationName", AppName);
            capabilities.SetValue("ApplicationDescription", "Reproductor de vídeo");
            capabilities.SetValue("ApplicationIcon", icon);
        }
        Registry.CurrentUser.DeleteSubKeyTree(CapabilitiesKey + @"\FileAssociations", throwOnMissingSubKey: false);
        using var associations = Registry.CurrentUser.CreateSubKey(CapabilitiesKey + @"\FileAssociations");
        using var application = Registry.CurrentUser.CreateSubKey($@"{Classes}\Applications\{Path.GetFileName(exe)}");
        application.SetValue("FriendlyAppName", AppName);
        using (var open = application.CreateSubKey(@"shell\open\command"))
            open.SetValue("", OpenCommand(exe));
        using var supported = application.CreateSubKey("SupportedTypes");

        foreach (var extension in Extensions)
        {
            var progId = ProgId(extension);
            using (var type = Registry.CurrentUser.CreateSubKey($@"{Classes}\{progId}"))
            {
                type.SetValue("", $"Vídeo {extension[1..].ToUpperInvariant()}");
                using (var defaultIcon = type.CreateSubKey("DefaultIcon"))
                    defaultIcon.SetValue("", icon);
                using var command = type.CreateSubKey(@"shell\open\command");
                command.SetValue("", OpenCommand(exe));
            }
            using (var openWith = Registry.CurrentUser.CreateSubKey($@"{Classes}\{extension}\OpenWithProgids"))
                openWith.SetValue(progId, Array.Empty<byte>(), RegistryValueKind.None);
            if (Main.Contains(extension))
                associations.SetValue(extension, progId);
            supported.SetValue(extension, "");
        }

        using (var registered = Registry.CurrentUser.CreateSubKey(RegisteredApplications))
            registered.SetValue(AppName, CapabilitiesKey);
        NotifyShell();
    }

    /// <summary>Borra todo lo que puso <see cref="Register"/>; de cada extensión solo nuestro valor.</summary>
    public static void Unregister()
    {
        if (Packaged.IsPackaged)
            return;
        using (var registered = Registry.CurrentUser.OpenSubKey(RegisteredApplications, writable: true))
            registered?.DeleteValue(AppName, throwOnMissingValue: false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\VideoSeeker", throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree($@"{Classes}\Applications\{Path.GetFileName(Exe)}", throwOnMissingSubKey: false);
        foreach (var extension in Extensions)
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{Classes}\{ProgId(extension)}", throwOnMissingSubKey: false);
            using var openWith = Registry.CurrentUser.OpenSubKey($@"{Classes}\{extension}\OpenWithProgids", writable: true);
            openWith?.DeleteValue(ProgId(extension), throwOnMissingValue: false);
        }
        NotifyShell();
    }

    /// <summary>
    /// Si se movió la carpeta del programa o cambió la lista de tipos principales, vuelve a registrar (solo si ya
    /// estaba registrado).
    /// </summary>
    public static void RefreshIfMoved()
    {
        try
        {
            if (Packaged.IsPackaged || !IsRegistered)
                return;
            using var command = Registry.CurrentUser.OpenSubKey($@"{Classes}\{ProgId(".mkv")}\shell\open\command");
            using var associations = Registry.CurrentUser.OpenSubKey(CapabilitiesKey + @"\FileAssociations");
            if (!string.Equals(command?.GetValue("") as string, OpenCommand(Exe), StringComparison.OrdinalIgnoreCase)
                || associations?.GetValueNames().Order().SequenceEqual(Main.Order()) != true)
                Register();
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Sin acceso al registro: se queda como estaba.
        }
    }

    /// <summary>La página de VideoSeeker en Aplicaciones predeterminadas (en Windows 10, la lista general).</summary>
    public static void OpenWindowsSettings() =>
        Process.Start(new ProcessStartInfo(Packaged.AppUserModelId is { } aumid
            ? $"ms-settings:defaultapps?registeredAUMID={Uri.EscapeDataString(aumid)}"
            : $"ms-settings:defaultapps?registeredAppUser={AppName}") { UseShellExecute = true });

    private static void NotifyShell() => SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0 /* SHCNF_IDLIST */, 0, 0);

    [LibraryImport("shell32.dll")]
    private static partial void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);
}
