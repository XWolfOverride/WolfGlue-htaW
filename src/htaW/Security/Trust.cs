using System.Text.Json;
using System.Text.Json.Serialization;

namespace HtaW.Security;

/// <summary>De donde viene un archivo.</summary>
public enum FileOrigin { Local, External }

/// <summary>Deteccion de archivos descargados (DOC/HTAW-SEGURIDAD.md).</summary>
public static class OriginDetector
{
    public static FileOrigin Detect(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return FromZoneIdentifier(path);
            // macOS (quarantine) y Linux se implementaran con sus backends.
        }
        catch { /* sin acceso al marcador: se trata como local */ }
        return FileOrigin.Local;
    }

    /// <summary>
    /// Mark of the Web: flujo alternativo <c>Zone.Identifier</c> con <c>ZoneId</c> &gt;= 3 (Internet o no confiable).
    /// </summary>
    public static FileOrigin FromZoneIdentifier(string path)
    {
        var ads = path + ":Zone.Identifier";
        if (!File.Exists(ads)) return FileOrigin.Local;
        return ParseZoneId(File.ReadAllText(ads)) >= 3 ? FileOrigin.External : FileOrigin.Local;
    }

    public static int ParseZoneId(string content)
    {
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("ZoneId=", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.AsSpan(7), out var zone)) return zone;
        }
        return 0;
    }
}

/// <summary>Decisiones de confianza guardadas en <c>trust.json</c>.</summary>
public sealed class TrustStore
{
    public sealed class Data
    {
        /// <summary>Hash SHA-256 del archivo -> permisos aceptados.</summary>
        public Dictionary<string, Entry> Files { get; set; } = [];
        /// <summary>Id de la app -> permisos aceptados (para detectar versiones que piden mas).</summary>
        public Dictionary<string, string[]> Apps { get; set; } = [];
    }

    public sealed class Entry
    {
        public string Id { get; set; } = "";
        public string Path { get; set; } = "";
        public string[] Permissions { get; set; } = [];
        public DateTime Accepted { get; set; }
    }

    private readonly string _file;
    private Data _data;

    public TrustStore(string file)
    {
        _file = file;
        _data = Load(file);
    }

    public static string DefaultFile => System.IO.Path.Combine(HtawPaths.Root, "trust.json");

    private static Data Load(string file)
    {
        try
        {
            if (File.Exists(file)) return JsonSerializer.Deserialize(File.ReadAllText(file), TrustJson.Default.Data) ?? new();
        }
        catch (JsonException) { /* archivo danado: se empieza de cero (se volvera a preguntar) */ }
        return new();
    }

    private void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_file)!);
        var tmp = _file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_data, TrustJson.Default.Data));
        File.Move(tmp, _file, true);
    }

    /// <summary>
    /// Hay que preguntar si el archivo viene de fuera y no se acepto este contenido exacto, o si la app (por id) ya
    /// se habia aceptado y ahora pide permisos que no estaban entre los aceptados.
    /// </summary>
    public bool NeedsPrompt(string hash, string id, IReadOnlySet<string> permissions, FileOrigin origin)
    {
        if (_data.Apps.TryGetValue(id, out var accepted) && !permissions.IsSubsetOf(accepted)) return true;
        if (origin == FileOrigin.Local) return false;
        return !(_data.Files.TryGetValue(hash, out var e) && permissions.IsSubsetOf(e.Permissions));
    }

    public void Accept(string hash, string id, string path, IReadOnlySet<string> permissions)
    {
        _data.Files[hash] = new Entry { Id = id, Path = path, Permissions = [.. permissions], Accepted = DateTime.UtcNow };
        var prev = _data.Apps.TryGetValue(id, out var p) ? p : [];
        _data.Apps[id] = [.. prev.Union(permissions).Order(StringComparer.Ordinal)];
        Save();
    }

    /// <summary>Registra los permisos de una app local abierta sin preguntar, para detectar ampliaciones futuras.</summary>
    public void RememberApp(string id, IReadOnlySet<string> permissions)
    {
        if (_data.Apps.TryGetValue(id, out var prev) && permissions.IsSubsetOf(prev)) return;
        _data.Apps[id] = [.. (prev ?? []).Union(permissions).Order(StringComparer.Ordinal)];
        Save();
    }

    /// <summary>Olvida por hash o por id. Devuelve cuantas entradas se borraron.</summary>
    public int Forget(string? hash, string? id)
    {
        int n = 0;
        if (hash is not null && _data.Files.Remove(hash)) n++;
        if (id is not null)
        {
            if (_data.Apps.Remove(id)) n++;
            foreach (var k in _data.Files.Where(kv => kv.Value.Id == id).Select(kv => kv.Key).ToList())
            {
                _data.Files.Remove(k);
                n++;
            }
        }
        if (n > 0) Save();
        return n;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(TrustStore.Data))]
internal partial class TrustJson : JsonSerializerContext;

/// <summary>Carpetas de datos de htaW.</summary>
public static class HtawPaths
{
    /// <summary>Raiz: <c>%LOCALAPPDATA%\htaW</c> (o <c>~/.local/share/htaW</c>). Forzable con HTAW_DATA.</summary>
    public static string Root =>
        Environment.GetEnvironmentVariable("HTAW_DATA") is { Length: > 0 } d ? d
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "htaW");

    public static string App(string id) => Path.Combine(Root, "apps", id);
}
