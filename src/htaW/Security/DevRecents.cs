using System.Text.Json;
using System.Text.Json.Serialization;

namespace HtaW.Security;

/// <summary>
/// Carpetas (o archivos) abiertos en modo desarrollo, de la mas reciente a la mas antigua, en <c>recent-dev.json</c>.
/// Se anotan al abrir con <c>--dev</c> (desde la linea de comandos, el panel o arrastrando la carpeta sobre htaw).
/// </summary>
public sealed class DevRecents(string file)
{
    public const int Max = 10;

    public static string DefaultFile => Path.Combine(HtawPaths.Root, "recent-dev.json");

    public List<string> Load()
    {
        try
        {
            if (File.Exists(file)) return JsonSerializer.Deserialize(File.ReadAllText(file), RecentsJson.Default.ListString) ?? [];
        }
        catch (JsonException) { /* archivo danado: se empieza vacio */ }
        catch (IOException) { }
        return [];
    }

    /// <summary>Pone <paramref name="path"/> el primero (sin duplicados) y deja como mucho <see cref="Max"/>.</summary>
    public void Add(string path)
    {
        path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var list = Load();
        list.RemoveAll(p => Same(p, path));
        list.Insert(0, path);
        if (list.Count > Max) list.RemoveRange(Max, list.Count - Max);
        Save(list);
    }

    public bool Remove(string path)
    {
        var list = Load();
        if (list.RemoveAll(p => Same(p, path)) == 0) return false;
        Save(list);
        return true;
    }

    public bool Contains(string path) => Load().Any(p => Same(p, path));

    private static bool Same(string a, string b) =>
        string.Equals(a, b, OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    private void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(list, RecentsJson.Default.ListString));
            File.Move(tmp, file, true);
        }
        catch (IOException) { /* la lista de recientes no debe impedir abrir la app */ }
        catch (UnauthorizedAccessException) { }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<string>))]
internal partial class RecentsJson : JsonSerializerContext;
