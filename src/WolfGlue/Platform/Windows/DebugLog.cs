using System.Diagnostics;

namespace WolfGlue.Platform.Windows;

/// <summary>
/// Registro de diagnostico **solo de compilaciones Debug** (<c>dotnet run</c>, pruebas): en Release no existe (las llamadas se
/// eliminan y <see cref="Enabled"/> es siempre false). Escribe en <c>wolfglue-debug.log</c> en el directorio de trabajo del
/// proceso al arrancar (en <c>run.cmd</c>, la raiz del repositorio); la variable <c>WOLFGLUE_DEBUG_LOG</c> cambia el archivo y
/// <c>0</c> lo desactiva. Cada ejecucion empieza con una linea <c>--- sesion</c>. Cuenta los eventos de pintado de la ventana y del
/// contenedor del webview (un resumen por segundo), y escribe en el acto los puntuales: repintados forzados, teclas del menu de
/// ventana y por que via llegan.
/// </summary>
internal static class DebugLog
{
#if DEBUG
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly string? Path = Resolve();
    private static readonly Dictionary<string, int> Counts = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static long _lastFlush;
    private static bool _started;

    public static bool Enabled => Path is not null;

    private static string? Resolve()
    {
        var v = Environment.GetEnvironmentVariable("WOLFGLUE_DEBUG_LOG");
        if (v == "0") return null;
        return string.IsNullOrWhiteSpace(v) ? System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wolfglue-debug.log") : v;
    }

    /// <summary>Cuenta un evento frecuente; cada segundo se escribe el resumen de lo contado.</summary>
    [Conditional("DEBUG")]
    public static void Count(string kind)
    {
        if (Path is null) return;
        Counts[kind] = Counts.GetValueOrDefault(kind) + 1;
        if (Clock.ElapsedMilliseconds - _lastFlush < 1000) return;
        Flush();
    }

    /// <summary>Evento puntual: se escribe en el acto, con lo contado hasta entonces.</summary>
    [Conditional("DEBUG")]
    public static void Event(string what)
    {
        if (Path is null) return;
        Flush();
        Write($"{Stamp()}  EVENTO  {what}");
    }

    private static void Flush()
    {
        _lastFlush = Clock.ElapsedMilliseconds;
        if (Counts.Count == 0) return;
        var sb = new System.Text.StringBuilder(Stamp()).Append("  resumen ");
        foreach (var (k, v) in Counts.OrderBy(p => p.Key)) sb.Append(k).Append('=').Append(v).Append(' ');
        Counts.Clear();
        Write(sb.ToString().TrimEnd());
    }

    private static string Stamp() => (Clock.ElapsedMilliseconds / 1000.0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "s";

    private static void Write(string line)
    {
        try
        {
            if (!_started)
            {
                _started = true;
                // Sin crecer sin limite: un registro de mas de 2 MB se empieza de nuevo.
                if (File.Exists(Path!) && new FileInfo(Path!).Length > MaxBytes) File.Delete(Path!);
                File.AppendAllText(Path!, $"--- sesion {Environment.ProcessId} {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
            }
            File.AppendAllText(Path!, line + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* el diagnostico no debe romper nada */ }
    }
#else
    public static bool Enabled => false;

    [Conditional("DEBUG")]
    public static void Count(string kind) { }

    [Conditional("DEBUG")]
    public static void Event(string what) { }
#endif
}
