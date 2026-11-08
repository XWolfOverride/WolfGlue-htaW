namespace WolfGlue;

/// <summary>Opciones de la aplicacion.</summary>
public sealed class GlueAppOptions
{
    /// <summary>
    /// Identificador de la aplicacion (por ejemplo <c>com.ejemplo.miapp</c>). Se usa para la carpeta de
    /// datos del webview. Obligatorio.
    /// </summary>
    public required string AppId { get; init; }

    /// <summary>
    /// Host del origen propio: el contenido se sirve en <c>https://{Host}/</c>. Todas las ventanas comparten
    /// origen, asi que <c>localStorage</c> y <c>BroadcastChannel</c> funcionan entre ellas.
    /// </summary>
    public string Host { get; init; } = "app.local";

    /// <summary>Cuando termina la aplicacion.</summary>
    public GlueQuitMode QuitMode { get; init; } = GlueQuitMode.MainWindowClosed;

    /// <summary>Activa las DevTools (F12) y el menu contextual del motor.</summary>
    public bool DevTools { get; init; }

    /// <summary>
    /// Origen adicional permitido para navegar (por ejemplo un servidor de desarrollo
    /// <c>http://localhost:5173</c>). Los mensajes del bridge tambien se aceptan desde el.
    /// </summary>
    public string? DevServerOrigin { get; init; }

    /// <summary>Carpeta de datos del webview. Por defecto, la carpeta de datos locales del usuario + AppId.</summary>
    public string? DataFolder { get; init; }

    /// <summary>
    /// Expone los servicios nativos a la web (<c>glue.dialog</c>, <c>glue.clipboard</c>, <c>glue.screens</c>,
    /// <c>glue.theme</c>, <c>glue.shell</c>). Activo por defecto; desactivarlo si la web no es de confianza o si la
    /// aplicacion quiere exponer su propia version con comprobaciones (como htaW).
    /// </summary>
    public bool NativeServicesInWeb { get; init; } = true;

    /// <summary>
    /// Icono (bytes de un .ico o un .png) de las ventanas que no fijan el suyo. Si es null, el del ejecutable.
    /// </summary>
    public byte[]? Icon { get; init; }

    /// <summary>Color inicial de las ventanas que no fijan el suyo (ver <see cref="GlueWindowOptions.Background"/>).</summary>
    public GlueBackground? Background { get; init; }

    /// <summary>
    /// Comprobacion de reputacion de las URL por el webview (SmartScreen en Windows). Desactivada por defecto: el
    /// contenido es propio, las URL externas se abren en el navegador del sistema y la comprobacion retrasa cada
    /// navegacion del orden de 2 s. Activarla si la app navega a sitios de terceros dentro de sus ventanas.
    /// </summary>
    public bool UrlReputationCheck { get; init; }

    /// <summary>
    /// Teclas propias de un navegador en el webview (F5/Ctrl+R recargar, Ctrl+P imprimir, Ctrl+F buscar, Ctrl+/- zoom,
    /// F12 herramientas, Alt+flechas atras/adelante). Desactivadas por defecto: los webviews de macOS y Linux no las
    /// tienen y una app no deberia recargarse con F5. La pagina sigue recibiendo esas teclas (<c>keydown</c>) para usarlas.
    /// </summary>
    public bool BrowserKeys { get; init; }

    /// <summary>
    /// Los enlaces y <c>window.open</c> a URL externas (http, https, mailto) solo abren el navegador del sistema si
    /// los inicio el usuario (un clic o una tecla). Activado por defecto: asi una pagina no puede abrir pestanas por
    /// su cuenta (<c>location.href = …</c> en un temporizador). Lo que la app abra desde C# con
    /// <see cref="GlueApp.OpenExternal"/> no se ve afectado.
    /// </summary>
    public bool ExternalLinksRequireUserAction { get; init; } = true;

    /// <summary>
    /// Identificador de la app en Windows (AppUserModelID) para sus notificaciones y su registro en el sistema; por defecto
    /// <see cref="AppId"/>. La app que lo fije como identificador del proceso debe usar el mismo valor.
    /// </summary>
    public string? AppUserModelId { get; init; }

    /// <summary>Nombre visible de la app en las notificaciones del sistema; por defecto <see cref="AppId"/>.</summary>
    public string? AppName { get; init; }

    /// <summary>Autor de la app (ventana «Acerca de»).</summary>
    public string? Author { get; init; }

    /// <summary>Version de la app (ventana «Acerca de»).</summary>
    public string? Version { get; init; }

    /// <summary>
    /// Pagina propia de «Acerca de» (ruta en el contenido de la app). Si se define, se usa en lugar de la ventana generica y
    /// no lleva el pie «Desarrollado con».
    /// </summary>
    public string? AboutPage { get; init; }

    /// <summary>Tamano de la ventana «Acerca de» propia (unidades logicas).</summary>
    public int AboutWidth { get; init; } = 420;
    public int AboutHeight { get; init; } = 320;

    /// <summary>Nombre que sale en el pie de la ventana generica («Desarrollado con …»).</summary>
    public string PoweredBy { get; init; } = "WolfGlue";

    /// <summary>Icono de la ventana «Acerca de» generica si la app no tiene <see cref="Icon"/>.</summary>
    public byte[]? AboutIcon { get; init; }

    /// <summary>Fondo del sistema por defecto de todas las ventanas (la ventana lo puede cambiar).</summary>
    public GlueBackdrop Backdrop { get; init; } = GlueBackdrop.Solid;

    /// <summary>
    /// Fondo que declara el HTML de la pagina inicial (por ejemplo, un bloque <c>htaw+json</c>); solo cuenta si la ventana no
    /// fija el suyo. Devuelve null si la pagina no dice nada.
    /// </summary>
    public Func<string, GlueBackdrop?>? PageBackdrop { get; init; }

    /// <summary>Menu de ventana que declara el HTML de la pagina inicial; solo cuenta si la ventana no fija el suyo.</summary>
    public Func<string, List<Native.GlueMenuItem>?>? PageMenu { get; init; }

    /// <summary>
    /// Lee la barra de titulo que declara una pagina (su HTML) para las ventanas que no la reciben por opciones. Se llama al
    /// mostrar la ventana, con la pagina inicial si el contenido la da sin esperar; null = sin barra propia. htaW la saca del
    /// bloque de manifiesto del <c>&lt;head&gt;</c> de cada pagina.
    /// </summary>
    public Func<string, GlueTitleBar?>? PageTitleBar { get; init; }
}

/// <summary>Cuando termina <see cref="GlueApp.Run"/>.</summary>
public enum GlueQuitMode
{
    /// <summary>Al cerrarse la ultima ventana.</summary>
    LastWindowClosed,
    /// <summary>Al cerrarse la ventana principal (la primera que se muestra, o <see cref="GlueApp.MainWindow"/>).</summary>
    MainWindowClosed,
    /// <summary>Solo al llamar a <see cref="GlueApp.Quit"/>.</summary>
    Explicit,
}
