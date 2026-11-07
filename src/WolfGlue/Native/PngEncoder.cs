using System.Buffers.Binary;
using System.IO.Compression;

namespace WolfGlue.Native;

/// <summary>
/// Codificador PNG minimo (RGBA de 8 bits, sin filtros por fila salvo "Up"), para pasar imagenes del portapapeles a la
/// web en un formato comun sin depender de codecs del sistema.
/// </summary>
internal static class PngEncoder
{
    private static readonly uint[] Crc = BuildCrc();

    /// <summary>PNG a partir de pixeles BGRA de arriba abajo (alfa sin premultiplicar), <paramref name="stride"/> bytes por fila.</summary>
    public static byte[] FromBgra(ReadOnlySpan<byte> bgra, int width, int height, int stride)
    {
        // Filas con su byte de filtro: 2 (Up) comprime bien capturas de pantalla.
        int rowLen = width * 4;
        var raw = new byte[(rowLen + 1) * height];
        var prev = new byte[rowLen];
        var cur = new byte[rowLen];
        for (int y = 0; y < height; y++)
        {
            var src = bgra.Slice(y * stride, rowLen);
            for (int x = 0; x < rowLen; x += 4)
            {
                cur[x] = src[x + 2]; cur[x + 1] = src[x + 1]; cur[x + 2] = src[x]; cur[x + 3] = src[x + 3];
            }
            int o = y * (rowLen + 1);
            raw[o] = 2;
            for (int i = 0; i < rowLen; i++) raw[o + 1 + i] = (byte)(cur[i] - prev[i]);
            (prev, cur) = (cur, prev);
        }

        using var ms = new MemoryStream();
        ms.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
        ihdr[8] = 8; // bits
        ihdr[9] = 6; // RGBA
        Chunk(ms, "IHDR", ihdr);
        using (var z = new MemoryStream())
        {
            using (var zs = new ZLibStream(z, CompressionLevel.Fastest, leaveOpen: true)) zs.Write(raw);
            Chunk(ms, "IDAT", z.ToArray());
        }
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    /// <summary>True si los bytes empiezan por la firma PNG.</summary>
    public static bool IsPng(ReadOnlySpan<byte> data) =>
        data.Length > 8 && data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G';

    private static void Chunk(Stream s, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> head = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(head, data.Length);
        for (int i = 0; i < 4; i++) head[4 + i] = (byte)type[i];
        s.Write(head);
        s.Write(data);
        uint c = Update(0xFFFFFFFF, head[4..]);
        c = Update(c, data) ^ 0xFFFFFFFF;
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, c);
        s.Write(crc);
    }

    private static uint Update(uint c, ReadOnlySpan<byte> data)
    {
        foreach (var b in data) c = Crc[(c ^ b) & 0xFF] ^ (c >> 8);
        return c;
    }

    private static uint[] BuildCrc()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }
}
