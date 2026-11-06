using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using HtaW.Api;
using HtaW.Package;
using HtaW.Security;
using WolfGlue;
using WolfGlue.Native;

namespace HtaW;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] argv)
    {
        // Ejecutable portable: lleva una app dentro y todos los argumentos son de la app (sin linea de comandos de htaW).
        if (EmbeddedApp.Find(Environment.ProcessPath) is { } embedded) return RunEmbedded(embedded, argv);

        var cmd = CliCommand.Parse(argv);
        try
        {
            switch (cmd.Action)
            {
                case CliAction.Run: return RunApp(cmd.Path!, cmd.AppArgs, dev: false);
                case CliAction.Dev: return RunApp(cmd.Path!, cmd.AppArgs, dev: true);
                case CliAction.Choose: return Choose();
            }
        }
        catch (HtawFormatException e)
        {
            GlueApp.ShowError("htaW", Strings.CannotOpen(cmd.Path ?? "") + "\n\n" + e.Message);
            return 1;
        }

        // Comandos de consola: htaw es WinExe, asi que se engancha a la consola desde la que se lanzo.
        AttachParentConsole();
        try
        {
            switch (cmd.Action)
            {
                case CliAction.Help:
                    Console.WriteLine(Strings.Help);
                    return 0;
                case CliAction.Version:
                    Console.WriteLine("htaW " + Version);
                    return 0;
                case CliAction.Pack:
                    var (output, count) = Packer.Pack(cmd.Path!, cmd.Output);
                    Console.WriteLine(Strings.Packed(output, count));
                    return 0;
                case CliAction.Bundle:
                    var b = Bundler.Bundle(cmd.Path!, cmd.Output, cmd.Base);
                    if (b.IconNote is not null) Console.Error.WriteLine(b.IconNote);
                    Console.WriteLine(Strings.Bundled(b.Output, b.Size, b.IconSet));
                    return 0;
                case CliAction.Register:
                    if (!FileAssociation.Register(Environment.ProcessPath!)) { Console.Error.WriteLine(Strings.RegisterUnsupported); return 1; }
                    Console.WriteLine(Strings.Registered);
                    return 0;
                case CliAction.Unregister:
                    if (!FileAssociation.Unregister()) { Console.Error.WriteLine(Strings.RegisterUnsupported); return 1; }
                    Console.WriteLine(Strings.Unregistered);
                    return 0;
                case CliAction.Forget:
                    Console.WriteLine(Strings.Forgotten(Forget(cmd.Path!)));
                    return 0;
                default:
                    Console.Error.WriteLine(cmd.Error);
                    return 1;
            }
        }
        catch (Exception e) when (e is HtawFormatException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    internal static string Version =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    private static int RunEmbedded(EmbeddedApp app, string[] args)
    {
        var exe = Environment.ProcessPath!;
        try
        {
            using var pkg = HtawPackage.OpenEmbedded(exe, app);
            return new HtawHost(pkg, args, dev: false).Run();
        }
        catch (Exception e) when (e is HtawFormatException or InvalidDataException or IOException)
        {
            GlueApp.ShowError(Path.GetFileNameWithoutExtension(exe), Strings.CannotOpen(exe) + "\n\n" + e.Message);
            return 1;
        }
    }

    private static int RunApp(string path, string[] args, bool dev)
    {
        using var pkg = HtawPackage.Open(path, allowFolder: dev);
        // Solo se anota lo que se abre bien: la lista del panel no se llena de rutas que fallan.
        if (dev) new DevRecents(DevRecents.DefaultFile).Add(pkg.SourcePath);
        return new HtawHost(pkg, args, dev).Run();
    }

    /// <summary>Sin argumentos: panel con las acciones de la linea de comandos.</summary>
    private static int Choose() => new Launcher().Run();

    private static int Forget(string target)
    {
        var store = new TrustStore(TrustStore.DefaultFile);
        if (File.Exists(target))
        {
            using var pkg = HtawPackage.Open(target);
            return store.Forget(Convert.ToHexStringLower(pkg.ComputeHash()), pkg.Id);
        }
        return store.Forget(null, target);
    }

    private static void AttachParentConsole()
    {
        if (OperatingSystem.IsWindows()) AttachConsole(-1);
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}
