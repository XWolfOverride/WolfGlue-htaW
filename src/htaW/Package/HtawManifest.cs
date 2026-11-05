using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HtaW.Package;

/// <summary>Manifiesto de una aplicacion .htaw (DOC/HTAW-FORMATO.md).</summary>
public sealed class HtawManifest
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Entry { get; set; }
    public string? Icon { get; set; }
    public string? Lang { get; set; }
    public HtawWindow Window { get; set; } = new();
    public bool SingleInstance { get; set; }
    /// <summary>native | none</summary>
    public string Theme { get; set; } = "native";
    /// <summary>Cadenas (<c>"fs"</c>) u objetos (<c>{"shell.exec": [...]}</c>).</summary>
    public JsonElement[] Permissions { get; set; } = [];

    /// <summary>Campos del JSON que no se reconocen (se avisan, no son error).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; set; }
}

public sealed class HtawWindow
{
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public int MinWidth { get; set; }
    public int MinHeight { get; set; }
    public int? MaxWidth { get; set; }
    public int? MaxHeight { get; set; }
    public bool Resizable { get; set; } = true;
    public bool Center { get; set; } = true;
    /// <summary>native | none (none aun no esta soportado: se usa native)</summary>
    public string Frame { get; set; } = "native";
    public bool Maximized { get; set; }
    public bool AlwaysOnTop { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(HtawManifest))]
internal partial class ManifestJson : JsonSerializerContext;

/// <summary>Error de formato o de manifiesto: se muestra al usuario y la app no se abre.</summary>
public sealed class HtawFormatException(string message) : Exception(message);

public static partial class ManifestReader
{
    /// <summary>Patron valido de id: minusculas, digitos, punto y guion; empieza y acaba en letra o digito.</summary>
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9.-]{0,98}[a-z0-9])?$")]
    private static partial Regex IdRegex();

    [GeneratedRegex("""<script\b[^>]*\btype\s*=\s*["']application/htaw\+json["'][^>]*>(?<json>[\s\S]*?)</script\s*>""",
        RegexOptions.IgnoreCase)]
    private static partial Regex ScriptBlock();

    [GeneratedRegex("</head\\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadEnd();

    public static bool IsValidId(string id) => IdRegex().IsMatch(id) && !id.Contains("..");

    /// <summary>Lee y valida un manifiesto JSON. <paramref name="warnings"/> recibe los avisos no fatales.</summary>
    public static HtawManifest Parse(string json, List<string> warnings)
    {
        HtawManifest? m;
        try { m = JsonSerializer.Deserialize(json, ManifestJson.Default.HtawManifest); }
        catch (JsonException e) { throw new HtawFormatException(Strings.ManifestInvalidJson(e.Message)); }
        if (m is null) throw new HtawFormatException(Strings.ManifestInvalidJson("null"));
        Validate(m, warnings);
        return m;
    }

    /// <summary>
    /// Extrae el bloque <c>&lt;script type="application/htaw+json"&gt;</c> del <c>&lt;head&gt;</c> de un HTML.
    /// Devuelve null si no hay.
    /// </summary>
    public static string? ExtractFromHtml(string html)
    {
        var end = HeadEnd().Match(html);
        var head = end.Success ? html[..end.Index] : html;
        var m = ScriptBlock().Match(head);
        return m.Success ? m.Groups["json"].Value : null;
    }

    private static void Validate(HtawManifest m, List<string> warnings)
    {
        if (m.Id is not null && !IsValidId(m.Id)) throw new HtawFormatException(Strings.ManifestBadId(m.Id));
        if (m.Theme is not ("native" or "none")) throw new HtawFormatException(Strings.ManifestBadValue("theme", m.Theme));
        var w = m.Window;
        if (w.Width < 100 || w.Height < 80 || w.Width > 10000 || w.Height > 10000)
            throw new HtawFormatException(Strings.ManifestBadValue("window.width/height", $"{w.Width}x{w.Height}"));
        if (w.MinWidth < 0 || w.MinHeight < 0) throw new HtawFormatException(Strings.ManifestBadValue("window.min*", "<0"));
        if (w.Frame is not ("native" or "none")) throw new HtawFormatException(Strings.ManifestBadValue("window.frame", w.Frame));
        if (w.Frame == "none") warnings.Add(Strings.WarnFrameNone);
        if (m.Entry is not null && Content.ContentPathCheck(m.Entry) is null)
            throw new HtawFormatException(Strings.ManifestBadValue("entry", m.Entry));
        foreach (var k in m.Unknown?.Keys ?? Enumerable.Empty<string>()) warnings.Add(Strings.WarnUnknownField(k));
        foreach (var k in w.Unknown?.Keys ?? Enumerable.Empty<string>()) warnings.Add(Strings.WarnUnknownField("window." + k));
        // Los permisos se validan al construir el PermissionSet.
        _ = Security.PermissionSet.FromManifest(m.Permissions);
    }
}

internal static class Content
{
    /// <summary>Ruta relativa valida (sin salir de la raiz) o null.</summary>
    internal static string? ContentPathCheck(string path)
    {
        var n = WolfGlue.Content.ContentPath.Normalize(path);
        return string.IsNullOrEmpty(n) ? null : n;
    }
}
