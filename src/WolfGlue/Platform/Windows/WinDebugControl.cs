#if DEBUG
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using WolfGlue.Native;

namespace WolfGlue.Platform.Windows;

/// <summary>
/// Canal de control para pruebas automaticas, **solo compilaciones Debug** y solo si existe la variable de entorno
/// <c>WOLFGLUE_DEBUG_CONTROL</c> (distinta de <c>0</c>): una tuberia con nombre <c>wolfglue-debug-&lt;pid&gt;</c> (solo el usuario
/// actual; el nombre tambien queda en <c>wolfglue-debug.pipe</c>, en el directorio de trabajo) por la que se mandan comandos JSON,
/// uno por linea, y se recibe una respuesta JSON por linea: <c>estado</c> (volcado de ventanas, barra de titulo, menus, fondo),
/// <c>eval</c> (JavaScript en la pagina, con promesas), <c>captura</c> (PNG de la ventana) y <c>pixel</c> (color de una zona).
/// Pensado para comprobar interfaz con numeros y archivos, sin controlar el escritorio. Cliente: <c>tools/debug/wg.ps1</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WinDebugControl
{
    private static GlueApp _app = null!;
    private static WinPlatform _platform = null!;
    private static readonly Dictionary<string, TaskCompletionSource<(bool Ok, string Value)>> Pending = [];
    private static int _nextId;
    private static int _shot;

    public static void Start(WinPlatform platform, GlueApp app)
    {
        var v = Environment.GetEnvironmentVariable("WOLFGLUE_DEBUG_CONTROL");
        if (string.IsNullOrEmpty(v) || v == "0") return;
        _platform = platform;
        _app = app;
        // Resultado de eval: la pagina lo entrega por el puente (asi se esperan las promesas).
        app.Bridge.Register<JsonElement>("glue.debug.result", (el, _) =>
        {
            var id = el.GetProperty("id").GetString() ?? "";
            TaskCompletionSource<(bool Ok, string Value)>? tcs;
            lock (Pending) Pending.Remove(id, out tcs);
            tcs?.TrySetResult((el.GetProperty("ok").GetBoolean(), el.GetProperty("value").GetString() ?? "null"));
        });
        var name = "wolfglue-debug-" + Environment.ProcessId;
        try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "wolfglue-debug.pipe"), name); } catch (IOException) { }
        DebugLog.Event("control de depuracion en la tuberia " + name);
        new Thread(() => Serve(name)) { IsBackground = true, Name = "wolfglue-debug-pipe" }.Start();
    }

    private static void Serve(string name)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        while (true)
        {
            try
            {
                var server = NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
                server.WaitForConnection();
                _ = Task.Run(() => Client(server));
            }
            catch (Exception e)
            {
                DebugLog.Event("tuberia de control: " + e.Message);
                Thread.Sleep(200);
            }
        }
    }

    private static async Task Client(NamedPipeServerStream pipe)
    {
        using var _ = pipe;
        using var reader = new StreamReader(pipe, Encoding.UTF8);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.Length == 0) continue;
            string response;
            try { response = await Handle(line); }
            catch (Exception e) { response = Error(e.Message); }
            await writer.WriteLineAsync(response);
        }
    }

    // ------------------------------------------------------------- JSON sin reflexion
    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms)) write(w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string Quote(string s) => Json(w => w.WriteStringValue(s));

    private static string Error(string message) => Json(w => { w.WriteStartObject(); w.WriteBoolean("ok", false); w.WriteString("error", message); w.WriteEndObject(); });

    private static string Ok(string rawJson) => "{\"ok\":true,\"result\":" + rawJson + "}";

    private static async Task<string> Handle(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        var cmd = root.GetProperty("cmd").GetString();
        string? Str(string n) => root.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        int Int(string n, int d = 0) => root.TryGetProperty(n, out var p) && p.TryGetInt32(out var i) ? i : d;
        string? window = Str("window");
        switch (cmd)
        {
            case "estado": return Ok(await OnUi(State));
            case "eval": return await Eval(window, Str("js") ?? "", Int("timeout", 10000));
            case "captura": return Ok(await OnUi(() => Shot(window, Str("file"), Str("mode"))));
            case "pixel": return Ok(await OnUi(() => Pixel(window, Int("x"), Int("y"), Int("w", 1), Int("h", 1), Str("mode"))));
            default: return Error("comando desconocido: " + cmd + " (estado, eval, captura, pixel)");
        }
    }

    private static Task<T> OnUi<T>(Func<T> f)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _platform.Post(() =>
        {
            try { tcs.SetResult(f()); }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }

    private static WinWindow Find(string? id)
    {
        var w = (id is null ? (_app.MainWindow ?? _app.Windows.FirstOrDefault()) : _app.FindWindow(id))
            ?? throw new InvalidOperationException(id is null ? "no hay ventanas" : "no existe la ventana " + id);
        return w.Native as WinWindow ?? throw new InvalidOperationException("ventana no valida");
    }

    // ------------------------------------------------------------- estado
    private static string State() => Json(w =>
    {
        w.WriteStartObject();
        w.WriteNumber("pid", Environment.ProcessId);
        w.WriteString("quitMode", _app.Options.QuitMode.ToString());
        w.WriteString("appId", _app.Options.AppId);
        w.WriteBoolean("trayIsSet", _app.Native.Tray.IsSet);
        w.WriteString("main", _app.MainWindow?.Id);
        w.WriteStartArray("windows");
        foreach (var win in _app.Windows)
            if (win.Native is WinWindow ww) { w.WriteStartObject(); w.WriteString("id", win.Id); ww.DebugWrite(w); w.WriteEndObject(); }
        w.WriteEndArray();
        w.WriteEndObject();
    });

    // ------------------------------------------------------------- eval
    private static async Task<string> Eval(string? window, string js, int timeoutMs)
    {
        var id = "e" + Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<(bool Ok, string Value)>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (Pending) Pending[id] = tcs;
        var script = "(async()=>{let ok=true,v;try{const AF=Object.getPrototypeOf(async function(){}).constructor;const src=" + Quote(js) + ";let f;try{f=new AF('return ('+src+'\\n)');}catch(_){f=new AF(src);}v=await f();}catch(e){ok=false;v=String((e&&e.message)||e);}" +
                     "let s;try{s=JSON.stringify(v);if(s===undefined)s='null';}catch(e){s=JSON.stringify(String(v));}" +
                     "glue.call('glue.debug.result',{id:'" + id + "',ok:ok,value:s});})()";
        await OnUi(() => { Find(window).DebugRunScript(script); return true; });
        var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        if (done != tcs.Task)
        {
            lock (Pending) Pending.Remove(id);
            return Error("tiempo agotado esperando el resultado de eval");
        }
        var (ok, value) = tcs.Task.Result;
        if (ok) return Ok(value);
        using var doc = JsonDocument.Parse(value);
        return Error("eval: " + doc.RootElement.GetString());
    }

    // ------------------------------------------------------------- captura y pixeles
    private static string Shot(string? window, string? file, string? mode)
    {
        var ww = Find(window);
        Win32.GetClientRect(ww.Hwnd, out var c);
        var (bits, w, h) = WinDebugGdi.Capture(ww.Hwnd, 0, 0, c.Right, c.Bottom, mode);
        for (int i = 3; i < bits.Length; i += 4) bits[i] = 255; // la captura no trae alfa fiable
        var png = PngEncoder.FromBgra(bits, w, h, w * 4);
        file ??= Path.Combine(Directory.GetCurrentDirectory(), $"wolfglue-debug-{Environment.ProcessId}-{Interlocked.Increment(ref _shot)}.png");
        File.WriteAllBytes(file, png);
        return Json(j => { j.WriteStartObject(); j.WriteString("file", Path.GetFullPath(file)); j.WriteNumber("width", w); j.WriteNumber("height", h); j.WriteString("mode", mode ?? "screen"); j.WriteEndObject(); });
    }

    private static string Pixel(string? window, int x, int y, int rw, int rh, string? mode)
    {
        var ww = Find(window);
        var (bits, w, h) = WinDebugGdi.Capture(ww.Hwnd, x, y, rw, rh, mode);
        long r = 0, g = 0, b = 0;
        var distinct = new HashSet<int>();
        int n = w * h;
        for (int i = 0; i < n; i++)
        {
            byte bb = bits[i * 4], gg = bits[i * 4 + 1], rr = bits[i * 4 + 2];
            r += rr; g += gg; b += bb;
            if (distinct.Count < 64) distinct.Add(rr << 16 | gg << 8 | bb);
        }
        return Json(j =>
        {
            j.WriteStartObject();
            j.WriteNumber("x", x); j.WriteNumber("y", y); j.WriteNumber("width", w); j.WriteNumber("height", h);
            j.WriteStartArray("avg"); j.WriteNumberValue((int)(r / n)); j.WriteNumberValue((int)(g / n)); j.WriteNumberValue((int)(b / n)); j.WriteEndArray();
            j.WriteString("first", "#" + (bits[2] << 16 | bits[1] << 8 | bits[0]).ToString("x6"));
            j.WriteNumber("distinctColors", distinct.Count); // 64 = "64 o mas"
            j.WriteBoolean("uniform", distinct.Count == 1);
            j.WriteEndObject();
        });
    }
}

/// <summary>Captura de pantalla de una zona del area de cliente (GDI).</summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class WinDebugGdi
{
    /// <summary>
    /// Copia una zona del area de cliente (pixeles fisicos, BGRA de arriba abajo): <c>screen</c> = lo que se ve en pantalla (la ventana
    /// tiene que estar visible y sin tapar); <c>print</c> = PrintWindow (aunque este tapada).
    /// </summary>
    public static (byte[] Bgra, int W, int H) Capture(nint hwnd, int x, int y, int w, int h, string? mode)
    {
        Win32.GetClientRect(hwnd, out var c);
        w = Math.Clamp(w, 1, Math.Max(1, c.Right - x));
        h = Math.Clamp(h, 1, Math.Max(1, c.Bottom - y));
        var screen = Win32.GetDC(0);
        var mem = Win32.CreateCompatibleDC(screen);
        var bmp = Win32.CreateCompatibleBitmap(screen, w, h);
        var old = Win32.SelectObject(mem, bmp);
        try
        {
            if (mode == "print")
            {
                var full = Win32.CreateCompatibleBitmap(screen, c.Right, c.Bottom);
                var fullDc = Win32.CreateCompatibleDC(screen);
                var oldFull = Win32.SelectObject(fullDc, full);
                PrintWindow(hwnd, fullDc, 1 | 2); // PW_CLIENTONLY | PW_RENDERFULLCONTENT
                Win32.BitBlt(mem, 0, 0, w, h, fullDc, x, y, Win32.SRCCOPY);
                Win32.SelectObject(fullDc, oldFull);
                Win32.DeleteObject(full);
                Win32.DeleteDC(fullDc);
            }
            else
            {
                var origin = new Win32.POINT { X = x, Y = y };
                Win32.ClientToScreen(hwnd, ref origin);
                Win32.BitBlt(mem, 0, 0, w, h, screen, origin.X, origin.Y, Win32.SRCCOPY | 0x40000000 /* CAPTUREBLT */);
            }
            Win32.SelectObject(mem, old);
            var info = new BitmapInfoHeader { Size = 40, Width = w, Height = -h, Planes = 1, BitCount = 32 };
            var bits = new byte[w * h * 4];
            fixed (byte* p = bits) GetDIBits(mem, bmp, 0, (uint)h, p, &info, 0);
            return (bits, w, h);
        }
        finally
        {
            Win32.DeleteObject(bmp);
            Win32.DeleteDC(mem);
            Win32.ReleaseDC(0, screen);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant;
    }

    [LibraryImport("gdi32.dll")]
    private static partial int GetDIBits(nint hdc, nint bitmap, uint start, uint lines, byte* bits, BitmapInfoHeader* info, uint usage);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PrintWindow(nint hwnd, nint hdc, uint flags);
}

/// <summary>Manejador de <c>ExecuteScript</c> de depuracion: el resultado de eval llega por el puente, aqui no se usa.</summary>
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
internal sealed partial class DebugScriptHandler : ICoreWebView2ExecuteScriptCompletedHandler
{
    public int Invoke(int errorCode, nint resultObjectAsJson) => 0;
}
#endif
