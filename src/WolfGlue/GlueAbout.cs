using System.Net;
using WolfGlue.Content;

namespace WolfGlue;

/// <summary>
/// Ventana «Acerca de» generica: nombre de la app, version y autor si los hay, icono si lo hay y, abajo y pequeno en gris,
/// «Desarrollado con …». Solo se usa si la app no define su propia pagina (<see cref="GlueAppOptions.AboutPage"/>).
/// </summary>
internal static class GlueAbout
{
    /// <summary>HTML de la ventana generica. Los datos de la app se escapan; el icono va como URL <c>data:</c>.</summary>
    internal static string Html(GlueAppOptions o)
    {
        var name = WebUtility.HtmlEncode(o.AppName ?? o.AppId);
        var icon = o.Icon ?? o.AboutIcon;
        var sb = new System.Text.StringBuilder();
        sb.Append("<!doctype html><html lang=\"").Append(Strings.Spanish ? "es" : "en").Append("\"><head><meta charset=\"utf-8\"><title>")
          .Append(WebUtility.HtmlEncode(Strings.AboutTitle(o.AppName ?? o.AppId))).Append("</title><style>")
          .Append(":root{color-scheme:light dark;--bg:#f3f3f3;--fg:#1b1b1b;--muted:#6b6b6b}")
          .Append("@media(prefers-color-scheme:dark){:root{--bg:#202020;--fg:#fff;--muted:#a0a0a0}}")
          .Append("html,body{height:100%;margin:0}body{display:flex;flex-direction:column;align-items:center;justify-content:center;")
          .Append("gap:6px;background:var(--bg);color:var(--fg);font:14px system-ui,\"Segoe UI\",sans-serif;user-select:none;cursor:default;text-align:center}")
          .Append("img{width:72px;height:72px;object-fit:contain;margin-bottom:8px}h1{font-size:20px;font-weight:600;margin:0}")
          .Append(".muted{color:var(--muted)}main{flex:1;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:6px;padding:16px}")
          .Append("footer{font-size:11px;color:var(--muted);padding:10px}</style></head><body><main>");
        if (icon is { Length: > 0 }) sb.Append("<img alt=\"\" src=\"").Append(IconUri(icon)).Append("\">");
        sb.Append("<h1>").Append(name).Append("</h1>");
        if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append("<div class=\"muted\">").Append(WebUtility.HtmlEncode(Strings.AboutVersion)).Append(' ').Append(WebUtility.HtmlEncode(o.Version)).Append("</div>");
        if (!string.IsNullOrWhiteSpace(o.Author)) sb.Append("<div>").Append(WebUtility.HtmlEncode(o.Author)).Append("</div>");
        sb.Append("</main><footer>").Append(WebUtility.HtmlEncode(Strings.PoweredBy(o.PoweredBy))).Append("</footer></body></html>");
        return sb.ToString();
    }

    private static string IconUri(byte[] icon)
    {
        bool png = GlueIcon.Detect(icon) == GlueIconFormat.Png;
        return (png ? "data:image/png;base64," : "data:image/x-icon;base64,") + Convert.ToBase64String(icon);
    }

    /// <summary>Contenido de la ventana generica: una sola pagina.</summary>
    internal sealed class Content(GlueAppOptions options) : IContentProvider
    {
        public ValueTask<GlueResponse?> GetAsync(GlueRequest request) => new(request.Path is "" or "index.html"
            ? GlueResponse.Text(Html(options), "text/html; charset=utf-8")
            : null);
    }
}
