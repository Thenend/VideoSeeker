using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace VideoSeeker.Inspect;

/// <summary>
/// Pinta frames de la caché en una ventana hija hermana de la de mpv. Esa ventana es transparente al
/// ratón (los clics llegan a la de debajo). Nunca se oculta: para "ocultarla" se pone detrás de la de mpv.
/// Si se ocultara de verdad, al volver a mostrarla Windows enseñaría un instante lo último que tenía pintado
/// (un frame viejo) antes del nuevo. Todo se usa desde el hilo de la interfaz.
/// </summary>
public sealed partial class FrameRenderer : IDisposable
{
    private const string ClassName = "VideoSeekerInspect";
    private const int WS_CHILD = 0x40000000, WS_CLIPSIBLINGS = 0x04000000;
    private const int WM_NCHITTEST = 0x0084, HTTRANSPARENT = -1;
    private const int WS_VISIBLE = 0x10000000;
    private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010;
    private static readonly nint HWND_TOP = 0, HWND_BOTTOM = 1;

    private static readonly WndProcDelegate WndProcKeepAlive = WndProc;
    private static bool _classRegistered;

    private readonly GpuDevice _gpu;
    private readonly nint _parent;
    private readonly nint _hwnd;
    private readonly IDXGISwapChain1 _swapChain;
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pixelShader;
    private readonly ID3D11SamplerState _sampler;
    private readonly ID3D11Buffer _constants;
    private ID3D11RenderTargetView? _target;
    private int _width = 1, _height = 1;

    public bool Visible { get; private set; }

    public FrameRenderer(GpuDevice gpu, nint parentHwnd)
    {
        _gpu = gpu;
        _parent = parentHwnd;
        RegisterClassOnce();
        _hwnd = CreateWindowExW(0, ClassName, "", WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS, 0, 0, 1, 1, parentHwnd, 0, GetModuleHandleW(null), 0);
        if (_hwnd == 0)
            throw new InvalidOperationException($"CreateWindowEx falló: {Marshal.GetLastWin32Error()}");
        // Nace encima de sus hermanas: se manda detrás de la de mpv hasta que haya algo que enseñar.
        SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        using var dxgiDevice = gpu.Device.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        using var factory = adapter.GetParent<IDXGIFactory2>();
        _swapChain = factory.CreateSwapChainForHwnd(gpu.Device, _hwnd, new SwapChainDescription1
        {
            Width = 1,
            Height = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2,
            SwapEffect = SwapEffect.FlipDiscard,
            Scaling = Scaling.Stretch,
        });
        factory.MakeWindowAssociation(_hwnd, WindowAssociationFlags.IgnoreAll);

        var vs = Compiler.Compile(FrameShaders.Source, "VS", "frame.hlsl", "vs_5_0");
        var ps = Compiler.Compile(FrameShaders.Source, "PS", "frame.hlsl", "ps_5_0");
        _vertexShader = gpu.Device.CreateVertexShader(vs.Span);
        _pixelShader = gpu.Device.CreatePixelShader(ps.Span);
        _sampler = gpu.Device.CreateSamplerState(SamplerDescription.LinearClamp);
        _constants = gpu.Device.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Params>(), BindFlags.ConstantBuffer));
    }

    /// <summary>Pinta el frame y muestra la ventana encima del vídeo de mpv.</summary>
    public void Render(CachedFrame frame)
    {
        UpdateSize();
        var parameters = BuildParams(frame);
        var ctx = _gpu.Context;
        ctx.UpdateSubresource(in parameters, _constants);
        ctx.OMSetRenderTargets(_target!);
        ctx.ClearRenderTargetView(_target!, new Color4(0, 0, 0, 1));
        ctx.RSSetViewport(LetterboxViewport(frame.DisplayAspect));
        ctx.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        ctx.VSSetShader(_vertexShader);
        ctx.PSSetShader(_pixelShader);
        ctx.PSSetSampler(0, _sampler);
        ctx.PSSetConstantBuffer(0, _constants);
        if (frame.Layout == FrameLayout.BiPlanar)
        {
            ctx.PSSetShaderResource(0, frame.Plane0);
            ctx.PSSetShaderResource(1, frame.Plane1!);
        }
        else
        {
            ctx.PSSetShaderResource(2, frame.Plane0);
        }
        ctx.Draw(3, 0);
        // Soltar las texturas: la caché puede liberarlas en cualquier momento.
        ctx.PSSetShaderResource(0, null!);
        ctx.PSSetShaderResource(1, null!);
        ctx.PSSetShaderResource(2, null!);
        _swapChain.Present(0, PresentFlags.None);

        if (!Visible)
        {
            // Esperar a que el escritorio componga el frame nuevo antes de subir la ventana encima de mpv.
            _gpu.Context.Flush();
            DwmFlush();
            Visible = true;
        }
        // mpv puede recrear su ventana (al abrir otro archivo) por encima: la nuestra se vuelve a subir.
        SetWindowPos(_hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void Hide()
    {
        if (!Visible)
            return;
        SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        Visible = false;
    }

    private void UpdateSize()
    {
        GetClientRect(_parent, out var rect);
        var width = Math.Max(1, rect.Right - rect.Left);
        var height = Math.Max(1, rect.Bottom - rect.Top);
        if (_target != null && width == _width && height == _height)
            return;
        _width = width;
        _height = height;
        MoveWindow(_hwnd, 0, 0, width, height, false);
        _target?.Dispose();
        _gpu.Context.OMSetRenderTargets((ID3D11RenderTargetView)null!);
        _swapChain.ResizeBuffers(2, (uint)width, (uint)height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
        using var buffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _target = _gpu.Device.CreateRenderTargetView(buffer);
    }

    // Igual que mpv: la imagen entera dentro de la ventana, centrada, con bandas negras.
    private Viewport LetterboxViewport(double aspect)
    {
        double w = _width, h = _height;
        if (w / h > aspect)
            w = h * aspect;
        else
            h = w / aspect;
        return new Viewport((float)Math.Round((_width - w) / 2), (float)Math.Round((_height - h) / 2), (float)Math.Round(w), (float)Math.Round(h));
    }

    private static Params BuildParams(CachedFrame frame)
    {
        if (frame.Layout == FrameLayout.Bgra)
            return new Params { Layout = 1 };

        var c = frame.Color;
        var (kr, kb) = c.Matrix switch
        {
            ColorMatrix.Bt601 => (0.299, 0.114),
            ColorMatrix.Bt2020 => (0.2627, 0.0593),
            _ => (0.2126, 0.0722),
        };
        var kg = 1 - kr - kb;

        // Valores normalizados 0..1 de la textura; en rango limitado, Y va de 16 a 235 y C de 16 a 240 (a 8 bits).
        var max = (1 << c.BitDepth) - 1.0;
        var unit = (1 << (c.BitDepth - 8)) / max;
        double yOffset = c.FullRange ? 0 : 16 * unit, yScale = c.FullRange ? 1 : 1 / (219 * unit);
        double cOffset = 128 * unit, cScale = c.FullRange ? 1 : 1 / (224 * unit);

        // R = Y + 2(1-Kr)·Cr ; B = Y + 2(1-Kb)·Cb ; G = (Y - Kr·R - Kb·B) / Kg
        double rCr = 2 * (1 - kr), bCb = 2 * (1 - kb);
        double gCb = -kb * bCb / kg, gCr = -kr * rCr / kg;
        Vector4 Row(double cb, double cr) => new(
            (float)yScale, (float)(cb * cScale), (float)(cr * cScale),
            (float)(-yOffset * yScale - (cb + cr) * cOffset * cScale));

        return new Params
        {
            RowR = Row(0, rCr),
            RowG = Row(gCb, gCr),
            RowB = Row(bCb, 0),
            Layout = 0,
            Transfer = (int)c.Transfer,
            Peak = 1000f / 203,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Params
    {
        public Vector4 RowR, RowG, RowB;
        public int Layout, Transfer;
        public float Peak, Pad;
    }

    private static void RegisterClassOnce()
    {
        if (_classRegistered)
            return;
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProcKeepAlive),
            hInstance = GetModuleHandleW(null),
            lpszClassName = Marshal.StringToHGlobalUni(ClassName),
        };
        if (RegisterClassExW(ref wc) == 0)
            throw new InvalidOperationException($"RegisterClassEx falló: {Marshal.GetLastWin32Error()}");
        _classRegistered = true;
    }

    // Transparente al ratón: los clics y arrastres los recibe la ventana del vídeo que hay debajo.
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam) =>
        msg == WM_NCHITTEST ? HTTRANSPARENT : DefWindowProcW(hwnd, msg, wParam, lParam);

    public void Dispose()
    {
        _target?.Dispose();
        _constants.Dispose();
        _sampler.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
        _swapChain.Dispose();
        DestroyWindow(_hwnd);
    }

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public nint lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground, lpszMenuName, lpszClassName, hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial ushort RegisterClassExW(ref WNDCLASSEXW wc);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowExW(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmFlush();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MoveWindow(nint hwnd, int x, int y, int width, int height, [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    private static partial nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandleW(string? moduleName);
}
