using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using HtaW.Security;
using WolfGlue;
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

    /// <summary>Tamano maximo del icono de la app.</summary>
    public const int MaxIconBytes = 1024 * 1024;

    private ZipArchive? _zip;
    private readonly EmbeddedApp? _embedded;

    private HtawPackage(string path, PackageKind kind, HtawManifest manifest, string entry, IContentProvider content,
        List<string> warnings, ZipArchive? zip, EmbeddedApp? embedded = null)
    {
        SourcePath = path;
        Kind = kind;
        Manifest = manifest;
        Entry = entry;
        Content = content;
        Warnings = warnings;
        _zip = zip;
        _embedded = embedded;
        Id = manifest.Id ?? DerivedId(path);
        Permissions = PermissionSet.FromManifest(manifest.Permissions);
        LoadIcon();
    }

    /// <summary>Ruta completa del archivo o carpeta (en una app incrustada, la del ejecutable).</summary>
    public string SourcePath { get; }
    public PackageKind Kind { get; }
    /// <summary>La app va dentro del propio ejecutable de htaW (ejecutable portable).</summary>
    public bool Embedded => _embedded is not null;
    public HtawManifest Manifest { get; }
    public string Id { get; }
    public PermissionSet Permissions { get; }
    /// <summary>Pagina inicial, relativa al origen.</summary>
    public string Entry { get; }
    public IContentProvider Content { get; }
    /// <summary>Avisos no fatales del manifiesto (se muestran en la consola de DevTools).</summary>
    public List<string> Warnings { get; }
    /// <summary>Icono de la app (bytes de un .png o .ico), o null si no tiene o no se pudo leer.</summary>
    public byte[]? Icon { get; private set; }
    /// <summary>Por que no se pudo usar el <c>icon</c> declarado (null si no hay problema).</summary>
    public string? IconProblem { get; private set; }
    /// <summary>Carpeta de la app (HTML suelto, carpeta de desarrollo, o la del ejecutable); null en un zip.</summary>
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
        if (IsZip(path)) return OpenZip(path, File.OpenRead(path), warnings, null);
        var html = File.ReadAllText(path); // detecta BOM; UTF-8 por defecto
        var name = Path.GetFileName(path);
        return OpenHtml(path, html, name, new HtmlFileContent(path), warnings, null);
    }

    /// <summary>Abre la app incrustada al final de un ejecutable (ver <see cref="EmbeddedApp"/>).</summary>
    public static HtawPackage OpenEmbedded(string exePath, EmbeddedApp app)
    {
        exePath = Path.GetFullPath(exePath);
        var warnings = new List<string>();
        var file = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        try
        {
            var slice = new SubStream(file, app.Offset, app.Length);
            if (app.Kind == EmbeddedKind.Zip) return OpenZip(exePath, slice, warnings, app);
            byte[] bytes = new byte[app.Length];
            slice.ReadExactly(bytes);
            file.Dispose();
            var html = DecodeText(bytes);
            // La pagina incrustada se sirve como index.html; el resto, desde la carpeta del ejecutable.
            var content = new HtmlBytesContent(bytes, "index.html", Path.GetDirectoryName(exePath)!);
            return OpenHtml(exePath, html, "index.html", content, warnings, app);
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    /// <summary>Un zip empieza por <c>PK\x03\x04</c>; cualquier otra cosa se trata como HTML.</summary>
    public static bool IsZip(string path)
    {
        using var f = File.OpenRead(path);
        return IsZip(f);
    }

    internal static bool IsZip(Stream s)
    {
        Span<byte> head = stackalloc byte[4];
        return s.Read(head) == 4 && head[0] == 'P' && head[1] == 'K' && head[2] == 3 && head[3] == 4;
    }

    /// <summary>Texto con BOM UTF-8/UTF-16 o, sin BOM, UTF-8 (igual que File.ReadAllText).</summary>
    private static string DecodeText(byte[] bytes)
    {
        using var r = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return r.ReadToEnd();
    }

    private static HtawPackage OpenHtml(string path, string html, string entryName, IContentProvider content,
        List<string> warnings, EmbeddedApp? embedded)
    {
        var json = ManifestReader.ExtractFromHtml(html);
        var m = json is null ? new HtawManifest() : ManifestReader.Parse(json, warnings);
        if (json is null) warnings.Add(Strings.WarnNoManifest);
        if (m.Entry is not null) warnings.Add(Strings.WarnEntryIgnored);
        return new HtawPackage(path, PackageKind.Html, m, Uri.EscapeDataString(entryName), content, warnings, null, embedded);
    }

    private static HtawPackage OpenZip(string path, Stream stream, List<string> warnings, EmbeddedApp? embedded)
    {
        ZipArchive zip;
        try { zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false); }
        catch (InvalidDataException e)
        {
            stream.Dispose();
            throw new HtawFormatException(Strings.BadZip(e.Message));
        }
        try
        {
            var content = new ZipContent(zip);
            var mfEntry = zip.GetEntry(ManifestFile) ?? throw new HtawFormatException(Strings.MissingManifest(ManifestFile));
            string json;
            using (var r = new StreamReader(mfEntry.Open())) json = r.ReadToEnd();
            var m = ManifestReader.Parse(json, warnings);
            var entry = RequireEntry(m);
            if (!content.Contains(entry)) throw new HtawFormatException(Strings.MissingEntry(entry));
            return new HtawPackage(path, PackageKind.Zip, m, entry, content, warnings, zip, embedded);
        }
        catch
        {
            zip.Dispose();
            throw;
        }
    }

    private static string RequireEntry(HtawManifest m) =>
        Package.Content.ContentPathCheck(m.Entry ?? "") ?? throw new HtawFormatException(Strings.ManifestBadValue("entry", m.Entry ?? "(null)"));

    // ------------------------------------------------------------- icono
    /// <summary>
    /// Lee el <c>icon</c> del manifiesto: <c>data:</c> o ruta relativa (dentro del zip, o junto al HTML / en la carpeta).
    /// Si no se puede usar, no es un error al abrir: se anota el motivo y queda el icono de htaW.
    /// </summary>
    private void LoadIcon()
    {
        var spec = Manifest.Icon;
        if (string.IsNullOrEmpty(spec)) return;
        byte[]? data;
        try
        {
            data = IconSource.TryDataUrl(spec, out var inline) ? inline : ReadRelative(spec);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { data = null; }
        if (data is null) IconProblem = Strings.IconNotFound(spec);
        else if (data.Length > MaxIconBytes) IconProblem = Strings.IconTooBig(spec, MaxIconBytes / 1024);
        else if (GlueIcon.Detect(data) == GlueIconFormat.Unknown) IconProblem = Strings.IconBadFormat(spec);
        else if (GlueIcon.Detect(data) == GlueIconFormat.Ico && !IconSource.IcoIsValid(data)) IconProblem = Strings.IconBadFormat(spec);
        else Icon = data;
        if (IconProblem is not null) Warnings.Add(IconProblem + Strings.IconFallback);
    }

    /// <summary>Bytes de un archivo de la app por ruta relativa, o null si no existe.</summary>
    private byte[]? ReadRelative(string relative)
    {
        var rel = Package.Content.ContentPathCheck(relative);
        if (rel is null) return null;
        if (_zip is not null)
        {
            var e = _zip.Entries.FirstOrDefault(x => string.Equals(ContentPath.Normalize(x.FullName), rel, StringComparison.OrdinalIgnoreCase));
            if (e is null || e.Length > MaxIconBytes + 1) return e is null ? null : new byte[MaxIconBytes + 1];
            using var s = e.Open();
            var buf = new byte[e.Length];
            s.ReadExactly(buf);
            return buf;
        }
        var dir = Directory;
        if (dir is null) return null;
        var full = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full)) return null;
        if (new FileInfo(full).Length > MaxIconBytes) return new byte[MaxIconBytes + 1];
        return File.ReadAllBytes(full);
    }

    /// <summary>Contenido completo del archivo (para calcular el hash de confianza); en un ejecutable, solo la app.</summary>
    public byte[] ComputeHash()
    {
        if (Kind == PackageKind.Folder) return SHA256.HashData(Encoding.UTF8.GetBytes(SourcePath));
        using var f = File.OpenRead(SourcePath);
        if (_embedded is { } e) return SHA256.HashData(new SubStream(f, e.Offset, e.Length));
        return SHA256.HashData(f);
    }

    public void Dispose()
    {
        _zip?.Dispose();
        _zip = null;
    }
}

/// <summary>Formas de indicar el icono en el manifiesto.</summary>
internal static class IconSource
{
    /// <summary><c>data:[tipo][;base64],datos</c>. Devuelve false si no es una URL data (y entonces es una ruta).</summary>
    public static bool TryDataUrl(string spec, out byte[]? data)
    {
        data = null;
        if (!spec.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
        var comma = spec.IndexOf(',');
        if (comma < 0) return true;
        var meta = spec[5..comma];
        if (!meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return true; // solo base64 (binario)
        try { data = Convert.FromBase64String(spec[(comma + 1)..].Trim()); }
        catch (FormatException) { data = null; }
        return true;
    }

    public static bool IcoIsValid(byte[] ico)
    {
        try { return GlueIcon.IcoImages(ico).Length > 0; }
        catch (FormatException) { return false; }
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

/// <summary>HTML incrustado en memoria como <paramref name="name"/>; el resto desde <paramref name="folder"/>.</summary>
internal sealed class HtmlBytesContent(byte[] html, string name, string folder) : IContentProvider
{
    private readonly FolderContent _folder = new(folder);

    public async ValueTask<GlueResponse?> GetAsync(GlueRequest request)
    {
        if (request.Path.Length == 0 || request.Path.Equals(name, StringComparison.OrdinalIgnoreCase))
            return GlueResponse.Bytes(html, MimeTypes.For(".html"));
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

/// <summary>Vista de solo lectura de un tramo de otro flujo (la app al final del ejecutable).</summary>
internal sealed class SubStream : Stream
{
    private readonly Stream _inner;
    private readonly long _start, _length;
    private long _pos;

    public SubStream(Stream inner, long start, long length)
    {
        if (start < 0 || length < 0 || start + length > inner.Length) throw new InvalidDataException("Tramo fuera del archivo");
        (_inner, _start, _length) = (inner, start, length);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _pos; set => Seek(value, SeekOrigin.Begin); }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var left = _length - _pos;
        if (left <= 0) return 0;
        if (buffer.Length > left) buffer = buffer[..(int)left];
        _inner.Position = _start + _pos;
        int n = _inner.Read(buffer);
        _pos += n;
        return n;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var p = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => _pos + offset, _ => _length + offset };
        if (p < 0) throw new IOException("Posicion negativa");
        return _pos = p;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}
