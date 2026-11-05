using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WolfGlue.Platform.Windows;

/// <summary>Backend de Windows: user32 + WebView2.</summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class WinPlatform : IPlatform
{
    internal const string WindowClass = "WolfGlueWindow";
    private const string MessageClass = "WolfGlueMessages";
    private const uint WM_GLUE_INVOKE = Win32.WM_APP + 1;

    internal static WinPlatform? Instance { get; private set; }

    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly List<Action<ICoreWebView2Environment>> _waitingForEnvironment = [];
    private ExceptionDispatchInfo? _pendingError;
    private uint _uiThread;
    private nint _messageWindow;
    private GlueApp _app = null!;
    private bool _quitting;

    internal ICoreWebView2Environment? Environment { get; private set; }
    internal nint HInstance { get; private set; }
    internal bool DarkMode { get; private set; }
    internal nint MessageWindow => _messageWindow;

    private WinServices? _services;
    public IPlatformServices Services => _services ??= new WinServices(this);

    public bool IsUiThread => Win32.GetCurrentThreadId() == _uiThread;

    public void Initialize(GlueApp app)
    {
        _app = app;
        Instance = this;
        _uiThread = Win32.GetCurrentThreadId();

        int hr = Win32.CoInitializeEx(0, Win32.COINIT_APARTMENTTHREADED);
        if (hr == Win32.RPC_E_CHANGED_MODE) throw new InvalidOperationException(Strings.NeedsSta);

        Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        DarkMode = ReadDarkMode();
        HInstance = Win32.GetModuleHandleW(null);
        RegisterClasses();

        _messageWindow = Win32.CreateWindowExW(0, MessageClass, null, 0, 0, 0, 0, 0, Win32.HWND_MESSAGE, 0, HInstance, 0);
        if (_messageWindow == 0) throw new InvalidOperationException("No se pudo crear la ventana de mensajes.");

        CreateEnvironment();
    }

    private void RegisterClasses()
    {
        fixed (char* cls = WindowClass)
        fixed (char* msgCls = MessageClass)
        {
            var wc = new Win32.WNDCLASSEXW
            {
                cbSize = (uint)sizeof(Win32.WNDCLASSEXW),
                lpfnWndProc = &WinWindow.WndProc,
                hInstance = HInstance,
                hCursor = Win32.LoadCursorW(0, Win32.IDC_ARROW),
                hIcon = Win32.LoadIconW(HInstance, 32512), // icono de la aplicacion si existe
                hbrBackground = Win32.CreateSolidBrush(DarkMode ? 0x202020u : 0xFFFFFFu),
                lpszClassName = cls,
            };
            if (wc.hIcon == 0) wc.hIcon = Win32.LoadIconW(0, 32512);
            Win32.RegisterClassExW(&wc);

            var mc = new Win32.WNDCLASSEXW
            {
                cbSize = (uint)sizeof(Win32.WNDCLASSEXW),
                lpfnWndProc = &MessageWndProc,
                hInstance = HInstance,
                lpszClassName = msgCls,
            };
            Win32.RegisterClassExW(&mc);
        }
    }

    // ------------------------------------------------------------- entorno WebView2 (uno por proceso)
    private void CreateEnvironment()
    {
        var dataFolder = _app.Options.DataFolder ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            _app.Options.AppId, "WebView2");
        var handler = new EnvironmentCompletedHandler(OnEnvironmentCreated);
        var ptr = (nint)ComInterfaceMarshaller<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>.ConvertToUnmanaged(handler);
        int hr;
        try
        {
            hr = Win32.CreateCoreWebView2EnvironmentWithOptions(null, dataFolder, 0, ptr);
        }
        catch (DllNotFoundException)
        {
            hr = unchecked((int)0x8007007E); // ERROR_MOD_NOT_FOUND
        }
        finally
        {
            Marshal.Release(ptr);
        }
        if (hr < 0) Post(() => EnvironmentFailed(hr));
    }

    private void OnEnvironmentCreated(int hr, ICoreWebView2Environment? env)
    {
        if (hr < 0 || env is null) { EnvironmentFailed(hr); return; }
        Environment = env;
        foreach (var a in _waitingForEnvironment) a(env);
        _waitingForEnvironment.Clear();
    }

    private void EnvironmentFailed(int hr)
    {
        // 0x80070002 = runtime no encontrado.
        _app.Fatal(hr == unchecked((int)0x80070002) ? Strings.WebView2Missing : Strings.WebViewFailed(hr));
    }

    /// <summary>Ejecuta la accion cuando el entorno este listo (en el acto si ya lo esta).</summary>
    internal void WhenEnvironment(Action<ICoreWebView2Environment> action)
    {
        if (Environment is not null) action(Environment);
        else _waitingForEnvironment.Add(action);
    }

    // ------------------------------------------------------------- bucle
    public int Run()
    {
        while (Win32.GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            Win32.TranslateMessage(msg);
            Win32.DispatchMessageW(msg);
            if (_pendingError is { } err) { _pendingError = null; err.Throw(); }
        }
        _quitting = false;
        return _app.ExitCode;
    }

    public void Quit(int exitCode)
    {
        if (_quitting) return;
        _quitting = true;
        Win32.PostQuitMessage(exitCode);
    }

    public void Post(Action action)
    {
        _queue.Enqueue(action);
        Win32.PostMessageW(_messageWindow, WM_GLUE_INVOKE, 0, 0);
    }

    /// <summary>Guarda una excepcion ocurrida en un callback nativo para relanzarla en el bucle.</summary>
    internal void Report(Exception e) => _pendingError ??= ExceptionDispatchInfo.Capture(e);

    [UnmanagedCallersOnly]
    private static nint MessageWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_GLUE_INVOKE && Instance is { } p)
        {
            while (p._queue.TryDequeue(out var a))
            {
                try { a(); }
                catch (Exception e) { p.Report(e); }
            }
            return 0;
        }
        return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    // ------------------------------------------------------------- resto del contrato
    public IPlatformWindow CreateWindow(GlueWindow window) => new WinWindow(this, window);

    public void OpenExternal(string url) => Win32.ShellExecuteW(0, "open", url, null, null, Win32.SW_SHOWNORMAL);

    public void ShowError(string title, string message) =>
        Win32.MessageBoxW(0, message, title, Win32.MB_OK | Win32.MB_ICONERROR);

    /// <summary>El sistema aviso de un cambio de tema: se relee y se notifica a la app (una vez aunque avisen todas las ventanas).</summary>
    internal void RefreshTheme()
    {
        DarkMode = ReadDarkMode();
        _app.Native.Theme.Refresh();
    }

    internal static bool ReadDarkMode()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }
}
