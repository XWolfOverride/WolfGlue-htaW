using System.Buffers.Binary;
using System.Text;

namespace WolfGlue;

/// <summary>Lectura minima de fuentes TrueType/OpenType (TTF/OTF): nombre de familia, para usarlas tras cargarlas en memoria.</summary>
internal static class GlueFontInfo
{
    /// <summary>
    /// Nombre de familia (registro 1, el que usa GDI; si no hay, el 16 «tipografico») de un TTF u OTF; null si no es una fuente valida.
    /// Prefiere los nombres de Windows (UTF-16) y, si no hay, los de Mac Roman.
    /// </summary>
    public static string? FamilyName(ReadOnlySpan<byte> font)
    {
        try
        {
            if (font.Length < 12) return null;
            uint version = BinaryPrimitives.ReadUInt32BigEndian(font);
            if (version is not (0x00010000 or 0x4F54544F /* OTTO */ or 0x74727565 /* true */)) return null;
            int tables = BinaryPrimitives.ReadUInt16BigEndian(font[4..]);
            for (int i = 0; i < tables; i++)
            {
                var rec = font.Slice(12 + i * 16, 16);
                if (Encoding.ASCII.GetString(rec[..4]) != "name") continue;
                int off = (int)BinaryPrimitives.ReadUInt32BigEndian(rec[8..]);
                return FromNameTable(font[off..]);
            }
        }
        catch (ArgumentOutOfRangeException) { }
        return null;
    }

    private static string? FromNameTable(ReadOnlySpan<byte> t)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(t[2..]);
        int strings = BinaryPrimitives.ReadUInt16BigEndian(t[4..]);
        string? best = null;
        int bestScore = -1;
        for (int i = 0; i < count; i++)
        {
            var r = t.Slice(6 + i * 12, 12);
            int platform = BinaryPrimitives.ReadUInt16BigEndian(r);
            int encoding = BinaryPrimitives.ReadUInt16BigEndian(r[2..]);
            int nameId = BinaryPrimitives.ReadUInt16BigEndian(r[6..]);
            int len = BinaryPrimitives.ReadUInt16BigEndian(r[8..]);
            int off = BinaryPrimitives.ReadUInt16BigEndian(r[10..]);
            if (nameId is not (1 or 16)) continue;
            var bytes = t.Slice(strings + off, len);
            string? s = platform switch
            {
                3 when encoding is 0 or 1 or 10 => Encoding.BigEndianUnicode.GetString(bytes),
                0 => Encoding.BigEndianUnicode.GetString(bytes),
                1 when encoding == 0 => Encoding.Latin1.GetString(bytes),
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(s)) continue;
            int score = (nameId == 1 ? 2 : 0) + (platform == 3 ? 1 : 0); // GDI busca por la familia «heredada» (registro 1)
            if (score > bestScore) { best = s; bestScore = score; }
        }
        return best;
    }
}
