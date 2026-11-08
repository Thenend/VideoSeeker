using System.Diagnostics;
using System.Windows;
using VideoSeeker.App.Data;
using System.IO;
using VideoSeeker.Inspect;
using VideoSeeker.Playback;

namespace VideoSeeker.App;

public partial class App : Application
{
    private SingleInstance? _instance;

    /// <summary>Marcas de tiempo del arranque, para startup.log.</summary>
    public static readonly List<(string Stage, DateTime At)> StartupMarks = [];

    /// <summary>
    /// Todos los deslizadores: pinchar en la línea fuera de la bolita la lleva ahí y, sin soltar, la bolita sigue al
    /// ratón (como en Windows); WPF solo la movía al punto.
    /// </summary>
    private static void OnSliderPressed(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Slider { Template: { } template } slider
            || template.FindName("PART_Track", slider) is not System.Windows.Controls.Primitives.Track { Thumb: { } thumb } track
            || thumb.IsMouseOver)
            return;
        var value = track.ValueFromPoint(e.GetPosition(track));
        if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
            value = slider.Minimum + Math.Round((value - slider.Minimum) / slider.TickFrequency) * slider.TickFrequency;
        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        slider.UpdateLayout();
        // El arrastre empieza ya desde la bolita, en su nuevo sitio.
        thumb.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(e.MouseDevice, e.Timestamp, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            Source = thumb,
        });
        e.Handled = true;
    }

    public static void Mark(string stage)
    {
        lock (StartupMarks)
            StartupMarks.Add((stage, DateTime.Now));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Mark("OnStartup");
        // Hasta que se vea el primer frame, WPF pinta por software: no inicia Direct3D (unos 100 ms) a la vez que mpv
        // prepara el primer frame. Luego la ventana vuelve a pintar con la tarjeta (MainWindow.UseHardwareRendering).
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        // Windows carga ya el driver de la tarjeta, mientras se monta la ventana: mpv lo necesita para el primer frame.
        Task.Run(GpuDevice.WarmUp);
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Slider), UIElement.PreviewMouseLeftButtonDownEvent,
            new System.Windows.Input.MouseButtonEventHandler(OnSliderPressed));
        // Para un instalador: registra o quita los tipos de vídeo y sale, sin ventana.
        if (e.Args is ["--register" or "--unregister"])
        {
            if (e.Args[0] == "--register")
                FileAssociations.Register();
            else
                FileAssociations.Unregister();
            Shutdown();
            return;
        }
        var path = FullPath(e.Args.FirstOrDefault());
        // Las autopruebas van por su cuenta: con la app de Mario abierta no le pasan el vídeo a ella, ni reciben los
        // que él abra.
        var selfTest = Environment.GetEnvironmentVariable("VIDEOSEEKER_SELFTEST");
        if (selfTest == null)
        {
            _instance = new SingleInstance();
            if (!_instance.IsFirst && SingleInstance.Forward(path))
            {
                Shutdown();
                return;
            }
        }
        if (selfTest == "rapida")
            global::VideoSeeker.App.MainWindow.WatchBindingErrors();

        var store = new DataStore();
        Mark("datos cargados");
        var player = new MpvPlayer();
        // mpv tarda en cargar su DLL e inicializarse: lo hacemos en paralelo mientras se crea la ventana.
        var playerReady = Task.Run(() =>
        {
            player.Initialize(store.Settings.Volume, store.Settings.Mute, store.Settings.HardwareDecoding, Path.Combine(store.Folder, "cache", "mpv"));
            Mark("mpv inicializado");
        });

        // Las DLL de FFmpeg (motor de inspección) también se cargan en paralelo.
        var ffmpegReady = Task.Run(() => FFmpegLib.Initialize(Path.Combine(AppContext.BaseDirectory, "ffmpeg")));

        var window = new MainWindow(store, player, playerReady, ffmpegReady, path, Process.GetCurrentProcess().StartTime);
        Mark("ventana creada");
        playerReady.Wait();
        window.StartEarly();
        Mark("mpv arrancado");
        _instance?.Listen(p => window.Dispatcher.BeginInvoke(() => window.OpenFromOtherInstance(p)));
        window.Show();
        // Las autopruebas pueden ejecutarse desde una copia en otra carpeta: no se apunta Windows a ella.
        if (selfTest == null)
            Task.Run(FileAssociations.RefreshIfMoved);
    }

    // La instancia abierta puede tener otra carpeta de trabajo: se le pasa la ruta completa.
    private static string? FullPath(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return null;
        try
        {
            return Path.GetFullPath(arg);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return arg;
        }
    }

    // Reiniciar (al cambiar la carpeta de datos): la nueva instancia se lanza al salir, ya soltada la de instancia
    // única, para que no le pase el vídeo a esta.
    private static (bool Requested, string? Path) _restart;

    /// <summary>Cierra la app y la vuelve a abrir (con ese vídeo, si se da).</summary>
    public static void Restart(string? path)
    {
        _restart = (true, path);
        Current.MainWindow?.Close();
    }

    /// <summary>
    /// Deja de ser la instancia abierta (al cerrar, con la ventana ya oculta): si se abre otro vídeo mientras esta acaba
    /// de cerrar mpv, arranca una nueva en lugar de pasárselo a esta.
    /// </summary>
    public static void ReleaseSingleInstance()
    {
        if (Current is App app)
        {
            app._instance?.Dispose();
            app._instance = null;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        if (_restart.Requested && Environment.ProcessPath is { } exe)
        {
            var start = new ProcessStartInfo(exe) { UseShellExecute = false };
            if (_restart.Path != null)
                start.ArgumentList.Add(_restart.Path);
            Process.Start(start);
        }
        base.OnExit(e);
    }
}
