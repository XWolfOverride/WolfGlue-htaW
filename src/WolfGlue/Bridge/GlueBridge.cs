using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace WolfGlue.Bridge;

/// <summary>Contexto de una llamada desde la web.</summary>
public sealed class GlueCallContext
{
    internal GlueCallContext(IBridgePeer peer) { Peer = peer; }
    internal IBridgePeer Peer { get; }
    /// <summary>Ventana que hizo la llamada (null en pruebas sin ventana).</summary>
    public GlueWindow? Window => Peer.Window;
    /// <summary>Se cancela si la ventana se cierra antes de responder.</summary>
    public CancellationToken Cancellation => Peer.Closing;
}

/// <summary>Error que se devuelve a la web con un codigo conocido.</summary>
public sealed class GlueException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Extremo web del bridge (una ventana). Interno para poder probar el bridge sin ventanas.</summary>
internal interface IBridgePeer
{
    string Id { get; }
    GlueWindow? Window { get; }
    CancellationToken Closing { get; }
    /// <summary>Envia un mensaje JSON a la pagina. Se llama siempre en el hilo de UI.</summary>
    void SendToWeb(string json);
}

/// <summary>
/// Puente JSON-RPC entre la web y C#. Protocolo en DOC/BRIDGE.md. Sin reflexion: los tipos se resuelven con
/// los contextos de System.Text.Json registrados con <see cref="UseJson"/>.
/// </summary>
public sealed class GlueBridge
{
    private delegate ValueTask<WriteResult?> Handler(JsonElement param, GlueCallContext ctx);
    private delegate void WriteResult(Utf8JsonWriter w);

    private readonly Dictionary<string, Handler> _methods = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Action<JsonElement, GlueCallContext>>> _events = new(StringComparer.Ordinal);
    private readonly List<IJsonTypeInfoResolver> _resolvers = [BridgeJsonContext.Default];
    private JsonSerializerOptions? _options;

    /// <summary>Se produce cuando la web emite un evento hacia otras ventanas (<c>glue.emit</c>).</summary>
    internal event Action<IBridgePeer, string, string, string>? Broadcast; // emisor, nombre, datos JSON, destino

    /// <summary>
    /// Registra un contexto de System.Text.Json con los tipos de los parametros y resultados de la aplicacion.
    /// Debe llamarse antes de <see cref="GlueApp.Run"/>.
    /// </summary>
    public void UseJson(IJsonTypeInfoResolver resolver)
    {
        if (_options is not null) throw new InvalidOperationException("UseJson debe llamarse antes de la primera llamada desde la web.");
        _resolvers.Insert(0, resolver);
    }

    /// <summary>Opciones JSON efectivas (se congelan con el primer uso).</summary>
    public JsonSerializerOptions JsonOptions => _options ??= new JsonSerializerOptions
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine([.. _resolvers]),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private JsonTypeInfo<T> Info<T>()
    {
        try { return (JsonTypeInfo<T>)JsonOptions.GetTypeInfo(typeof(T)); }
        catch (NotSupportedException e)
        {
            throw new InvalidOperationException(
                $"El tipo {typeof(T)} no esta en ningun contexto JSON. Anadelo a tu JsonSerializerContext y registralo con UseJson.", e);
        }
    }

    // Los tipos se resuelven en la primera llamada, para que UseJson pueda llamarse despues de registrar.
    private Lazy<JsonTypeInfo<T>> LazyInfo<T>() => new(Info<T>);

    /// <summary>Registra un metodo asincrono.</summary>
    public void Register<TReq, TRes>(string name, Func<TReq, GlueCallContext, Task<TRes>> handler)
    {
        var req = LazyInfo<TReq>();
        var res = LazyInfo<TRes>();
        _methods[name] = async (p, ctx) =>
        {
            var arg = Deserialize(p, req.Value);
            var r = await handler(arg, ctx);
            return w => JsonSerializer.Serialize(w, r, res.Value);
        };
    }

    /// <summary>Registra un metodo sincrono.</summary>
    public void Register<TReq, TRes>(string name, Func<TReq, TRes> handler)
    {
        var req = LazyInfo<TReq>();
        var res = LazyInfo<TRes>();
        _methods[name] = (p, ctx) =>
        {
            var r = handler(Deserialize(p, req.Value));
            return new(w => JsonSerializer.Serialize(w, r, res.Value));
        };
    }

    /// <summary>Registra un metodo sin resultado (la promesa se resuelve con <c>undefined</c>).</summary>
    public void Register<TReq>(string name, Action<TReq, GlueCallContext> handler)
    {
        var req = LazyInfo<TReq>();
        _methods[name] = (p, ctx) => { handler(Deserialize(p, req.Value), ctx); return new((WriteResult?)null); };
    }

    /// <summary>Registra un metodo asincrono sin resultado.</summary>
    public void Register<TReq>(string name, Func<TReq, GlueCallContext, Task> handler)
    {
        var req = LazyInfo<TReq>();
        _methods[name] = async (p, ctx) => { await handler(Deserialize(p, req.Value), ctx); return null; };
    }

    /// <summary>Escucha un evento emitido desde la web con <c>glue.emit(nombre, datos)</c>.</summary>
    public void On<T>(string name, Action<T, GlueCallContext> handler)
    {
        var info = LazyInfo<T>();
        if (!_events.TryGetValue(name, out var list)) _events[name] = list = [];
        list.Add((d, ctx) => handler(Deserialize(d, info.Value), ctx));
    }

    /// <summary>Indica si hay un metodo registrado con ese nombre.</summary>
    public bool Has(string name) => _methods.ContainsKey(name);

    /// <summary>
    /// Argumento <paramref name="index"/> de una llamada con parametros posicionales (<c>[a, b]</c>) o con nombre
    /// (<c>{ nombre: a }</c>). Lo usa el codigo generado para las clases con <see cref="GlueServiceAttribute"/>.
    /// </summary>
    public T Arg<T>(JsonElement args, int index, string name)
    {
        var e = args.ValueKind switch
        {
            JsonValueKind.Array => index < args.GetArrayLength() ? args[index] : default,
            JsonValueKind.Object => args.TryGetProperty(name, out var p) ? p : default,
            _ => default,
        };
        return Deserialize(e, Info<T>());
    }

    private T Deserialize<T>(JsonElement e, JsonTypeInfo<T> info)
    {
        if (e.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return default!;
        try { return e.Deserialize(info)!; }
        catch (JsonException ex) { throw new GlueException("InvalidArgument", ex.Message); }
    }

    /// <summary>Serializa un evento para enviarlo a la web.</summary>
    internal string EventMessage<T>(string name, T data)
    {
        var info = Info<T>();
        return Write(w =>
        {
            w.WriteString("t", "event");
            w.WriteString("n", name);
            w.WritePropertyName("d");
            JsonSerializer.Serialize(w, data, info);
        });
    }

    /// <summary>Evento con datos ya serializados (reenvio de <c>glue.emit</c> entre ventanas).</summary>
    internal static string RawEventMessage(string name, string dataJson, string from) => Write(w =>
    {
        w.WriteString("t", "event");
        w.WriteString("n", name);
        w.WritePropertyName("d");
        w.WriteRawValue(dataJson.Length == 0 ? "null" : dataJson);
        w.WriteString("from", from);
    });

    private static string Write(Action<Utf8JsonWriter> body)
    {
        var buf = new ArrayBufferWriter<byte>(256);
        using (var w = new Utf8JsonWriter(buf))
        {
            w.WriteStartObject();
            body(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buf.WrittenSpan);
    }

    /// <summary>Procesa un mensaje recibido de la web. Se llama en el hilo de UI.</summary>
    internal async Task HandleAsync(IBridgePeer peer, string json)
    {
        string type, name;
        long id = 0;
        JsonElement param;
        string to = "*";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            type = root.GetProperty("t").GetString() ?? "";
            if (type == "call")
            {
                id = root.GetProperty("id").GetInt64();
                name = root.GetProperty("m").GetString() ?? "";
            }
            else if (type == "emit")
            {
                name = root.GetProperty("n").GetString() ?? "";
                if (root.TryGetProperty("to", out var t) && t.ValueKind == JsonValueKind.String) to = t.GetString()!;
            }
            else return;
            param = root.TryGetProperty(type == "call" ? "p" : "d", out var p) ? p.Clone() : default;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return; // mensaje mal formado: se ignora
        }

        var ctx = new GlueCallContext(peer);
        if (type == "emit")
        {
            if (_events.TryGetValue(name, out var handlers))
                foreach (var h in handlers)
                {
                    try { h(param, ctx); } catch { /* un manejador de eventos no debe romper el bridge */ }
                }
            Broadcast?.Invoke(peer, name, param.ValueKind == JsonValueKind.Undefined ? "null" : param.GetRawText(), to);
            return;
        }

        string reply;
        try
        {
            if (!_methods.TryGetValue(name, out var handler))
                throw new GlueException("MethodNotFound", $"Metodo no registrado: {name}");
            var result = await handler(param, ctx);
            reply = Write(w =>
            {
                w.WriteString("t", "result");
                w.WriteNumber("id", id);
                if (result is not null) { w.WritePropertyName("r"); result(w); }
            });
        }
        catch (Exception e)
        {
            var (code, msg) = e switch
            {
                GlueException g => (g.Code, g.Message),
                OperationCanceledException => ("Cancelled", e.Message),
                UnauthorizedAccessException => ("AccessDenied", e.Message),
                FileNotFoundException or DirectoryNotFoundException => ("NotFound", e.Message),
                ArgumentException => ("InvalidArgument", e.Message),
                IOException => ("IOError", e.Message),
                _ => ("Error", e.Message),
            };
            reply = Write(w =>
            {
                w.WriteString("t", "error");
                w.WriteNumber("id", id);
                w.WriteStartObject("e");
                w.WriteString("code", code);
                w.WriteString("message", msg);
                w.WriteEndObject();
            });
        }
        if (!peer.Closing.IsCancellationRequested) peer.SendToWeb(reply);
    }
}

/// <summary>Tipos basicos disponibles sin que la aplicacion registre nada.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(WindowOpenArgs))]
[JsonSerializable(typeof(WindowInfo))]
[JsonSerializable(typeof(SizeArgs))]
[JsonSerializable(typeof(GlueTitleBar))]
[JsonSerializable(typeof(TitleBarUpdateArgs))]
[JsonSerializable(typeof(GlueTitleBarEvent))]
[JsonSerializable(typeof(WolfGlue.Native.FileDialogOptions))]
[JsonSerializable(typeof(WolfGlue.Native.FolderDialogOptions))]
[JsonSerializable(typeof(WolfGlue.Native.MessageOptions))]
[JsonSerializable(typeof(WolfGlue.Native.MessageResult))]
[JsonSerializable(typeof(WolfGlue.Native.ScreenInfo[]))]
[JsonSerializable(typeof(WolfGlue.Native.ThemeInfo))]
internal partial class BridgeJsonContext : JsonSerializerContext;

/// <summary>Parametros de <c>glue.window.open</c>.</summary>
internal sealed class WindowOpenArgs
{
    public string Url { get; set; } = "";
    public string? Title { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public bool Modal { get; set; }
    public bool Resizable { get; set; } = true;
    /// <summary>Barra de titulo: objeto = esa barra; false = ninguna (ni la de la pagina); ausente = la que declare la pagina.</summary>
    public System.Text.Json.JsonElement? Titlebar { get; set; }
}

/// <summary>Datos de una ventana que se envian a la web.</summary>
internal sealed class WindowInfo
{
    public string Id { get; set; } = "";
    public string? Result { get; set; }
}

internal sealed class SizeArgs
{
    public int Width { get; set; }
    public int Height { get; set; }
}
