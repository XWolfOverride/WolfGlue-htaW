using System.Buffers.Binary;

namespace HtaW.Package;

public enum EmbeddedKind { Html = 1, Zip = 2 }

/// <summary>
/// App .htaw anadida al final de un ejecutable de htaW (ejecutable portable). Formato:
/// <c>[ejecutable][app][longitud: int64][tipo: int32][version: int32][firma: "htaWapp\0"]</c>, todo en little-endian.
/// Los sistemas cargan el ejecutable por sus cabeceras y no leen lo que va detras, asi que el programa funciona igual.
/// </summary>
public sealed record EmbeddedApp(long Offset, long Length, EmbeddedKind Kind)
{
    public const int TrailerSize = 24;
    private const int Version = 1;
    private static ReadOnlySpan<byte> Magic => "htaWapp\0"u8;

    /// <summary>La app incrustada en <paramref name="exe"/>, o null si no lleva ninguna.</summary>
    public static EmbeddedApp? Find(string? exe)
    {
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return null;
        try
        {
            using var f = new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Find(f);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    internal static EmbeddedApp? Find(Stream s)
    {
        if (s.Length < TrailerSize) return null;
        Span<byte> t = stackalloc byte[TrailerSize];
        s.Position = s.Length - TrailerSize;
        s.ReadExactly(t);
        if (!t[16..].SequenceEqual(Magic)) return null;
        long length = BinaryPrimitives.ReadInt64LittleEndian(t);
        int kind = BinaryPrimitives.ReadInt32LittleEndian(t[8..]);
        int version = BinaryPrimitives.ReadInt32LittleEndian(t[12..]);
        long offset = s.Length - TrailerSize - length;
        if (version != Version || length <= 0 || offset < 0 || kind is not (1 or 2)) return null;
        return new EmbeddedApp(offset, length, (EmbeddedKind)kind);
    }

    /// <summary>Escribe la app y la cola de datos al final de <paramref name="output"/>.</summary>
    internal static void Append(Stream output, ReadOnlySpan<byte> app, EmbeddedKind kind)
    {
        output.Write(app);
        Span<byte> t = stackalloc byte[TrailerSize];
        BinaryPrimitives.WriteInt64LittleEndian(t, app.Length);
        BinaryPrimitives.WriteInt32LittleEndian(t[8..], (int)kind);
        BinaryPrimitives.WriteInt32LittleEndian(t[12..], Version);
        Magic.CopyTo(t[16..]);
        output.Write(t);
    }
}
