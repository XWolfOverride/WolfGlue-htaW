using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HtaW.Api;
using HtaW.Package;
using HtaW.Security;
using WolfGlue;
using WolfGlue.Bridge;
using WolfGlue.Content;
using WolfGlue.Native;

namespace HtaW;

/// <summary>
/// Panel de htaW sin argumentos: las mismas acciones que la linea de comandos (abrir, modo desarrollo, empaquetar,
/// asociar la extension, olvidar permisos) en una ventana propia hecha con htaw.css.
/// </summary>
internal sealed class Launcher
{
    private readonly GlueApp _app;
    private readonly string _exe = Environment.ProcessPath!;
    private string? _lastBundle;

    public Launcher()
    {
        _app = new GlueApp(new GlueAppOptions
        {
            AppId = "htaW",
            Host = "launcher.htaw.local",
            NativeServicesInWeb = false,
            DataFolder = Path.Combine(HtawPaths.Root, "WebView2"),
            Background = HtawHost.HtawBackground,
        });
        _app.Bridge.UseJson(LauncherJson.Default);
        _app.Content = new LauncherContent(() => HtmlInjector.ThemeStyle(_app.Native.Theme.Current));
        Register();
    }

    public int Run()
    {
        _app.Invoke(() => _app.CreateWindow(new GlueWindowOptions
        {
            Title = "htaW",
            Width = 640,
            Height = 680,
            MinWidth = 480,
            MinHeight = 420,
            StartPage = "index.html",
        }).Show());
        return _app.Run();
    }

    private void Register()
    {
        var b = _app.Bridge;
        var d = _app.Native.Dialogs;

        b.Register<JsonElement, LauncherState>("launcher.state", _ => State());

        // Abrir una app: se lanza en su propio proceso y el panel se cierra (hace de selector).
        b.Register<RunArgs, bool>("launcher.open", async (a, ctx) =>
        {
            var path = a.Path;
            if (string.IsNullOrEmpty(path))
            {
                var files = await d.OpenFileAsync(new FileDialogOptions
                {
                    Title = Strings.ChooseTitle,
                    Filters = [new FileFilter(Strings.ChooseFilter, "htaw")],
                }, ctx.Window);
                if (files is not { Length: > 0 }) return false;
                path = files[0];
            }
            Spawn([path, .. CommandLine.Split(a.Args)]);
            _app.Quit(0);
            return true;
        });

        // Modo desarrollo: carpeta o archivo; el panel sigue abierto (se suele volver a el para empaquetar).
        b.Register<RunArgs, string?>("launcher.dev", async (a, ctx) =>
        {
            string? path;
            if (a.Folder)
                path = await d.ChooseFolderAsync(new FolderDialogOptions { Title = Strings.LauncherDevFolder }, ctx.Window);
            else
            {
                var files = await d.OpenFileAsync(new FileDialogOptions
                {
                    Title = Strings.LauncherDevFile,
                    Filters = [new FileFilter(Strings.ChooseFilter, "htaw", "html", "htm")],
                }, ctx.Window);
                path = files is { Length: > 0 } ? files[0] : null;
            }
            if (path is null) return null;
            Spawn(["--dev", path, .. CommandLine.Split(a.Args)]);
            return path;
        });

        // Recientes de desarrollo: solo rutas de la lista (la pagina no puede lanzar rutas arbitrarias).
        b.Register<RunArgs, LauncherState>("launcher.devRecent", a =>
        {
            var recents = new DevRecents(DevRecents.DefaultFile);
            if (a.Path is null || !recents.Contains(a.Path)) throw new GlueException("NotFound", Strings.FileNotFound(a.Path ?? ""));
            Spawn(["--dev", a.Path, .. CommandLine.Split(a.Args)]);
            return State();
        });
        b.Register<string, LauncherState>("launcher.devForget", path =>
        {
            new DevRecents(DevRecents.DefaultFile).Remove(path ?? "");
            return State();
        });

        b.Register<JsonElement, PackResult?>("launcher.pack", async (_, ctx) =>
        {
            var folder = await d.ChooseFolderAsync(new FolderDialogOptions { Title = Strings.LauncherPackFolder }, ctx.Window);
            if (folder is null) return null;
            folder = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Se valida antes de preguntar donde guardar, para no pedir un destino que no servira.
            try { using (HtawPackage.Open(folder, allowFolder: true)) { } }
            catch (HtawFormatException e) { throw new GlueException("InvalidPackage", e.Message); }
            var output = await d.SaveFileAsync(new FileDialogOptions
            {
                Title = Strings.LauncherPackSave,
                DefaultPath = Path.GetDirectoryName(folder),
                FileName = Path.GetFileName(folder) + ".htaw",
                DefaultExtension = "htaw",
                Filters = [new FileFilter(Strings.ChooseFilter, "htaw")],
            }, ctx.Window);
            if (output is null) return null;
            try
            {
                var (file, count) = await Task.Run(() => Packer.Pack(folder, output));
                return new PackResult { Output = file, Count = count };
            }
            catch (HtawFormatException e) { throw new GlueException("InvalidPackage", e.Message); }
        });

        // Ejecutable portable: carpeta o archivo + destino; la base es este mismo htaw (tiene que estar publicado).
        b.Register<RunArgs, BundleInfo?>("launcher.bundle", async (a, ctx) =>
        {
            string? source;
            if (a.Folder)
                source = await d.ChooseFolderAsync(new FolderDialogOptions { Title = Strings.LauncherPackFolder }, ctx.Window);
            else
            {
                var files = await d.OpenFileAsync(new FileDialogOptions
                {
                    Title = Strings.LauncherBundleFile,
                    Filters = [new FileFilter(Strings.ChooseFilter, "htaw", "html", "htm")],
                }, ctx.Window);
                source = files is { Length: > 0 } ? files[0] : null;
            }
            if (source is null) return null;
            source = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            try { using (HtawPackage.Open(source, allowFolder: true)) { } }
            catch (HtawFormatException e) { throw new GlueException("InvalidPackage", e.Message); }
            var name = Directory.Exists(source) ? Path.GetFileName(source) : Path.GetFileNameWithoutExtension(source);
            var output = await d.SaveFileAsync(new FileDialogOptions
            {
                Title = Strings.LauncherBundleSave,
                DefaultPath = Path.GetDirectoryName(source),
                FileName = name + (OperatingSystem.IsWindows() ? ".exe" : ""),
                DefaultExtension = OperatingSystem.IsWindows() ? "exe" : null,
                Filters = OperatingSystem.IsWindows() ? [new FileFilter(Strings.ExeFilter, "exe")] : [],
            }, ctx.Window);
            if (output is null) return null;
            try
            {
                var r = await Task.Run(() => Bundler.Bundle(source, output));
                _lastBundle = r.Output;
                return new BundleInfo { Output = r.Output, Size = r.Size, IconSet = r.IconSet, Note = r.IconNote };
            }
            catch (HtawFormatException e) { throw new GlueException("InvalidPackage", e.Message); }
        });

        b.Register<string>("launcher.reveal", (path, _) => _app.Native.Shell.Reveal(path));
        // Solo el ultimo ejecutable creado desde el panel: la pagina no puede lanzar rutas arbitrarias.
        b.Register<JsonElement>("launcher.runBundle", (_, _) =>
        {
            if (_lastBundle is not null) Process.Start(new ProcessStartInfo(_lastBundle) { UseShellExecute = false });
        });
        b.Register<JsonElement>("launcher.openData", (_, _) =>
        {
            Directory.CreateDirectory(HtawPaths.Root);
            _app.Native.Shell.Open(HtawPaths.Root);
        });

        b.Register<bool, LauncherState>("launcher.register", on =>
        {
            if (!(on ? FileAssociation.Register(_exe) : FileAssociation.Unregister()))
                throw new GlueException("NotSupported", Strings.RegisterUnsupported);
            return State();
        });

        b.Register<string?, LauncherState>("launcher.forget", id =>
        {
            var store = new TrustStore(TrustStore.DefaultFile);
            if (id is null) store.ForgetAll();
            else store.Forget(null, id);
            return State();
        });
    }

    private void Spawn(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(_exe) { UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        Process.Start(psi);
    }

    private LauncherState State()
    {
        var (assoc, target) = FileAssociation.Status(_exe);
        var store = new TrustStore(TrustStore.DefaultFile);
        var ids = store.Apps.Keys.Union(store.Files.Values.Select(f => f.Id)).Order(StringComparer.Ordinal);
        return new LauncherState
        {
            Spanish = Strings.Spanish,
            Version = Program.Version,
            Exe = _exe,
            Os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux",
            Association = assoc.ToString().ToLowerInvariant(),
            AssociationTarget = target,
            DataDir = HtawPaths.Root,
            Help = Strings.Help,
            Recent = [.. new DevRecents(DevRecents.DefaultFile).Load().Select(RecentInfo)],
            Apps = [.. ids.Select(id => new TrustedApp
            {
                Id = id,
                Permissions = store.Apps.TryGetValue(id, out var p) ? p : [],
                Files = [.. store.Files.Values.Where(f => f.Id == id).Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase)],
            })],
        };
    }

    /// <summary>Nombre de la app (del manifiesto) y si la ruta sigue existiendo y abriendose.</summary>
    private static DevRecent RecentInfo(string path)
    {
        var r = new DevRecent { Path = path, Name = Path.GetFileName(path), IsFolder = Directory.Exists(path) };
        r.Exists = r.IsFolder || File.Exists(path);
        if (!r.Exists) return r;
        try
        {
            using var pkg = HtawPackage.Open(path, allowFolder: true);
            r.Name = pkg.DisplayName;
        }
        catch (Exception e) when (e is HtawFormatException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            r.Problem = e.Message;
        }
        return r;
    }

    /// <summary>Pagina del panel (incrustada) con htaw.css y el tema real, como una app con <c>theme: native</c>.</summary>
    private sealed class LauncherContent(Func<string> themeStyle) : IContentProvider
    {
        public ValueTask<GlueResponse?> GetAsync(GlueRequest request) => new(request.Path switch
        {
            "" or "index.html" => new GlueResponse
            {
                ContentType = "text/html; charset=utf-8",
                Body = HtmlInjector.Inject(Assets.Bytes("launcher.html"), HtmlInjector.CssLink + themeStyle()),
            },
            "_htaw/htaw.css" => GlueResponse.Bytes(Assets.Bytes("htaw.css"), "text/css; charset=utf-8"),
            "icon.png" => GlueResponse.Bytes(Assets.Bytes("icon.png"), "image/png"),
            _ => null,
        });
    }
}

/// <summary>Divide una linea de argumentos al estilo de Windows: espacios separan, las comillas agrupan.</summary>
internal static class CommandLine
{
    public static string[] Split(string? line)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(line)) return [];
        var sb = new StringBuilder();
        bool quoted = false, any = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\\' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; any = true; }
            else if (c == '"') { quoted = !quoted; any = true; }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (any) result.Add(sb.ToString());
                sb.Clear();
                any = false;
            }
            else { sb.Append(c); any = true; }
        }
        if (any) result.Add(sb.ToString());
        return [.. result];
    }
}

public sealed class LauncherState
{
    public bool Spanish { get; set; }
    public string Version { get; set; } = "";
    public string Exe { get; set; } = "";
    public string Os { get; set; } = "";
    /// <summary>none | this | other | foreign | unsupported</summary>
    public string Association { get; set; } = "";
    public string? AssociationTarget { get; set; }
    public string DataDir { get; set; } = "";
    public string Help { get; set; } = "";
    public TrustedApp[] Apps { get; set; } = [];
    public DevRecent[] Recent { get; set; } = [];
}

public sealed class DevRecent
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsFolder { get; set; }
    public bool Exists { get; set; }
    /// <summary>Por que no se abriria (manifiesto no valido...), o null.</summary>
    public string? Problem { get; set; }
}

public sealed class TrustedApp
{
    public string Id { get; set; } = "";
    public string[] Permissions { get; set; } = [];
    public string[] Files { get; set; } = [];
}

public sealed class RunArgs
{
    /// <summary>Archivo a abrir; si falta, se pregunta.</summary>
    public string? Path { get; set; }
    /// <summary>Argumentos para la app, en una linea.</summary>
    public string? Args { get; set; }
    /// <summary>Modo desarrollo: elegir carpeta en vez de archivo.</summary>
    public bool Folder { get; set; }
}

public sealed class BundleInfo
{
    public string Output { get; set; } = "";
    public long Size { get; set; }
    public bool IconSet { get; set; }
    public string? Note { get; set; }
}

public sealed class PackResult
{
    public string Output { get; set; } = "";
    public int Count { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LauncherState))]
[JsonSerializable(typeof(RunArgs))]
[JsonSerializable(typeof(PackResult))]
[JsonSerializable(typeof(BundleInfo))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
internal partial class LauncherJson : JsonSerializerContext;
