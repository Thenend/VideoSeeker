using Vortice.Direct3D11;

namespace VideoSeeker.Inspect;

public enum FrameLayout
{
    /// <summary>NV12 o P010: plano de luma más plano de croma entrelazado.</summary>
    BiPlanar,
    /// <summary>Ya convertido a BGRA (códecs sin decodificación por GPU).</summary>
    Bgra,
}

public enum ColorMatrix { Bt601, Bt709, Bt2020 }

public enum TransferCurve { Sdr, Pq, Hlg }

/// <summary>Cómo interpretar los colores de un frame, sacado de los metadatos del vídeo.</summary>
public readonly record struct ColorInfo(ColorMatrix Matrix, bool FullRange, TransferCurve Transfer, int BitDepth);

/// <summary>Un frame decodificado que vive en la GPU, listo para pintarse.</summary>
public sealed class CachedFrame : IDisposable
{
    public required long Pts { get; init; }
    /// <summary>Segundos en la misma escala que el time-pos de mpv (el vídeo empieza en 0).</summary>
    public required double Time { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required double DisplayAspect { get; init; }
    public required FrameLayout Layout { get; init; }
    public required ColorInfo Color { get; init; }
    public required long Bytes { get; init; }
    public required ID3D11Texture2D Texture { get; init; }
    /// <summary>Luma (BiPlanar) o la imagen completa (Bgra).</summary>
    public required ID3D11ShaderResourceView Plane0 { get; init; }
    /// <summary>Croma; null en Bgra.</summary>
    public ID3D11ShaderResourceView? Plane1 { get; init; }

    public void Dispose()
    {
        Plane1?.Dispose();
        Plane0.Dispose();
        Texture.Dispose();
    }
}
