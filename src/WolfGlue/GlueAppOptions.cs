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
    public GlueQuitMode QuitMode { get; init; } = GlueQuitMode.LastWindowClosed;

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
    /// Los enlaces y <c>window.open</c> a URL externas (http, https, mailto) solo abren el navegador del sistema si
    /// los inicio el usuario (un clic o una tecla). Activado por defecto: asi una pagina no puede abrir pestanas por
    /// su cuenta (<c>location.href = …</c> en un temporizador). Lo que la app abra desde C# con
    /// <see cref="GlueApp.OpenExternal"/> no se ve afectado.
    /// </summary>
    public bool ExternalLinksRequireUserAction { get; init; } = true;
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
