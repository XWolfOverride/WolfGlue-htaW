using System.Text.Json.Serialization;

namespace WolfGlue.Native;

/// <summary>
/// Opcion de un menu nativo (contextual). Modelo comun con la barra de titulo y la bandeja (<c>id</c>, <c>text</c>,
/// <c>enabled</c>, <c>checked</c>, separadores) mas submenus (<see cref="Items"/>) y un atajo para mostrar.
/// </summary>
public sealed class GlueMenuItem
{
    /// <summary>Omitido, o <c>separator</c> para una linea separadora.</summary>
    public string? Type { get; set; }
    /// <summary>Lo que devuelve el menu al elegir la opcion. Un submenu no lo necesita.</summary>
    public string? Id { get; set; }
    public string Text { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool Checked { get; set; }
    /// <summary>Papel estandar de la opcion: <c>about</c> abre la ventana «Acerca de» de la app (y en macOS va al menu de la aplicacion). Sin <c>id</c> se le da <see cref="AboutId"/>.</summary>
    public string? Role { get; set; }

    /// <summary>Id de la opcion con <c>role: "about"</c>.</summary>
    public const string AboutId = "glue:about";

    /// <summary>Atajo que se muestra a la derecha (<c>Ctrl+C</c>). Solo es texto: la pagina atiende las teclas.</summary>
    public string? Shortcut { get; set; }
    /// <summary>Submenu. Si existe, la opcion no se puede elegir (solo abre el submenu).</summary>
    public List<GlueMenuItem>? Items { get; set; }

    [JsonIgnore]
    public bool IsSeparator => Type == "separator";

    /// <summary>Profundidad maxima de submenus.</summary>
    public const int MaxDepth = 4;

    /// <summary>Lanza <see cref="ArgumentException"/> si el menu no es valido.</summary>
    public static void Validate(IReadOnlyList<GlueMenuItem>? items)
    {
        if (items is not { Count: > 0 }) throw new ArgumentException("El menu necesita al menos una opcion.", nameof(items));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Check(items, 1, ids);
    }

    /// <summary>Lee un menu de ventana en JSON y lo valida. Lanza <see cref="FormatException"/> con el motivo.</summary>
    public static List<GlueMenuItem> ParseBar(string json)
    {
        try
        {
            var menu = System.Text.Json.JsonSerializer.Deserialize(json, Bridge.BridgeJsonContext.Default.ListGlueMenuItem)
                ?? throw new FormatException("Menu vacio.");
            ValidateBar(menu);
            return menu;
        }
        catch (System.Text.Json.JsonException e) { throw new FormatException("Menu no valido: " + e.Message, e); }
        catch (ArgumentException e) { throw new FormatException(e.Message, e); }
    }

    /// <summary>Menu de ventana: cada entrada de primer nivel es un menu (<c>Archivo</c>) con sus opciones; sin separadores ni opciones sueltas.</summary>
    public static void ValidateBar(IReadOnlyList<GlueMenuItem>? menus)
    {
        Validate(menus);
        foreach (var m in menus!)
            if (m.IsSeparator || m.Items is null)
                throw new ArgumentException("En el menu de ventana, cada entrada de primer nivel debe ser un menu con opciones.", nameof(menus));
    }

    private static void Check(IReadOnlyList<GlueMenuItem> items, int depth, HashSet<string> ids)
    {
        if (depth > MaxDepth) throw new ArgumentException($"Submenus anidados como mucho {MaxDepth} niveles.", nameof(items));
        bool any = false;
        foreach (var m in items)
        {
            if (m.IsSeparator) continue;
            if (m.Type is not null) throw new ArgumentException($"Tipo de opcion desconocido: {m.Type}", nameof(items));
            if (string.IsNullOrWhiteSpace(m.Text)) throw new ArgumentException("Cada opcion necesita texto.", nameof(items));
            if (m.Role is not null)
            {
                if (m.Role != "about") throw new ArgumentException($"Papel desconocido: {m.Role}", nameof(items));
                m.Id ??= AboutId;
            }
            any = true;
            if (m.Items is not null)
            {
                if (m.Items.Count == 0) throw new ArgumentException($"El submenu \"{m.Text}\" esta vacio.", nameof(items));
                Check(m.Items, depth + 1, ids);
                continue;
            }
            if (string.IsNullOrEmpty(m.Id)) throw new ArgumentException($"La opcion \"{m.Text}\" necesita id.", nameof(items));
            if (!ids.Add(m.Id)) throw new ArgumentException($"Id repetido en el menu: {m.Id}", nameof(items));
        }
        if (!any) throw new ArgumentException("Un menu no puede tener solo separadores.", nameof(items));
    }
}

/// <summary>Evento <c>glue.window.menu</c>: se eligio una opcion del menu de ventana.</summary>
public sealed class WindowMenuEvent
{
    public string Id { get; set; } = "";
}

/// <summary>Evento <c>glue.window.menuState</c>: el menu de ventana esta puesto, la barra tiene el foco del teclado y letras de acceso.</summary>
public sealed class MenuStateEvent
{
    public bool On { get; set; }
    public bool KeyMode { get; set; }
    /// <summary>Letras de acceso de los menus de primer nivel (mayusculas, juntas).</summary>
    public string Mnemonics { get; set; } = "";
}

/// <summary>Tecla reenviada por la pagina al menu de ventana (<c>glue.window.menuKey</c>). <c>Kind</c>: 0 tecla, 1 soltada, 2 con Alt, 3 soltada con Alt.</summary>
public sealed class MenuKeyArgs
{
    public int Kind { get; set; }
    public uint Vk { get; set; }
}

/// <summary>Argumentos de <c>menu.popup</c> desde la web.</summary>
public sealed class GlueMenuPopupArgs
{
    public List<GlueMenuItem> Items { get; set; } = [];
    /// <summary>Posicion en pixeles CSS dentro de la pagina (por ejemplo <c>clientX</c>/<c>clientY</c>); sin ella, junto al raton.</summary>
    public double? X { get; set; }
    public double? Y { get; set; }
}

/// <summary>Menus contextuales nativos.</summary>
public sealed class NativeMenus
{
    private readonly GlueApp _app;
    internal NativeMenus(GlueApp app) => _app = app;

    /// <summary>
    /// Muestra un menu contextual y devuelve el <c>id</c> de la opcion elegida, o <c>null</c> si se cierra sin elegir.
    /// Sin <paramref name="position"/>, aparece junto al raton; si se da, es relativa al area de contenido de
    /// <paramref name="owner"/>, en unidades logicas (pixeles CSS).
    /// </summary>
    public Task<string?> PopupAsync(IReadOnlyList<GlueMenuItem> items, GlueWindow? owner = null, (double X, double Y)? position = null)
    {
        GlueMenuItem.Validate(items);
        return GlueNative.Queue(_app, () =>
            _app.Platform.Services.ShowMenu(owner is { IsClosed: false } ? owner.Native : null, items, position));
    }
}
