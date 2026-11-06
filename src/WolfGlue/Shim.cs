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

    /// <summary>
    /// Fundido de entrada del documento: el <c>body</c> empieza transparente (se ve el fondo de la pagina, que deberia
    /// coincidir con el de la ventana) y pasa a opaco. Es el body y no la raiz porque el lienzo bajo una raiz
    /// transparente lo pinta el motor con su color de <c>color-scheme</c>, no con el de la ventana. Pasa a opaco cuando el backend envia el mensaje <see cref="FadeInMessage"/> (pagina cargada y webview ya visible), o como mucho
    /// a los 3 s. Usa Web Animations para no tocar el DOM ni los estilos de la pagina. Solo en el marco principal y si no
    /// se piden animaciones reducidas.
    /// </summary>
    internal static string FadeIn(int ms) => ms <= 0 ? "" : $$"""
        (()=>{if(window!==window.top||matchMedia('(prefers-reduced-motion: reduce)').matches)return;
        const D={{ms}};let a=null,want=false,done=false;
        const go=()=>{want=true;if(done||!a)return;done=true;requestAnimationFrame(()=>a.play());};
        window.chrome?.webview?.addEventListener('message',e=>{if(e.data==='{{FadeInMessage}}')go();});
        const start=()=>{const r=document.body;if(!r)return false;
        a=r.animate([{opacity:0},{opacity:1}],{duration:D,easing:'ease-out',fill:'backwards'});a.pause();
        if(want)go();return true;};
        setTimeout(go,3000);
        if(!start())new MutationObserver((_,o)=>{if(start())o.disconnect();}).observe(document,{childList:true,subtree:true});})();
        """;

    /// <summary>
    /// Mensaje del webview que arranca el fundido (sin efecto si ya arranco o esta desactivado). Es un mensaje y no un
    /// script ejecutado desde el host porque ejecutar scripts (ExecuteScript) le da a la pagina activacion de usuario,
    /// como si hubiera habido un clic: eso dejaria a la pagina abrir enlaces externos por su cuenta.
    /// </summary>
    internal const string FadeInMessage = "__glueFadeIn";
}
