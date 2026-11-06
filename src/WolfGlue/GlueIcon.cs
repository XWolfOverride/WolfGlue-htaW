using System.Buffers.Binary;

namespace WolfGlue;

/// <summary>Formato de un icono.</summary>
public enum GlueIconFormat { Unknown, Ico, Png }

/// <summary>Una imagen de un archivo .ico: tamano, profundidad y posicion de sus datos (PNG o DIB) en el archivo.</summary>
public readonly record struct GlueIcoImage(int Width, int Height, byte ColorCount, ushort Planes, ushort BitCount, int Offset, int Length);

/// <summary>Lectura minima de iconos .ico y .png (sin decodificar pixeles), comun a todas las plataformas.</summary>
public static class GlueIcon
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static GlueIconFormat Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 24 && data[..8].SequenceEqual(PngSignature)) return GlueIconFormat.Png;
        if (data.Length >= 6 && data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0
            && BinaryPrimitives.ReadUInt16LittleEndian(data[4..]) > 0) return GlueIconFormat.Ico;
        return GlueIconFormat.Unknown;
    }

    /// <summary>Ancho y alto de un PNG (cabecera IHDR).</summary>
    public static (int width, int height) PngSize(ReadOnlySpan<byte> png) =>
        ((int)BinaryPrimitives.ReadUInt32BigEndian(png[16..]), (int)BinaryPrimitives.ReadUInt32BigEndian(png[20..]));

    /// <summary>Imagenes de un .ico. Lanza <see cref="FormatException"/> si el directorio no es coherente.</summary>
    public static GlueIcoImage[] IcoImages(ReadOnlySpan<byte> ico)
    {
        if (Detect(ico) != GlueIconFormat.Ico) throw new FormatException("No es un .ico");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(ico[4..]);
        if (6 + count * 16 > ico.Length) throw new FormatException("Directorio de .ico truncado");
        var result = new GlueIcoImage[count];
        for (int i = 0; i < count; i++)
        {
            var e = ico.Slice(6 + i * 16, 16);
            int length = BinaryPrimitives.ReadInt32LittleEndian(e[8..]);
            int offset = BinaryPrimitives.ReadInt32LittleEndian(e[12..]);
            if (length <= 0 || offset < 6 + count * 16 || (long)offset + length > ico.Length)
                throw new FormatException("Imagen de .ico fuera del archivo");
            int w = e[0] == 0 ? 256 : e[0], h = e[1] == 0 ? 256 : e[1];
            // Las imagenes PNG dentro del .ico pueden pasar de 256: su tamano real esta en el propio PNG.
            if (Detect(ico.Slice(offset, length)) == GlueIconFormat.Png) (w, h) = PngSize(ico.Slice(offset, length));
            result[i] = new GlueIcoImage(w, h, e[2], BinaryPrimitives.ReadUInt16LittleEndian(e[4..]),
                BinaryPrimitives.ReadUInt16LittleEndian(e[6..]), offset, length);
        }
        return result;
    }

    /// <summary>
    /// La imagen mas adecuada para <paramref name="size"/> pixeles: la menor que lo alcance (escalar hacia abajo se ve
    /// mejor), o la mayor si ninguna llega; a igualdad, la de mas color.
    /// </summary>
    public static GlueIcoImage Best(GlueIcoImage[] images, int size)
    {
        if (images.Length == 0) throw new FormatException(".ico vacio");
        return images
            .OrderBy(i => i.Width >= size ? 0 : 1)
            .ThenBy(i => i.Width >= size ? i.Width : -i.Width)
            .ThenByDescending(i => i.BitCount)
            .First();
    }
}
