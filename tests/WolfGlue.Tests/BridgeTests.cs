using System.Text.Json;
using System.Text.Json.Serialization;
using WolfGlue.Bridge;

namespace WolfGlue.Tests;

public class BridgeTests
{
    private sealed class FakePeer(string id = "w1") : IBridgePeer
    {
        public readonly CancellationTokenSource Cts = new();
        public List<string> Sent { get; } = [];
        public string Id => id;
        public GlueWindow? Window => null;
        public CancellationToken Closing => Cts.Token;
        public void SendToWeb(string json) => Sent.Add(json);
        public JsonElement Last => JsonDocument.Parse(Sent[^1]).RootElement;
    }

    private static string Call(long id, string method, string paramsJson = "null") =>
        $$"""{"t":"call","id":{{id}},"m":"{{method}}","p":{{paramsJson}}}""";

    [Fact]
    public async Task SyncCallReturnsResult()
    {
        var b = new GlueBridge();
        b.Register<string, string>("eco", s => "eco:" + s);
        var peer = new FakePeer();
        await b.HandleAsync(peer, Call(7, "eco", "\"hola\""));
        Assert.Equal("result", peer.Last.GetProperty("t").GetString());
        Assert.Equal(7, peer.Last.GetProperty("id").GetInt64());
        Assert.Equal("eco:hola", peer.Last.GetProperty("r").GetString());
    }

    [Fact]
    public async Task AsyncCallWithCustomTypes()
    {
        var b = new GlueBridge();
        b.Register<Point, Point>("doble", async (p, _) => { await Task.Yield(); return new Point(p.X * 2, p.Y * 2); });
        b.UseJson(TestJson.Default); // despues de registrar: permitido
        var peer = new FakePeer();
        await b.HandleAsync(peer, Call(1, "doble", """{"x":2,"y":3}"""));
        var r = peer.Last.GetProperty("r");
        Assert.Equal(4, r.GetProperty("x").GetInt32());
        Assert.Equal(6, r.GetProperty("y").GetInt32());
    }

    [Fact]
    public async Task UseJsonAfterFirstCallThrows()
    {
        var b = new GlueBridge();
        b.Register<string, string>("eco", s => s);
        await b.HandleAsync(new FakePeer(), Call(1, "eco", "\"x\""));
        Assert.Throws<InvalidOperationException>(() => b.UseJson(TestJson.Default));
    }

    [Fact]
    public async Task VoidCallHasNoResultProperty()
    {
        var b = new GlueBridge();
        string? got = null;
        b.Register<string>("set", (s, _) => got = s);
        var peer = new FakePeer();
        await b.HandleAsync(peer, Call(2, "set", "\"v\""));
        Assert.Equal("v", got);
        Assert.Equal("result", peer.Last.GetProperty("t").GetString());
        Assert.False(peer.Last.TryGetProperty("r", out _));
    }

    [Theory]
    [InlineData("nope", "MethodNotFound")]
    [InlineData("glue", "Demo")]
    [InlineData("io", "IOError")]
    [InlineData("arg", "InvalidArgument")]
    [InlineData("badparam", "InvalidArgument")]
    public async Task ErrorsCarryCodes(string method, string code)
    {
        var b = new GlueBridge();
        b.Register<string, string>("glue", _ => throw new GlueException("Demo", "mensaje"));
        b.Register<string, string>("io", _ => throw new IOException("disco"));
        b.Register<string, string>("arg", _ => throw new ArgumentException("mal"));
        b.Register<int, int>("badparam", i => i);
        var peer = new FakePeer();
        await b.HandleAsync(peer, Call(3, method, method == "badparam" ? "\"no es un numero\"" : "\"x\""));
        Assert.Equal("error", peer.Last.GetProperty("t").GetString());
        Assert.Equal(3, peer.Last.GetProperty("id").GetInt64());
        Assert.Equal(code, peer.Last.GetProperty("e").GetProperty("code").GetString());
    }

    [Fact]
    public async Task MalformedMessagesAreIgnored()
    {
        var b = new GlueBridge();
        var peer = new FakePeer();
        await b.HandleAsync(peer, "no es json");
        await b.HandleAsync(peer, """{"t":"call"}""");
        await b.HandleAsync(peer, """{"t":"otro"}""");
        Assert.Empty(peer.Sent);
    }

    [Fact]
    public async Task NoReplyWhenWindowClosed()
    {
        var b = new GlueBridge();
        var gate = new TaskCompletionSource<string>();
        b.Register<string, string>("lento", (_, _) => gate.Task);
        var peer = new FakePeer();
        var t = b.HandleAsync(peer, Call(1, "lento", "\"x\""));
        peer.Cts.Cancel();
        gate.SetResult("tarde");
        await t;
        Assert.Empty(peer.Sent);
    }

    [Fact]
    public async Task EmitReachesHandlersAndBroadcast()
    {
        var b = new GlueBridge();
        string? received = null;
        b.On<string>("log", (s, ctx) => received = s + "@" + ctx.Peer.Id);
        (string from, string name, string data, string to)? bc = null;
        b.Broadcast += (p, n, d, to) => bc = (p.Id, n, d, to);
        await b.HandleAsync(new FakePeer("w9"), """{"t":"emit","n":"log","d":"hola","to":"w2"}""");
        Assert.Equal("hola@w9", received);
        Assert.Equal(("w9", "log", "\"hola\"", "w2"), bc);
    }

    [Fact]
    public void EventMessagesAreWellFormed()
    {
        var b = new GlueBridge();
        var e = JsonDocument.Parse(b.EventMessage("hora", "12:00")).RootElement;
        Assert.Equal("event", e.GetProperty("t").GetString());
        Assert.Equal("hora", e.GetProperty("n").GetString());
        Assert.Equal("12:00", e.GetProperty("d").GetString());

        var raw = JsonDocument.Parse(GlueBridge.RawEventMessage("x", """{"a":1}""", "w1")).RootElement;
        Assert.Equal(1, raw.GetProperty("d").GetProperty("a").GetInt32());
        Assert.Equal("w1", raw.GetProperty("from").GetString());
    }

    [Fact]
    public async Task MissingJsonTypeGivesClearError()
    {
        var b = new GlueBridge();
        b.Register<Unregistered, string>("x", _ => "");
        var peer = new FakePeer();
        await b.HandleAsync(peer, Call(1, "x", "{}"));
        Assert.Equal("error", peer.Last.GetProperty("t").GetString());
        Assert.Contains("JsonSerializerContext", peer.Last.GetProperty("e").GetProperty("message").GetString());
    }
}

public sealed record Point(int X, int Y);
public sealed record Unregistered(int A);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Point))]
internal partial class TestJson : JsonSerializerContext;
