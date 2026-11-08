using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using WolfGlue.Content;
using WolfGlue.Native;

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
        _titleText = o.Title ?? "";
        ApplyTheme();
        UpdateBackdrop();
        ApplyIcon();
        _menu = o.Menu;
        if (o.TitleBar is { } bar) _barSpec = bar;
        if (EffectiveBar is not null) ApplyBar();
    }

    // ------------------------------------------------------------- barra de titulo propia (WinTitleBar)
    private WinTitleBar? _bar;
    private string _titleText = "";
    private bool _active = true;

    internal IWindowHost Host => _host;
    internal bool DarkMode => _platform.DarkMode;
    internal GlueRgb BarBackground => Background;
    internal bool Active => _active;
    /// <summary>Alto de la franja de la barra propia en pixeles fisicos (el contenido empieza debajo).</summary>
    internal int ContentTop => BarPx;
#if DEBUG
    /// <summary>Ejecuta JavaScript en la pagina (solo depuracion; el resultado lo entrega la propia pagina por el puente).</summary>
    internal void DebugRunScript(string js)
    {
        if (_webview is null) throw new InvalidOperationException("el webview aun no esta listo");
        _webview.ExecuteScript(js, new DebugScriptHandler());
    }

    /// <summary>Estado de la ventana para el canal de control de depuracion (rectangulos en pixeles fisicos).</summary>
    internal void DebugWrite(System.Text.Json.Utf8JsonWriter w)
    {
        Win32.GetWindowRect(Hwnd, out var wr);
        Win32.GetClientRect(Hwnd, out var cr);
        w.WriteString("title", _titleText);
        w.WriteNumber("hwnd", Hwnd);
        w.WriteBoolean("visible", Win32.IsWindowVisible(Hwnd));
        w.WriteBoolean("minimized", Win32.IsIconic(Hwnd));
        w.WriteBoolean("maximized", IsMaximized);
        w.WriteBoolean("active", _active);
        w.WriteNumber("dpi", Dpi);
        w.WriteBoolean("darkMode", DarkMode);
        w.WriteStartObject("window"); w.WriteNumber("x", wr.Left); w.WriteNumber("y", wr.Top); w.WriteNumber("width", wr.Width); w.WriteNumber("height", wr.Height); w.WriteEndObject();
        w.WriteStartObject("client"); w.WriteNumber("width", cr.Right); w.WriteNumber("height", cr.Bottom); w.WriteEndObject();
        w.WriteStartObject("backdrop");
        w.WriteString("requested", _host.Backdrop.ToName());
        w.WriteBoolean("active", _backdropActive);
        w.WriteBoolean("containerClipChildren", (Win32.GetWindowLongPtr(_container, Win32.GWL_STYLE) & Win32.WS_CLIPCHILDREN) != 0);
        w.WriteEndObject();
        w.WriteNumber("contentTop", ContentTop);
        if (_bar is null) w.WriteNull("bar");
        else { w.WriteStartObject("bar"); _bar.DebugWrite(w); w.WriteEndObject(); }
    }
#endif
    /// <summary>Ejecuta algo en el hilo de UI, fuera del callback actual (los menus son modales).</summary>
    internal void PostUi(Action a) => _platform.Post(a);
    /// <summary>Devuelve el foco a la pagina.</summary>
    internal void FocusPage() => FocusContent();
    internal string TitleText => _titleText;
    internal bool IsMaximized => Win32.IsZoomed(Hwnd);
    internal bool HasMaximize => (_style & Win32.WS_MAXIMIZEBOX) != 0;
    internal bool HasMinimize => (_style & Win32.WS_MINIMIZEBOX) != 0;
    /// <summary>Icono pequeno: el propio o, si no hay, el de la clase (el del ejecutable).</summary>
    internal nint IconSmall
    {
        get
        {
            if (_iconSmall != 0) return _iconSmall;
            var h = Win32.SendMessageW(Hwnd, Win32.WM_GETICON, 2 /* ICON_SMALL2 */, 0);
            if (h == 0) h = Win32.GetClassLongPtrW(Hwnd, -34 /* GCLP_HICONSM */);
            if (h == 0) h = Win32.GetClassLongPtrW(Hwnd, -14 /* GCLP_HICON */);
            return h;
        }
    }

    /// <summary>Alto de la franja propia en pixeles fisicos (0 sin barra propia).</summary>
    private int BarPx => _bar?.HeightPx ?? 0;

    private GlueTitleBar? _barSpec;
    private IReadOnlyList<GlueMenuItem>? _menu;

    /// <summary>Barra propia declarada; si no hay pero si hay menu de ventana, una barra `title` sin elementos que lo aloje.</summary>
    private GlueTitleBar? EffectiveBar => _barSpec ?? (_menu is { Count: > 0 } ? new GlueTitleBar { Mode = GlueTitleBar.ModeTitle } : null);

    public void SetTitleBar(GlueTitleBar? bar)
    {
        _barSpec = bar;
        ApplyBar();
    }

    public void MenuKey(int kind, uint vk) => _bar?.OnAccelerator(kind, vk, 0, "pagina");

    public void SetMenu(IReadOnlyList<GlueMenuItem>? menu)
    {
        _menu = menu;
        ApplyBar();
    }

    internal void RaiseMenu(string id) => _host.OnMenuEvent(id);

    private void ApplyBar()
    {
        if (_destroyed) return;
        var bar = EffectiveBar;
        if (bar is null)
        {
            if (_bar is null) return;
            _bar.Dispose();
            _bar = null;
        }
        else if (_bar is null) _bar = new WinTitleBar(this, bar);
        else _bar.Set(bar);
        _bar?.SetMenu(_menu);
        if (_backdropActive) ApplyFrameMargins();
        // Vuelve a calcular el area no cliente (WM_NCCALCSIZE) y recoloca el webview bajo la franja.
        Win32.SetWindowPos(Hwnd, 0, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
        ResizeWebView();
        Win32.InvalidateRect(Hwnd, 0, true);
    }

    public void FocusTitleBarItem(string id) => _bar?.Focus(id);

    /// <summary>Devuelve el foco a la pagina (Escape en el campo de busqueda vacio).</summary>
    internal void FocusContent() => _controller?.MoveFocus(WebView2Const.MOVE_FOCUS_REASON_PROGRAMMATIC);

    private readonly nint _container;
    private bool _contentShown;
    private nint _iconSmall, _iconBig;

    /// <summary>
    /// Icono propio (opcion de la ventana o de la app) a los tamanos del sistema para el DPI actual: pequeno para la barra
    /// de titulo y grande para Alt+Tab y la barra de tareas. Sin icono propio queda el de la clase (el del ejecutable).
    /// </summary>
    private void ApplyIcon()
    {
        if (_host.Icon is not { } data) return;
        var dpi = Win32.GetDpiForWindow(Hwnd);
        if (dpi == 0) dpi = 96;
        var small = WinIcon.Create(data, Win32.GetSystemMetricsForDpi(Win32.SM_CXSMICON, dpi));
        var big = WinIcon.Create(data, Win32.GetSystemMetricsForDpi(Win32.SM_CXICON, dpi));
        if (small == 0 || big == 0)
        {
            if (small != 0) WinIcon.DestroyIcon(small);
            if (big != 0) WinIcon.DestroyIcon(big);
            return;
        }
        Win32.SendMessageW(Hwnd, Win32.WM_SETICON, 0 /* ICON_SMALL */, small);
        Win32.SendMessageW(Hwnd, Win32.WM_SETICON, 1 /* ICON_BIG */, big);
        DestroyIcons();
        (_iconSmall, _iconBig) = (small, big);
        _bar?.Invalidate();
    }

    private void DestroyIcons()
    {
        if (_iconSmall != 0) WinIcon.DestroyIcon(_iconSmall);
        if (_iconBig != 0) WinIcon.DestroyIcon(_iconBig);
        _iconSmall = _iconBig = 0;
    }

    // ------------------------------------------------------------- IPlatformWindow
    /// <summary>
    /// La ventana se muestra cuando la pagina inicial ha cargado o, como mucho, a los
    /// <see cref="GlueWindowOptions.ShowDelayMs"/>, pintada con el color de fondo inicial; la pagina aparece con un
    /// fundido desde ese color (ver <see cref="Shim.FadeIn"/>).
    /// </summary>
    public void Show()
    {
        var o = _host.Options;
        UpdateBackdrop(); // la pagina inicial puede haber fijado el fondo
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
        _webview?.PostWebMessageAsString(Shim.FadeInMessage);
        if (_backdropActive) _host.OnBackdropChanged(true); // la pagina nueva recibe el estado
        _bar?.NotifyMenuState(); // idem el estado del menu de ventana (Alt, teclas de acceso)
        if (_contentShown) return;
        _contentShown = true;
        Win32.ShowWindow(_container, Win32.SW_SHOWNA);
        _controller?.MoveFocus(WebView2Const.MOVE_FOCUS_REASON_PROGRAMMATIC);
    }

    public void Close()
    {
        if (!_destroyed) Win32.PostMessageW(Hwnd, Win32.WM_CLOSE, 0, 0);
    }

    public void SetTitle(string title)
    {
        if (_destroyed) return;
        _titleText = title;
        Win32.SetWindowTextW(Hwnd, title);
        _bar?.Invalidate();
    }

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

    public void SetVisible(bool visible)
    {
        if (_destroyed) return;
        if (!visible) { Win32.ShowWindow(Hwnd, Win32.SW_HIDE); return; }
        if (!Win32.IsWindowVisible(Hwnd)) Win32.ShowWindow(Hwnd, _host.Options.Maximized ? Win32.SW_SHOWMAXIMIZED : Win32.SW_SHOWNORMAL);
        Focus();
    }

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
    internal uint Dpi
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
        // Con barra propia no hay titulo ni borde superior del sistema: arriba va la franja (el contenido sigue midiendo lo pedido).
        if (_bar is not null) { r.Bottom += Scale(_bar.LogicalTotalHeight, dpi); r.Top = 0; }
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

    // -------------------------------------------------------------
    // Fondo del sistema (Mica / Acrylic, Windows 11 22H2 o posterior).
    private bool _backdropActive, _backdropNotified;
    private GlueBackdrop _backdropNotifiedRequested;

    internal static bool BackdropSupported => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>Efectos de transparencia de Personalizacion > Colores (si el usuario los quita, el material no se ve).</summary>
    private static bool TransparencyEnabled()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("EnableTransparency") is not int v || v != 0;
        }
        catch { return true; }
    }

    /// <summary>
    /// Con el fondo activo el webview es transparente y compone (DirectComposition) sobre lo que haya en la superficie de la ventana,
    /// que sigue siendo GDI de toda la vida. Esa zona la cubre el contenedor, y con <c>WS_CLIPCHILDREN</c> el contenedor no puede
    /// pintar bajo las ventanas del webview: lo que hubiera debajo (la barra antes de un cambio de DPI, de barra o de fondo) nunca se
    /// invalida. Solo cuando cambia la disposicion (tamano, DPI, barra, fondo) se le quita el recorte un instante, se fuerza el
    /// repintado de toda la zona (en negro, cristal) y se repone: el resto del tiempo conserva <c>WS_CLIPCHILDREN</c>, asi que el
    /// contenido de la pagina (animaciones, canvas) no provoca repintados del contenedor.
    /// </summary>
    private void RepaintUnderWebView(string reason)
    {
        if (_destroyed) return;
        if (DebugLog.Enabled) DebugLog.Event($"repintado forzado bajo el webview ({reason}, fondo activo: {_backdropActive})");
        if (!_backdropActive)
        {
            SetContainerClip(true); // sin efecto, como siempre
            return;
        }
        SetContainerClip(false);                                                                                                       // quitar
        Win32.RedrawWindow(_container, 0, 0, Win32.RDW_INVALIDATE | Win32.RDW_ERASE | Win32.RDW_ALLCHILDREN | Win32.RDW_UPDATENOW);  // pintar
        SetContainerClip(true);                                                                                                        // reponer
    }

    private void SetContainerClip(bool on)
    {
        var style = Win32.GetWindowLongPtr(_container, Win32.GWL_STYLE);
        var wanted = on ? style | (nint)Win32.WS_CLIPCHILDREN : style & ~(nint)Win32.WS_CLIPCHILDREN;
        if (wanted == style) return;
        Win32.SetWindowLongPtr(_container, Win32.GWL_STYLE, wanted);
        Win32.SetWindowPos(_container, 0, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// El material llega al area de cliente con margenes -1 (marco de cristal completo); sin efecto, se devuelve el marco normal.
    /// Con barra propia el cristal no debe incluir la franja superior: si no, DWM dibuja sus botones de ventana encima de los de
    /// la barra (duplicados). Esa franja la pinta WinTitleBar, asi que el margen superior es 0 y los demas cubren todo.
    /// </summary>
    private unsafe void ApplyFrameMargins()
    {
        int* margins = stackalloc int[4];
        if (!_backdropActive) { margins[0] = margins[1] = margins[2] = margins[3] = 0; }
        else if (_bar is not null) { margins[0] = margins[1] = margins[3] = 16384; margins[2] = 0; }
        else { margins[0] = margins[1] = margins[2] = margins[3] = -1; }
        Win32.DwmExtendFrameIntoClientArea(Hwnd, margins);
    }

    public unsafe void UpdateBackdrop()
    {
        if (_destroyed) return;
        var requested = _host.Backdrop;
        bool active = requested != GlueBackdrop.Solid && BackdropSupported && TransparencyEnabled();
        bool changed = active != _backdropActive;
        bool wasActive = _backdropActive;
        if (active || wasActive)
        {
            // 1 = ninguno, 2 = Mica (MAINWINDOW), 3 = Acrylic (TRANSIENTWINDOW)
            int type = !active ? 1 : requested == GlueBackdrop.Tinted ? 2 : 3;
            Win32.DwmSetWindowAttribute(Hwnd, Win32.DWMWA_SYSTEMBACKDROP_TYPE, &type, sizeof(int));
            _backdropActive = active;
            ApplyFrameMargins();
        }
        _backdropActive = active;
        // Con el efecto activo el webview es transparente (se ve el material detras) y el fondo propio se pinta negro (cristal).
        if (changed && _controller is ICoreWebView2Controller2 c2) c2.put_DefaultBackgroundColor(BackgroundColor);
        if (changed) { Win32.InvalidateRect(Hwnd, 0, true); RepaintUnderWebView("cambio de fondo"); }
        if (!_backdropNotified || changed || _backdropNotifiedRequested != requested)
        {
            _backdropNotified = true;
            _backdropNotifiedRequested = requested;
            _host.OnBackdropChanged(active);
        }
    }

    private GlueRgb Background => _host.Background.For(_platform.DarkMode);

    private COREWEBVIEW2_COLOR BackgroundColor
    {
        get
        {
            var c = Background;
            if (_backdropActive) return new() { A = 0, R = c.R, G = c.G, B = c.B };
            return new() { A = 255, R = c.R, G = c.G, B = c.B };
        }
    }

    /// <summary>Pinta el area de cliente con el color de fondo (visible hasta que el webview pinta).</summary>
    private void EraseBackground(nint hdc)
    {
        var c = Background;
        var brush = Win32.CreateSolidBrush(_backdropActive ? 0u : c.R | (uint)c.G << 8 | (uint)c.B << 16);
        Win32.GetClientRect(Hwnd, out var r);
        r.Top = BarPx; // la franja la pinta WinTitleBar (doble bufer, sin parpadeo)
        Win32.FillRect(hdc, ref r, brush);
        Win32.DeleteObject(brush);
    }

    private void ResizeWebView()
    {
        Win32.GetClientRect(Hwnd, out var c);
        int top = BarPx;
        var r = new RECT { Right = c.Width, Bottom = Math.Max(0, c.Height - top) };
        Win32.SetWindowPos(_container, 0, 0, top, r.Width, r.Height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        _controller?.put_Bounds(r);
        _bar?.Invalidate();
        RepaintUnderWebView("tamano, DPI o barra");
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

        // Teclas antes que la pagina: Alt, F10 y Alt+letra para el menu de ventana (WinTitleBar.OnAccelerator).
        controller.add_AcceleratorKeyPressed(new AcceleratorKeyHandler((kind, vk, lParam) =>
        {
            if (DebugLog.Enabled) DebugLog.Event($"AcceleratorKeyPressed kind={kind} vk=0x{vk:X} (barra: {_bar is not null})");
            return _bar?.OnAccelerator(kind, vk, lParam) ?? false;
        }));
        if (DebugLog.Enabled) DebugLog.Event("AcceleratorKeyPressed registrado");

        var s = wv.get_Settings();
        s.put_AreDevToolsEnabled(_host.DevTools);
        s.put_AreDefaultContextMenusEnabled(_host.DevTools);
        s.put_IsStatusBarEnabled(false);
        s.put_IsZoomControlEnabled(false);
        // Comprobacion de reputacion de URL (SmartScreen): retrasa cada navegacion ~2 s y el contenido es propio.
        // Teclas propias de navegador (F5, Ctrl+R, Ctrl+P, Ctrl+F, zoom, F12...): no existen en macOS ni Linux.
        if (s is ICoreWebView2Settings3 s3) s3.put_AreBrowserAcceleratorKeysEnabled(((GlueWindow)_host).App.Options.BrowserKeys);
        if (s is ICoreWebView2Settings8 s8) s8.put_IsReputationCheckingRequired(((GlueWindow)_host).App.Options.UrlReputationCheck);

        wv.add_WebMessageReceived(new WebMessageReceivedHandler(OnWebMessage));
        wv.add_WebResourceRequested(new WebResourceRequestedHandler(args => OnResourceRequested(env, args)));
        wv.AddWebResourceRequestedFilter(_host.Origin + "/*", WebView2Const.WEB_RESOURCE_CONTEXT_ALL);
        wv.add_NewWindowRequested(new NewWindowRequestedHandler(OnNewWindowRequested));
        wv.add_NavigationStarting(new NavigationStartingHandler(a =>
        {
            if (!_host.OnNavigationStarting(a.get_Uri(), UserInitiated(a.get_IsUserInitiated))) a.put_Cancel(true);
        }));
        wv.add_DocumentTitleChanged(new DocumentTitleChangedHandler(() => _host.OnDocumentTitleChanged(wv.get_DocumentTitle())));
        wv.add_WindowCloseRequested(new WindowCloseRequestedHandler(Close));
        // Pagina inicial cargada (bien o con error): se muestra ya, sin esperar al plazo.
        wv.add_NavigationCompleted(new NavigationCompletedHandler(OnNavigationCompleted));

        var scripts = Shim.For(((GlueWindow)_host).Id, "windows", _backdropActive ? _host.Backdrop.ToName() : "solid") + "\n;" + Shim.FadeIn(_host.Options.FadeInMs);
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

    /// <summary>Si la navegacion la inicio el usuario. Ante un error de COM se toma como no iniciada por el usuario.</summary>
    private static bool UserInitiated(Func<bool> get)
    {
        try { return get(); } catch (COMException) { return false; }
    }

    private void OnNewWindowRequested(ICoreWebView2NewWindowRequestedEventArgs args)
    {
        args.put_Handled(true);
        var uri = args.get_Uri();
        var user = UserInitiated(args.get_IsUserInitiated);
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
        _platform.Post(() => host.OnNewWindowRequested(uri, w, h, user));
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

    /// <summary>Mensajes de la barra de titulo propia. Devuelve true si el mensaje queda atendido (con su resultado).</summary>
    private unsafe bool HandleTitleBar(nint hwnd, uint msg, nint wParam, nint lParam, out nint result)
    {
        result = 0;
        var bar = _bar!;
        int x = (short)Win32.LOWORD(lParam), y = (short)Win32.HIWORD(lParam);
        switch (msg)
        {
            case Win32.WM_NCCALCSIZE when wParam != 0:
            {
                // Bordes del sistema a izquierda, derecha y abajo; arriba nada (la franja es area de cliente). Maximizada,
                // la ventana sobresale de la pantalla el grosor del marco: se compensa para que la franja se vea entera.
                var p = (Win32.NCCALCSIZE_PARAMS*)lParam;
                int top = p->rgrc0.Top;
                Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
                p->rgrc0.Top = top;
                if (Win32.IsZoomed(hwnd))
                    p->rgrc0.Top += Win32.GetSystemMetricsForDpi(Win32.SM_CYFRAME, Dpi) + Win32.GetSystemMetricsForDpi(Win32.SM_CXPADDEDBORDER, Dpi);
                return true;
            }
            case Win32.WM_NCHITTEST:
            {
                result = Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
                if (result != Win32.HTCLIENT) return true;
                var pt = new Win32.POINT { X = x, Y = y };
                Win32.ScreenToClient(hwnd, ref pt);
                result = bar.HitTest(pt.X, pt.Y, (_style & Win32.WS_THICKFRAME) != 0);
                return true;
            }
            case Win32.WM_PAINT:
            {
                var hdc = Win32.BeginPaint(hwnd, out var ps);
                bar.Paint(hdc);
                Win32.EndPaint(hwnd, ref ps);
                return true;
            }
            case Win32.WM_ACTIVATE:
                _active = Win32.LOWORD(wParam) != Win32.WA_INACTIVE;
                bar.Invalidate();
                return false;
            case Win32.WM_MOUSEMOVE:
                bar.OnMouseMove(x, y);
                return false;
            case Win32.WM_MOUSELEAVE:
                bar.OnMouseLeave();
                return true;
            case Win32.WM_LBUTTONDOWN:
            case Win32.WM_LBUTTONDBLCLK:
                return bar.OnLButtonDown(x, y);
            case Win32.WM_LBUTTONUP:
                return bar.OnLButtonUp(x, y);
            case Win32.WM_CAPTURECHANGED:
                bar.OnCaptureLost();
                return false;
            case Win32.WM_NCMOUSEMOVE:
                bar.OnNcMouseMove((int)wParam);
                return false;
            case Win32.WM_NCMOUSELEAVE:
                bar.OnNcMouseLeave();
                return false;
            // El boton de maximizar se marca como tal (HTMAXBUTTON) para el menu de distribuciones de Windows 11, pero
            // el clic lo gestiona la barra: el sistema pintaria encima su boton clasico.
            case Win32.WM_NCLBUTTONDOWN when wParam == Win32.HTMAXBUTTON:
            case Win32.WM_NCLBUTTONDBLCLK when wParam == Win32.HTMAXBUTTON:
                bar.OnNcLButtonDown((int)wParam);
                return true;
            case Win32.WM_NCLBUTTONUP when wParam == Win32.HTMAXBUTTON:
                bar.OnNcLButtonUp((int)wParam);
                return true;
            // Campo de busqueda (control EDIT hijo)
            case Win32.WM_COMMAND:
                return bar.OnCommand(wParam, lParam);
            case Win32.WM_CTLCOLOREDIT:
                result = bar.EditColors(wParam, lParam);
                return result != 0;
            case Win32.WM_TIMER when wParam == WinTitleBar.SearchTimerId:
                bar.OnSearchTimer();
                return true;
            case WinTitleBar.MsgSearchSubmit:
                bar.OnSearchSubmit();
                return true;
            case WinTitleBar.MsgSearchEscape:
                bar.OnSearchEscape();
                return true;
        }
        return false;
    }

    private unsafe nint Handle(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (_bar is not null && HandleTitleBar(hwnd, msg, wParam, lParam, out var barResult)) return barResult;
        // El explorador se reinicio: la bandeja hay que ponerla otra vez (el aviso solo llega a ventanas normales).
        if (msg == WinTray.TaskbarCreated) _platform.ServicesImpl.Refresh();
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
                ApplyIcon();
                _bar?.OnDpiChanged();
                ResizeWebView(); // la franja cambia de alto con el DPI: el webview y lo que queda bajo la barra se recalculan y repintan
                Win32.InvalidateRect(hwnd, 0, true);
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
                    UpdateBackdrop();
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

            case 0x0138 /* WM_CTLCOLORSTATIC */ when _backdropActive && lParam == _container:
                // Negro = cristal para DWM: asi el contenedor limpia lo que hubiera debajo del webview transparente (restos de la barra
                // de antes de un cambio de DPI, de barra o de fondo) en lugar de dejarlo como estaba.
                DebugLog.Count("contenedor-pinta");
                return Win32.GetStockObject(4 /* BLACK_BRUSH */);

            // Teclas con el foco en la propia ventana (por ejemplo tras pulsar en la barra): las mismas que llegan por WebView2 cuando
            // el foco esta en la pagina. Alt suelto, F10 y Alt+letra activan el menu de ventana.
            case Win32.WM_KEYDOWN or Win32.WM_KEYUP or Win32.WM_SYSKEYDOWN or Win32.WM_SYSKEYUP when _bar is not null:
            {
                int kind = msg switch { Win32.WM_KEYDOWN => 0, Win32.WM_KEYUP => 1, Win32.WM_SYSKEYDOWN => 2, _ => 3 };
                if (_bar!.OnAccelerator(kind, (uint)wParam, (int)lParam, "ventana")) return 0;
                break;
            }

            case Win32.WM_MENUSELECT:
                WinMenu.OnMenuSelect(wParam, lParam);
                break;

            case Win32.WM_ERASEBKGND:
                DebugLog.Count("ventana-borra");
                EraseBackground(wParam);
                return 1;

            case Win32.WM_DESTROY:
                _destroyed = true;
                try { _controller?.Close(); } catch (COMException) { }
                _controller = null;
                _webview = null;
                Windows.Remove(hwnd);
                DestroyIcons();
                _bar?.Dispose();
                _bar = null;
                _host.OnClosed();
                return 0;
        }
        return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
