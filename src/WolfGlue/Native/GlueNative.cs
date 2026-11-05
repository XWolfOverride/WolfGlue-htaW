using WolfGlue.Platform;

namespace WolfGlue.Native;

/// <summary>
/// Servicios nativos del sistema, con la misma API en todos los SO (<c>app.Native</c>).
/// Se pueden usar desde cualquier hilo: lo que necesita el hilo de UI se encola en el.
/// </summary>
public sealed class GlueNative
{
    internal GlueNative(GlueApp app)
    {
        Dialogs = new NativeDialogs(app);
        Clipboard = new NativeClipboard(app);
        Screens = new NativeScreens(app);
        Theme = new NativeTheme(app);
        Shell = new NativeShell(app);
    }

    public NativeDialogs Dialogs { get; }
    public NativeClipboard Clipboard { get; }
    public NativeScreens Screens { get; }
    public NativeTheme Theme { get; }
    public NativeShell Shell { get; }

    /// <summary>
    /// Ejecuta en el hilo de UI, siempre encolado (nunca en linea): asi un dialogo modal no se abre dentro de
    /// un callback del webview, donde un bucle de mensajes anidado puede dar problemas de reentrada.
    /// </summary>
    internal static Task<T> Queue<T>(GlueApp app, Func<T> f)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Platform.Post(() =>
        {
            try { tcs.SetResult(f()); }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }

    /// <summary>En linea si ya estamos en el hilo de UI; si no, encolado.</summary>
    internal static Task<T> OnUi<T>(GlueApp app, Func<T> f)
    {
        if (!app.IsUiThread) return Queue(app, f);
        try { return Task.FromResult(f()); }
        catch (Exception e) { return Task.FromException<T>(e); }
    }
}

/// <summary>Dialogos nativos: abrir, guardar, elegir carpeta y mensajes.</summary>
public sealed class NativeDialogs
{
    private readonly GlueApp _app;
    internal NativeDialogs(GlueApp app) => _app = app;

    private IPlatformServices S => _app.Platform.Services;
    private static IPlatformWindow? Owner(GlueWindow? w) => w is { IsClosed: false } ? w.Native : null;

    /// <summary>Elige uno o varios archivos existentes. Devuelve null si se cancela.</summary>
    public Task<string[]?> OpenFileAsync(FileDialogOptions? options = null, GlueWindow? owner = null) =>
        GlueNative.Queue(_app, () => S.OpenFile(Owner(owner), options ?? new()));

    /// <summary>Elige donde guardar (pide confirmacion si ya existe). Devuelve null si se cancela.</summary>
    public Task<string?> SaveFileAsync(FileDialogOptions? options = null, GlueWindow? owner = null) =>
        GlueNative.Queue(_app, () => S.SaveFile(Owner(owner), options ?? new()));

    /// <summary>Elige una carpeta. Devuelve null si se cancela.</summary>
    public Task<string?> ChooseFolderAsync(FolderDialogOptions? options = null, GlueWindow? owner = null) =>
        GlueNative.Queue(_app, () => S.ChooseFolder(Owner(owner), options ?? new()));

    /// <summary>Mensaje modal con iconos y botones del sistema.</summary>
    public Task<MessageResult> MessageAsync(MessageOptions options, GlueWindow? owner = null) =>
        GlueNative.Queue(_app, () =>
        {
            var title = options.Title ?? owner?.Title ?? _app.Options.AppId;
            return S.Message(Owner(owner), title, options);
        });

    /// <summary>Atajo: mensaje informativo con boton Aceptar.</summary>
    public Task<MessageResult> MessageAsync(string text, GlueWindow? owner = null) =>
        MessageAsync(new MessageOptions { Text = text }, owner);
}

/// <summary>Portapapeles (texto).</summary>
public sealed class NativeClipboard
{
    private readonly GlueApp _app;
    internal NativeClipboard(GlueApp app) => _app = app;

    /// <summary>Texto del portapapeles, o null si no contiene texto.</summary>
    public Task<string?> GetTextAsync() => GlueNative.OnUi(_app, () => _app.Platform.Services.GetClipboardText());

    public Task SetTextAsync(string text) => GlueNative.OnUi(_app, () =>
    {
        _app.Platform.Services.SetClipboardText(text);
        return true;
    });
}

/// <summary>Monitores conectados.</summary>
public sealed class NativeScreens
{
    private readonly GlueApp _app;
    internal NativeScreens(GlueApp app) => _app = app;

    /// <summary>Lista de monitores (el principal primero).</summary>
    public IReadOnlyList<ScreenInfo> GetAll() =>
        [.. _app.Platform.Services.GetScreens().OrderByDescending(s => s.Primary)];
}

/// <summary>Tema del sistema y su evento de cambio.</summary>
public sealed class NativeTheme
{
    private readonly GlueApp _app;
    private ThemeInfo? _last;
    internal NativeTheme(GlueApp app) => _app = app;

    /// <summary>Tema actual.</summary>
    public ThemeInfo Current => _last ??= _app.Platform.Services.GetTheme();

    /// <summary>
    /// Se produce en el hilo de UI cuando cambia el tema (claro/oscuro, acento, contraste). Ademas se envia a todas
    /// las ventanas el evento <c>glue.theme</c>.
    /// </summary>
    public event Action<ThemeInfo>? Changed;

    /// <summary>Lo llama el backend cuando el sistema avisa de un cambio de ajustes.</summary>
    internal void Refresh()
    {
        var now = _app.Platform.Services.GetTheme();
        if (now == _last) return;
        _last = now;
        Changed?.Invoke(now);
        _app.Emit("glue.theme", now);
    }
}

/// <summary>Integracion con el shell del sistema.</summary>
public sealed class NativeShell
{
    private readonly GlueApp _app;
    internal NativeShell(GlueApp app) => _app = app;

    /// <summary>Abre una URL (http, https o mailto) con el navegador del sistema.</summary>
    public void OpenUrl(string url)
    {
        if (!_app.OpenExternal(url)) throw new ArgumentException("Solo URLs http, https o mailto: " + url, nameof(url));
    }

    /// <summary>Abre un archivo o carpeta con la aplicacion por defecto.</summary>
    public void Open(string path) => _app.Platform.Services.Open(Path.GetFullPath(path));

    /// <summary>Muestra el archivo seleccionado en el explorador de archivos.</summary>
    public void Reveal(string path) => _app.Platform.Services.Reveal(Path.GetFullPath(path));
}
