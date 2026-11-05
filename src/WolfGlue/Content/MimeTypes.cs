namespace WolfGlue.Content;

/// <summary>Tabla propia de tipos MIME por extension (no depende del registro del sistema).</summary>
public static class MimeTypes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".htm"] = "text/html; charset=utf-8",
        [".htaw"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".map"] = "application/json; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8",
        [".md"] = "text/markdown; charset=utf-8",
        [".xml"] = "application/xml; charset=utf-8",
        [".csv"] = "text/csv; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".ico"] = "image/x-icon",
        [".bmp"] = "image/bmp",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".wasm"] = "application/wasm",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
    };

    /// <summary>Tipo MIME para la ruta, o <c>application/octet-stream</c>.</summary>
    public static string For(string path) =>
        Map.TryGetValue(Path.GetExtension(path), out var t) ? t : "application/octet-stream";
}
