using WolfGlue.Content;

namespace WolfGlue.Platform;

/// <summary>Backend de un sistema operativo. Contrato interno (ver DOC/ARQUITECTURA.md).</summary>
internal interface IPlatform
{
    /// <summary>Prepara el backend en el hilo actual, que pasa a ser el hilo de UI.</summary>
    void Initialize(GlueApp app);
    /// <summary>Bucle de mensajes. Bloquea hasta <see cref="Quit"/>.</summary>
    int Run();
    void Quit(int exitCode);
    /// <summary>Encola una accion en el hilo de UI. Se puede llamar desde cualquier hilo.</summary>
    void Post(Action action);
    bool IsUiThread { get; }
    IPlatformWindow CreateWindow(GlueWindow window);
    /// <summary>Abre una URL o archivo con la aplicacion por defecto del sistema.</summary>
    void OpenExternal(string url);
    /// <summary>Mensaje de error nativo bloqueante (para cuando no hay webview).</summary>
    void ShowError(string title, string message);
    /// <summary>Servicios nativos (dialogos, portapapeles, pantallas, tema, shell).</summary>
    IPlatformServices Services { get; }
}

/// <summary>
/// Servicios nativos de un SO. Todo se llama en el hilo de UI (la fachada <see cref="Native.GlueNative"/> se encarga);
/// los dialogos son modales y bloquean hasta que se cierran (con bucle de mensajes anidado).
/// </summary>
internal interface IPlatformServices
{
    /// <summary>null si se cancela.</summary>
    string[]? OpenFile(IPlatformWindow? owner, Native.FileDialogOptions options);
    string? SaveFile(IPlatformWindow? owner, Native.FileDialogOptions options);
    string? ChooseFolder(IPlatformWindow? owner, Native.FolderDialogOptions options);
    Native.MessageResult Message(IPlatformWindow? owner, string title, Native.MessageOptions options);
    string? GetClipboardText();
    void SetClipboardText(string text);
    IReadOnlyList<Native.ScreenInfo> GetScreens();
    Native.ThemeInfo GetTheme();
    /// <summary>Abre un archivo o carpeta con la aplicacion por defecto.</summary>
    void Open(string path);
    /// <summary>Muestra el archivo seleccionado en el explorador de archivos.</summary>
    void Reveal(string path);
}

/// <summary>Ventana nativa con su webview.</summary>
internal interface IPlatformWindow
{
    void Show();
    /// <summary>Pide el cierre (pasa por <see cref="IWindowHost.OnCloseRequested"/>).</summary>
    void Close();
    void SetTitle(string title);
    void SetSize(int width, int height);
    void Center();
    void Minimize();
    void Maximize();
    void Restore();
    void Focus();
    void Navigate(string url);
    void PostMessage(string json);
    void OpenDevTools();
}

/// <summary>Lo que la ventana nativa notifica a <see cref="GlueWindow"/>. Todo en el hilo de UI.</summary>
internal interface IWindowHost
{
    GlueWindowOptions Options { get; }
    string StartUrl { get; }
    string Origin { get; }
    bool DevTools { get; }
    void OnWebViewReady();
    void OnWebMessage(string json, string source);
    /// <summary>Devuelve false para cancelar el cierre.</summary>
    bool OnCloseRequested();
    void OnClosed();
    void OnDocumentTitleChanged(string title);
    ValueTask<GlueResponse> HandleRequestAsync(GlueRequest request);
    /// <summary>La pagina pide una ventana nueva (window.open / target=_blank).</summary>
    void OnNewWindowRequested(string url, int? width, int? height);
    /// <summary>Devuelve false para cancelar la navegacion.</summary>
    bool OnNavigationStarting(string url);
}
