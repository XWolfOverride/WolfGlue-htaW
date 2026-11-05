using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using HtaW.Security;
using WolfGlue;
using WolfGlue.Bridge;

namespace HtaW.Api;

/// <summary>Ejecucion de procesos para <c>htaw.shell.exec</c> / <c>spawn</c>. Nunca pasa por un shell.</summary>
internal sealed class Processes(PermissionSet permissions, string defaultCwd)
{
    private readonly ConcurrentDictionary<int, Process> _spawned = new();
    private int _nextId;

    static Processes() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private ProcessStartInfo Prepare(ExecArgs a, bool stdin)
    {
        permissions.Require("shell.exec");
        var policy = permissions.Exec!;
        var exe = policy.Resolve(a.Name);
        // .bat/.cmd los interpreta cmd.exe con sus propias reglas de comillas: solo si ya se permite todo.
        var ext = Path.GetExtension(exe);
        if (OperatingSystem.IsWindows() && (ext.Equals(".bat", StringComparison.OrdinalIgnoreCase)
                                            || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            && !policy.AllowsAnything)
            throw new GlueException("PermissionDenied", Strings.ExecBatchNeedsAll(a.Name));

        var enc = ResolveEncoding(a.Encoding);
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin,
            StandardOutputEncoding = enc,
            StandardErrorEncoding = enc,
            WorkingDirectory = string.IsNullOrEmpty(a.Cwd) ? defaultCwd : Path.GetFullPath(a.Cwd),
        };
        if (stdin) psi.StandardInputEncoding = new UTF8Encoding(false);
        foreach (var arg in a.Args) psi.ArgumentList.Add(arg);
        if (a.Env is not null) foreach (var (k, v) in a.Env) psi.Environment[k] = v;
        if (!Directory.Exists(psi.WorkingDirectory)) throw new DirectoryNotFoundException(psi.WorkingDirectory);
        return psi;
    }

    internal static Encoding ResolveEncoding(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Equals("utf-8", StringComparison.OrdinalIgnoreCase)
            || name.Equals("utf8", StringComparison.OrdinalIgnoreCase)) return new UTF8Encoding(false);
        if (name.Equals("oem", StringComparison.OrdinalIgnoreCase))
            return OperatingSystem.IsWindows() ? Encoding.GetEncoding(Native.GetOEMCP()) : new UTF8Encoding(false);
        try { return Encoding.GetEncoding(name); }
        catch (ArgumentException) { throw new GlueException("InvalidArgument", Strings.BadEncoding(name)); }
    }

    public async Task<ExecResult> ExecAsync(ExecArgs a, CancellationToken cancel)
    {
        var psi = Prepare(a, a.Input is not null);
        using var p = Process.Start(psi) ?? throw new GlueException("Error", Strings.ExecNotFound(a.Name));
        var stdout = p.StandardOutput.ReadToEndAsync(cancel);
        var stderr = p.StandardError.ReadToEndAsync(cancel);
        if (a.Input is not null)
        {
            await p.StandardInput.WriteAsync(a.Input);
            p.StandardInput.Close();
        }
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        if (a.Timeout is > 0) cts.CancelAfter(a.Timeout.Value);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ya termino */ }
            throw cancel.IsCancellationRequested
                ? new GlueException("Cancelled", "exec cancelado")
                : new GlueException("Timeout", Strings.ExecTimeout(a.Name, a.Timeout ?? 0));
        }
        return new ExecResult { Code = p.ExitCode, Stdout = await stdout, Stderr = await stderr };
    }

    /// <summary>Lanza un proceso largo; la salida llega a la ventana como eventos <c>htaw.proc</c>.</summary>
    public int Spawn(ExecArgs a, GlueWindow window)
    {
        var psi = Prepare(a, stdin: true);
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        int id = Interlocked.Increment(ref _nextId);
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) window.Emit("htaw.proc", new ProcEvent { Id = id, Type = "stdout", Data = e.Data + "\n" }); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) window.Emit("htaw.proc", new ProcEvent { Id = id, Type = "stderr", Data = e.Data + "\n" }); };
        p.Exited += (_, _) =>
        {
            // Esperar a que se vacien los flujos antes de avisar del final.
            try { p.WaitForExit(); } catch { }
            int code = 0;
            try { code = p.ExitCode; } catch { }
            _spawned.TryRemove(id, out _);
            window.Emit("htaw.proc", new ProcEvent { Id = id, Type = "exit", Code = code });
            p.Dispose();
        };
        if (!p.Start()) throw new GlueException("Error", Strings.ExecNotFound(a.Name));
        _spawned[id] = p;
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return id;
    }

    public async Task WriteAsync(ProcWrite w)
    {
        if (!_spawned.TryGetValue(w.Id, out var p)) throw new GlueException("NotFound", "proceso " + w.Id);
        await p.StandardInput.WriteAsync(w.Data);
        await p.StandardInput.FlushAsync();
    }

    public void CloseInput(int id)
    {
        if (_spawned.TryGetValue(id, out var p)) p.StandardInput.Close();
    }

    public void Kill(int id)
    {
        if (_spawned.TryGetValue(id, out var p))
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        }
    }

    /// <summary>Al cerrar la app se terminan los procesos que sigan vivos.</summary>
    public void KillAll()
    {
        foreach (var id in _spawned.Keys) Kill(id);
    }
}

internal static partial class Native
{
    [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
    internal static partial int GetOEMCP();
}
