using WolfGlue.Bridge;
using WolfGlue.Content;
using WolfGlue.Platform;

namespace WolfGlue;

/// <summary>
/// Aplicacion WolfGlue: una por proceso. Duena del hilo de UI, del motor web compartido y de las ventanas.
/// Se crea y se ejecuta en el hilo principal (marcar <c>Main</c> con <c>[STAThread]</c>).
/// </summary>
public sealed class GlueApp
{
    private readonly List<GlueWindow> _windows = [];
    private readonly IPlatform _platform;
    private bool _running;

    /// <summary>Aplicacion en curso.</summary>
    public static GlueApp? Current { get; private set; }

    public GlueApp(GlueAppOptions options)
    {
        if (Current is not null) throw new InvalidOperationException("Solo puede haber un GlueApp por proceso.");
        if (string.IsNullOrWhiteSpace(options.AppId)) throw new ArgumentException("AppId es obligatorio.", nameof(options));
        Options = options;
        Origin = "https://" + options.Host;
        _platform = CreatePlatform();
        Current = this;
        Native = new Native.GlueNative(this);
        _platform.Initialize(this);
        Bridge.Broadcast += OnBroadcast;
        BuiltInServices.Register(this);
        if (options.NativeServicesInWeb) BuiltInServices.RegisterNative(this);
    }

    /// <summary>Servicios nativos: dialogos, portapapeles, pantallas, tema y shell.</summary>
    public Native.GlueNative Native { get; }

    /// <summary>
    /// Mensaje de error nativo y bloqueante, sin necesidad de un GlueApp (para errores de arranque).
    /// En sistemas sin backend todavia, se escribe en la salida de error.
    /// </summary>
    public static void ShowError(string title, string message)
    {
        if (OperatingSystem.IsWindows())
            WolfGlue.Platform.Windows.Win32.MessageBoxW(0, message, title, WolfGlue.Platform.Windows.Win32.MB_OK | WolfGlue.Platform.Windows.Win32.MB_ICONERROR);
        else
            Console.Error.WriteLine($"{title}: {message}");
    }

    private static IPlatform CreatePlatform()
    {
        if (OperatingSystem.IsWindows()) return new Platform.Windows.WinPlatform();
        throw new PlatformNotSupportedException(Strings.UnsupportedOs);
    }

    internal IPlatform Platform => _platform;

    public GlueAppOptions Options { get; }

    /// <summary>Origen propio (<c>https://host</c>).</summary>
    public string Origin { get; }

    /// <summary>Puente web ↔ C# compartido por todas las ventanas.</summary>
    public GlueBridge Bridge { get; } = new();

    /// <summary>
    /// Scripts JS propios que se inyectan en cada documento justo despues del shim (<c>glue</c> ya existe), antes que
    /// los de la pagina. Anadirlos antes de mostrar las ventanas.
    /// </summary>
    public List<string> Scripts { get; } = [];

    /// <summary>Contenido por defecto de las ventanas.</summary>
    public IContentProvider? Content { get; set; }

    /// <summary>Ventanas abiertas.</summary>
    public IReadOnlyList<GlueWindow> Windows => _windows;

    /// <summary>Ventana principal (por defecto, la primera que se muestra).</summary>
    public GlueWindow? MainWindow { get; set; }

    /// <summary>Codigo de salida de <see cref="Run"/>.</summary>
    public int ExitCode { get; private set; }

    /// <summary>Crea una ventana (no se muestra hasta <see cref="GlueWindow.Show"/>).</summary>
    public GlueWindow CreateWindow(GlueWindowOptions options) => new(this, options);

    /// <summary>
    /// Muestra la ventana «Acerca de»: la que la app haya marcado (<see cref="GlueWindowOptions.IsAbout"/>), su pagina propia
    /// (<see cref="GlueAppOptions.AboutPage"/>) o una generica con nombre, version, autor, icono y «Desarrollado con …». Si ya esta
    /// abierta, la trae al frente. En macOS es lo que abre la opcion «Acerca de» del menu de la aplicacion.
    /// </summary>
    public GlueWindow ShowAboutBox()
    {
        CheckThread();
        if (_windows.FirstOrDefault(w => w.Options.IsAbout && !w.IsClosed) is { } open) { open.Present(); return open; }
        var o = new GlueWindowOptions
        {
            Title = Strings.AboutTitle(Options.AppName ?? Options.AppId),
            IsAbout = true,
            Width = Options.AboutWidth,
            Height = Options.AboutHeight,
            Resizable = false,
            Owner = MainWindow is { IsClosed: false } m ? m : null,
        };
        if (Options.AboutPage is { Length: > 0 } page) o.StartPage = page;
        else o.Content = new GlueAbout.Content(Options);
        var win = CreateWindow(o);
        win.Show();
        return win;
    }

    /// <summary>Busca una ventana por su id.</summary>
    public GlueWindow? FindWindow(string id) => _windows.FirstOrDefault(w => w.Id == id);

    /// <summary>Ejecuta el bucle de la aplicacion hasta que termina. Devuelve el codigo de salida.</summary>
    public int Run()
    {
        CheckThread();
        if (_running) throw new InvalidOperationException("Run ya esta en marcha.");
        _running = true;
        var prev = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new GlueSynchronizationContext(this));
        try { return _platform.Run(); }
        finally
        {
            Native.Notifications.CloseAll();
            Native.Tray.Cleanup();
            SynchronizationContext.SetSynchronizationContext(prev);
            _running = false;
        }
    }

    /// <summary>Termina la aplicacion (cierra el bucle de <see cref="Run"/>).</summary>
    public void Quit(int exitCode = 0)
    {
        ExitCode = exitCode;
        if (_platform.IsUiThread) _platform.Quit(exitCode);
        else _platform.Post(() => _platform.Quit(exitCode));
    }

    /// <summary>Indica si el hilo actual es el de UI.</summary>
    public bool IsUiThread => _platform.IsUiThread;

    /// <summary>Ejecuta la accion en el hilo de UI (en el acto si ya se esta en el).</summary>
    public void Invoke(Action action)
    {
        if (_platform.IsUiThread) action();
        else _platform.Post(action);
    }

    /// <summary>Encola la accion en el hilo de UI y devuelve una tarea que termina con ella.</summary>
    public Task InvokeAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _platform.Post(async () =>
        {
            try { await action(); tcs.SetResult(); }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }

    /// <summary>Envia un evento a todas las ventanas.</summary>
    public void Emit<T>(string name, T data)
    {
        var json = Bridge.EventMessage(name, data);
        Invoke(() => { foreach (var w in _windows.ToArray()) w.SendToWeb(json); });
    }

    /// <summary>
    /// Abre una URL web (<c>http</c>, <c>https</c>, <c>mailto</c>) con la aplicacion por defecto del sistema.
    /// Cualquier otro esquema (<c>file:</c>, protocolos de otras aplicaciones…) se rechaza y devuelve false: el
    /// shell podria ejecutar programas con ellos. Para abrir archivos esta <c>Native.Shell.Open</c>.
    /// </summary>
    public bool OpenExternal(string url)
    {
        if (!IsWebUrl(url, out var uri)) return false;
        _platform.OpenExternal(uri.AbsoluteUri);
        return true;
    }

    /// <summary>URL absoluta con esquema http, https o mailto.</summary>
    public static bool IsWebUrl(string url, out Uri uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp
            || u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeMailto))
        {
            uri = u;
            return true;
        }
        uri = null!;
        return false;
    }

    internal void CheckThread()
    {
        if (!_platform.IsUiThread) throw new InvalidOperationException(Strings.WrongThread);
    }

    internal void Attach(GlueWindow w)
    {
        _windows.Add(w);
        MainWindow ??= w;
    }

    internal void Detach(GlueWindow w)
    {
        _windows.Remove(w);
        // Ventana principal cerrada: el resto se cierra con ella.
        if (Options.QuitMode == GlueQuitMode.MainWindowClosed && w == MainWindow)
            foreach (var other in _windows.ToArray()) other.ForceClose();
        bool quit = Options.QuitMode switch
        {
            GlueQuitMode.LastWindowClosed => _windows.Count == 0,
            GlueQuitMode.MainWindowClosed => w == MainWindow,
            _ => false,
        };
        if (w == MainWindow) MainWindow = _windows.FirstOrDefault();
        if (quit) Quit(ExitCode);
    }

    internal void OnBroadcast(IBridgePeer from, string name, string dataJson, string to)
    {
        var json = GlueBridge.RawEventMessage(name, dataJson, from.Id);
        foreach (var w in _windows.ToArray())
        {
            if (to == "*" ? w.Id == from.Id : w.Id != to) continue;
            w.SendToWeb(json);
        }
    }

    /// <summary>Error fatal de arranque: mensaje nativo y salida.</summary>
    internal void Fatal(string message)
    {
        _platform.ShowError(Options.AppId, message);
        Quit(1);
    }
}

/// <summary>Hace que los <c>await</c> vuelvan al hilo de UI.</summary>
internal sealed class GlueSynchronizationContext(GlueApp app) : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => app.Platform.Post(() => d(state));

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (app.IsUiThread) { d(state); return; }
        using var done = new ManualResetEventSlim();
        Exception? error = null;
        app.Platform.Post(() =>
        {
            try { d(state); } catch (Exception e) { error = e; } finally { done.Set(); }
        });
        done.Wait();
        if (error is not null) throw error;
    }

    public override SynchronizationContext CreateCopy() => this;
}
