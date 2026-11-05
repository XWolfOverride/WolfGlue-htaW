using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HtaW.Package;
using HtaW.Security;
using WolfGlue;
using WolfGlue.Bridge;
using WolfGlue.Content;
using WolfGlue.Native;

namespace HtaW.Api;

/// <summary>Ejecuta una app .htaw: crea la GlueApp, la API <c>htaw.*</c> con permisos, el contenido y la ventana.</summary>
internal sealed class HtawHost
{
    private readonly HtawPackage _pkg;
    private readonly string[] _args;
    private readonly bool _dev;
    private readonly GlueApp _app;
    private readonly PermissionSet _perms;
    private readonly Processes _procs;
    private readonly Dictionary<int, FileSystemWatcher> _watchers = [];
    private Dictionary<string, JsonElement>? _settings;
    private int _nextWatch;
    private FileSystemWatcher? _devWatcher;
    private Timer? _devReload;

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public HtawHost(HtawPackage pkg, string[] args, bool dev)
    {
        _pkg = pkg;
        _args = args;
        _dev = dev;
        _perms = pkg.Permissions;
        _procs = new Processes(_perms, pkg.Directory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        _app = new GlueApp(new GlueAppOptions
        {
            AppId = "htaW",
            Host = pkg.Host,
            DevTools = dev,
            NativeServicesInWeb = false, // htaW expone sus versiones con permisos
            DataFolder = Path.Combine(HtawPaths.Root, "WebView2"),
        });
        _app.Bridge.UseJson(ApiJson.Default);
        _app.Content = new HtawContent(pkg, _perms, () => HtmlInjector.ThemeStyle(_app.Native.Theme.Current));
        RegisterApi();
    }

    public int Run()
    {
        _app.InvokeAsync(StartAsync);
        int code = _app.Run();
        _procs.KillAll();
        foreach (var w in _watchers.Values) w.Dispose();
        _devWatcher?.Dispose();
        return code;
    }

    private async Task StartAsync()
    {
        // Confirmacion de permisos (no en modo desarrollo: ahi se aplican, pero no se pregunta).
        if (!_dev && !await CheckTrustAsync())
        {
            _app.Quit(2);
            return;
        }
        _app.Scripts.Add(BuildInitScript() + "\n" + Assets.Read("htaw.js"));
        var w = _pkg.Manifest.Window;
        var win = _app.CreateWindow(new GlueWindowOptions
        {
            Title = _pkg.Manifest.Name,
            Width = w.Width,
            Height = w.Height,
            MinWidth = w.MinWidth,
            MinHeight = w.MinHeight,
            MaxWidth = w.MaxWidth ?? 0,
            MaxHeight = w.MaxHeight ?? 0,
            Resizable = w.Resizable,
            Maximized = w.Maximized,
            AlwaysOnTop = w.AlwaysOnTop,
            StartPage = _pkg.Entry,
        });
        win.Show();
        if (_dev) StartDevWatcher();
    }

    private async Task<bool> CheckTrustAsync()
    {
        var store = new TrustStore(TrustStore.DefaultFile);
        var canonical = _perms.Canonical();
        var hash = Convert.ToHexStringLower(_pkg.ComputeHash());
        var origin = _pkg.Kind == PackageKind.Folder ? FileOrigin.Local : OriginDetector.Detect(_pkg.SourcePath);
        if (!store.NeedsPrompt(hash, _pkg.Id, canonical, origin))
        {
            if (origin == FileOrigin.Local) store.RememberApp(_pkg.Id, canonical);
            return true;
        }
        var r = await _app.Native.Dialogs.MessageAsync(new MessageOptions
        {
            Title = "htaW",
            Text = PermissionPrompt.Text(_pkg, _perms, origin),
            Kind = _perms.IsDangerous || origin == FileOrigin.External ? MessageKind.Warning : MessageKind.Question,
            Buttons = MessageButtons.OkCancel,
            DefaultButton = 1, // Cancelar
        });
        if (r != MessageResult.Ok) return false;
        store.Accept(hash, _pkg.Id, _pkg.SourcePath, canonical);
        return true;
    }

    private string BuildInitScript()
    {
        var init = new HtawInit
        {
            App = new InitApp
            {
                Id = _pkg.Id,
                Name = _pkg.DisplayName,
                Version = _pkg.Manifest.Version,
                Args = _args,
                Dir = _pkg.Directory,
                File = _pkg.SourcePath,
            },
            Permissions = [.. _perms.Canonical()],
            Theme = _pkg.Manifest.Theme == "native" ? _app.Native.Theme.Current : null,
            Os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux",
            Warnings = [.. _pkg.Warnings],
            Dev = _dev,
        };
        if (_perms.Has("env"))
        {
            init.Env = new InitEnv
            {
                Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Temp = Path.GetTempPath(),
                Documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Os = init.Os,
                Arch = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            };
        }
        return "window.__htawInit=" + JsonSerializer.Serialize(init, ApiJson.Default.HtawInit) + ";";
    }

    private void StartDevWatcher()
    {
        var dir = _pkg.Directory;
        if (dir is null) return;
        _devWatcher = new FileSystemWatcher(dir) { IncludeSubdirectories = true, EnableRaisingEvents = true };
        void Changed(object? s, FileSystemEventArgs e)
        {
            if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}.git", StringComparison.Ordinal)) return;
            // Agrupa rafagas de cambios (los editores guardan en varios pasos).
            _devReload?.Dispose();
            _devReload = new Timer(_ => _app.Invoke(() => { foreach (var w in _app.Windows) w.Reload(); }), null, 300, Timeout.Infinite);
        }
        _devWatcher.Changed += Changed;
        _devWatcher.Created += Changed;
        _devWatcher.Renamed += (s, e) => Changed(s, e);
        _devWatcher.Deleted += Changed;
    }

    // ================================================================== API
    private void RegisterApi()
    {
        var b = _app.Bridge;
        var n = _app.Native;

        // ------------------------------------------------------------- app y ventanas (sin permiso)
        b.Register<JsonElement>("htaw.app.relaunch", (_, _) =>
        {
            var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            if (_dev) psi.ArgumentList.Add("--dev");
            psi.ArgumentList.Add(_pkg.SourcePath);
            foreach (var a in _args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            _app.Quit(0);
        });

        // ------------------------------------------------------------- dialogos
        b.Register<FileDialogOptions, string[]?>("htaw.dialog.open", (o, ctx) => Need("dialog", () => n.Dialogs.OpenFileAsync(o, ctx.Window)));
        b.Register<FileDialogOptions, string?>("htaw.dialog.save", (o, ctx) => Need("dialog", () => n.Dialogs.SaveFileAsync(o, ctx.Window)));
        b.Register<FolderDialogOptions, string?>("htaw.dialog.folder", (o, ctx) => Need("dialog", () => n.Dialogs.ChooseFolderAsync(o, ctx.Window)));
        b.Register<MessageOptions, MessageResult>("htaw.dialog.message", (o, ctx) => Need("dialog", () => n.Dialogs.MessageAsync(o, ctx.Window)));

        // ------------------------------------------------------------- portapapeles
        b.Register<JsonElement, string?>("htaw.clipboard.readText", (_, _) => Need("clipboard", () => n.Clipboard.GetTextAsync()));
        b.Register<string>("htaw.clipboard.writeText", (t, _) => Need("clipboard", () => n.Clipboard.SetTextAsync(t ?? "")));

        // ------------------------------------------------------------- shell
        b.Register<string>("htaw.shell.open", (target, _) => { _perms.Require("shell.open"); ShellOpen(target); });
        b.Register<string>("htaw.shell.reveal", (path, _) => { _perms.Require("shell.open"); n.Shell.Reveal(FullPath(path)); });
        b.Register<ExecArgs, ExecResult>("htaw.shell.exec", (a, ctx) => _procs.ExecAsync(a, ctx.Cancellation));
        b.Register<ExecArgs, int>("htaw.shell.spawn", (a, ctx) =>
            Task.FromResult(_procs.Spawn(a, ctx.Window ?? throw new GlueException("InvalidArgument", "sin ventana"))));
        b.Register<ProcWrite>("htaw.shell.write", (w, _) => _procs.WriteAsync(w));
        b.Register<int>("htaw.shell.closeInput", (id, _) => _procs.CloseInput(id));
        b.Register<int>("htaw.shell.kill", (id, _) => _procs.Kill(id));

        // ------------------------------------------------------------- archivos
        b.Register<string, string>("htaw.fs.readText", (p, _) => Need("fs", () => File.ReadAllTextAsync(FullPath(p))));
        b.Register<FsWriteText>("htaw.fs.writeText", (a, _) =>
        {
            _perms.Require("fs");
            var full = FullPath(a.Path);
            return a.Append ? File.AppendAllTextAsync(full, a.Text) : File.WriteAllTextAsync(full, a.Text);
        });
        b.Register<string, FsEntry[]>("htaw.fs.list", p => { _perms.Require("fs"); return List(FullPath(p)); });
        b.Register<string, FsStat>("htaw.fs.stat", p => { _perms.Require("fs"); return Stat(FullPath(p)); });
        b.Register<string, bool>("htaw.fs.exists", p =>
        {
            _perms.Require("fs");
            var f = FullPath(p);
            return File.Exists(f) || Directory.Exists(f);
        });
        b.Register<string>("htaw.fs.mkdir", (p, _) => { _perms.Require("fs"); Directory.CreateDirectory(FullPath(p)); });
        b.Register<FsRemove>("htaw.fs.remove", (a, _) =>
        {
            _perms.Require("fs");
            var f = FullPath(a.Path);
            if (Directory.Exists(f)) Directory.Delete(f, a.Recursive);
            else if (File.Exists(f)) File.Delete(f);
            else throw new FileNotFoundException(f, f);
        });
        b.Register<FsTwoPaths>("htaw.fs.rename", (a, _) =>
        {
            _perms.Require("fs");
            string from = FullPath(a.From), to = FullPath(a.To);
            if (Directory.Exists(from)) Directory.Move(from, to);
            else File.Move(from, to, a.Overwrite);
        });
        b.Register<FsTwoPaths>("htaw.fs.copy", (a, _) =>
        {
            _perms.Require("fs");
            File.Copy(FullPath(a.From), FullPath(a.To), a.Overwrite);
        });
        b.Register<FsWatchArgs, int>("htaw.fs.watch", (a, ctx) =>
            Task.FromResult(Watch(a, ctx.Window ?? throw new GlueException("InvalidArgument", "sin ventana"))));
        b.Register<int>("htaw.fs.unwatch", (id, _) =>
        {
            if (_watchers.Remove(id, out var w)) w.Dispose();
        });

        // ------------------------------------------------------------- ajustes (sin permiso: solo de esta app)
        b.Register<string, JsonElement?>("htaw.settings.get", key => Settings.TryGetValue(key ?? "", out var v) ? v : null);
        b.Register<SettingsSet>("htaw.settings.set", (s, _) =>
        {
            if (string.IsNullOrEmpty(s.Key)) throw new GlueException("InvalidArgument", "key");
            Settings[s.Key] = s.Value.Clone();
            SaveSettings();
        });
        b.Register<string>("htaw.settings.remove", (key, _) =>
        {
            if (Settings.Remove(key ?? "")) SaveSettings();
        });
        b.Register<JsonElement, Dictionary<string, JsonElement>>("htaw.settings.all", _ => Settings);

        // ------------------------------------------------------------- http sin CORS
        b.Register<HttpArgs, HttpResult>("htaw.http.fetch", (a, ctx) => Need("http", () => FetchAsync(a, ctx.Cancellation)));

        // ------------------------------------------------------------- entorno
        b.Register<string, string?>("htaw.env.get", name => { _perms.Require("env"); return Environment.GetEnvironmentVariable(name ?? ""); });
    }

    /// <summary>Comprueba el permiso antes de ejecutar la operacion asincrona.</summary>
    private Task<T> Need<T>(string permission, Func<Task<T>> f)
    {
        _perms.Require(permission);
        return f();
    }

    private Task Need(string permission, Func<Task> f)
    {
        _perms.Require(permission);
        return f();
    }

    /// <summary>Rutas absolutas; las relativas se toman desde la carpeta de la app (o la del usuario en un zip).</summary>
    private string FullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new GlueException("InvalidArgument", Strings.EmptyPath);
        if (path.StartsWith('~')) path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
        return Path.GetFullPath(path, _pkg.Directory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    private void ShellOpen(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "mailto")
        {
            _app.Native.Shell.OpenUrl(u.AbsoluteUri);
            return;
        }
        var full = FullPath(target);
        if (!File.Exists(full) && !Directory.Exists(full)) throw new FileNotFoundException(full, full);
        // Abrir un ejecutable con la app por defecto es ejecutarlo.
        if (ExecPolicy.IsExecutableFile(full) && _perms.Exec is not { All: true })
            throw new GlueException("PermissionDenied", Strings.OpenExecutableDenied(Path.GetFileName(full)));
        _app.Native.Shell.Open(full);
    }

    private static FsEntry[] List(string dir)
    {
        var info = new DirectoryInfo(dir);
        if (!info.Exists) throw new DirectoryNotFoundException(dir);
        return [.. info.EnumerateFileSystemInfos().Select(i => new FsEntry
        {
            Name = i.Name,
            Path = i.FullName,
            IsDirectory = i is DirectoryInfo,
            Size = i is FileInfo f ? f.Length : 0,
            Modified = i.LastWriteTimeUtc,
        }).OrderBy(e => !e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static FsStat Stat(string path)
    {
        if (File.Exists(path))
        {
            var f = new FileInfo(path);
            return new FsStat { Exists = true, Size = f.Length, Modified = f.LastWriteTimeUtc, Created = f.CreationTimeUtc, ReadOnly = f.IsReadOnly };
        }
        if (Directory.Exists(path))
        {
            var d = new DirectoryInfo(path);
            return new FsStat { Exists = true, IsDirectory = true, Modified = d.LastWriteTimeUtc, Created = d.CreationTimeUtc };
        }
        return new FsStat();
    }

    private int Watch(FsWatchArgs a, GlueWindow window)
    {
        _perms.Require("fs");
        var full = FullPath(a.Path);
        int id = ++_nextWatch;
        FileSystemWatcher w = Directory.Exists(full)
            ? new FileSystemWatcher(full) { IncludeSubdirectories = a.Recursive }
            : new FileSystemWatcher(Path.GetDirectoryName(full)!, Path.GetFileName(full));
        void Send(string type, string path, string? old = null) =>
            window.Emit("htaw.watch", new FsWatchEvent { Id = id, Type = type, Path = path, OldPath = old });
        w.Changed += (_, e) => Send("change", e.FullPath);
        w.Created += (_, e) => Send("create", e.FullPath);
        w.Deleted += (_, e) => Send("delete", e.FullPath);
        w.Renamed += (_, e) => Send("rename", e.FullPath, e.OldFullPath);
        w.EnableRaisingEvents = true;
        _watchers[id] = w;
        return id;
    }

    // ------------------------------------------------------------- ajustes
    private string SettingsFile => Path.Combine(HtawPaths.App(_pkg.Id), "settings.json");

    private Dictionary<string, JsonElement> Settings
    {
        get
        {
            if (_settings is not null) return _settings;
            try
            {
                if (File.Exists(SettingsFile))
                    _settings = JsonSerializer.Deserialize(File.ReadAllText(SettingsFile), ApiJson.Default.DictionaryStringJsonElement);
            }
            catch (JsonException) { /* archivo danado: se empieza vacio */ }
            return _settings ??= [];
        }
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        var tmp = SettingsFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Settings, ApiJson.Default.DictionaryStringJsonElement));
        File.Move(tmp, SettingsFile, true);
    }

    // ------------------------------------------------------------- http
    private static async Task<HttpResult> FetchAsync(HttpArgs a, CancellationToken cancel)
    {
        if (!Uri.TryCreate(a.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new GlueException("InvalidArgument", Strings.BadUrl(a.Url));
        using var req = new HttpRequestMessage(new HttpMethod(string.IsNullOrEmpty(a.Method) ? "GET" : a.Method.ToUpperInvariant()), uri);
        if (a.BodyBase64 is not null) req.Content = new ByteArrayContent(Convert.FromBase64String(a.BodyBase64));
        else if (a.Body is not null) req.Content = new StringContent(a.Body, Encoding.UTF8);
        if (a.Headers is not null)
            foreach (var (k, v) in a.Headers)
                if (!req.Headers.TryAddWithoutValidation(k, v) && req.Content is not null)
                {
                    req.Content.Headers.Remove(k);
                    req.Content.Headers.TryAddWithoutValidation(k, v);
                }
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        cts.CancelAfter(a.Timeout is > 0 ? a.Timeout.Value : 100_000);
        using var resp = await Http.SendAsync(req, cts.Token);
        var result = new HttpResult { Status = (int)resp.StatusCode, StatusText = resp.ReasonPhrase ?? "" };
        foreach (var h in resp.Headers.Concat(resp.Content.Headers))
            result.Headers[h.Key.ToLowerInvariant()] = string.Join(", ", h.Value);
        var bytes = await resp.Content.ReadAsByteArrayAsync(cts.Token);
        result.Body = a.ResponseType == "base64" ? Convert.ToBase64String(bytes) : Encoding.UTF8.GetString(bytes);
        return result;
    }

    // ================================================================== contenido
    /// <summary>
    /// Contenido de la app + rutas internas <c>/_htaw/</c> (CSS y archivos binarios) + CSS nativo inyectado en el HTML.
    /// </summary>
    private sealed class HtawContent(HtawPackage pkg, PermissionSet perms, Func<string> themeStyle) : IContentProvider
    {
        private const string Prefix = "_htaw/";

        public async ValueTask<GlueResponse?> GetAsync(GlueRequest request)
        {
            if (request.Path.StartsWith(Prefix, StringComparison.Ordinal)) return await Internal(request);
            var r = await pkg.Content.GetAsync(request);
            if (r is null || pkg.Manifest.Theme != "native" || !r.ContentType.StartsWith("text/html", StringComparison.Ordinal))
                return r;
            return new GlueResponse
            {
                Status = r.Status,
                ContentType = r.ContentType,
                Headers = r.Headers,
                Body = HtmlInjector.Inject(r.Body.Span, HtmlInjector.CssLink + themeStyle()),
            };
        }

        private async ValueTask<GlueResponse?> Internal(GlueRequest request)
        {
            var name = request.Path[Prefix.Length..];
            switch (name)
            {
                case "htaw.css":
                    return GlueResponse.Bytes(Assets.Bytes("htaw.css"), "text/css; charset=utf-8");
                case "fs":
                    // Binarios por fetch: GET lee, POST/PUT escribe. ?path=ruta
                    if (!perms.Has("fs")) return GlueResponse.Text("403 " + Strings.PermissionDenied("fs"), status: 403);
                    var path = Query(request.Query, "path");
                    if (string.IsNullOrEmpty(path)) return GlueResponse.Text("400 path", status: 400);
                    var full = Path.GetFullPath(path, pkg.Directory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                    try
                    {
                        if (request.Method is "POST" or "PUT")
                        {
                            await File.WriteAllBytesAsync(full, request.Body.ToArray());
                            return new GlueResponse { Status = 204 };
                        }
                        if (!File.Exists(full)) return GlueResponse.Text("404 " + full, status: 404);
                        return GlueResponse.Bytes(await File.ReadAllBytesAsync(full), "application/octet-stream");
                    }
                    catch (UnauthorizedAccessException e) { return GlueResponse.Text("403 " + e.Message, status: 403); }
                    catch (IOException e) { return GlueResponse.Text("500 " + e.Message, status: 500); }
                default:
                    return null;
            }
        }

        private static string? Query(string query, string key)
        {
            foreach (var part in query.Split('&'))
            {
                var eq = part.IndexOf('=');
                if (eq > 0 && part[..eq] == key) return Uri.UnescapeDataString(part[(eq + 1)..].Replace('+', ' '));
            }
            return null;
        }
    }
}

/// <summary>Inserta la hoja de estilo de htaW al principio del &lt;head&gt;, para que la de la app la pueda sobreescribir.</summary>
internal static partial class HtmlInjector
{
    public const string CssLink = "<link rel=\"stylesheet\" href=\"/_htaw/htaw.css\">";

    /// <summary>
    /// Variables del tema real del sistema, justo detras de htaw.css (para que ganen a sus valores por defecto y la
    /// hoja de la app, que va despues, las pueda sobreescribir). El shim la actualiza si el tema cambia.
    /// </summary>
    public static string ThemeStyle(ThemeInfo t) =>
        "<style id=\"htaw-theme\">:root{--htaw-accent:" + SafeCss(t.Accent) + ";--htaw-accent-text:" + ContrastText(t.Accent) +
        ";--htaw-font:\"" + SafeCss(t.FontFamily) + "\",system-ui,sans-serif}</style>";

    /// <summary>Texto blanco o negro segun la luminancia del color (#RRGGBB).</summary>
    public static string ContrastText(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#') return "#ffffff";
        static double Lin(int v)
        {
            var c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        try
        {
            int r = Convert.ToInt32(hex[1..3], 16), g = Convert.ToInt32(hex[3..5], 16), b = Convert.ToInt32(hex[5..7], 16);
            return 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b) > 0.4 ? "#000000" : "#ffffff";
        }
        catch (FormatException) { return "#ffffff"; }
    }

    /// <summary>Evita que un valor cierre la regla o el elemento style.</summary>
    private static string SafeCss(string s) =>
        new(s.Where(c => c is not ('"' or '\'' or ';' or '{' or '}' or '<' or '>' or '\\')).ToArray());

    [System.Text.RegularExpressions.GeneratedRegex("<head\\b[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex HeadTag();

    [System.Text.RegularExpressions.GeneratedRegex("<html\\b[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex HtmlTag();

    [System.Text.RegularExpressions.GeneratedRegex("<!doctype\\b[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex Doctype();

    public static byte[] Inject(ReadOnlySpan<byte> body, string snippet)
    {
        var html = Encoding.UTF8.GetString(body);
        int at;
        var m = HeadTag().Match(html);
        if (m.Success) at = m.Index + m.Length;
        else if ((m = HtmlTag().Match(html)).Success) at = m.Index + m.Length;
        else if ((m = Doctype().Match(html)).Success) at = m.Index + m.Length; // nunca antes del doctype (modo quirks)
        else at = html.Length > 0 && html[0] == '﻿' ? 1 : 0;
        return Encoding.UTF8.GetBytes(html.Insert(at, snippet));
    }
}

/// <summary>Recursos incrustados de htaW.</summary>
internal static class Assets
{
    public static byte[] Bytes(string name)
    {
        using var s = typeof(Assets).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Falta " + name);
        var data = new byte[s.Length];
        s.ReadExactly(data);
        return data;
    }

    public static string Read(string name) => Encoding.UTF8.GetString(Bytes(name));
}

/// <summary>Texto de la ventana de permisos.</summary>
internal static class PermissionPrompt
{
    public static string Text(HtawPackage pkg, PermissionSet perms, FileOrigin origin)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Strings.PromptHeader(pkg.DisplayName));
        sb.AppendLine();
        sb.AppendLine($"{Strings.PromptId}: {pkg.Id}" + (pkg.Manifest.Version is { } v ? $"   {Strings.PromptVersion}: {v}" : ""));
        sb.AppendLine($"{Strings.PromptFile}: {pkg.SourcePath}");
        sb.AppendLine($"{Strings.PromptOrigin}: {(origin == FileOrigin.External ? Strings.OriginExternal : Strings.OriginLocal)}");
        sb.AppendLine();
        var list = perms.Simple.Select(Strings.PermissionLabel).ToList();
        if (perms.Exec is { } e)
        {
            if (e.All) list.Add(Strings.PermissionExecAll);
            else list.Add(Strings.PermissionExec(string.Join(", ", e.Names.Concat(e.Paths))));
        }
        if (list.Count == 0) sb.AppendLine(Strings.PromptNoPermissions);
        else
        {
            sb.AppendLine(Strings.PromptPermissions);
            foreach (var p in list) sb.AppendLine("  • " + p);
        }
        if (perms.IsDangerous)
        {
            sb.AppendLine();
            sb.AppendLine("⚠ " + Strings.PromptDanger);
        }
        sb.AppendLine();
        sb.Append(Strings.PromptQuestion);
        return sb.ToString();
    }
}
