using System.Globalization;
using System.Runtime.InteropServices;

namespace WolfGlue;

/// <summary>Textos visibles para el usuario, en espanol e ingles. Se fuerza con WOLFGLUE_LANG=es|en.</summary>
internal static partial class Strings
{
    internal static bool Spanish { get; } = DetectSpanish();

    private static bool DetectSpanish()
    {
        var forced = Environment.GetEnvironmentVariable("WOLFGLUE_LANG");
        if (!string.IsNullOrWhiteSpace(forced)) return forced.Trim().StartsWith("es", StringComparison.OrdinalIgnoreCase);
        if (OperatingSystem.IsWindows())
            return (GetUserDefaultUILanguage() & 0x3FF) == 0x0A; // LANG_SPANISH
        var lang = Environment.GetEnvironmentVariable("LC_ALL") ?? Environment.GetEnvironmentVariable("LC_MESSAGES")
                   ?? Environment.GetEnvironmentVariable("LANG");
        if (!string.IsNullOrEmpty(lang)) return lang.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";
    }

    [LibraryImport("kernel32.dll")]
    private static partial ushort GetUserDefaultUILanguage();

    private static string T(string es, string en) => Spanish ? es : en;

    internal static string UnsupportedOs => T("Sistema operativo no soportado.", "Unsupported operating system.");
    internal static string WrongThread => T(
        "Esta operacion debe hacerse en el hilo de UI (usa GlueApp.Invoke).",
        "This operation must run on the UI thread (use GlueApp.Invoke).");
    internal static string WebView2Missing => T(
        "No se encuentra el runtime de Microsoft Edge WebView2, necesario para esta aplicacion.\n\n" +
        "Instalalo desde https://go.microsoft.com/fwlink/p/?LinkId=2124703 y vuelve a abrir la aplicacion.",
        "The Microsoft Edge WebView2 runtime, required by this application, was not found.\n\n" +
        "Install it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and open the application again.");
    internal static string WebViewFailed(int hr) => T(
        $"No se pudo iniciar el motor web (0x{hr:X8}).", $"The web engine could not be started (0x{hr:X8}).");
    internal static string NeedsSta => T(
        "El hilo principal debe ser STA: marca Main con [STAThread].",
        "The main thread must be STA: mark Main with [STAThread].");
}
