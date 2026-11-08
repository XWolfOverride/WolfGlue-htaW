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
    /// <summary>Autor (ventana «Acerca de» y <c>htaw.app.author</c>).</summary>
    public string? Author { get; set; }
    /// <summary>
    /// Pagina propia de «Acerca de»: una ruta (<c>"acerca.html"</c>) o <c>{ "page", "width", "height" }</c>. Sin ella, una ventana
    /// generica con nombre, version, autor e icono.
    /// </summary>
    public JsonElement? About { get; set; }
    /// <summary><c>main</c> (por defecto): al cerrar la ventana principal se cierra la app y el resto de ventanas; <c>last</c>: la app vive mientras haya alguna ventana.</summary>
    public string Lifecycle { get; set; } = "main";
    public string? Entry { get; set; }
    public string? Icon { get; set; }
    public string? Lang { get; set; }
    public HtawWindow Window { get; set; } = new();
    public bool SingleInstance { get; set; }
    /// <summary>Bandeja del sistema (permiso <c>tray</c>).</summary>
    public HtawTray Tray { get; set; } = new();
    /// <summary>native | none</summary>
    public string Theme { get; set; } = "native";
    /// <summary>Cadenas (<c>"fs"</c>) u objetos (<c>{"shell.exec": [...]}</c>).</summary>
    public JsonElement[] Permissions { get; set; } = [];

    /// <summary>Campos del JSON que no se reconocen (se avisan, no son error).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; set; }
}

public sealed class HtawTray
{
    /// <summary>
    /// Cerrar la ventana principal la oculta (la app sigue viva) mientras haya un icono en la bandeja. Sin icono, se cierra
    /// como siempre: una app oculta y sin icono no se podria volver a ver.
    /// </summary>
    public bool KeepRunning { get; set; }

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
    /// <summary>Color mientras carga: <c>#rgb</c>, <c>#rrggbb</c> o <c>light-dark(#claro, #oscuro)</c>.</summary>
    public string? Background { get; set; }
    /// <summary>Duracion del fundido de entrada en ms (0 = sin fundido; por defecto 150).</summary>
    public int FadeIn { get; set; } = 150;

    /// <summary>Fondo del sistema: <c>solid</c> (por defecto), <c>tinted</c> (Mica) o <c>blurred</c> (Acrylic).</summary>
    public string? Backdrop { get; set; }

    /// <summary>Barra de titulo propia (mismo esquema que <see cref="WolfGlue.GlueTitleBar"/>, ver el manual).</summary>
    public JsonElement? Titlebar { get; set; }

    /// <summary><see cref="Titlebar"/> ya leida y validada.</summary>
    [JsonIgnore]
    public WolfGlue.GlueTitleBar? ParsedTitleBar { get; set; }

    /// <summary>Menu de ventana (Archivo, Editar...): lista de menus con sus opciones (ver el manual).</summary>
    public JsonElement? Menu { get; set; }

    /// <summary><see cref="Menu"/> ya leido y validado.</summary>
    [JsonIgnore]
    public List<WolfGlue.Native.GlueMenuItem>? ParsedMenu { get; set; }

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
        if (w.Background is not null && !WolfGlue.GlueBackground.TryParse(w.Background, out _))
            throw new HtawFormatException(Strings.ManifestBadValue("window.background", w.Background));
        if (w.FadeIn is < 0 or > 2000) throw new HtawFormatException(Strings.ManifestBadValue("window.fadeIn", w.FadeIn.ToString()));
        if (w.Backdrop is not null && !WolfGlue.GlueBackdropExtensions.TryParse(w.Backdrop, out _))
            throw new HtawFormatException(Strings.ManifestBadValue("window.backdrop", w.Backdrop));
        if (w.Titlebar is { ValueKind: not JsonValueKind.Null } tb)
        {
            try { w.ParsedTitleBar = WolfGlue.GlueTitleBar.Parse(tb.GetRawText()); }
            catch (FormatException e) { throw new HtawFormatException(Strings.ManifestBadValue("window.titlebar", e.Message)); }
        }
        if (w.Menu is { ValueKind: not JsonValueKind.Null } mn)
        {
            try { w.ParsedMenu = WolfGlue.Native.GlueMenuItem.ParseBar(mn.GetRawText()); }
            catch (FormatException e) { throw new HtawFormatException(Strings.ManifestBadValue("window.menu", e.Message)); }
        }
        if (m.Icon is { Length: > 0 } icon)
        {
            if (IconSource.TryDataUrl(icon, out var data) ? data is null : Content.ContentPathCheck(icon) is null)
                throw new HtawFormatException(Strings.ManifestBadValue("icon", icon.Length > 60 ? icon[..60] + "…" : icon));
        }
        if (m.Lifecycle is not ("main" or "last")) throw new HtawFormatException(Strings.ManifestBadValue("lifecycle", m.Lifecycle));
        if (m.About is { ValueKind: not JsonValueKind.Null } ab)
        {
            var (page, _, _) = Content.ReadAbout(ab);
            if (page is null || Content.ContentPathCheck(page) is null)
                throw new HtawFormatException(Strings.ManifestBadValue("about", ab.ToString()));
        }
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
    /// <summary>Lee <c>about</c>: pagina, ancho y alto (null si no se indican).</summary>
    internal static (string? Page, int? Width, int? Height) ReadAbout(JsonElement ab)
    {
        if (ab.ValueKind == JsonValueKind.String) return (ab.GetString(), null, null);
        if (ab.ValueKind != JsonValueKind.Object) return (null, null, null);
        string? page = ab.TryGetProperty("page", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        int? w = ab.TryGetProperty("width", out var wi) && wi.TryGetInt32(out var wv) ? wv : null;
        int? h = ab.TryGetProperty("height", out var he) && he.TryGetInt32(out var hv) ? hv : null;
        return (page, w, h);
    }

    /// <summary>Ruta relativa valida (sin salir de la raiz) o null.</summary>
    internal static string? ContentPathCheck(string path)
    {
        var n = WolfGlue.Content.ContentPath.Normalize(path);
        return string.IsNullOrEmpty(n) ? null : n;
    }
}
