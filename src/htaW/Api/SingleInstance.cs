using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HtaW.Api;

/// <summary>
/// Instancia unica por app y usuario (<c>singleInstance</c> del manifiesto). La primera instancia toma un mutex con
/// nombre y atiende una tuberia con nombre (en Linux y macOS, un socket de dominio Unix: .NET lo hace igual); las
/// siguientes, al ver el mutex ocupado, pasan sus argumentos y su carpeta actual por la tuberia y terminan.
/// En Windows la segunda instancia (que tiene el foco, porque la acaba de lanzar el usuario) permite a la primera
/// ponerse delante (<c>AllowSetForegroundWindow</c>); si no, Windows solo haria parpadear su boton.
/// </summary>
internal sealed partial class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipe;
    private readonly CancellationTokenSource _stop = new();
    private readonly bool _primary;
    private bool _disposed;

    private SingleInstance(Mutex mutex, string pipe, bool primary)
    {
        _mutex = mutex;
        _pipe = pipe;
        _primary = primary;
    }

    /// <summary>Nombre comun a todas las instancias de la app para este usuario (y sesion, en Windows).</summary>
    internal static string NameFor(string appId)
    {
        var who = Environment.UserName + "|" + Environment.UserDomainName + "|" + (OperatingSystem.IsWindows() ? SessionId() : 0);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(appId + "|" + who)))[..24];
        return "htaW-" + hash;
    }

    private static int SessionId()
    {
        try { return System.Diagnostics.Process.GetCurrentProcess().SessionId; } catch { return 0; }
    }

    /// <summary>
    /// Intenta ser la instancia principal. Devuelve la instancia (hay que mantenerla viva y llamar a <see cref="Listen"/>)
    /// o null si ya habia otra y se le han pasado los argumentos (este proceso debe terminar). Si la otra no responde,
    /// tambien devuelve una instancia (sin mutex propio): mejor dos ventanas que ninguna.
    /// </summary>
    public static SingleInstance? Acquire(string appId, string[] args)
    {
        var name = NameFor(appId);
        var mutex = new Mutex(true, "Local\\" + name, out bool created);
        if (created) return new SingleInstance(mutex, name, primary: true);
        mutex.Dispose();
        if (Forward(name, args)) return null;
        // La otra instancia no contesta (se esta cerrando o colgada): se arranca igualmente, sin atender a nadie.
        return new SingleInstance(new Mutex(false), name, primary: false);
    }

    /// <summary>Pasa los argumentos a la instancia principal. True si los ha recibido.</summary>
    internal static bool Forward(string name, string[] args)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
                client.Connect(1000);
                var pidBytes = new byte[4];
                client.ReadExactly(pidBytes);
                if (OperatingSystem.IsWindows()) AllowSetForegroundWindow(BitConverter.ToInt32(pidBytes));
                var json = JsonSerializer.SerializeToUtf8Bytes(new SecondInstanceInfo { Args = args, Cwd = Environment.CurrentDirectory },
                    ApiJson.Default.SecondInstanceInfo);
                client.Write(BitConverter.GetBytes(json.Length));
                client.Write(json);
                client.Flush();
                return client.ReadByte() == 1;
            }
            catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(150); // la principal aun esta arrancando su tuberia
            }
        }
        return false;
    }

    /// <summary>Atiende a las instancias siguientes en segundo plano; <paramref name="received"/> se llama desde ese hilo.</summary>
    public void Listen(Action<SecondInstanceInfo> received)
    {
        if (!_primary) return;
        var token = _stop.Token;
        var thread = new Thread(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(_pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    server.WaitForConnectionAsync(token).GetAwaiter().GetResult();
                    server.Write(BitConverter.GetBytes(Environment.ProcessId));
                    server.Flush();
                    var len = new byte[4];
                    server.ReadExactly(len);
                    int n = BitConverter.ToInt32(len);
                    if (n is <= 0 or > 1 << 20) continue;
                    var data = new byte[n];
                    server.ReadExactly(data);
                    var info = JsonSerializer.Deserialize(data, ApiJson.Default.SecondInstanceInfo);
                    server.WriteByte(1);
                    server.Flush();
                    if (info is not null) received(info);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { /* siguiente */ }
            }
        }) { IsBackground = true, Name = "htaW single instance" };
        thread.Start();
    }

    /// <summary>Deja de ser la instancia principal (antes de relanzar la app, para que la nueva lo sea).</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop.Cancel();
        if (_primary) try { _mutex.ReleaseMutex(); } catch (ApplicationException) { /* otro hilo */ }
        _mutex.Dispose();
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}

/// <summary>Lo que llega de una segunda instancia: sus argumentos y su carpeta actual.</summary>
public sealed class SecondInstanceInfo
{
    public string[] Args { get; set; } = [];
    public string Cwd { get; set; } = "";
}
