using System.Globalization;
using System.Runtime.InteropServices;

namespace HtaW;

/// <summary>Textos visibles de htaW en espanol e ingles. Se fuerza con HTAW_LANG=es|en.</summary>
internal static partial class Strings
{
    internal static bool Spanish { get; set; } = Detect();

    private static bool Detect()
    {
        var forced = Environment.GetEnvironmentVariable("HTAW_LANG");
        if (!string.IsNullOrWhiteSpace(forced)) return forced.Trim().StartsWith("es", StringComparison.OrdinalIgnoreCase);
        if (OperatingSystem.IsWindows()) return (GetUserDefaultUILanguage() & 0x3FF) == 0x0A;
        var lang = Environment.GetEnvironmentVariable("LC_ALL") ?? Environment.GetEnvironmentVariable("LC_MESSAGES")
                   ?? Environment.GetEnvironmentVariable("LANG");
        if (!string.IsNullOrEmpty(lang)) return lang.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";
    }

    [LibraryImport("kernel32.dll")]
    private static partial ushort GetUserDefaultUILanguage();

    private static string T(string es, string en) => Spanish ? es : en;

    // ------------------------------------------------------------- formato y manifiesto
    public static string ManifestInvalidJson(string m) => T($"El manifiesto no es JSON valido: {m}", $"The manifest is not valid JSON: {m}");
    public static string ManifestBadId(string id) => T(
        $"Id no valido: \"{id}\". Usa minusculas, digitos, punto y guion (por ejemplo com.ejemplo.app).",
        $"Invalid id: \"{id}\". Use lowercase letters, digits, dots and dashes (for example com.example.app).");
    public static string ManifestBadValue(string field, string value) => T($"Valor no valido en \"{field}\": {value}", $"Invalid value in \"{field}\": {value}");
    public static string WarnFrameNone => T("window.frame \"none\" aun no esta soportado: se usa el marco del sistema.",
        "window.frame \"none\" is not supported yet: the system frame is used.");
    public static string WarnUnknownField(string k) => T($"Campo desconocido en el manifiesto: {k}", $"Unknown manifest field: {k}");
    public static string WarnNoManifest => T("El HTML no tiene bloque <script type=\"application/htaw+json\">: se usan valores por defecto.",
        "The HTML has no <script type=\"application/htaw+json\"> block: defaults are used.");
    public static string WarnEntryIgnored => T("\"entry\" se ignora en un HTML suelto.", "\"entry\" is ignored in a standalone HTML.");
    public static string FolderNeedsDev => T("Para ejecutar una carpeta usa htaw --dev <carpeta>.", "To run a folder use htaw --dev <folder>.");
    public static string MissingManifest(string f) => T($"Falta {f} en la raiz.", $"{f} is missing from the root.");
    public static string MissingEntry(string e) => T($"No existe la pagina inicial \"{e}\".", $"The start page \"{e}\" does not exist.");
    public static string FileNotFound(string p) => T($"No existe: {p}", $"Not found: {p}");
    public static string BadZip(string m) => T($"El archivo zip esta danado: {m}", $"The zip file is damaged: {m}");
    public static string CannotOpen(string file) => T($"No se puede abrir {file}", $"Cannot open {file}");

    // ------------------------------------------------------------- permisos
    public static string UnknownPermission(string n) => T($"Permiso desconocido: {n}", $"Unknown permission: {n}");
    public static string ExecNeedsList => T(
        "\"shell.exec\" necesita la lista de procesos: {\"shell.exec\": [\"git\"]}, o \"*.*\" para todos.",
        "\"shell.exec\" needs the list of processes: {\"shell.exec\": [\"git\"]}, or \"*.*\" for all.");
    public static string PermissionDenied(string p) => T($"La aplicacion no declaro el permiso \"{p}\".", $"The application did not declare the \"{p}\" permission.");
    public static string ExecNotAllowed(string r) => T($"\"{r}\" no esta en la lista de shell.exec.", $"\"{r}\" is not in the shell.exec list.");
    public static string ExecNotFound(string r) => T($"No se encuentra el programa \"{r}\".", $"Program \"{r}\" was not found.");
    public static string ExecBatchNeedsAll(string r) => T(
        $"\"{r}\" es un script de cmd: solo se permite con shell.exec \"*.*\".",
        $"\"{r}\" is a cmd script: it is only allowed with shell.exec \"*.*\".");
    public static string ExecTimeout(string r, int ms) => T($"\"{r}\" supero el tiempo limite ({ms} ms).", $"\"{r}\" timed out ({ms} ms).");
    public static string BadEncoding(string n) => T($"Codificacion desconocida: {n}", $"Unknown encoding: {n}");
    public static string OpenExecutableDenied(string n) => T(
        $"\"{n}\" es un programa o script: abrirlo requiere shell.exec \"*.*\".",
        $"\"{n}\" is a program or script: opening it requires shell.exec \"*.*\".");
    public static string EmptyPath => T("Ruta vacia.", "Empty path.");
    public static string BadUrl(string u) => T($"URL no valida (solo http y https): {u}", $"Invalid URL (only http and https): {u}");

    public static string PermissionLabel(string p) => p switch
    {
        "fs" => T("Leer y escribir archivos", "Read and write files"),
        "dialog" => T("Mostrar dialogos del sistema", "Show system dialogs"),
        "clipboard" => T("Leer y escribir el portapapeles", "Read and write the clipboard"),
        "http" => T("Conectarse a Internet sin restricciones", "Connect to the Internet without restrictions"),
        "env" => T("Leer variables de entorno y carpetas del usuario", "Read environment variables and user folders"),
        "shell.open" => T("Abrir archivos y direcciones con otras aplicaciones", "Open files and addresses with other applications"),
        _ => p,
    };
    public static string PermissionExec(string list) => T($"Ejecutar estos programas: {list}", $"Run these programs: {list}");
    public static string PermissionExecAll => T("Ejecutar cualquier programa", "Run any program");

    // ------------------------------------------------------------- ventana de permisos
    public static string PromptHeader(string name) => T($"La aplicacion \"{name}\" quiere abrirse.", $"The application \"{name}\" wants to open.");
    public static string PromptId => "Id";
    public static string PromptVersion => T("Version", "Version");
    public static string PromptFile => T("Archivo", "File");
    public static string PromptOrigin => T("Origen", "Origin");
    public static string OriginExternal => T("descargado de Internet", "downloaded from the Internet");
    public static string OriginLocal => T("local", "local");
    public static string PromptPermissions => T("Pide permiso para:", "It asks permission to:");
    public static string PromptNoPermissions => T("No pide ningun permiso especial.", "It does not ask for any special permission.");
    public static string PromptDanger => T("Esta aplicacion puede ejecutar cualquier programa con tus permisos.",
        "This application can run any program with your permissions.");
    public static string PromptQuestion => T("¿Permitir y abrir?", "Allow and open?");

    // ------------------------------------------------------------- linea de comandos
    public static string Help => T("""
        htaW: lanza aplicaciones .htaw (HTML + API de escritorio).

        Uso:
          htaw <archivo.htaw> [args...]     abre la aplicacion
          htaw --dev <carpeta|archivo>      modo desarrollo (DevTools, recarga al guardar)
          htaw pack <carpeta> [-o salida]   crea un .htaw (zip) y valida el manifiesto
          htaw --register | --unregister    asocia (o no) la extension .htaw a htaW
          htaw --forget <archivo|id>        olvida los permisos concedidos
          htaw --version | --help

        Sin argumentos, pide el archivo a abrir.
        """, """
        htaW: runs .htaw applications (HTML + desktop API).

        Usage:
          htaw <file.htaw> [args...]        open the application
          htaw --dev <folder|file>          development mode (DevTools, reload on save)
          htaw pack <folder> [-o output]    create a .htaw (zip) and validate the manifest
          htaw --register | --unregister    associate (or not) the .htaw extension with htaW
          htaw --forget <file|id>           forget the granted permissions
          htaw --version | --help

        Without arguments, it asks for the file to open.
        """);
    public static string UnknownOption(string o) => T($"Opcion desconocida: {o} (htaw --help)", $"Unknown option: {o} (htaw --help)");
    public static string MissingArgument(string o) => T($"Falta el argumento de {o}", $"Missing argument for {o}");
    public static string Packed(string file, int count) => T($"Creado {file} ({count} archivos)", $"Created {file} ({count} files)");
    public static string Registered => T("Extension .htaw asociada a htaW para este usuario.", "The .htaw extension is now associated with htaW for this user.");
    public static string Unregistered => T("Asociacion de .htaw eliminada.", ".htaw association removed.");
    public static string RegisterUnsupported => T("--register aun no esta disponible en este sistema.", "--register is not available on this system yet.");
    public static string Forgotten(int n) => T($"Entradas olvidadas: {n}", $"Entries forgotten: {n}");
    public static string ChooseTitle => T("Abrir aplicacion htaW", "Open htaW application");
    public static string ChooseFilter => T("Aplicaciones htaW", "htaW applications");
}
