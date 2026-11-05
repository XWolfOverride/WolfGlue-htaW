namespace WolfGlue.Content;

/// <summary>Peticion al origen propio.</summary>
/// <param name="Method">Metodo HTTP en mayusculas.</param>
/// <param name="Path">Ruta sin la barra inicial, sin consulta y decodificada (<c>img/logo.png</c>).</param>
/// <param name="Query">Consulta sin el <c>?</c> (vacia si no hay).</param>
/// <param name="Body">Cuerpo de la peticion (vacio si no hay).</param>
public sealed record GlueRequest(string Method, string Path, string Query, ReadOnlyMemory<byte> Body)
{
    /// <summary>Separa la ruta y la consulta de una URL ya recortada al origen (<c>/a/b.html?x=1#y</c>).</summary>
    public static (string path, string query) SplitPath(string pathAndQuery)
    {
        var s = pathAndQuery;
        int hash = s.IndexOf('#');
        if (hash >= 0) s = s[..hash];
        string query = "";
        int q = s.IndexOf('?');
        if (q >= 0) { query = s[(q + 1)..]; s = s[..q]; }
        s = Uri.UnescapeDataString(s).TrimStart('/');
        return (s, query);
    }
}

/// <summary>Respuesta del origen propio.</summary>
public sealed class GlueResponse
{
    public int Status { get; init; } = 200;
    public string ContentType { get; init; } = "application/octet-stream";
    public ReadOnlyMemory<byte> Body { get; init; }
    /// <summary>Cabeceras adicionales.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    public static GlueResponse Bytes(ReadOnlyMemory<byte> body, string contentType) =>
        new() { Body = body, ContentType = contentType };

    public static GlueResponse Text(string text, string contentType = "text/plain; charset=utf-8", int status = 200) =>
        new() { Body = System.Text.Encoding.UTF8.GetBytes(text), ContentType = contentType, Status = status };

    public static GlueResponse NotFound() => Text("404 Not Found", status: 404);

    internal static string ReasonPhrase(int status) => status switch
    {
        200 => "OK",
        204 => "No Content",
        400 => "Bad Request",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        500 => "Internal Server Error",
        _ => "Status",
    };
}

/// <summary>Fuente del contenido que se sirve en el origen propio.</summary>
public interface IContentProvider
{
    /// <summary>
    /// Devuelve la respuesta para la peticion, o null si no existe (se respondera 404).
    /// Se llama en el hilo de UI: lo que tarde debe devolver una tarea no completada.
    /// </summary>
    ValueTask<GlueResponse?> GetAsync(GlueRequest request);
}

/// <summary>Utilidades comunes para proveedores de archivos.</summary>
public static class ContentPath
{
    /// <summary>
    /// Normaliza una ruta relativa: separadores <c>/</c>, sin segmentos vacios ni <c>.</c>.
    /// Devuelve null si intenta salir de la raiz (<c>..</c>) o es absoluta.
    /// </summary>
    public static string? Normalize(string path)
    {
        if (path.Length == 0) return "";
        var p = path.Replace('\\', '/');
        if (p.StartsWith('/') || p.Contains(':')) return null;
        var parts = new List<string>();
        foreach (var seg in p.Split('/'))
        {
            if (seg.Length == 0 || seg == ".") continue;
            if (seg == "..") return null;
            parts.Add(seg);
        }
        return string.Join('/', parts);
    }
}
