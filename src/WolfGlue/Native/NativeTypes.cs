using System.Text.Json;
using System.Text.Json.Serialization;

namespace WolfGlue.Native;

/// <summary>Convierte enums a texto en camelCase (<c>"okCancel"</c>) para la web, sin reflexion.</summary>
public sealed class CamelCaseEnumConverter<T>() : JsonStringEnumConverter<T>(JsonNamingPolicy.CamelCase)
    where T : struct, Enum;

/// <summary>Filtro de un dialogo de archivos: nombre visible y patrones (<c>*.txt</c> o <c>txt</c>).</summary>
public sealed class FileFilter
{
    public string Name { get; set; } = "";
    public string[] Patterns { get; set; } = [];

    public FileFilter() { }
    public FileFilter(string name, params string[] patterns) { Name = name; Patterns = patterns; }

    /// <summary>Patrones normalizados y unidos con <c>;</c> (<c>*.txt;*.md</c>). <c>*</c> si no hay ninguno.</summary>
    public string Spec()
    {
        var parts = Patterns
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => p.Contains('*') || p.Contains('?') ? p : "*." + p.TrimStart('.'))
            .ToArray();
        return parts.Length == 0 ? "*" : string.Join(';', parts);
    }
}

/// <summary>Opciones de los dialogos de abrir y guardar.</summary>
public sealed class FileDialogOptions
{
    public string? Title { get; set; }
    public FileFilter[] Filters { get; set; } = [];
    /// <summary>Carpeta inicial, o ruta de archivo completa (en guardar, se usa tambien como nombre propuesto).</summary>
    public string? DefaultPath { get; set; }
    /// <summary>Guardar: nombre de archivo propuesto.</summary>
    public string? FileName { get; set; }
    /// <summary>Guardar: extension que se anade si el usuario no escribe ninguna (sin punto).</summary>
    public string? DefaultExtension { get; set; }
    /// <summary>Abrir: permitir varios archivos.</summary>
    public bool Multiple { get; set; }
}

/// <summary>Opciones del dialogo de elegir carpeta.</summary>
public sealed class FolderDialogOptions
{
    public string? Title { get; set; }
    public string? DefaultPath { get; set; }
}

[JsonConverter(typeof(CamelCaseEnumConverter<MessageKind>))]
public enum MessageKind { Info, Warning, Error, Question }

[JsonConverter(typeof(CamelCaseEnumConverter<MessageButtons>))]
public enum MessageButtons { Ok, OkCancel, YesNo, YesNoCancel }

[JsonConverter(typeof(CamelCaseEnumConverter<MessageResult>))]
public enum MessageResult { Ok, Cancel, Yes, No }

/// <summary>Mensaje nativo.</summary>
public sealed class MessageOptions
{
    public string Text { get; set; } = "";
    /// <summary>Titulo. Si es null, el de la ventana duena o el AppId.</summary>
    public string? Title { get; set; }
    public MessageKind Kind { get; set; } = MessageKind.Info;
    public MessageButtons Buttons { get; set; } = MessageButtons.Ok;
}

/// <summary>Rectangulo en pixeles fisicos del escritorio virtual.</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height);

/// <summary>Un monitor.</summary>
/// <param name="Bounds">Area completa en pixeles fisicos.</param>
/// <param name="WorkArea">Area util (sin barra de tareas) en pixeles fisicos.</param>
/// <param name="Scale">Factor de escala (1 = 96 ppp, 1,5 = 144 ppp…). Unidades logicas = pixeles / Scale.</param>
public sealed record ScreenInfo(ScreenRect Bounds, ScreenRect WorkArea, double Scale, bool Primary);

/// <summary>Tema del sistema.</summary>
/// <param name="Dark">Las aplicaciones usan el modo oscuro.</param>
/// <param name="Accent">Color de acento <c>#RRGGBB</c>.</param>
/// <param name="FontFamily">Fuente de interfaz del sistema.</param>
/// <param name="FontSize">Tamano de esa fuente en pixeles logicos (CSS).</param>
/// <param name="HighContrast">Modo de alto contraste activo.</param>
public sealed record ThemeInfo(bool Dark, string Accent, string FontFamily, double FontSize, bool HighContrast);
