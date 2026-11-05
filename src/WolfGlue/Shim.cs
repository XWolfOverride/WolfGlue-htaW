using System.Text.Json;

namespace WolfGlue;

/// <summary>Script JS que se inyecta en cada documento (<c>Resources/glue.js</c>).</summary>
internal static class Shim
{
    private static readonly Lazy<string> Source = new(() =>
    {
        using var s = typeof(Shim).Assembly.GetManifestResourceStream("WolfGlue.glue.js")
            ?? throw new InvalidOperationException("Falta el recurso glue.js");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    });

    /// <summary>Shim con los datos de la ventana delante.</summary>
    internal static string For(string windowId, string platform) =>
        "window.__glueInit={id:" + JsonSerializer.Serialize(windowId, Bridge.BridgeJsonContext.Default.String) +
        ",platform:" + JsonSerializer.Serialize(platform, Bridge.BridgeJsonContext.Default.String) + "};\n" + Source.Value;
}
