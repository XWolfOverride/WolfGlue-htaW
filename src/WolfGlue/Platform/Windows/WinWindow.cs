using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using WolfGlue.Content;

namespace WolfGlue.Platform.Windows;

/// <summary>Ventana Win32 con un controlador WebView2 ocupando todo el area de cliente.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WinWindow : IPlatformWindow
{
    private static readonly Dictionary<nint, WinWindow> Windows = [];
    private static WinWindow? _creating;

    private readonly WinPlatform _platform;
    private readonly IWindowHost _host;
    private readonly uint _style, _exStyle;
    private readonly nint _owner;
    private ICoreWebView2Controller? _controller;
    private ICoreWebView2? _webview;
    private string? _pendingUrl;
    private bool _destroyed;

    internal nint Hwnd { get; private set; }

    public WinWindow(WinPlatform platform, IWindowHost host)
    {
        _platform = platform;
        _host = host;
        var o = host.Options;
        _owner = o.Owner?.Native is WinWindow ow ? ow.Hwnd : 0;

        _style = Win32.WS_OVERLAPPEDWINDOW | Win32.WS_CLIPCHILDREN;
        if (!o.Resizable) _style &= ~(Win32.WS_THICKFRAME | Win32.WS_MAXIMIZEBOX);
        if (o.Modal) _style &= ~Win32.WS_MINIMIZEBOX;
        _exStyle = o.AlwaysOnTop ? Win32.WS_EX_TOPMOST : 0;

        _creating = this;
        try
        {
            Hwnd = Win32.CreateWindowExW(_exStyle, WinPlatform.WindowClass, o.Title ?? "", _style,
                Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT,
                _owner, 0, platform.HInstance, 0);
        }
        finally { _creating = null; }
        if (Hwnd == 0) throw new InvalidOperationException("CreateWindowExW fallo: " + Marshal.GetLastPInvokeError());
        Windows[Hwnd] = this;
        // Contenedor del webview, oculto hasta que la pagina carga: WebView2 pinta su propio gris hasta tener el primer
        // fotograma (DefaultBackgroundColor no se aplica antes), asi que mientras tanto se ve el fondo de esta ventana.
        _container = Win32.CreateWindowExW(0, "STATIC", null, Win32.WS_CHILD | Win32.WS_CLIPCHILDREN,
            0, 0, 0, 0, Hwnd, 0, platform.HInstance, 0);
        ApplyTheme();
    }

    private readonly nint _container;
    private bool _contentShown;

    // ------------------------------------------------------------- IPlatformWindow
    /// <summary>
    /// La ventana se muestra cuando la pagina inicial ha cargado o, como mucho, a los
    /// <see cref="GlueWindowOptions.ShowDelayMs"/>, pintada con el color de fondo inicial; la pagina aparece con un
    /// fundido desde ese color (ver <see cref="Shim.FadeIn"/>).
    /// </summary>
    public void Show()
    {
        var o = _host.Options;
        PlaceWindow(o);
        if (o.Modal && _owner != 0) Win32.EnableWindow(_owner, false);
        if (o.ShowDelayMs > 0) Win32.SetTimer(Hwnd, RevealTimerId, (uint)o.ShowDelayMs, 0);
        else Reveal();
        _platform.WhenEnvironment(CreateController);
    }

    private const nint RevealTimerId = 1;
    private bool _revealed;

    /// <summary>Hace visible la ventana (una sola vez).</summary>
    private void Reveal()
    {
        if (_revealed || _destroyed) return;
        _revealed = true;
        Win32.KillTimer(Hwnd, RevealTimerId);
        Win32.ShowWindow(Hwnd, _host.Options.Maximized ? Win32.SW_SHOWMAXIMIZED : Win32.SW_SHOWNORMAL);
        Win32.UpdateWindow(Hwnd);
        Win32.SetForegroundWindow(Hwnd);
        if (_contentShown) _controller?.MoveFocus(WebView2Const.MOVE_FOCUS_REASON_PROGRAMMATIC);
    }

    /// <summary>
    /// Pagina cargada: muestra la ventana si aun no lo estaba, hace visible el webview y arranca el fundido. Se repite en
    /// cada navegacion (el contenedor ya visible no cambia; el fundido es el del documento nuevo).
    /// </summary>
    private void OnNavigationCompleted()
    {
        if (_destroyed) return;
        Reveal();
        _webview?.ExecuteScript(Shim.FadeInGo, null);
        if (_contentShown) return;
        _contentShown = true;
        Win32.ShowWindow(_container, Win32.SW_SHOWNA);
        _controller?.MoveFocus(WebView2Const.MOVE_FOCUS_REASON_PROGRAMMATIC);
    }

    public void Close()
    {
        if (!_destroyed) Win32.PostMessageW(Hwnd, Win32.WM_CLOSE, 0, 0);
    }

    public void SetTitle(string title) { if (!_destroyed) Win32.SetWindowTextW(Hwnd, title); }

    public void SetSize(int width, int height)
    {
        if (_destroyed) return;
        var r = WindowRectForClient(width, height, Dpi);
        Win32.SetWindowPos(Hwnd, 0, 0, 0, r.Width, r.Height, Win32.SWP_NOMOVE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    public unsafe void Center()
    {
        if (_destroyed) return;
        Win32.GetWindowRect(Hwnd, out var r);
        var area = CenterArea();
        Win32.SetWindowPos(Hwnd, 0, area.Left + (area.Width - r.Width) / 2, area.Top + (area.Height - r.Height) / 2,
            0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    public void Minimize() { if (!_destroyed) Win32.ShowWindow(Hwnd, Win32.SW_SHOWMINIMIZED); }
    public void Maximize() { if (!_destroyed) Win32.ShowWindow(Hwnd, Win32.SW_SHOWMAXIMIZED); }
    public void Restore() { if (!_destroyed) Win32.ShowWindow(Hwnd, Win32.SW_RESTORE); }

    public void Focus()
    {
        if (_destroyed) return;
        if (Win32.IsIconic(Hwnd)) Win32.ShowWindow(Hwnd, Win32.SW_RESTORE);
        Win32.SetForegroundWindow(Hwnd);
    }

    public void Navigate(string url)
    {
        if (_webview is null) _pendingUrl = url;
        else _webview.Navigate(url);
    }

    public void PostMessage(string json) => _webview?.PostWebMessageAsString(json);

    public void OpenDevTools() => _webview?.OpenDevToolsWindow();

    public void Reload() => _webview?.Reload();

    // ------------------------------------------------------------- tamano y posicion
    private uint Dpi
    {
        get
        {
            var d = Win32.GetDpiForWindow(Hwnd);
            return d == 0 ? 96u : d;
        }
    }

    private static int Scale(int logical, uint dpi) => (int)Math.Round(logical * dpi / 96.0);

    private RECT WindowRectForClient(int width, int height, uint dpi)
    {
        var r = new RECT { Right = Scale(width, dpi), Bottom = Scale(height, dpi) };
        Win32.AdjustWindowRectExForDpi(ref r, _style, false, _exStyle, dpi);
        return r;
    }

    private unsafe RECT CenterArea()
    {
        if (_owner != 0 && !Win32.IsIconic(_owner))
        {
            Win32.GetWindowRect(_owner, out var or);
            return or;
        }
        var mon = Win32.MonitorFromWindow(_owner != 0 ? _owner : Hwnd, Win32.MONITOR_DEFAULTTONEAREST);
        var mi = new Win32.MONITORINFO { cbSize = (uint)sizeof(Win32.MONITORINFO) };
        Win32.GetMonitorInfoW(mon, ref mi);
        return mi.rcWork;
    }

    private void PlaceWindow(GlueWindowOptions o)
    {
        var dpi = _owner != 0 ? Win32.GetDpiForWindow(_owner) : Dpi;
        if (dpi == 0) dpi = 96;
        var r = WindowRectForClient(o.Width, o.Height, dpi);
        int x, y;
        if (o.X is int lx && o.Y is int ly) { x = Scale(lx, dpi); y = Scale(ly, dpi); }
        else
        {
            var area = CenterArea();
            x = area.Left + (area.Width - r.Width) / 2;
            y = area.Top + (area.Height - r.Height) / 2;
        }
        Win32.SetWindowPos(Hwnd, 0, x, y, r.Width, r.Height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    private unsafe void ApplyTheme()
    {
        int dark = _platform.DarkMode ? 1 : 0;
        Win32.DwmSetWindowAttribute(Hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, &dark, sizeof(int));
        if (_controller is ICoreWebView2Controller2 c2) c2.put_DefaultBackgroundColor(BackgroundColor);
        Win32.InvalidateRect(Hwnd, 0, true);
    }

    private GlueRgb Background => _host.Background.For(_platform.DarkMode);

    private COREWEBVIEW2_COLOR BackgroundColor
    {
        get
        {
            var c = Background;
            return new() { A = 255, R = c.R, G = c.G, B = c.B };
        }
    }

    /// <summary>Pinta el area de cliente con el color de fondo (visible hasta que el webview pinta).</summary>
    private void EraseBackground(nint hdc)
    {
        var c = Background;
        var brush = Win32.CreateSolidBrush(c.R | (uint)c.G << 8 | (uint)c.B << 16);
        Win32.GetClientRect(Hwnd, out var r);
        Win32.FillRect(hdc, ref r, brush);
        Win32.DeleteObject(brush);
    }

    private void ResizeWebView()
    {
        Win32.GetClientRect(Hwnd, out var r);
        Win32.SetWindowPos(_container, 0, 0, 0, r.Width, r.Height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        _controller?.put_Bounds(r);
    }

    // ------------------------------------------------------------- WebView2
    private void CreateController(ICoreWebView2Environment env)
    {
        if (_destroyed) return;
        env.CreateCoreWebView2Controller(_container, new ControllerCompletedHandler((hr, controller) =>
        {
            if (_destroyed) { controller?.Close(); return; }
            if (hr < 0 || controller is null)
            {
                GlueApp.Current?.Fatal(Strings.WebViewFailed(hr));
                return;
            }
            SetupWebView(env, controller);
        }));
    }

    private void SetupWebView(ICoreWebView2Environment env, ICoreWebView2Controller controller)
    {
        _controller = controller;
        _webview = controller.get_CoreWebView2();
        var wv = _webview;

        if (controller is ICoreWebView2Controller2 c2) c2.put_DefaultBackgroundColor(BackgroundColor);

        var s = wv.get_Settings();
        s.put_AreDevToolsEnabled(_host.DevTools);
        s.put_AreDefaultContextMenusEnabled(_host.DevTools);
        s.put_IsStatusBarEnabled(false);
        s.put_IsZoomControlEnabled(false);
        // Comprobacion de reputacion de URL (SmartScreen): retrasa cada navegacion ~2 s y el contenido es propio.
        if (s is ICoreWebView2Settings8 s8) s8.put_IsReputationCheckingRequired(((GlueWindow)_host).App.Options.UrlReputationCheck);

        wv.add_WebMessageReceived(new WebMessageReceivedHandler(OnWebMessage));
        wv.add_WebResourceRequested(new WebResourceRequestedHandler(args => OnResourceRequested(env, args)));
        wv.AddWebResourceRequestedFilter(_host.Origin + "/*", WebView2Const.WEB_RESOURCE_CONTEXT_ALL);
        wv.add_NewWindowRequested(new NewWindowRequestedHandler(OnNewWindowRequested));
        wv.add_NavigationStarting(new NavigationStartingHandler(a =>
        {
            if (!_host.OnNavigationStarting(a.get_Uri())) a.put_Cancel(true);
        }));
        wv.add_DocumentTitleChanged(new DocumentTitleChangedHandler(() => _host.OnDocumentTitleChanged(wv.get_DocumentTitle())));
        wv.add_WindowCloseRequested(new WindowCloseRequestedHandler(Close));
        // Pagina inicial cargada (bien o con error): se muestra ya, sin esperar al plazo.
        wv.add_NavigationCompleted(new NavigationCompletedHandler(OnNavigationCompleted));

        var scripts = Shim.For(((GlueWindow)_host).Id, "windows") + "\n;" + Shim.FadeIn(_host.Options.FadeInMs);
        foreach (var extra in _host.ExtraScripts) scripts += "\n;" + extra;
        wv.AddScriptToExecuteOnDocumentCreated(scripts, new AddScriptCompletedHandler());

        ResizeWebView();
        controller.put_IsVisible(true);
        controller.MoveFocus(WebView2Const.MOVE_FOCUS_REASON_PROGRAMMATIC);

        wv.Navigate(_pendingUrl ?? _host.StartUrl);
        _pendingUrl = null;
        _host.OnWebViewReady();
    }

    private void OnWebMessage(ICoreWebView2WebMessageReceivedEventArgs args)
    {
        if (args.TryGetWebMessageAsString(out var p) < 0 || p == 0) return;
        string json;
        try { json = Marshal.PtrToStringUni(p) ?? ""; }
        finally { Marshal.FreeCoTaskMem(p); }
        _host.OnWebMessage(json, args.get_Source());
    }

    private void OnResourceRequested(ICoreWebView2Environment env, ICoreWebView2WebResourceRequestedEventArgs args)
    {
        var req = args.get_Request();
        var uri = req.get_Uri();
        if (!GlueWindow.IsSameOrigin(uri, _host.Origin)) return;
        var method = req.get_Method().ToUpperInvariant();
        var body = method is "GET" or "HEAD" ? [] : Win32.ReadAndReleaseStream(req.get_Content());
        var (path, query) = GlueRequest.SplitPath(uri[_host.Origin.Length..]);
        var task = _host.HandleRequestAsync(new GlueRequest(method, path, query, body));
        if (task.IsCompletedSuccessfully)
        {
            args.put_Response(CreateResponse(env, task.Result));
            return;
        }
        var deferral = args.GetDeferral();
        _ = CompleteLater(env, args, deferral, task);
    }

    private async Task CompleteLater(ICoreWebView2Environment env, ICoreWebView2WebResourceRequestedEventArgs args,
        ICoreWebView2Deferral deferral, ValueTask<GlueResponse> task)
    {
        try
        {
            GlueResponse r;
            try { r = await task; }
            catch (Exception e) { r = GlueResponse.Text("500 " + e.Message, status: 500); }
            if (!_destroyed) args.put_Response(CreateResponse(env, r));
        }
        finally { deferral.Complete(); }
    }

    private static unsafe ICoreWebView2WebResourceResponse CreateResponse(ICoreWebView2Environment env, GlueResponse r)
    {
        var headers = new StringBuilder();
        headers.Append("Content-Type: ").Append(r.ContentType).Append("\r\nCache-Control: no-cache");
        if (r.Headers is not null)
            foreach (var (k, v) in r.Headers) headers.Append("\r\n").Append(k).Append(": ").Append(v);
        nint stream;
        fixed (byte* p = r.Body.Span) stream = Win32.SHCreateMemStream(p, (uint)r.Body.Length);
        try
        {
            return env.CreateWebResourceResponse(stream, r.Status, GlueResponse.ReasonPhrase(r.Status), headers.ToString());
        }
        finally
        {
            if (stream != 0) Marshal.Release(stream);
        }
    }

    private void OnNewWindowRequested(ICoreWebView2NewWindowRequestedEventArgs args)
    {
        args.put_Handled(true);
        var uri = args.get_Uri();
        int? w = null, h = null;
        try
        {
            var f = args.get_WindowFeatures();
            if (f.get_HasSize())
            {
                // window.open da el tamano en pixeles CSS, que equivalen a unidades logicas.
                w = (int)f.get_Width();
                h = (int)f.get_Height();
            }
        }
        catch (COMException) { }
        var host = _host;
        _platform.Post(() => host.OnNewWindowRequested(uri, w, h));
    }

    // ------------------------------------------------------------- mensajes de ventana
    [UnmanagedCallersOnly]
    internal static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            if (!Windows.TryGetValue(hwnd, out var w))
            {
                w = _creating;
                if (w is null) return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
            }
            return w.Handle(hwnd, msg, wParam, lParam);
        }
        catch (Exception e)
        {
            WinPlatform.Instance?.Report(e);
            return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    private unsafe nint Handle(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Win32.WM_SIZE:
                ResizeWebView();
                return 0;

            case Win32.WM_MOVE:
            case Win32.WM_MOVING:
                _controller?.NotifyParentWindowPositionChanged();
                break;

            case Win32.WM_SETFOCUS:
                _controller?.MoveFocus(WebView2Const.MOVE_FOCUS_REASON_PROGRAMMATIC);
                return 0;

            case Win32.WM_DPICHANGED:
            {
                var r = (RECT*)lParam;
                Win32.SetWindowPos(hwnd, 0, r->Left, r->Top, r->Width, r->Height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
                return 0;
            }

            case Win32.WM_GETMINMAXINFO:
            {
                var o = _host.Options;
                var mmi = (Win32.MINMAXINFO*)lParam;
                var dpi = Win32.GetDpiForWindow(hwnd);
                if (dpi == 0) break;
                if (o.MinWidth > 0 || o.MinHeight > 0)
                {
                    var r = WindowRectForClient(Math.Max(o.MinWidth, 1), Math.Max(o.MinHeight, 1), dpi);
                    if (o.MinWidth > 0) mmi->ptMinTrackSize.X = r.Width;
                    if (o.MinHeight > 0) mmi->ptMinTrackSize.Y = r.Height;
                }
                if (o.MaxWidth > 0 || o.MaxHeight > 0)
                {
                    var r = WindowRectForClient(Math.Max(o.MaxWidth, 1), Math.Max(o.MaxHeight, 1), dpi);
                    if (o.MaxWidth > 0) mmi->ptMaxTrackSize.X = r.Width;
                    if (o.MaxHeight > 0) mmi->ptMaxTrackSize.Y = r.Height;
                }
                return 0;
            }

            case Win32.WM_SETTINGCHANGE:
                if (lParam != 0 && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
                {
                    _platform.RefreshTheme();
                    ApplyTheme();
                }
                break;

            case Win32.WM_CLOSE:
                if (_host.OnCloseRequested())
                {
                    // La duena se rehabilita antes de destruir, para que recupere el foco.
                    if (_host.Options.Modal && _owner != 0) Win32.EnableWindow(_owner, true);
                    Win32.DestroyWindow(hwnd);
                }
                return 0;

            case Win32.WM_TIMER:
                // Plazo cumplido: se muestra con el color de fondo aunque la pagina aun no este.
                if (wParam == RevealTimerId) { Reveal(); return 0; }
                break;

            case Win32.WM_ERASEBKGND:
                EraseBackground(wParam);
                return 1;

            case Win32.WM_DESTROY:
                _destroyed = true;
                try { _controller?.Close(); } catch (COMException) { }
                _controller = null;
                _webview = null;
                Windows.Remove(hwnd);
                _host.OnClosed();
                return 0;
        }
        return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
