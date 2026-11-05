using System.Text.Json;
using HtaW.Package;

namespace HtaW.Security;

/// <summary>Permisos declarados en el manifiesto (DOC/HTAW-SEGURIDAD.md).</summary>
public sealed class PermissionSet
{
    /// <summary>Permisos simples que se pueden declarar.</summary>
    public static readonly string[] Known = ["fs", "dialog", "clipboard", "http", "env", "shell.open"];

    private readonly HashSet<string> _simple = new(StringComparer.Ordinal);

    public ExecPolicy? Exec { get; private set; }

    public bool Has(string permission) => permission == "shell.exec" ? Exec is not null : _simple.Contains(permission);

    public IEnumerable<string> Simple => _simple.OrderBy(s => Array.IndexOf(Known, s));

    /// <summary>Lanza <c>PermissionDenied</c> si no se declaro el permiso.</summary>
    public void Require(string permission)
    {
        if (!Has(permission)) throw new WolfGlue.Bridge.GlueException("PermissionDenied", Strings.PermissionDenied(permission));
    }

    /// <summary>Indica si el conjunto permite ejecutar cualquier programa (aviso destacado).</summary>
    public bool IsDangerous => Exec is { } e && e.AllowsAnything;

    /// <summary>Forma canonica para guardar y comparar: <c>fs</c>, <c>shell.exec:git</c>, <c>shell.exec:*</c>.</summary>
    public IReadOnlySet<string> Canonical()
    {
        var set = new SortedSet<string>(_simple, StringComparer.Ordinal);
        if (Exec is { } e)
        {
            if (e.All) set.Add("shell.exec:*");
            foreach (var n in e.Names) set.Add("shell.exec:" + n.ToLowerInvariant());
            foreach (var p in e.Paths) set.Add("shell.exec:" + p.ToLowerInvariant());
        }
        return set;
    }

    public static PermissionSet FromManifest(IEnumerable<JsonElement> items)
    {
        var set = new PermissionSet();
        foreach (var item in items)
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var name = item.GetString()!;
                if (name == "shell.exec") throw new HtawFormatException(Strings.ExecNeedsList);
                if (!Known.Contains(name)) throw new HtawFormatException(Strings.UnknownPermission(name));
                set._simple.Add(name);
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in item.EnumerateObject())
                {
                    if (prop.Name != "shell.exec") throw new HtawFormatException(Strings.UnknownPermission(prop.Name));
                    set.Exec = ExecPolicy.Parse(prop.Value, set.Exec);
                }
            }
            else throw new HtawFormatException(Strings.UnknownPermission(item.GetRawText()));
        }
        return set;
    }
}

/// <summary>
/// Que procesos puede ejecutar la app. Se compara el ejecutable ya resuelto, no el texto que manda la web.
/// </summary>
public sealed class ExecPolicy
{
    /// <summary>
    /// Ejecutables que permiten ejecutar cualquier cosa: pedirlos equivale a pedir <c>*.*</c>.
    /// Unica lista; se puede ampliar.
    /// </summary>
    public static readonly string[] Interpreters =
    [
        "cmd", "powershell", "pwsh", "bash", "sh", "zsh", "fish", "dash", "ksh", "csh", "tcsh",
        "python", "python3", "py", "pythonw", "node", "deno", "bun", "perl", "ruby", "php", "lua",
        "wscript", "cscript", "mshta", "rundll32", "regsvr32", "msiexec", "osascript", "env", "sudo", "doas",
        "xargs", "find", "dotnet", "java", "javaw", "wsl", "start", "explorer", "open", "xdg-open",
    ];

    private static StringComparison Cmp => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public bool All { get; private set; }
    public List<string> Names { get; } = [];
    public List<string> Paths { get; } = [];

    /// <summary>Cualquier programa: <c>*.*</c>, <c>""</c> o un interprete/shell en la lista.</summary>
    public bool AllowsAnything => All || Names.Any(IsInterpreter) || Paths.Any(p => IsInterpreter(Path.GetFileName(p)));

    public static bool IsInterpreter(string name)
    {
        var n = Path.GetFileNameWithoutExtension(name);
        // python3.12, pwsh-preview… cuentan como su familia
        return Interpreters.Any(i => n.Equals(i, StringComparison.OrdinalIgnoreCase)
            || (n.StartsWith(i, StringComparison.OrdinalIgnoreCase) && n.Length > i.Length && !char.IsLetter(n[i.Length])));
    }

    private static bool IsAllMarker(string s) => s.Length == 0 || s == "*.*" || s == "*";

    internal static ExecPolicy Parse(JsonElement value, ExecPolicy? existing)
    {
        var p = existing ?? new ExecPolicy();
        if (value.ValueKind == JsonValueKind.String)
        {
            var s = value.GetString()!.Trim();
            if (!IsAllMarker(s)) throw new HtawFormatException(Strings.ExecNeedsList);
            p.All = true;
            return p;
        }
        if (value.ValueKind != JsonValueKind.Array) throw new HtawFormatException(Strings.ExecNeedsList);
        foreach (var e in value.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.String) throw new HtawFormatException(Strings.ExecNeedsList);
            var s = e.GetString()!.Trim();
            if (IsAllMarker(s)) p.All = true;
            else if (s.Contains('/') || s.Contains('\\')) p.Paths.Add(Path.GetFullPath(s));
            else p.Names.Add(s);
        }
        return p;
    }

    /// <summary>
    /// Resuelve el ejecutable pedido y comprueba que esta permitido. Devuelve la ruta completa.
    /// Lanza <c>PermissionDenied</c> o <c>NotFound</c>.
    /// </summary>
    public string Resolve(string requested, string? pathEnv = null, string? pathExtEnv = null)
    {
        if (string.IsNullOrWhiteSpace(requested)) throw new WolfGlue.Bridge.GlueException("InvalidArgument", "exec: nombre vacio");
        bool hasDir = requested.Contains('/') || requested.Contains('\\') || Path.IsPathRooted(requested);
        if (hasDir)
        {
            var full = Path.GetFullPath(requested);
            if (!All && !Paths.Contains(full, Comparer))
                throw new WolfGlue.Bridge.GlueException("PermissionDenied", Strings.ExecNotAllowed(requested));
            if (!File.Exists(full)) throw new FileNotFoundException(Strings.ExecNotFound(requested), full);
            return full;
        }
        var bare = Path.GetFileNameWithoutExtension(requested);
        bool allowed = All || Names.Any(n => n.Equals(requested, Cmp) || Path.GetFileNameWithoutExtension(n).Equals(bare, Cmp));
        if (!allowed) throw new WolfGlue.Bridge.GlueException("PermissionDenied", Strings.ExecNotAllowed(requested));
        return FindInPath(requested, pathEnv, pathExtEnv)
               ?? throw new FileNotFoundException(Strings.ExecNotFound(requested), requested);
    }

    /// <summary>Busca un nombre sin ruta en el PATH (con PATHEXT en Windows). No mira la carpeta actual.</summary>
    public static string? FindInPath(string name, string? pathEnv = null, string? pathExtEnv = null)
    {
        pathEnv ??= Environment.GetEnvironmentVariable("PATH") ?? "";
        string[] exts = [""];
        if (OperatingSystem.IsWindows())
        {
            var pe = (pathExtEnv ?? Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries);
            exts = Path.HasExtension(name) ? ["", .. pe] : pe;
        }
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var d = dir.Trim().Trim('"');
            if (d.Length == 0) continue;
            foreach (var ext in exts)
            {
                string candidate;
                try { candidate = Path.Combine(d, name + ext); }
                catch (ArgumentException) { continue; }
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        }
        return null;
    }

    /// <summary>
    /// <c>shell.open</c> no debe ejecutar programas: extensiones ejecutables o scripts (o bit de ejecucion en Unix).
    /// </summary>
    public static bool IsExecutableFile(string path)
    {
        string[] exts = [".exe", ".com", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
            ".msi", ".msp", ".lnk", ".url", ".scr", ".pif", ".cpl", ".hta", ".htaw", ".jar", ".reg", ".appref-ms",
            ".sh", ".command", ".app", ".desktop", ".run", ".bin", ".appimage", ".py", ".pl", ".rb"];
        var ext = Path.GetExtension(path);
        if (exts.Contains(ext, StringComparer.OrdinalIgnoreCase)) return true;
        if (!OperatingSystem.IsWindows() && File.Exists(path))
        {
            try { return (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0; }
            catch { return true; }
        }
        return false;
    }
}
