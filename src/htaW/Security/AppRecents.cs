using System.Text.Json;
using System.Text.Json.Serialization;

namespace HtaW.Security;

/// <summary>Una app abierta con htaw: lo que el panel necesita para mostrarla sin volver a abrirla.</summary>
public sealed class AppRecent
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Icono como <c>data:</c> (png o ico), o null si la app no declara ninguno o es demasiado grande.</summary>
    public string? Icon { get; set; }
    /// <summary>Ultima apertura (UTC).</summary>
    public DateTime Date { get; set; }
}

/// <summary>
/// Apps abiertas con htaw, de la mas reciente a la mas antigua, en <c>recent.json</c> (las 10 ultimas). Las anota
/// <c>htaw</c> al abrir bien una app (tambien con doble clic o desde la linea de comandos), nunca en modo desarrollo
/// (tiene su propia lista, <see cref="DevRecents"/>) ni en un ejecutable portable (no pasa por el panel).
/// </summary>
public sealed class AppRecents(string file)
{
    public const int Max = 10;
    /// <summary>Un icono mayor que esto no se guarda en la lista (el panel muestra uno generico).</summary>
    public const int MaxIconBytes = 64 * 1024;

    public static string DefaultFile => System.IO.Path.Combine(HtawPaths.Root, "recent.json");

    public List<AppRecent> Load()
    {
        try
        {
            if (File.Exists(file)) return JsonSerializer.Deserialize(File.ReadAllText(file), AppRecentsJson.Default.ListAppRecent) ?? [];
        }
        catch (JsonException) { /* archivo danado: se empieza vacio */ }
        catch (IOException) { }
        return [];
    }

    /// <summary>Pone la app la primera (sin duplicados por ruta) y deja como mucho <see cref="Max"/>.</summary>
    public void Add(string path, string name, byte[]? icon, DateTime? now = null)
    {
        path = System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var list = Load();
        list.RemoveAll(r => Same(r.Path, path));
        list.Insert(0, new AppRecent { Path = path, Name = name, Icon = IconUri(icon), Date = now ?? DateTime.UtcNow });
        if (list.Count > Max) list.RemoveRange(Max, list.Count - Max);
        Save(list);
    }

    public bool Remove(string path)
    {
        var list = Load();
        if (list.RemoveAll(r => Same(r.Path, path)) == 0) return false;
        Save(list);
        return true;
    }

    public bool Contains(string path) => Load().Any(r => Same(r.Path, path));

    internal static string? IconUri(byte[]? icon)
    {
        if (icon is not { Length: > 0 } || icon.Length > MaxIconBytes) return null;
        var png = icon.Length > 4 && icon[0] == 0x89 && icon[1] == 'P' && icon[2] == 'N' && icon[3] == 'G';
        return (png ? "data:image/png;base64," : "data:image/x-icon;base64,") + Convert.ToBase64String(icon);
    }

    private static bool Same(string a, string b) =>
        string.Equals(a, b, OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    private void Save(List<AppRecent> list)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(list, AppRecentsJson.Default.ListAppRecent));
            File.Move(tmp, file, true);
        }
        catch (IOException) { /* la lista de recientes no debe impedir abrir la app */ }
        catch (UnauthorizedAccessException) { }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<AppRecent>))]
internal partial class AppRecentsJson : JsonSerializerContext;
