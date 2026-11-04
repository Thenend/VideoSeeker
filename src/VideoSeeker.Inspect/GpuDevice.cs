using FFmpeg.AutoGen;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using ID3D11Device = Vortice.Direct3D11.ID3D11Device;
using ID3D11DeviceContext = Vortice.Direct3D11.ID3D11DeviceContext;

namespace VideoSeeker.Inspect;

/// <summary>
/// Dispositivo Direct3D 11 compartido por los decodificadores (FFmpeg decodifica directamente en él)
/// y por el render, de modo que los frames nunca salen de la GPU.
/// </summary>
public sealed unsafe class GpuDevice : IDisposable
{
    private AVBufferRef* _hwDevice;

    public ID3D11Device Device { get; }
    public ID3D11DeviceContext Context { get; }
    /// <summary>La tarjeta del dispositivo; null si no se puede saber.</summary>
    public AdapterInfo? Adapter { get; }

    public GpuDevice()
    {
        D3D11.D3D11CreateDevice(null, DriverType.Hardware,
            DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport,
            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0],
            out ID3D11Device device, out ID3D11DeviceContext context).CheckError();
        Device = device;
        Context = context;
        Adapter = AdapterOf(device);

        // Decodificadores (en sus hilos) y render usan el mismo contexto inmediato: que D3D lo serialice.
        using (var multithread = Device.QueryInterface<ID3D11Multithread>())
            multithread.SetMultithreadProtected(true);

        _hwDevice = ffmpeg.av_hwdevice_ctx_alloc(AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA);
        var hwContext = (AVHWDeviceContext*)_hwDevice->data;
        var d3d = (AVD3D11VADeviceContext*)hwContext->hwctx;
        // FFmpeg libera su referencia al destruir el contexto: le damos una propia.
        Device.AddRef();
        d3d->device = (FFmpeg.AutoGen.ID3D11Device*)Device.NativePointer;
        FFmpegLib.Check(ffmpeg.av_hwdevice_ctx_init(_hwDevice), "av_hwdevice_ctx_init");
    }

    /// <summary>
    /// Nombre y memoria de la tarjeta por defecto (la que usa el motor, que crea su dispositivo sin elegir adaptador);
    /// null si no se puede saber.
    /// </summary>
    public static AdapterInfo? DefaultAdapter()
    {
        try
        {
            using var factory = Vortice.DXGI.DXGI.CreateDXGIFactory1<Vortice.DXGI.IDXGIFactory1>();
            if (factory.EnumAdapters1(0, out var adapter).Failure)
                return null;
            using (adapter)
            {
                var description = adapter.Description1;
                // Con .Value: la conversión implícita pasa por int y desborda con más de 2 GB.
                return new AdapterInfo(description.Description, (long)description.DedicatedVideoMemory.Value,
                    (long)description.SharedSystemMemory.Value);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static AdapterInfo? AdapterOf(ID3D11Device device)
    {
        try
        {
            using var dxgi = device.QueryInterface<Vortice.DXGI.IDXGIDevice>();
            using var adapter = dxgi.GetAdapter();
            using var adapter1 = adapter.QueryInterface<Vortice.DXGI.IDXGIAdapter1>();
            var description = adapter1.Description1;
            return new AdapterInfo(description.Description, (long)description.DedicatedVideoMemory.Value,
                (long)description.SharedSystemMemory.Value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Crea y suelta un dispositivo para que Windows cargue ya el driver de la tarjeta: lo que tarda (más de 100 ms) se
    /// ahorra después mpv al crear el suyo para el primer frame.
    /// </summary>
    public static void WarmUp()
    {
        try
        {
            D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport,
                [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0], out ID3D11Device device, out ID3D11DeviceContext context).CheckError();
            context.Dispose();
            device.Dispose();
        }
        catch (Exception)
        {
            // Sin dispositivo, mpv y el motor lo intentarán por su cuenta.
        }
    }

    /// <summary>Nueva referencia al dispositivo de FFmpeg para asignarla a un AVCodecContext.</summary>
    internal AVBufferRef* NewHwDeviceRef() => ffmpeg.av_buffer_ref(_hwDevice);

    public void Dispose()
    {
        var hw = _hwDevice;
        ffmpeg.av_buffer_unref(&hw);
        _hwDevice = null;
        Context.Dispose();
        Device.Dispose();
    }
}

/// <summary>Nombre y memoria de una tarjeta gráfica.</summary>
public sealed record AdapterInfo(string Name, long DedicatedBytes, long SharedBytes)
{
    private const long Megabyte = 1024L * 1024;

    /// <summary>
    /// Gráficos integrados: su memoria "dedicada" es un trozo pequeño de la RAM (unos 128 MB en Intel) y en realidad
    /// usan la compartida.
    /// </summary>
    public bool Integrated => DedicatedBytes < 512 * Megabyte;

    /// <summary>La memoria con la que cuenta la tarjeta: la dedicada o, en gráficos integrados, la compartida.</summary>
    public long MemoryBytes => Integrated ? SharedBytes : DedicatedBytes;

    /// <summary>
    /// Caché de frames por defecto: la mitad de la memoria de la tarjeta (un cuarto de la compartida en los gráficos
    /// integrados, que sale de la RAM de todo el sistema), en pasos de 256 MB, entre 512 MB y 3 GB.
    /// </summary>
    public long RecommendedCacheBytes
    {
        get
        {
            var bytes = Integrated ? SharedBytes / 4 : DedicatedBytes / 2;
            var steps = bytes / (256 * Megabyte);
            return Math.Clamp(steps * 256 * Megabyte, 512 * Megabyte, 3072 * Megabyte);
        }
    }

    /// <summary>Caché de frames cuando no se sabe qué tarjeta hay.</summary>
    public const long FallbackCacheBytes = 3072 * Megabyte;
}
