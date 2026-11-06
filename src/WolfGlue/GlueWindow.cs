using System.ComponentModel;
using WolfGlue.Bridge;
using WolfGlue.Content;
using WolfGlue.Platform;

namespace WolfGlue;

/// <summary>Ventana nativa ocupada por completo por el webview. Se usa solo desde el hilo de UI.</summary>
public sealed class GlueWindow : IBridgePeer, IWindowHost
{
    private static int _nextId;
    private readonly IPlatformWindow _native;
    private readonly CancellationTokenSource _closing = new();
    private readonly TaskCompletionSource<string?> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Queue<string> _pendingMessages = new();
    private bool _ready, _shown, _isClosed, _forceClose;
    private string? _title, _documentTitle;

    internal GlueWindow(GlueApp app, GlueWindowOptions options)
    {
        app.CheckThread();
        App = app;
        Options = options;
        Id = "w" + Interlocked.Increment(ref _nextId);
        _title = options.Title;
        _native = app.Platform.CreateWindow(this);
    }

    internal IPlatformWindow Native => _native;

    public GlueApp App { get; }
    public GlueWindowOptions Options { get; }

    /// <summary>Identificador unico dentro de la app (<c>w1</c>, <c>w2</c>…). La web lo ve en <c>glue.window.id</c>.</summary>
    public string Id { get; }

    /// <summary>Resultado que la pagina paso a <c>glue.window.close(valor)</c> (JSON), o null.</summary>
    public string? Result { get; set; }

    public bool IsClosed => _isClosed;

    /// <summary>Titulo de la ventana. Si no se fija, se usa el del documento.</summary>
    public string? Title
    {
        get => _title ?? _documentTitle;
        set { _title = value; if (value is not null) _native.SetTitle(value); }
    }

    /// <summary>Se produce antes de cerrar; se puede cancelar.</summary>
    public event EventHandler<CancelEventArgs>? Closing;
    /// <summary>Se produce al cerrarse la ventana.</summary>
    public event EventHandler? Closed;
    /// <summary>El webview esta listo y ha empezado a cargar la pagina inicial.</summary>
    public event EventHandler? Ready;

    /// <summary>Muestra la ventana (si es modal, deshabilita la duena).</summary>
    public void Show()
    {
        App.CheckThread();
        if (_shown || _isClosed) return;
        _shown = true;
        App.Attach(this);
        _background = ResolveBackground();
        _native.Show();
    }

    /// <summary>Muestra la ventana como dialogo modal sobre su duena y espera a que se cierre. Devuelve <see cref="Result"/>.</summary>
    public Task<string?> ShowDialogAsync()
    {
        if (Options.Owner is null) throw new InvalidOperationException("Un dialogo necesita Owner.");
        Options.Modal = true;
        Show();
        return _closed.Task;
    }

    /// <summary>Tarea que termina al cerrarse la ventana, con <see cref="Result"/>.</summary>
    public Task<string?> WhenClosed => _closed.Task;

    /// <summary>Pide el cierre (pasa por <see cref="Closing"/>).</summary>
    public void Close() { if (!_isClosed) _native.Close(); }

    /// <summary>Cierra sin preguntar a <see cref="Closing"/>.</summary>
    public void ForceClose() { _forceClose = true; Close(); }

    public void SetSize(int width, int height) => _native.SetSize(width, height);
    public void Center() => _native.Center();
    public void Minimize() => _native.Minimize();
    public void Maximize() => _native.Maximize();
    public void Restore() => _native.Restore();
    public void Focus() => _native.Focus();
    public void OpenDevTools() => _native.OpenDevTools();
    /// <summary>Recarga la pagina actual.</summary>
    public void Reload() => _native.Reload();

    /// <summary>Navega a una ruta del origen propio o a una URL completa.</summary>
    public void Navigate(string pageOrUrl) => _native.Navigate(ResolveUrl(pageOrUrl));

    /// <summary>Envia un evento a esta ventana.</summary>
    public void Emit<T>(string name, T data) => SendToWeb(App.Bridge.EventMessage(name, data));

    internal string ResolveUrl(string pageOrUrl) =>
        pageOrUrl.Contains("://", StringComparison.Ordinal) || pageOrUrl.StartsWith("about:", StringComparison.Ordinal)
            ? pageOrUrl
            : App.Origin + "/" + pageOrUrl.TrimStart('/');

    // ---------------------------------------------------------------- IBridgePeer
    CancellationToken IBridgePeer.Closing => _closing.Token;
    GlueWindow? IBridgePeer.Window => this;

    /// <summary>Envia JSON a la pagina; si el webview aun no esta listo, se encola.</summary>
    public void SendToWeb(string json)
    {
        if (_isClosed) return;
        if (!App.IsUiThread) { App.Invoke(() => SendToWeb(json)); return; }
        if (!_ready) { _pendingMessages.Enqueue(json); return; }
        _native.PostMessage(json);
    }

    // ---------------------------------------------------------------- IWindowHost
    string IWindowHost.StartUrl => ResolveUrl(Options.StartPage);
    string IWindowHost.Origin => App.Origin;
    bool IWindowHost.DevTools => App.Options.DevTools;
    IReadOnlyList<string> IWindowHost.ExtraScripts => App.Scripts;
    GlueBackground IWindowHost.Background => _background ??= ResolveBackground();
    byte[]? IWindowHost.Icon => Options.Icon ?? App.Options.Icon;

    private GlueBackground? _background;

    /// <summary>
    /// Opcion de la ventana; si no, <c>theme-color</c> de la pagina inicial (solo si el contenido la da sin esperar,
    /// para no retrasar la apertura); si no, el de la app; si no, el del sistema.
    /// </summary>
    internal GlueBackground ResolveBackground()
    {
        if (Options.Background is { } b) return b;
        try
        {
            var url = ResolveUrl(Options.StartPage);
            if (IsSameOrigin(url, App.Origin))
            {
                var (path, query) = GlueRequest.SplitPath(url[App.Origin.Length..]);
                var task = ((IWindowHost)this).HandleRequestAsync(new GlueRequest("GET", path, query, default));
                if (task.IsCompletedSuccessfully && task.Result is { Status: 200 } r
                    && r.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
                    && GlueColorParser.ThemeColorFromHtml(System.Text.Encoding.UTF8.GetString(r.Body.Span)) is { } tc)
                    return tc;
            }
        }
        catch (Exception) { /* sin theme-color: se sigue con los valores por defecto */ }
        return App.Options.Background ?? GlueBackground.System;
    }

    void IWindowHost.OnWebViewReady()
    {
        _ready = true;
        while (_pendingMessages.Count > 0) _native.PostMessage(_pendingMessages.Dequeue());
        Ready?.Invoke(this, EventArgs.Empty);
    }

    void IWindowHost.OnWebMessage(string json, string source)
    {
        if (!IsTrustedUrl(source)) return;
        _ = App.Bridge.HandleAsync(this, json);
    }

    bool IWindowHost.OnCloseRequested()
    {
        if (_forceClose) return true;
        var e = new CancelEventArgs();
        Closing?.Invoke(this, e);
        if (e.Cancel) return false;
        // Las ventanas con esta como duena se cierran con ella.
        foreach (var w in App.Windows.Where(w => w.Options.Owner == this).ToArray()) w.ForceClose();
        return true;
    }

    void IWindowHost.OnClosed()
    {
        if (_isClosed) return;
        _isClosed = true;
        _closing.Cancel();
        App.Detach(this);
        Closed?.Invoke(this, EventArgs.Empty);
        _closed.TrySetResult(Result);
    }

    void IWindowHost.OnDocumentTitleChanged(string title)
    {
        _documentTitle = title;
        if (_title is null) _native.SetTitle(title);
    }

    async ValueTask<GlueResponse> IWindowHost.HandleRequestAsync(GlueRequest request)
    {
        var provider = Options.Content ?? App.Content;
        try
        {
            if (provider is not null && await provider.GetAsync(request) is { } r) return r;
        }
        catch (Exception e)
        {
            return GlueResponse.Text("500 " + e.Message, status: 500);
        }
        return GlueResponse.NotFound();
    }

    void IWindowHost.OnNewWindowRequested(string url, int? width, int? height, bool userInitiated)
    {
        if (!IsTrustedUrl(url)) { OpenExternalFromPage(url, userInitiated); return; }
        var w = App.CreateWindow(new GlueWindowOptions
        {
            StartPage = url,
            Width = width ?? Options.Width,
            Height = height ?? Options.Height,
            Content = Options.Content,
        });
        w.Show();
    }

    bool IWindowHost.OnNavigationStarting(string url, bool userInitiated)
    {
        if (IsTrustedUrl(url) || url.StartsWith("about:", StringComparison.Ordinal)
            || url.StartsWith("data:", StringComparison.Ordinal)) return true;
        OpenExternalFromPage(url, userInitiated);
        return false;
    }

    /// <summary>
    /// Enlace o <c>window.open</c> a una URL externa: se abre en el navegador del sistema, salvo que la app exija
    /// accion del usuario (<see cref="GlueAppOptions.ExternalLinksRequireUserAction"/>) y la pagina lo haya pedido por
    /// su cuenta; entonces se ignora.
    /// </summary>
    internal bool OpenExternalFromPage(string url, bool userInitiated)
    {
        if (App.Options.ExternalLinksRequireUserAction && !userInitiated) return false;
        return App.OpenExternal(url);
    }

    /// <summary>URL del origen propio o del servidor de desarrollo.</summary>
    internal bool IsTrustedUrl(string url) =>
        IsSameOrigin(url, App.Origin) || (App.Options.DevServerOrigin is { } dev && IsSameOrigin(url, dev));

    internal static bool IsSameOrigin(string url, string origin)
    {
        origin = origin.TrimEnd('/');
        if (!url.StartsWith(origin, StringComparison.OrdinalIgnoreCase)) return false;
        return url.Length == origin.Length || url[origin.Length] is '/' or '?' or '#';
    }
}
