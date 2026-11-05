namespace HtaW;

public enum CliAction { Run, Dev, Pack, Register, Unregister, Forget, Version, Help, Choose, Error }

/// <summary>Linea de comandos ya interpretada.</summary>
public sealed record CliCommand(CliAction Action, string? Path = null, string[]? Args = null, string? Output = null, string? Error = null)
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
                return argv.Length < 2 ? Err(Strings.MissingArgument(a0)) : new(CliAction.Dev, argv[1], argv[2..]);
            case "pack":
            {
                if (argv.Length < 2) return Err(Strings.MissingArgument(a0));
                string? output = null;
                for (int i = 2; i < argv.Length; i++)
                {
                    if (argv[i] is "-o" or "--output")
                    {
                        if (i + 1 >= argv.Length) return Err(Strings.MissingArgument(argv[i]));
                        output = argv[++i];
                    }
                    else return Err(Strings.UnknownOption(argv[i]));
                }
                return new(CliAction.Pack, argv[1], Output: output);
            }
        }
        if (a0.StartsWith("--", StringComparison.Ordinal)) return Err(Strings.UnknownOption(a0));
        // Todo lo que va detras del archivo es de la app, aunque empiece por "-".
        return new(CliAction.Run, a0, argv[1..]);
    }

    private static CliCommand Err(string message) => new(CliAction.Error, Error: message);
}
