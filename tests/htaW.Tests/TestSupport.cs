using System.Text.Json;

namespace HtaW.Tests;

/// <summary>Carpeta temporal que se borra al terminar.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("htaw-test-").FullName;

    public string File(string relative, string content)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { /* algun archivo abierto: da igual en pruebas */ }
    }
}

internal static class Json
{
    public static JsonElement[] Array(string json) => JsonDocument.Parse(json).RootElement.EnumerateArray().Select(e => e.Clone()).ToArray();
}
