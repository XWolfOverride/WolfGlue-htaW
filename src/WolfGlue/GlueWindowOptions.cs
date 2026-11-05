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
}
