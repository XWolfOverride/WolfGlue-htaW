using System.Text.Json;
using System.Text.Json.Serialization;
using WolfGlue;
using WolfGlue.Bridge;
using WolfGlue.Content;

namespace Hola;

/// <summary>Ejemplo de WolfGlue: llamadas, eventos, varias ventanas y dialogo modal.</summary>
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new GlueApp(new GlueAppOptions { AppId = "WolfGlue.Hola", DevTools = true });
        app.Bridge.UseJson(HolaJson.Default);
        app.Content = new EmbeddedContent(typeof(Program).Assembly);

        // Llamada sincrona: string -> string
        app.Bridge.Register<string, string>("hola.saludo", nombre =>
            $"Hola, {(string.IsNullOrWhiteSpace(nombre) ? "mundo" : nombre)}. Respuesta desde C# (.NET {Environment.Version}).");

        // Llamada asincrona con tipo propio
        app.Bridge.Register<JsonElement, Info>("hola.info", async (_, ctx) =>
        {
            await Task.Delay(150); // simula trabajo; al volver estamos otra vez en el hilo de UI
            return new Info(Environment.MachineName.Length > 0 ? "ok" : "?", Environment.ProcessorCount,
                app.Windows.Count, ctx.Window?.Id ?? "");
        });

        // Error con codigo
        app.Bridge.Register<JsonElement, string>("hola.falla", _ =>
            throw new GlueException("Demo", "Este error viene de C#."));

        // Tema del sistema: aviso en la consola cuando cambia (la web recibe "glue.theme")
        app.Native.Theme.Changed += t => Console.WriteLine($"Tema: oscuro={t.Dark}, acento={t.Accent}");

        // Evento enviado desde la web a C#
        app.Bridge.On<string>("hola.log", (texto, ctx) => Console.WriteLine($"[{ctx.Window?.Id}] {texto}"));

        // Evento desde otro hilo cada segundo
        using var timer = new Timer(_ => app.Emit("hola.hora", DateTime.Now.ToString("HH:mm:ss")), null, 0, 1000);

        var main = app.CreateWindow(new GlueWindowOptions { Width = 820, Height = 640, MinWidth = 480, MinHeight = 360 });
        main.Show();
        return app.Run();
    }
}

internal sealed record Info(string Estado, int Procesadores, int Ventanas, string Ventana);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Info))]
internal partial class HolaJson : JsonSerializerContext;
