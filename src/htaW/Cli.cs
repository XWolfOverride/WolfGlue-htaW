namespace HtaW;

public enum CliAction { Run, Dev, Pack, Bundle, Register, Unregister, Forget, Version, Help, Choose, Error }

/// <summary>Linea de comandos ya interpretada.</summary>
public sealed record CliCommand(CliAction Action, string? Path = null, string[]? Args = null, string? Output = null, string? Error = null,
    string? Base = null)
{
    public string[] AppArgs => Args ?? [];

    public static CliCommand Parse(string[] argv)
    {
        if (argv.Length == 0) return new(CliAction.Choose);
        var a0 = argv[0];
        switch (a0)
        {
            case "--help" or "-h" or "/?":
                return new(CliAction.Help);
            case "--version" or "-v":
                return new(CliAction.Version);
            case "--register":
                return new(CliAction.Register);
            case "--unregister":
                return new(CliAction.Unregister);
            case "--forget":
                return argv.Length < 2 ? Err(Strings.MissingArgument(a0)) : new(CliAction.Forget, argv[1]);
            case "--dev":
                return argv.Length < 2 ? Err(Strings.MissingArgument(a0)) : new(CliAction.Dev, ManifestFolder(argv[1]) ?? argv[1], argv[2..]);
            case "pack" or "bundle":
            {
                if (argv.Length < 2) return Err(Strings.MissingArgument(a0));
                bool bundle = a0 == "bundle";
                string? output = null, baseExe = null;
                for (int i = 2; i < argv.Length; i++)
                {
                    if (argv[i] is "-o" or "--output" || (bundle && argv[i] == "--base"))
                    {
                        if (i + 1 >= argv.Length) return Err(Strings.MissingArgument(argv[i]));
                        if (argv[i] == "--base") baseExe = argv[++i];
                        else output = argv[++i];
                    }
                    else return Err(Strings.UnknownOption(argv[i]));
                }
                return new(bundle ? CliAction.Bundle : CliAction.Pack, ManifestFolder(argv[1]) ?? argv[1], Output: output, Base: baseExe);
            }
        }
        if (a0.StartsWith("--", StringComparison.Ordinal)) return Err(Strings.UnknownOption(a0));
        // Todo lo que va detras del archivo es de la app, aunque empiece por "-".
        // Una carpeta o su htaw.json (por ejemplo, arrastrados sobre htaw.exe) se abren en modo desarrollo.
        if (DevFolder(a0) is { } folder) return new(CliAction.Dev, folder, argv[1..]);
        return new(CliAction.Run, a0, argv[1..]);
    }

    /// <summary>Carpeta de una ruta a un <c>htaw.json</c>; null si la ruta no es un htaw.json.</summary>
    internal static string? ManifestFolder(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        if (!System.IO.Path.GetFileName(trimmed).Equals(Package.HtawPackage.ManifestFile, StringComparison.OrdinalIgnoreCase)) return null;
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(trimmed));
        return string.IsNullOrEmpty(dir) ? null : dir;
    }

    /// <summary>Carpeta en modo desarrollo: la ruta si es una carpeta existente, o la de su htaw.json.</summary>
    internal static string? DevFolder(string path) =>
        System.IO.Directory.Exists(path) ? path : ManifestFolder(path) is { } dir && File.Exists(path) ? dir : null;

    private static CliCommand Err(string message) => new(CliAction.Error, Error: message);
}
