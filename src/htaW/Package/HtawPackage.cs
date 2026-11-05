using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using HtaW.Security;
using WolfGlue.Content;

namespace HtaW.Package;

public enum PackageKind
{
    /// <summary>HTML suelto con el manifiesto en el &lt;head&gt;.</summary>
    Html,
    /// <summary>Zip renombrado con htaw.json.</summary>
    Zip,
    /// <summary>Carpeta sin empaquetar (solo <c>--dev</c>).</summary>
    Folder,
}

/// <summary>Una aplicacion .htaw abierta: manifiesto, permisos y contenido.</summary>
public sealed class HtawPackage : IDisposable
{
    public const string ManifestFile = "htaw.json";

    private ZipArchive? _zip;

    private HtawPackage(string path, PackageKind kind, HtawManifest manifest, string entry, IContentProvider content,
        List<string> warnings, ZipArchive? zip)
    {
        SourcePath = path;
        Kind = kind;
        Manifest = manifest;
        Entry = entry;
        Content = content;
        Warnings = warnings;
        _zip = zip;
        Id = manifest.Id ?? DerivedId(path);
        Permissions = PermissionSet.FromManifest(manifest.Permissions);
    }

    /// <summary>Ruta completa del archivo o carpeta.</summary>
    public string SourcePath { get; }
    public PackageKind Kind { get; }
    public HtawManifest Manifest { get; }
    public string Id { get; }
    public PermissionSet Permissions { get; }
    /// <summary>Pagina inicial, relativa al origen.</summary>
    public string Entry { get; }
    public IContentProvider Content { get; }
    /// <summary>Avisos no fatales del manifiesto (se muestran en la consola de DevTools).</summary>
    public List<string> Warnings { get; }
    /// <summary>Carpeta de la app (HTML suelto o carpeta de desarrollo); null en un zip.</summary>
    public string? Directory => Kind == PackageKind.Zip ? null
        : Kind == PackageKind.Folder ? SourcePath : Path.GetDirectoryName(SourcePath);

    /// <summary>Host del origen propio: <c>https://{id}.htaw.local/</c>.</summary>
    public string Host => Id + ".htaw.local";

    public string DisplayName => Manifest.Name ?? Path.GetFileNameWithoutExtension(SourcePath);

    /// <summary>Id para apps sin id: hash de la ruta, para que dos archivos distintos no compartan datos.</summary>
    internal static string DerivedId(string path)
    {
        var full = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows()) full = full.ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(full));
        return "local-" + Convert.ToHexStringLower(hash)[..16];
    }

    /// <summary>Abre un archivo .htaw (HTML o zip) o, con <paramref name="allowFolder"/>, una carpeta con htaw.json.</summary>
    public static HtawPackage Open(string path, bool allowFolder = false)
    {
        path = Path.GetFullPath(path);
        var warnings = new List<string>();
        if (System.IO.Directory.Exists(path))
        {
            if (!allowFolder) throw new HtawFormatException(Strings.FolderNeedsDev);
            var mf = Path.Combine(path, ManifestFile);
            if (!File.Exists(mf)) throw new HtawFormatException(Strings.MissingManifest(ManifestFile));
            var m = ManifestReader.Parse(File.ReadAllText(mf), warnings);
            var entry = RequireEntry(m);
            if (!File.Exists(Path.Combine(path, entry))) throw new HtawFormatException(Strings.MissingEntry(entry));
            return new HtawPackage(path, PackageKind.Folder, m, entry, new FolderContent(path), warnings, null);
        }
        if (!File.Exists(path)) throw new HtawFormatException(Strings.FileNotFound(path));
        return IsZip(path) ? OpenZip(path, warnings) : OpenHtml(path, warnings);
    }

    /// <summary>Un zip empieza por <c>PK\x03\x04</c>; cualquier otra cosa se trata como HTML.</summary>
    public static bool IsZip(string path)
    {
        Span<byte> head = stackalloc byte[4];
        using var f = File.OpenRead(path);
        return f.Read(head) == 4 && head[0] == 'P' && head[1] == 'K' && head[2] == 3 && head[3] == 4;
    }

    private static HtawPackage OpenHtml(string path, List<string> warnings)
    {
        var html = File.ReadAllText(path); // detecta BOM; UTF-8 por defecto
        var json = ManifestReader.ExtractFromHtml(html);
        var m = json is null ? new HtawManifest() : ManifestReader.Parse(json, warnings);
        if (json is null) warnings.Add(Strings.WarnNoManifest);
        if (m.Entry is not null) warnings.Add(Strings.WarnEntryIgnored);
        var name = Path.GetFileName(path);
        return new HtawPackage(path, PackageKind.Html, m, Uri.EscapeDataString(name), new HtmlFileContent(path), warnings, null);
    }

    private static HtawPackage OpenZip(string path, List<string> warnings)
    {
        ZipArchive zip;
        try { zip = ZipFile.OpenRead(path); }
        catch (InvalidDataException e) { throw new HtawFormatException(Strings.BadZip(e.Message)); }
        try
        {
            var content = new ZipContent(zip);
            var mfEntry = zip.GetEntry(ManifestFile) ?? throw new HtawFormatException(Strings.MissingManifest(ManifestFile));
            string json;
            using (var r = new StreamReader(mfEntry.Open())) json = r.ReadToEnd();
            var m = ManifestReader.Parse(json, warnings);
            var entry = RequireEntry(m);
            if (!content.Contains(entry)) throw new HtawFormatException(Strings.MissingEntry(entry));
            return new HtawPackage(path, PackageKind.Zip, m, entry, content, warnings, zip);
        }
        catch
        {
            zip.Dispose();
            throw;
        }
    }

    private static string RequireEntry(HtawManifest m) =>
        Package.Content.ContentPathCheck(m.Entry ?? "") ?? throw new HtawFormatException(Strings.ManifestBadValue("entry", m.Entry ?? "(null)"));

    /// <summary>Contenido completo del archivo (para calcular el hash de confianza).</summary>
    public byte[] ComputeHash()
    {
        if (Kind == PackageKind.Folder) return SHA256.HashData(Encoding.UTF8.GetBytes(SourcePath));
        using var f = File.OpenRead(SourcePath);
        return SHA256.HashData(f);
    }

    public void Dispose()
    {
        _zip?.Dispose();
        _zip = null;
    }
}

/// <summary>HTML suelto: el propio archivo es la pagina inicial; el resto se sirve desde su carpeta.</summary>
internal sealed class HtmlFileContent(string file) : IContentProvider
{
    private readonly FolderContent _folder = new(Path.GetDirectoryName(file)!);
    private readonly string _name = Path.GetFileName(file);

    public async ValueTask<GlueResponse?> GetAsync(GlueRequest request)
    {
        if (request.Path.Length == 0 || request.Path.Equals(_name, StringComparison.OrdinalIgnoreCase))
            return GlueResponse.Bytes(await File.ReadAllBytesAsync(file), MimeTypes.For(".html"));
        return await _folder.GetAsync(request);
    }
}

/// <summary>Sirve las entradas de un zip sin extraerlo. Se usa solo desde el hilo de UI.</summary>
internal sealed class ZipContent : IContentProvider
{
    private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public ZipContent(ZipArchive zip)
    {
        foreach (var e in zip.Entries)
        {
            if (e.FullName.EndsWith('/')) continue; // carpeta
            var n = ContentPath.Normalize(e.FullName);
            if (string.IsNullOrEmpty(n)) continue; // rutas absolutas o con ".." se ignoran
            _entries[n] = e;
        }
    }

    public bool Contains(string path) => ContentPath.Normalize(path) is { } n && _entries.ContainsKey(n);

    public ValueTask<GlueResponse?> GetAsync(GlueRequest request)
    {
        var path = ContentPath.Normalize(request.Path);
        if (string.IsNullOrEmpty(path) || !_entries.TryGetValue(path, out var e)) return new((GlueResponse?)null);
        using var s = e.Open();
        var data = new byte[e.Length];
        s.ReadExactly(data);
        return new(GlueResponse.Bytes(data, MimeTypes.For(path)));
    }
}
