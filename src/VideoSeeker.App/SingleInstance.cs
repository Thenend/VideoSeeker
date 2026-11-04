using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace VideoSeeker.App;

/// <summary>
/// Una sola ventana: si ya hay una abierta, la segunda instancia le pasa el archivo por un pipe y sale.
/// </summary>
public sealed partial class SingleInstance : IDisposable
{
    private static readonly string Id = $"VideoSeeker-{Environment.UserName}";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    public bool IsFirst { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(true, Id, out var createdNew);
        IsFirst = createdNew;
    }

    /// <summary>Envía la ruta a la instancia que ya está abierta.</summary>
    public static bool Forward(string? path)
    {
        try
        {
            // Quien nos lanzó (el Explorador) nos deja pasar al frente; se lo cedemos a la ventana abierta, o
            // Windows solo haría parpadear su botón en la barra de tareas.
            AllowSetForegroundWindow(-1 /* ASFW_ANY */);
            using var pipe = new NamedPipeClientStream(".", Id, PipeDirection.Out);
            pipe.Connect(2000);
            using var writer = new StreamWriter(pipe);
            writer.WriteLine(path ?? "");
            return true;
        }
        catch (Exception e) when (e is IOException or TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Escucha rutas enviadas por otras instancias; la ruta vacía solo pide traer la ventana al frente.</summary>
    public void Listen(Action<string> onPath)
    {
        Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(Id, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await pipe.WaitForConnectionAsync(_stop.Token);
                    using var reader = new StreamReader(pipe);
                    onPath(await reader.ReadLineAsync(_stop.Token) ?? "");
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    // Cliente que se desconecta a medias: seguimos escuchando.
                }
            }
        });
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);

    public void Dispose()
    {
        _stop.Cancel();
        if (IsFirst)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
