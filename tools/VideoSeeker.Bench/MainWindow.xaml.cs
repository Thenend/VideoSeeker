using System.Diagnostics;
using System.IO;
using System.Windows;
using VideoSeeker.Playback;

namespace VideoSeeker.Bench;

public partial class MainWindow : Window
{
    private readonly VideoHost _host = new();
    private Mpv? _mpv;

    public MainWindow()
    {
        InitializeComponent();
        VideoArea.Child = _host;
        ContentRendered += OnContentRendered;
        Closed += (_, _) => _mpv?.Dispose();
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        var windowShownMs = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;

        var sw = Stopwatch.StartNew();
        _mpv = new Mpv();
        foreach (var (name, value) in Bench.MpvOptions)
            _mpv.SetOption(name, value);
        // BENCH_MPV="opcion=valor;opcion=valor" permite probar variantes sin recompilar.
        foreach (var pair in (Environment.GetEnvironmentVariable("BENCH_MPV") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            _mpv.SetOption(pair[..pair.IndexOf('=')], pair[(pair.IndexOf('=') + 1)..]);
        _mpv.SetOption("wid", _host.Handle.ToInt64().ToString());
        _mpv.Initialize();
        var mpvInitMs = sw.Elapsed.TotalMilliseconds;

        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var samplesDir = FindSamplesDir();
        var files = args.Length > 0 ? args : Bench.FindVideos(samplesDir);
        var outPath = Path.Combine(Path.GetDirectoryName(samplesDir)!, "docs", "spike-f0-results.md");

        Task.Run(() =>
        {
            try
            {
                var bench = new Bench(_mpv, msg => Dispatcher.Invoke(() => Status.Text = msg));
                var results = bench.Run(files);
                File.WriteAllText(outPath, Bench.Report(results, windowShownMs, mpvInitMs));
            }
            catch (Exception ex)
            {
                File.WriteAllText(outPath + ".error.txt", ex.ToString());
            }
            Dispatcher.Invoke(Close);
        });
    }

    private static string FindSamplesDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples");
            if (Directory.Exists(candidate))
                return candidate;
        }
        throw new DirectoryNotFoundException("No se encontró la carpeta samples/");
    }
}
