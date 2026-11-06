using System.Text.RegularExpressions;

namespace WolfGlue;

/// <summary>Color opaco RGB.</summary>
public readonly record struct GlueRgb(byte R, byte G, byte B)
{
    /// <summary>Formato <c>#rrggbb</c>.</summary>
    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";
}

/// <summary>
/// Color de fondo con variante para el tema oscuro. Formatos admitidos (subconjunto de CSS):
/// <c>#rgb</c>, <c>#rrggbb</c> y <c>light-dark(#claro, #oscuro)</c>.
/// </summary>
public readonly record struct GlueBackground(GlueRgb Light, GlueRgb Dark)
{
    /// <summary>El que corresponde al tema actual.</summary>
    public GlueRgb For(bool dark) => dark ? Dark : Light;

    /// <summary>Por defecto del sistema cuando nadie dice nada: blanco en claro, gris oscuro en oscuro.</summary>
    public static GlueBackground System { get; } = new(new(0xff, 0xff, 0xff), new(0x20, 0x20, 0x20));

    public static bool TryParse(string? text, out GlueBackground value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        var m = GlueColorParser.LightDark().Match(text);
        if (m.Success)
        {
            if (!GlueColorParser.TryParseHex(m.Groups[1].Value, out var l) || !GlueColorParser.TryParseHex(m.Groups[2].Value, out var d))
                return false;
            value = new(l, d);
            return true;
        }
        if (!GlueColorParser.TryParseHex(text, out var c)) return false;
        value = new(c, c);
        return true;
    }

    public static GlueBackground Parse(string text) =>
        TryParse(text, out var v) ? v : throw new FormatException("Color no valido (use #rgb, #rrggbb o light-dark(a, b)): " + text);

    public override string ToString() => Light == Dark ? Light.ToString() : $"light-dark({Light}, {Dark})";
}

internal static partial class GlueColorParser
{
    [GeneratedRegex(@"^light-dark\(\s*([^,\s]+)\s*,\s*([^)\s]+)\s*\)$", RegexOptions.IgnoreCase)]
    internal static partial Regex LightDark();

    internal static bool TryParseHex(string s, out GlueRgb rgb)
    {
        rgb = default;
        s = s.Trim();
        if (s.Length is not (4 or 7) || s[0] != '#') return false;
        Span<byte> v = stackalloc byte[3];
        for (int i = 0; i < 3; i++)
        {
            int hi, lo;
            if (s.Length == 4) { hi = Hex(s[1 + i]); lo = hi; }
            else { hi = Hex(s[1 + 2 * i]); lo = Hex(s[2 + 2 * i]); }
            if (hi < 0 || lo < 0) return false;
            v[i] = (byte)(hi * 16 + lo);
        }
        rgb = new(v[0], v[1], v[2]);
        return true;
    }

    private static int Hex(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };

    // <meta name="theme-color" content="#xxx" [media="(prefers-color-scheme: dark)"]>
    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaTag();

    [GeneratedRegex(@"\b(name|content|media)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex Attr();

    [GeneratedRegex(@"</head\s*>|<body\b", RegexOptions.IgnoreCase)]
    private static partial Regex HeadEnd();

    /// <summary>
    /// Lee <c>&lt;meta name="theme-color"&gt;</c> del <c>&lt;head&gt;</c>. Admite una sola etiqueta o una por tema con
    /// <c>media="(prefers-color-scheme: light|dark)"</c>; si solo hay de un tema, se usa para ambos.
    /// </summary>
    internal static GlueBackground? ThemeColorFromHtml(string html)
    {
        var end = HeadEnd().Match(html);
        var head = end.Success ? html.AsSpan(0, end.Index).ToString() : html;
        GlueRgb? any = null, light = null, dark = null;
        foreach (Match tag in MetaTag().Matches(head))
        {
            string? name = null, content = null, media = null;
            foreach (Match a in Attr().Matches(tag.Value))
            {
                var val = a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Value;
                switch (a.Groups[1].Value.ToLowerInvariant())
                {
                    case "name": name = val; break;
                    case "content": content = val; break;
                    case "media": media = val; break;
                }
            }
            if (!string.Equals(name, "theme-color", StringComparison.OrdinalIgnoreCase) || content is null) continue;
            if (!TryParseHex(content, out var c)) continue;
            var mq = media?.Replace(" ", "").ToLowerInvariant();
            if (mq is null or "") any ??= c;
            else if (mq.Contains("prefers-color-scheme:dark")) dark ??= c;
            else if (mq.Contains("prefers-color-scheme:light")) light ??= c;
        }
        var l = light ?? any ?? dark;
        var d = dark ?? any ?? light;
        return l is { } lv && d is { } dv ? new GlueBackground(lv, dv) : null;
    }
}
