using System.Buffers.Binary;
using System.IO;
using System.IO.Hashing;

namespace VideoSeeker.App.Data;

/// <summary>
/// Huella de un vídeo: xxHash64 del tamaño más el primer y el último MB. Se calcula en milisegundos
/// incluso con archivos de varios GB y no cambia al renombrar o mover el archivo.
/// </summary>
public static class Fingerprint
{
    private const int Chunk = 1 << 20;

    public static string Compute(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.RandomAccess);
        var hash = new XxHash64();

        Span<byte> size = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(size, file.Length);
        hash.Append(size);

        var buffer = new byte[Chunk];
        hash.Append(buffer.AsSpan(0, file.ReadAtLeast(buffer, Chunk, throwOnEndOfStream: false)));
        if (file.Length > Chunk)
        {
            file.Seek(Math.Max(Chunk, file.Length - Chunk), SeekOrigin.Begin);
            hash.Append(buffer.AsSpan(0, file.ReadAtLeast(buffer, Chunk, throwOnEndOfStream: false)));
        }
        return Convert.ToHexStringLower(hash.GetCurrentHash());
    }
}
