using WolfGlue.Content;

namespace WolfGlue;

/// <summary>Opciones de una ventana. Los tamanos son unidades logicas (independientes del DPI) del area de contenido.</summary>
public sealed class GlueWindowOptions
{
    /// <summary>Titulo. Si es null, se usa el <c>&lt;title&gt;</c> del documento.</summary>
    public string? Title { get; set; }

    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public int MinWidth { get; set; }
    public int MinHeight { get; set; }
    /// <summary>0 = sin limite.</summary>
    public int MaxWidth { get; set; }
    /// <summary>0 = sin limite.</summary>
    public int MaxHeight { get; set; }

    /// <summary>Posicion (unidades logicas). Si es null, se centra (sobre el dueno si lo hay, si no en la pantalla).</summary>
    public int? X { get; set; }
    public int? Y { get; set; }

    public bool Resizable { get; set; } = true;
    public bool Maximized { get; set; }
    public bool AlwaysOnTop { get; set; }

    /// <summary>
    /// Icono de la barra de titulo y de tareas: bytes de un .ico (se elige el tamano mas cercano) o un .png (se escala).
    /// Si es null, <see cref="GlueAppOptions.Icon"/> o el del ejecutable.
    /// </summary>
    public byte[]? Icon { get; set; }

    /// <summary>Ventana duena: esta ventana queda siempre encima de ella y se minimiza con ella.</summary>
    public GlueWindow? Owner { get; set; }

    /// <summary>Con <see cref="Owner"/>: deshabilita la ventana duena mientras esta este abierta.</summary>
    public bool Modal { get; set; }

    /// <summary>
    /// Contenido que se sirve en el origen propio. Si es null, se usa el de la aplicacion
    /// (<see cref="GlueApp.Content"/>).
    /// </summary>
    public IContentProvider? Content { get; set; }

    /// <summary>Pagina inicial: ruta relativa al origen propio (<c>index.html</c>) o URL completa.</summary>
    public string StartPage { get; set; } = "index.html";

    /// <summary>
    /// Color con el que aparece la ventana mientras carga la pagina (la ventana se muestra al momento). Si es null:
    /// el <c>&lt;meta name="theme-color"&gt;</c> de la pagina inicial, luego <see cref="GlueAppOptions.Background"/>,
    /// luego blanco/gris oscuro segun el tema. Conviene que coincida con el fondo de la pagina.
    /// </summary>
    public GlueBackground? Background { get; set; }

    /// <summary>
    /// Duracion en ms del fundido del contenido desde el color de fondo al cargar cada pagina. 0 lo desactiva. Se omite
    /// si el sistema pide reducir animaciones.
    /// </summary>
    public int FadeInMs { get; set; } = 150;

    /// <summary>
    /// Espera maxima en ms antes de mostrar la ventana: si la pagina carga antes, se muestra entonces (ya con
    /// contenido); si no, aparece con <see cref="Background"/> y la pagina entra con el fundido. 0 = mostrar al momento.
    /// </summary>
    public int ShowDelayMs { get; set; } = 500;

    /// <summary>
    /// Barra de titulo con elementos propios (botones, grupos…) dibujada por WolfGlue. Null = barra normal del sistema.
    /// Se puede cambiar despues con <see cref="GlueWindow.SetTitleBar"/>.
    /// </summary>
    public GlueTitleBar? TitleBar { get; set; }

    /// <summary>
    /// Si no hay <see cref="TitleBar"/>, usar la que declare la pagina inicial (ver <see cref="GlueAppOptions.PageTitleBar"/>).
    /// Por defecto, si.
    /// </summary>
    public bool TitleBarFromPage { get; set; } = true;
}
