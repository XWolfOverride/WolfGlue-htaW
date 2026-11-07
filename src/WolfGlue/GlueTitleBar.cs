using System.Text.Json;
using System.Text.Json.Serialization;

namespace WolfGlue;

/// <summary>
/// Barra de titulo con elementos propios. La dibuja WolfGlue con el aspecto del sistema (sin estilos propios) y la
/// definicion es la misma en Windows, Linux y macOS: solo admite lo que los tres pueden ofrecer de forma nativa (el mas
/// restrictivo es la barra de herramientas de macOS). En JSON (manifiesto de htaW, <c>glue.window.titlebar.set</c>)
/// los nombres van en camelCase.
/// </summary>
public sealed class GlueTitleBar
{
    /// <summary>Modos de la barra.</summary>
    public const string ModeTitle = "title", ModeCompact = "compact", ModeLabels = "labels";

    /// <summary>
    /// <c>title</c>: alto normal del sistema, con uno a tres botones de icono junto a los botones de la ventana.
    /// <c>compact</c> (por defecto): barra de herramientas de una fila, solo iconos.
    /// <c>labels</c>: barra mas alta, con el texto de cada boton bajo su icono.
    /// </summary>
    public string Mode { get; set; } = ModeCompact;

    /// <summary>Mostrar el titulo de la ventana (por defecto, si).</summary>
    public bool Title { get; set; } = true;

    /// <summary>
    /// Fuente de iconos propia (TTF u OTF de la app, la misma que puede usar su CSS). Sus simbolos se usan con
    /// <c>"icon": "font:E8E5"</c>.
    /// </summary>
    public string? IconFont { get; set; }

    public List<GlueTitleBarItem> Items { get; set; } = [];

    /// <summary>Lee una barra en JSON y la valida. Lanza <see cref="FormatException"/> con el motivo.</summary>
    public static GlueTitleBar Parse(string json)
    {
        GlueTitleBar? bar;
        try { bar = JsonSerializer.Deserialize(json, Bridge.BridgeJsonContext.Default.GlueTitleBar); }
        catch (JsonException e) { throw new FormatException("Barra de titulo no valida: " + e.Message, e); }
        if (bar is null) throw new FormatException("Barra de titulo vacia.");
        bar.Validate();
        return bar;
    }

    /// <summary>Comprueba modo, tipos, ids, iconos y las reglas comunes a todos los sistemas. Lanza <see cref="FormatException"/>.</summary>
    public void Validate()
    {
        if (Mode is not (ModeTitle or ModeCompact or ModeLabels))
            throw new FormatException("titlebar.mode: title, compact o labels.");
        if (IconFont is { } f && !GlueTitleBarItem.IsSafePath(f))
            throw new FormatException("titlebar.iconFont no valida: " + f);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int centered = 0, searches = 0;
        for (int i = 0; i < Items.Count; i++)
        {
            var it = Items[i] ?? throw new FormatException($"titlebar.items[{i}] vacio.");
            var p = $"titlebar.items[{i}]";
            if (Mode == ModeTitle && it.Type != GlueTitleBarItem.Button)
                throw new FormatException($"{p}: en el modo title solo hay botones.");
            ValidateItem(it, p, ids, inGroup: false);
            if (it.Center) centered++;
            if (it.Type == GlueTitleBarItem.Search) searches++;
        }
        if (Mode == ModeTitle && Items.Count > 3) throw new FormatException("titlebar: el modo title admite como mucho 3 botones.");
        if (centered > 1) throw new FormatException("titlebar: solo un elemento puede ir centrado (center).");
        if (searches > 1) throw new FormatException("titlebar: solo puede haber un campo de busqueda.");
    }

    private void ValidateItem(GlueTitleBarItem it, string p, HashSet<string> ids, bool inGroup)
    {
        switch (it.Type)
        {
            case GlueTitleBarItem.Button:
                if (string.IsNullOrEmpty(it.Icon)) throw new FormatException($"{p}: un boton necesita icon.");
                if (string.IsNullOrEmpty(it.Text)) throw new FormatException($"{p}: un boton necesita text (se ve bajo el icono o como ayuda).");
                if (it.Split && it.Menu is not { Count: > 0 }) throw new FormatException($"{p}: split necesita menu.");
                if (it.Menu is { } menu) ValidateMenu(menu, p);
                break;
            case GlueTitleBarItem.Group:
                if (inGroup) throw new FormatException($"{p}: no se pueden anidar grupos.");
                if (it.Items is not { Count: > 0 } sub) throw new FormatException($"{p}: un grupo necesita items.");
                for (int j = 0; j < sub.Count; j++)
                {
                    var s = sub[j] ?? throw new FormatException($"{p}.items[{j}] vacio.");
                    if (s.Type != GlueTitleBarItem.Button) throw new FormatException($"{p}.items[{j}]: un grupo solo contiene botones.");
                    if (s.Center) throw new FormatException($"{p}.items[{j}]: center va en el grupo, no en sus botones.");
                    ValidateItem(s, $"{p}.items[{j}]", ids, inGroup: true);
                }
                break;
            case GlueTitleBarItem.Panel:
                if (string.IsNullOrEmpty(it.Text)) throw new FormatException($"{p}: un panel necesita text.");
                if (it.Width is < 120 or > 480) throw new FormatException($"{p}.width: de 120 a 480.");
                if (it.Menu is { } pm) ValidateMenu(pm, p);
                break;
            case GlueTitleBarItem.Search:
                if (it.Width is < 120 or > 480) throw new FormatException($"{p}.width: de 120 a 480.");
                if (string.IsNullOrEmpty(it.Id)) throw new FormatException($"{p}: el campo de busqueda necesita id.");
                if (it.Center) throw new FormatException($"{p}: la busqueda va siempre al final, no centrada.");
                break;
            case GlueTitleBarItem.Image:
                if (string.IsNullOrEmpty(it.Icon) || it.Icon.StartsWith("font:", StringComparison.OrdinalIgnoreCase) || !GlueTitleBarIcons.IsImagePath(it.Icon))
                    throw new FormatException($"{p}: una imagen necesita icon con una imagen PNG o ICO de la app.");
                break;
            case GlueTitleBarItem.Space:
                if (it.Center) throw new FormatException($"{p}: un espacio no se centra.");
                break;
            default:
                throw new FormatException($"{p}.type: button, group, panel, search, image o space.");
        }
        if (it.Icon is { } icon && !GlueTitleBarIcons.IsValid(icon))
            throw new FormatException($"{p}.icon no valido: «{icon}» (nombre de la lista, ruta de un PNG/ICO o font:XXXX).");
        if (it.Icon is { } fi && fi.StartsWith("font:", StringComparison.OrdinalIgnoreCase) && IconFont is null)
            throw new FormatException($"{p}.icon usa font: sin titlebar.iconFont.");
        if (it.Id is { } id && !ids.Add(id)) throw new FormatException($"{p}: id repetido «{id}».");
    }

    private static void ValidateMenu(List<GlueTitleBarMenuItem> menu, string p)
    {
        if (menu.Count == 0) throw new FormatException($"{p}.menu vacio.");
        for (int j = 0; j < menu.Count; j++)
        {
            var m = menu[j] ?? throw new FormatException($"{p}.menu[{j}] vacio.");
            if (m.Type is not ("item" or "separator")) throw new FormatException($"{p}.menu[{j}].type: item o separator.");
            if (m.Type == "item" && (string.IsNullOrEmpty(m.Id) || string.IsNullOrEmpty(m.Text)))
                throw new FormatException($"{p}.menu[{j}] necesita id y text.");
        }
    }

    /// <summary>Busca un elemento por id (tambien dentro de los grupos).</summary>
    public GlueTitleBarItem? Find(string id)
    {
        foreach (var it in Items)
        {
            if (it.Id == id) return it;
            if (it.Items is { } sub) foreach (var s in sub) if (s.Id == id) return s;
        }
        return null;
    }
}

/// <summary>Elemento de la barra de titulo.</summary>
public sealed class GlueTitleBarItem
{
    public const string Button = "button", Group = "group", Panel = "panel", Search = "search", Image = "image", Space = "space";

    /// <summary>
    /// <c>button</c> (por defecto), <c>group</c> (botones juntos con una etiqueta comun en el modo labels),
    /// <c>panel</c> (recuadro de informacion con una o dos lineas), <c>search</c> (campo de busqueda nativo, siempre al
    /// final), <c>image</c> (imagen decorativa) o <c>space</c> (espacio flexible: empuja lo siguiente a la derecha).
    /// </summary>
    public string Type { get; set; } = Button;

    /// <summary>Identificador: llega en los eventos y sirve para <c>update</c>.</summary>
    public string? Id { get; set; }

    /// <summary>Boton: texto bajo el icono (modo labels) o ayuda emergente. Grupo: su etiqueta. Panel: primera linea.
    /// Busqueda: texto inicial.</summary>
    public string? Text { get; set; }

    /// <summary>Panel: segunda linea.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Busqueda: texto de ayuda dentro del campo vacio.</summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Icono: un nombre de la lista comun (<c>open</c>, <c>save</c>, <c>settings</c>…, ver <see cref="GlueTitleBarIcons"/>),
    /// una ruta de la app a un PNG o ICO (<c>img/abrir.png</c>) o un simbolo de la fuente propia (<c>font:E8E5</c>).
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Un PNG se usa como silueta y el sistema lo pinta con el color del texto (claro u oscuro). Con <c>true</c> se
    /// muestra con sus colores.
    /// </summary>
    public bool Color { get; set; }

    public string? Tooltip { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>Boton conmutador: tiene estado marcado o no (<see cref="Checked"/>).</summary>
    public bool Toggle { get; set; }
    public bool Checked { get; set; }

    /// <summary>Menu desplegable nativo. Sin <see cref="Split"/>, el boton (o el panel) abre el menu al pulsarlo.</summary>
    public List<GlueTitleBarMenuItem>? Menu { get; set; }

    /// <summary>Boton partido: el cuerpo hace su accion (evento click) y la flecha abre <see cref="Menu"/>.</summary>
    public bool Split { get; set; }

    /// <summary>Centrado en la ventana (como mucho un elemento; normalmente el panel).</summary>
    public bool Center { get; set; }

    /// <summary>Panel o busqueda: ancho en unidades logicas (120–480; 240 por defecto).</summary>
    public int? Width { get; set; }

    /// <summary>Grupo: sus botones.</summary>
    public List<GlueTitleBarItem>? Items { get; set; }

    internal static bool IsSafePath(string path) =>
        path.Length > 0 && !path.Contains("..", StringComparison.Ordinal) && !path.Contains(':', StringComparison.Ordinal)
        || path.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Opcion del menu desplegable de un boton o panel.</summary>
public sealed class GlueTitleBarMenuItem
{
    /// <summary><c>item</c> (por defecto) o <c>separator</c>.</summary>
    public string Type { get; set; } = "item";
    public string? Id { get; set; }
    public string? Text { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Checked { get; set; }
}

/// <summary>Cambios en un elemento ya puesto (<c>glue.window.titlebar.update</c>): solo se aplica lo que no es null.</summary>
public sealed class GlueTitleBarUpdate
{
    public string? Text { get; set; }
    public string? Subtitle { get; set; }
    public string? Icon { get; set; }
    public string? Tooltip { get; set; }
    public bool? Enabled { get; set; }
    public bool? Checked { get; set; }
    public List<GlueTitleBarMenuItem>? Menu { get; set; }
}

/// <summary>
/// Evento de la barra. <see cref="Type"/>: <c>click</c> (boton, panel, imagen u opcion de menu), <c>input</c> (la
/// busqueda cambia) o <c>submit</c> (Intro en la busqueda). <see cref="Id"/>: el elemento o la opcion de menu;
/// <see cref="Button"/>: el elemento que abrio el menu (null si no hubo menu); <see cref="Value"/>: texto de la busqueda.
/// </summary>
public sealed record GlueTitleBarEvent(string Type, string Id, string? Button, string? Value);

internal sealed class TitleBarUpdateArgs
{
    public string Id { get; set; } = "";
    public GlueTitleBarUpdate? Changes { get; set; }
}
