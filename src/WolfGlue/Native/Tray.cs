using System.Text.Json.Serialization;

namespace WolfGlue.Native;

/// <summary>Opcion del menu de la bandeja. Sin submenus (los tres sistemas los admiten, pero no hacen falta).</summary>
public sealed class GlueTrayItem
{
    /// <summary>Omitido, o <c>separator</c> para una linea separadora.</summary>
    public string? Type { get; set; }
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool Checked { get; set; }
    /// <summary>La opcion que hace el clic izquierdo en el icono (como mucho una; en Windows, en negrita).</summary>
    public bool Default { get; set; }

    [JsonIgnore]
    public bool IsSeparator => Type == "separator";
}

/// <summary>
/// Icono de estado (bandeja del sistema). El menu es obligatorio: es la unica forma de interactuar en todos los sistemas.
/// </summary>
public sealed class GlueTray
{
    /// <summary>PNG o ICO. Sin icono, el de la app.</summary>
    public byte[]? Icon { get; set; }
    /// <summary>
    /// false (por defecto): el icono se usa solo como silueta y se tine segun el tema de la barra de tareas.
    /// true: se muestra con sus colores. El icono de la app (si no se da otro) siempre va en color.
    /// </summary>
    public bool Color { get; set; }
    public string? Tooltip { get; set; }
    public List<GlueTrayItem> Menu { get; set; } = [];

    /// <summary>Lanza <see cref="ArgumentException"/> si el modelo no es valido.</summary>
    public void Validate()
    {
        if (Menu is not { Count: > 0 }) throw new ArgumentException("El menu de la bandeja es obligatorio.", nameof(Menu));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int defaults = 0;
        foreach (var m in Menu)
        {
            if (m.IsSeparator) continue;
            if (m.Type is not null) throw new ArgumentException($"Tipo de opcion desconocido: {m.Type}", nameof(Menu));
            if (string.IsNullOrEmpty(m.Id)) throw new ArgumentException("Cada opcion del menu necesita id.", nameof(Menu));
            if (string.IsNullOrWhiteSpace(m.Text)) throw new ArgumentException($"La opcion \"{m.Id}\" necesita texto.", nameof(Menu));
            if (!ids.Add(m.Id)) throw new ArgumentException($"Id repetido en el menu: {m.Id}", nameof(Menu));
            if (m.Default) defaults++;
        }
        if (defaults > 1) throw new ArgumentException("Solo una opcion puede ser default.", nameof(Menu));
        if (ids.Count == 0) throw new ArgumentException("El menu necesita al menos una opcion.", nameof(Menu));
    }
}

/// <summary>Como <see cref="GlueTray"/> desde la web: el icono es una ruta del origen propio o una URL <c>data:</c>.</summary>
public sealed class GlueTrayRequest
{
    public string? Icon { get; set; }
    public bool Color { get; set; }
    public string? Tooltip { get; set; }
    public List<GlueTrayItem> Menu { get; set; } = [];

    /// <summary>Carga el icono con el contenido de la ventana que llama y construye el modelo.</summary>
    public async Task<GlueTray> ResolveAsync(GlueWindow? window)
    {
        byte[]? icon = null;
        if (!string.IsNullOrEmpty(Icon))
        {
            if (window is null) throw new ArgumentException("Llamada sin ventana.");
            icon = await ((Platform.IWindowHost)window).LoadTitleBarFileAsync(Icon)
                ?? throw new ArgumentException($"No se pudo cargar el icono: {Icon}");
        }
        return new GlueTray { Icon = icon, Color = Color, Tooltip = Tooltip, Menu = Menu ?? [] };
    }
}

/// <summary>Cambios de una opcion del menu (<c>null</c> = sin cambio).</summary>
public sealed class GlueTrayItemChanges
{
    public string? Text { get; set; }
    public bool? Enabled { get; set; }
    public bool? Checked { get; set; }
}

/// <summary>Argumentos de <c>tray.update</c> desde la web.</summary>
public sealed class GlueTrayUpdateArgs
{
    public string Id { get; set; } = "";
    public GlueTrayItemChanges Changes { get; set; } = new();
}

/// <summary>Evento <c>glue.tray</c>: se eligio una opcion del menu (o la <c>default</c> con el clic izquierdo).</summary>
public sealed class TrayEvent
{
    public string Id { get; set; } = "";
}

/// <summary>Bandeja del sistema: un icono por app.</summary>
public sealed class NativeTray
{
    private readonly GlueApp _app;
    private GlueTray? _current;
    internal NativeTray(GlueApp app) => _app = app;

    /// <summary>Hay bandeja en este sistema (Windows siempre; en un escritorio sin ella la app decide que hacer).</summary>
    public bool Available => _app.Platform.Services.TrayAvailable;

    /// <summary>Hay un icono puesto ahora mismo.</summary>
    public bool IsSet => _current is not null;

    /// <summary>Se produce en el hilo de UI al elegir una opcion del menu (o la <c>default</c> con el clic izquierdo).</summary>
    public event Action<string>? Click;

    /// <summary>Pone o sustituye el icono; <c>null</c> lo quita. Lanza <see cref="ArgumentException"/> si el modelo no es valido.</summary>
    public Task SetAsync(GlueTray? tray)
    {
        tray?.Validate();
        return GlueNative.OnUi(_app, () =>
        {
            _app.Platform.Services.SetTray(tray);
            _current = tray;
            return true;
        });
    }

    public Task RemoveAsync() => SetAsync(null);

    /// <summary>Cambia texto, estado o marca de una opcion del menu. Lanza <see cref="KeyNotFoundException"/> si no existe.</summary>
    public Task UpdateAsync(string id, GlueTrayItemChanges changes) => GlueNative.OnUi(_app, () =>
    {
        var tray = _current ?? throw new InvalidOperationException("No hay icono en la bandeja.");
        var item = tray.Menu.FirstOrDefault(m => !m.IsSeparator && m.Id == id) ?? throw new KeyNotFoundException($"No existe la opcion: {id}");
        if (changes.Text is { } t) { if (string.IsNullOrWhiteSpace(t)) throw new ArgumentException("Texto vacio."); item.Text = t; }
        if (changes.Enabled is { } e) item.Enabled = e;
        if (changes.Checked is { } c) item.Checked = c;
        _app.Platform.Services.SetTray(tray);
        return true;
    });

    /// <summary>Cambia el texto de ayuda del icono.</summary>
    public Task SetTooltipAsync(string? tooltip) => GlueNative.OnUi(_app, () =>
    {
        var tray = _current ?? throw new InvalidOperationException("No hay icono en la bandeja.");
        tray.Tooltip = tooltip;
        _app.Platform.Services.SetTray(tray);
        return true;
    });

    /// <summary>Lo llama el backend (hilo de UI) al elegir una opcion.</summary>
    internal void Raise(string id)
    {
        Click?.Invoke(id);
        _app.Emit("glue.tray", new TrayEvent { Id = id });
    }

    /// <summary>Quita el icono (al terminar la app).</summary>
    internal void Cleanup()
    {
        try { _app.Platform.Services.SetTray(null); }
        catch (Exception) { /* al salir no debe fallar nada */ }
        _current = null;
    }
}
