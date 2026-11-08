namespace WolfGlue;

/// <summary>
/// Material de fondo del sistema detras de la ventana (DOC/FONDO-DE-VENTANA.md). Si el sistema no puede, la ventana queda
/// solida y la app no tiene que hacer nada.
/// </summary>
public enum GlueBackdrop
{
    /// <summary>Fondo normal de la ventana.</summary>
    Solid,
    /// <summary>Teñido por el fondo de escritorio, para ventanas principales (Windows 11: Mica).</summary>
    Tinted,
    /// <summary>Lo de detras, difuminado, para ventanas auxiliares y paneles (Windows 11: Acrylic).</summary>
    Blurred,
}

public static class GlueBackdropExtensions
{
    /// <summary>Nombre comun (<c>solid</c>, <c>tinted</c>, <c>blurred</c>).</summary>
    public static string ToName(this GlueBackdrop b) => b switch
    {
        GlueBackdrop.Tinted => "tinted",
        GlueBackdrop.Blurred => "blurred",
        _ => "solid",
    };

    /// <summary>Lee el nombre comun; <c>false</c> si no es uno de los tres.</summary>
    public static bool TryParse(string? name, out GlueBackdrop backdrop)
    {
        switch (name)
        {
            case "solid": backdrop = GlueBackdrop.Solid; return true;
            case "tinted": backdrop = GlueBackdrop.Tinted; return true;
            case "blurred": backdrop = GlueBackdrop.Blurred; return true;
            default: backdrop = GlueBackdrop.Solid; return false;
        }
    }
}

/// <summary>Evento <c>glue.backdrop</c>: lo pedido y si el efecto esta activo ahora mismo.</summary>
public sealed class BackdropEvent
{
    public string Requested { get; set; } = "solid";
    public bool Active { get; set; }
}
