using System.Text.Json;
using System.Text.Json.Serialization;
using WolfGlue.Bridge;

namespace WolfGlue.Tests;

/// <summary>Una nota de prueba.</summary>
public sealed record Nota(int Id, string Titulo, string? Texto, List<string> Etiquetas);

[JsonSerializable(typeof(Nota))]
[JsonSerializable(typeof(Nota[]))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
internal partial class ServiceJson : JsonSerializerContext;

/// <summary>Servicio de notas para las pruebas del generador.</summary>
[GlueService]
public sealed class NotasService
{
    private readonly List<Nota> _notas = [new(1, "Primera", null, ["a"])];

    /// <summary>Suma dos numeros.</summary>
    public int Sumar(int a, int b) => a + b;

    public Nota[] Listar() => [.. _notas];

    public async Task<Nota?> BuscarAsync(int id, CancellationToken cancel)
    {
        await Task.Yield();
        return _notas.FirstOrDefault(n => n.Id == id);
    }

    public void Anadir(Nota nota) => _notas.Add(nota);

    public Task BorrarTodoAsync() { _notas.Clear(); return Task.CompletedTask; }

    public Dictionary<string, int> Contar(string? prefijo = null) =>
        _notas.GroupBy(n => n.Titulo[..1]).Where(g => prefijo is null || g.Key.StartsWith(prefijo))
            .ToDictionary(g => g.Key, g => g.Count());

    public string Ventana(GlueCallContext ctx) => ctx.Window?.Id ?? "sin ventana";

    [GlueIgnore]
    public void Interno() { }
}

[GlueService("calc")]
public sealed class Calculadora
{
    public double Mitad(double x) => x / 2;
}

public class GeneratorTests
{
    private sealed class Peer : IBridgePeer
    {
        public List<string> Sent { get; } = [];
        public string Id => "w1";
        public GlueWindow? Window => null;
        public CancellationToken Closing => default;
        public void SendToWeb(string json) => Sent.Add(json);
        public JsonElement Last => JsonDocument.Parse(Sent[^1]).RootElement;
    }

    private static (GlueBridge, Peer) Setup()
    {
        var b = new GlueBridge();
        b.Register(new NotasService());
        b.Register(new Calculadora());
        b.UseJson(ServiceJson.Default);
        return (b, new Peer());
    }

    private static async Task<JsonElement> Call(GlueBridge b, Peer p, string method, string args)
    {
        await b.HandleAsync(p, $$"""{"t":"call","id":1,"m":"{{method}}","p":{{args}}}""");
        var last = p.Last;
        Assert.True(last.GetProperty("t").GetString() == "result", p.Sent[^1]);
        return last.TryGetProperty("r", out var r) ? r : default;
    }

    [Fact]
    public async Task PositionalArguments()
    {
        var (b, p) = Setup();
        Assert.Equal(5, (await Call(b, p, "notas.sumar", "[2, 3]")).GetInt32());
        Assert.Equal(2.5, (await Call(b, p, "calc.mitad", "[5]")).GetDouble());
    }

    [Fact]
    public async Task NamedArgumentsAlsoWork() =>
        Assert.Equal(7, (await Call(Setup().Item1 is var b ? b : null!, new Peer(), "notas.sumar", """{"a":3,"b":4}""")).GetInt32());

    [Fact]
    public async Task AsyncSuffixIsRemovedAndTypesRoundTrip()
    {
        var (b, p) = Setup();
        await Call(b, p, "notas.anadir", """[{"id":2,"titulo":"Segunda","texto":"x","etiquetas":["b","c"]}]""");
        var n = await Call(b, p, "notas.buscar", "[2]");
        Assert.Equal("Segunda", n.GetProperty("titulo").GetString());
        Assert.Equal(2, n.GetProperty("etiquetas").GetArrayLength());
        Assert.Equal(2, (await Call(b, p, "notas.listar", "[]")).GetArrayLength());
        Assert.Equal(1, (await Call(b, p, "notas.contar", "[\"P\"]")).GetProperty("P").GetInt32());
        Assert.Equal(2, (await Call(b, p, "notas.contar", "[]")).EnumerateObject().Count()); // parametro opcional ausente
        await Call(b, p, "notas.borrarTodo", "[]");
        Assert.Equal(0, (await Call(b, p, "notas.listar", "[]")).GetArrayLength());
    }

    [Fact]
    public async Task ContextIsInjectedAndIgnoredMethodsAreNotRegistered()
    {
        var (b, p) = Setup();
        Assert.Equal("sin ventana", (await Call(b, p, "notas.ventana", "[]")).GetString());
        Assert.False(b.Has("notas.interno"));
    }

    [Fact]
    public void TypeScriptDefinitions()
    {
        var d = GlueServicesTypeScript.Definitions;
        Assert.Contains("notas: {", d);
        Assert.Contains("sumar(a: number, b: number): Promise<number>;", d);
        Assert.Contains("buscar(id: number): Promise<GlueTypes.Nota | null>;", d);
        Assert.Contains("listar(): Promise<GlueTypes.Nota[]>;", d);
        Assert.Contains("contar(prefijo?: string | null): Promise<Record<string, number>>;", d);
        Assert.Contains("borrarTodo(): Promise<void>;", d);
        Assert.Contains("ventana(): Promise<string>;", d);
        Assert.Contains("calc: {", d);
        Assert.Contains("interface Nota {", d);
        Assert.Contains("texto: string | null;", d);
        Assert.Contains("etiquetas: string[];", d);
        Assert.Contains("/** Suma dos numeros. */", d);
        Assert.DoesNotContain("interno", d);
    }
}