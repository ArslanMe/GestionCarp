using System.Security.Cryptography;

namespace GestionCarp.Core.Common;

/// <summary>Huella (SHA-256) del contenido de un archivo, para saber si dos archivos son idénticos.</summary>
public static class FileHasher
{
    private const int BufferSize = 1 << 20;

    public static string Hash(string path, CancellationToken cancellationToken = default)
        => Hash(path, long.MaxValue, cancellationToken);

    /// <summary>Huella de los primeros <paramref name="maxBytes"/> bytes (rápida, para descartar).</summary>
    public static string Hash(string path, long maxBytes, CancellationToken cancellationToken = default)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            BufferSize, FileOptions.SequentialScan);
        var buffer = new byte[(int)Math.Min(BufferSize, Math.Max(1, maxBytes))];
        long remaining = maxBytes;
        int read;
        while (remaining > 0 && (read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining))) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sha.AppendData(buffer, 0, read);
            remaining -= read;
        }
        return Convert.ToHexString(sha.GetHashAndReset());
    }
}
