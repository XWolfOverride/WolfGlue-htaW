using System.Reflection;

namespace WolfGlue.Content;

/// <summary>
/// Sirve recursos incrustados en un ensamblado. Los recursos deben tener un nombre logico con forma de ruta,
/// por ejemplo en el .csproj:
/// <code>&lt;EmbeddedResource Include="web\**" LogicalName="web/%(RecursiveDir)%(Filename)%(Extension)" /&gt;</code>
/// </summary>
public sealed class EmbeddedContent : IContentProvider
{
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Assembly _assembly;

    /// <param name="assembly">Ensamblado con los recursos.</param>
    /// <param name="prefix">Prefijo del nombre logico que corresponde a la raiz del origen (<c>web/</c>).</param>
    public EmbeddedContent(Assembly assembly, string prefix = "web/")
    {
        _assembly = assembly;
        prefix = prefix.Replace('\\', '/');
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var n = name.Replace('\\', '/');
            if (!n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rel = ContentPath.Normalize(n[prefix.Length..]);
            if (rel is { Length: > 0 }) _names[rel] = name;
        }
    }

    /// <summary>Rutas disponibles (para pruebas y diagnostico).</summary>
    public IEnumerable<string> Paths => _names.Keys;

    public ValueTask<GlueResponse?> GetAsync(GlueRequest request)
    {
        var path = ContentPath.Normalize(request.Path);
        if (path is null) return new((GlueResponse?)null);
        if (path.Length == 0 || path.EndsWith('/')) path += "index.html";
        if (!_names.TryGetValue(path, out var name)) return new((GlueResponse?)null);
        using var s = _assembly.GetManifestResourceStream(name);
        if (s is null) return new((GlueResponse?)null);
        var data = new byte[s.Length];
        s.ReadExactly(data);
        return new(GlueResponse.Bytes(data, MimeTypes.For(path)));
    }
}

/// <summary>Sirve archivos de una carpeta del disco, sin permitir salir de ella.</summary>
public sealed class FolderContent(string root) : IContentProvider
{
    private readonly string _root = Path.GetFullPath(root);

    public async ValueTask<GlueResponse?> GetAsync(GlueRequest request)
    {
        var path = ContentPath.Normalize(request.Path);
        if (path is null) return null;
        if (path.Length == 0 || path.EndsWith('/')) path += "index.html";
        var full = Path.GetFullPath(Path.Combine(_root, path));
        var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return null;
        if (!File.Exists(full)) return null;
        var data = await File.ReadAllBytesAsync(full).ConfigureAwait(false);
        return GlueResponse.Bytes(data, MimeTypes.For(full));
    }
}

/// <summary>Prueba varios proveedores en orden y devuelve la primera respuesta.</summary>
public sealed class CompositeContent(params IContentProvider[] providers) : IContentProvider
{
    public async ValueTask<GlueResponse?> GetAsync(GlueRequest request)
    {
        foreach (var p in providers)
        {
            var r = await p.GetAsync(request);
            if (r is not null) return r;
        }
        return null;
    }
}
