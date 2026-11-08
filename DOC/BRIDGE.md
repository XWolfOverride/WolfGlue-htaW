# Bridge: comunicación web ↔ C#

## Canales que da cada webview

| Sentido | Windows (WebView2) | WebKitGTK / WKWebView |
|---|---|---|
| Web → C# | `chrome.webview.postMessage(texto)` → `WebMessageReceived` | `window.webkit.messageHandlers.glue.postMessage(texto)` |
| C# → Web | `PostWebMessageAsString` → evento `message` | `evaluate_javascript` llamando a una función del shim |
| Contenido y binarios | `WebResourceRequested` sobre el host propio | `register_uri_scheme` / `WKURLSchemeHandler` |

WolfGlue solo usa **mensajes de texto** (JSON) y el **host propio**. Todo lo demás se construye encima.

## Host propio

- El contenido se sirve en `https://<host>.local/`. En Windows se usa un host `https` filtrado con
  `AddWebResourceRequestedFilter` porque WebView2 trata mejor un origen `https` que un esquema
  inventado (contexto seguro, `fetch`, cookies). En WebKit se registra un esquema propio con el mismo
  aspecto de URL.
- Proveedores de contenido intercambiables: recursos incrustados en el ensamblado, zip, carpeta en
  disco (solo dentro de esa carpeta, sin `..`).
- En desarrollo se puede navegar a un servidor externo (`http://localhost:5173`) para tener recarga en caliente.
- El tipo MIME se deduce de la extensión con una tabla propia (sin depender del registro del sistema).

## Shim JavaScript

Se inyecta antes de que cargue cualquier página (`AddScriptToExecuteOnDocumentCreated`,
`WKUserScript`, `webkit_user_content_manager_add_script`) y unifica los canales:

```js
const r = await glue.call("servicio.metodo", { ...params });   // Promise
const off = glue.on("evento", (datos, desde) => { ... });       // eventos desde C# u otras ventanas
glue.emit("evento", datos, { to: "*" });                        // a C# (Bridge.On) y a las demás ventanas ("*") o a una (id)
glue.platform                                                   // "windows" | "linux" | "macos"
glue.quit(codigo)

glue.window.id                                                  // "w1", "w2"…
glue.window.close(resultado?)                                   // resultado (JSON) para quien abrió el diálogo
glue.window.setTitle(t) / setSize(w, h) / center() / minimize() / maximize() / restore() / devtools()
glue.window.onClosing(fn)   // fn() → false o Promise<false> cancela el cierre del usuario/sistema. Protocolo: guardClose(true)
                            // al registrar; el host emite glue.closing, la pagina responde closeAck y closeReply(bool);
                            // sin closeAck en 2 s, otro intento de cierre ya no espera. Se desactiva al navegar.
await glue.window.open("pagina.html", { width, height, title, resizable, titlebar })   // → id de la ventana nueva
glue.window.titlebar.set(barra | null) / update(id, cambios) / focus(id) / on(tipo, fn)   // ver BARRA-DE-TITULO.md
await glue.window.dialog("pagina.html", { ... })                // modal; → resultado de close() o null
```

Servicios nativos (si `GlueAppOptions.NativeServicesInWeb`, activo por defecto):

```js
await glue.dialog.open({ title, multiple, defaultPath, filters: [{ name: "Texto", patterns: ["txt", "md"] }] })
                                                   // → string[] | null   (filtros también como ["*.txt"])
await glue.dialog.save({ title, fileName, defaultPath, defaultExtension, filters })  // → string | null
await glue.dialog.folder({ title, defaultPath })   // → string | null
await glue.dialog.message("texto", { title, kind: "question", buttons: "yesNoCancel" })
                                                   // kind: info|warning|error|question
                                                   // buttons: ok|okCancel|yesNo|yesNoCancel → "ok"|"cancel"|"yes"|"no"
await glue.clipboard.readText()                    // → string | null
await glue.clipboard.writeText("…")
await glue.clipboard.readImage()                   // → Blob image/png | null (viaja como PNG en base64)
await glue.clipboard.writeImage(blobCanvasImgOBytes) // se convierte a PNG en la pagina (OffscreenCanvas)
await glue.notifications.show({ id?, title, body?, silent?, buttons?: [{ id, text }] })  // → id (max. 2 botones); mismo id = sustituye
await glue.notifications.close(id); await glue.notifications.permission()  // "granted" | "denied"
await glue.menu.popup([{ id, text, enabled?, checked?, shortcut?, items?: [...] } | { type: "separator" }], { x?, y? })  // → id | null (menu contextual)
await glue.tray.set({ icon?, color?, tooltip?, menu: [{ id, text, enabled?, checked?, default? } | { type: "separator" }] })  // menu obligatorio
await glue.tray.update(id, { text?, enabled?, checked? }); await glue.tray.remove(); await glue.tray.available()
glue.tray.on("click", id => …)   // opcion elegida (o la default con el clic izquierdo)
await glue.window.show(); await glue.window.hide()
await glue.aboutBox()  // ventana Acerca de (propia o generica)
await glue.window.menu.set([{ text, items: [...] }] | null); glue.window.menu.on(id => …)  // menu de ventana (render propio en la barra de titulo)
await glue.window.setBackdrop("solid" | "tinted" | "blurred")  // fondo del sistema; glue.window.backdrop = el activo; clase glue-backdrop-* en <html> y evento glue.backdrop {requested, active}
glue.notifications.on(({ type: "click" | "button", id, button }) => …)
await glue.screens()                               // → [{ bounds:{x,y,width,height}, workArea, scale, primary }]
await glue.theme()                                 // → { dark, accent:"#RRGGBB", fontFamily, fontSize, highContrast }
glue.on("glue.theme", t => …)                      // cuando cambia el tema del sistema
await glue.shell.openUrl("https://…")              // solo http, https y mailto
await glue.shell.open(ruta)                        // con la aplicación por defecto
await glue.shell.reveal(ruta)                      // seleccionado en el explorador de archivos
```

Los diálogos son modales sobre la ventana que llama. Implementado en `src/WolfGlue/Resources/glue.js`; los
métodos los registra `BuiltInServices.cs`. Sin `title`, la ventana toma el `<title>` del documento.

## Protocolo (JSON-RPC simplificado)

```jsonc
// web → C#
{ "t": "call", "id": 17, "m": "files.list", "p": { "path": "/tmp" } }
// C# → web
{ "t": "result", "id": 17, "r": [ ... ] }
{ "t": "error",  "id": 17, "e": { "code": "NotFound", "message": "..." } }
{ "t": "event",  "n": "progress", "d": { "pct": 40 } }
// web → C# (evento hacia otras ventanas)
{ "t": "emit", "n": "refresh", "d": {}, "to": "*" }
```

- `id` lo genera el shim. Las promesas pendientes se guardan por `id`.
- Los errores de C# se convierten en `Error` de JS con `code` y `message`; nunca se envían trazas de pila.
  Códigos: el de `GlueException`, o `MethodNotFound`, `InvalidArgument` (también JSON que no encaja con el tipo),
  `NotFound`, `AccessDenied`, `IOError`, `Cancelled`, `Error`.
- Si la ventana se cierra antes de responder, la respuesta no se envía (`ctx.Cancellation` queda cancelado).
- Mensajes mal formados o de tipo desconocido se ignoran.
- Los eventos reenviados entre ventanas llevan `from` (id de la ventana emisora); `"*"` no incluye al emisor.

## Lado C# (sin reflexión)

Registro explícito; los tipos se resuelven con los contextos de `System.Text.Json` que registra la app:

```csharp
app.Bridge.UseJson(MiJson.Default);                                        // antes de app.Run()
app.Bridge.Register<ListReq, ListRes>("files.list", async (req, ctx) => ...);  // asíncrono
app.Bridge.Register<string, string>("eco", s => s);                            // síncrono
app.Bridge.Register<string>("log", (s, ctx) => ...);                           // sin resultado
app.Bridge.On<string>("aviso", (s, ctx) => ...);                               // glue.emit desde la web
window.Emit("progress", new Progress(40));                                     // a una ventana
app.Emit("hora", "12:00");                                                     // a todas (desde cualquier hilo)
```

- Tipos básicos (`string`, `bool`, `int`, `long`, `double`, `JsonElement`, `string[]`,
  `Dictionary<string,string>`) disponibles sin registrar nada. Propiedades en camelCase.
- Los tipos se resuelven en la primera llamada: un tipo que falte en los contextos da un error
  claro en la respuesta (no al registrar).
- `ctx.Window` es la ventana que hizo la llamada (para abrir diálogos modales sobre ella) y
  `ctx.Cancellation` se cancela si la ventana se cierra.
- Los manejadores corren en el hilo de UI y sus `await` vuelven a él.

## Servicios generados (`[GlueService]`)

`src/WolfGlue.Generators` es un generador de Roslyn (incremental, `netstandard2.0`) que `WolfGlue.targets` enlaza
como analizador en el proyecto de la aplicación. Sin reflexión: el registro se escribe al compilar (Native AOT).

```csharp
/// <summary>Lista de tareas.</summary>            // el resumen pasa al .d.ts como JSDoc
[GlueService]                                      // nombre: clase sin "Service", en camelCase → "tareas"
public sealed class TareasService                  // [GlueService("otro")] para elegirlo
{
    public Tarea[] Listar() => ...;                // tareas.listar()
    public Tarea Anadir(string texto) => ...;      // tareas.anadir(texto)
    public async Task<bool> MarcarAsync(int id, bool hecha, CancellationToken c) { ... }  // tareas.marcar(id, hecha)
    [GlueIgnore] public void Interno() { }         // no se expone
}

app.Bridge.UseJson(MiJson.Default);                // Tarea y Tarea[] en el contexto de la app, como siempre
app.Bridge.Register(new TareasService());          // generado: un Register por método
#if DEBUG
GlueServicesTypeScript.Write("web/glue-services.d.ts");   // tipos para el editor, si han cambiado
#endif
```

```js
/// <reference path="glue-services.d.ts" />
const t = await glue.services.tareas.anadir("Comprar pan");   // → call("tareas.anadir", ["Comprar pan"])
```

- Métodos públicos de instancia, no genéricos; el nombre en la web va en camelCase y sin el sufijo `Async`.
  Sobrecargas (WG001) y parámetros `ref`/`out`/`params` (WG002) son errores de compilación.
- Parámetros posicionales (`[a, b]`; también vale un objeto con sus nombres). `GlueCallContext`,
  `CancellationToken` (`ctx.Cancellation`) y `GlueWindow` (`ctx.Window`) se inyectan y no cuentan.
  Los parámetros que faltan llegan con su valor por defecto de tipo (`null`, 0).
- Resultados: `void`, `T`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`.
- `.d.ts`: `interface GlueServices` (lo que hay en `glue.services`), los tipos propios en `namespace GlueTypes`
  (propiedades públicas en camelCase, `[JsonPropertyName]` y `[JsonIgnore]` respetados) y `declare const glue`.
  Correspondencias: números → `number`, `string`/`char`/fechas/`Guid` → `string`, `byte[]` → `string` (base64),
  colecciones → `T[]`, diccionarios → `Record<string, T>`, `T?` → `T | null`, enums → `number` (como los serializa
  System.Text.Json por defecto), `JsonElement`/`object` → `any`.
- En JS, `glue.services` es un `Proxy`: cualquier `glue.services.x.y(...)` se convierte en `call("x.y", [...])`.
- Pruebas: `tests/WolfGlue.Tests/GeneratorTests.cs`. Ejemplo: `samples/Hola` (`TareasService`).

## Binarios y datos grandes

No se mandan por el canal de mensajes (base64 en JSON). Se usa `fetch` contra el host propio:
`fetch("https://app.local/_glue/blob/<id>")`, servido por el mismo manejador de contenido. Sirve
para descargas desde C# y para subidas mediante POST (en Linux, WebKitGTK ≥ 2.40).

## Seguridad

- Solo se aceptan mensajes cuyo origen sea el host propio. Los mensajes desde cualquier otro origen
  se descartan.
- La navegación de la ventana principal a URLs externas se cancela y se abre en el navegador del sistema,
  pero **solo** si el esquema es `http`, `https` o `mailto` (`GlueApp.OpenExternal` / `IsWebUrl`). Cualquier otro
  (`file:`, rutas, protocolos de otras aplicaciones) se descarta: el shell podría ejecutar programas con ellos.
- Además, por defecto solo si la navegación la **inició el usuario** (`IsUserInitiated` de WebView2,
  `GlueAppOptions.ExternalLinksRequireUserAction`). Por eso el host no ejecuta scripts en la página con
  `ExecuteScript`: le daría activación de usuario y cualquier navegación posterior contaría como del usuario.
- DevTools y el menú contextual por defecto solo están activos en Debug o con una opción explícita.
- Los servicios expuestos son los que registra la aplicación; no hay acceso genérico a .NET desde JS.
