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
          htaw <carpeta|htaw.json>          igual que --dev (sirve arrastrar la carpeta sobre htaw)
          htaw pack <carpeta> [-o salida]   crea un .htaw (zip) y valida el manifiesto
          htaw bundle <carpeta|archivo> [-o salida] [--base htaw]
                                            crea un ejecutable portable con la app dentro
          htaw --register | --unregister    asocia (o no) la extension .htaw a htaW
          htaw --forget <archivo|id>        olvida los permisos concedidos
          htaw --version | --help

        Sin argumentos, abre el panel de htaW con estas mismas acciones.
        """, """
        htaW: runs .htaw applications (HTML + desktop API).

        Usage:
          htaw <file.htaw> [args...]        open the application
          htaw --dev <folder|file>          development mode (DevTools, reload on save)
          htaw <folder|htaw.json>           same as --dev (dragging the folder onto htaw works)
          htaw pack <folder> [-o output]    create a .htaw (zip) and validate the manifest
          htaw bundle <folder|file> [-o output] [--base htaw]
                                            create a portable executable with the app inside
          htaw --register | --unregister    associate (or not) the .htaw extension with htaW
          htaw --forget <file|id>           forget the granted permissions
          htaw --version | --help

        Without arguments, it opens the htaW panel with these same actions.
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
    public static string LauncherDevFolder => T("Carpeta de la app en desarrollo", "Folder of the app in development");
    public static string LauncherDevFile => T("Archivo de la app en desarrollo", "File of the app in development");
    public static string LauncherPackFolder => T("Carpeta a empaquetar", "Folder to pack");
    public static string LauncherPackSave => T("Guardar el .htaw", "Save the .htaw");
    public static string IconNotFound(string i) => T($"No se encuentra el icono \"{i}\".", $"Icon \"{i}\" not found.");
    public static string IconTooBig(string i, int kb) => T($"El icono \"{i}\" pasa de {kb} KB.", $"Icon \"{i}\" is larger than {kb} KB.");
    public static string IconBadFormat(string i) => T($"El icono \"{i}\" no es un .png ni un .ico valido.", $"Icon \"{i}\" is not a valid .png or .ico.");
    public static string IconFallback => T(" Se usa el de htaW.", " The htaW icon is used.");
    public static string BundleSameFile(string f) => T($"La salida no puede ser el origen ni la base: {f}", $"The output cannot be the source or the base: {f}");
    public static string BundleIconWindowsOnly => T("El icono del ejecutable de Windows solo se puede poner desde Windows; el resultado lleva el de htaW.", "The Windows executable icon can only be set from Windows; the result keeps the htaW icon.");
    public static string BundleNeedsAot => T("Para crear un ejecutable hace falta el htaw publicado (publish.cmd o build.ps1), o indicarlo con --base <htaw.exe>.", "Creating an executable needs the published htaw (publish.cmd or build.ps1), or pass it with --base <htaw.exe>.");
    public static string Bundled(string f, long size, bool icon) => T($"Creado {f} ({size / 1024.0 / 1024.0:0.0} MB{(icon ? ", con el icono de la app" : "")})", $"Created {f} ({size / 1024.0 / 1024.0:0.0} MB{(icon ? ", with the app icon" : "")})");
    public static string LauncherBundleSave => T("Guardar el ejecutable", "Save the executable");
    public static string LauncherBundleFile => T("Aplicacion a convertir en ejecutable", "Application to turn into an executable");
    public static string ExeFilter => T("Programas", "Programs");
}
