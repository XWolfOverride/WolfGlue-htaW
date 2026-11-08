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
    /// <summary>Imagen del portapapeles como PNG, o null si no hay.</summary>
    byte[]? GetClipboardImage();
    /// <summary>Pone una imagen PNG en el portapapeles (sustituye lo que hubiera).</summary>
    void SetClipboardImage(byte[] png);
    IReadOnlyList<Native.ScreenInfo> GetScreens();
    Native.ThemeInfo GetTheme();
    /// <summary>Abre un archivo o carpeta con la aplicacion por defecto.</summary>
    void Open(string path);
    /// <summary>Muestra el archivo seleccionado en el explorador de archivos.</summary>
    void Reveal(string path);
    /// <summary>Muestra (o sustituye, si ya hay una con ese id) una notificacion del sistema.</summary>
    void ShowNotification(string id, Native.GlueNotification n);
    void CloseNotification(string id);
    void CloseAllNotifications();
    Native.NotificationPermission GetNotificationPermission();
    /// <summary>Hay bandeja del sistema.</summary>
    bool TrayAvailable { get; }
    /// <summary>Pone, cambia (misma llamada) o quita (null) el icono de la bandeja.</summary>
    void SetTray(Native.GlueTray? tray);
    /// <summary>Menu contextual modal; devuelve el id elegido o null. <paramref name="position"/> es relativa al contenido de <paramref name="owner"/> (unidades logicas).</summary>
    string? ShowMenu(IPlatformWindow? owner, IReadOnlyList<Native.GlueMenuItem> items, (double X, double Y)? position);
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
    /// <summary>Recarga la pagina actual.</summary>
    void Reload();
    /// <summary>Pone, cambia o quita (null) la barra de titulo propia. Tambien tras cambiar un elemento.</summary>
    void SetTitleBar(GlueTitleBar? bar);
    /// <summary>Da el foco a un elemento de la barra (el campo de busqueda).</summary>
    void FocusTitleBarItem(string id);
    /// <summary>Oculta o vuelve a mostrar (y pone delante) la ventana.</summary>
    void SetVisible(bool visible);
    /// <summary>Vuelve a leer <see cref="IWindowHost.Backdrop"/> y lo aplica.</summary>
    void UpdateBackdrop();
    /// <summary>Pone, cambia o quita (null) el menu de ventana (render propio dentro de la barra de titulo).</summary>
    void SetMenu(IReadOnlyList<Native.GlueMenuItem>? menu);
    /// <summary>Tecla que la pagina reenvia al menu de ventana (el webview se queda con Alt y las flechas cuando tiene el foco).</summary>
    void MenuKey(int kind, uint vk);
}

/// <summary>Lo que la ventana nativa notifica a <see cref="GlueWindow"/>. Todo en el hilo de UI.</summary>
internal interface IWindowHost
{
    GlueWindowOptions Options { get; }
    string StartUrl { get; }
    string Origin { get; }
    bool DevTools { get; }
    /// <summary>Scripts que se inyectan tras el shim en cada documento.</summary>
    IReadOnlyList<string> ExtraScripts { get; }
    /// <summary>Color de fondo inicial resuelto (opcion, theme-color, app, sistema).</summary>
    GlueBackground Background { get; }
    /// <summary>Icono (.ico o .png) de la ventana o de la app; null = el del ejecutable.</summary>
    byte[]? Icon { get; }
    void OnWebViewReady();
    void OnWebMessage(string json, string source);
    /// <summary>Devuelve false para cancelar el cierre.</summary>
    bool OnCloseRequested();
    void OnClosed();
    void OnDocumentTitleChanged(string title);
    ValueTask<GlueResponse> HandleRequestAsync(GlueRequest request);
    /// <summary>La pagina pide una ventana nueva (window.open / target=_blank).</summary>
    void OnNewWindowRequested(string url, int? width, int? height, bool userInitiated);
    /// <summary>Devuelve false para cancelar la navegacion. <paramref name="userInitiated"/>: la inicio el usuario (clic, tecla).</summary>
    bool OnNavigationStarting(string url, bool userInitiated);
    /// <summary>Evento de la barra de titulo propia (clic, cambio o Intro en la busqueda).</summary>
    void OnTitleBarEvent(GlueTitleBarEvent e);
    /// <summary>Bytes de un archivo de la barra (icono o fuente; ruta del origen propio o URL data:); null si no se encuentra.</summary>
    Task<byte[]?> LoadTitleBarFileAsync(string src);
    /// <summary>Fondo del sistema pedido (propio de la ventana, de la pagina o de la app).</summary>
    GlueBackdrop Backdrop { get; }
    /// <summary>El efecto esta activo ahora mismo.</summary>
    bool BackdropActive { get; }
    /// <summary>El backend informa: el efecto esta (o deja de estar) activo, o ha cambiado lo pedido.</summary>
    void OnBackdropChanged(bool active);
    /// <summary>Se eligio una opcion del menu de ventana.</summary>
    void OnMenuEvent(string id);
    /// <summary>El estado del menu de ventana cambio: puesto o no, foco del teclado y letras de acceso.</summary>
    void OnMenuState(bool on, bool keyMode, string mnemonics);
}
