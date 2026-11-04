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
glue.on("evento", datos => { ... });                            // eventos desde C#
glue.emit("evento", datos, { to: "*" });                        // a otras ventanas
glue.window                                                     // id y controles básicos de la ventana
```

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
- Los errores de C# se convierten en `Error` de JS con `code` y `message`; nunca se envían trazas de pila
  fuera de Debug.
- Las cancelaciones (por ejemplo al cerrar la ventana) rechazan las promesas pendientes con el código `Cancelled`.

## Lado C# (sin reflexión)

Registro explícito con tipos conocidos para `System.Text.Json` generado:

```csharp
app.Services.Register<ListReq, ListRes>("files.list", async (req, ctx) => ...);
window.Emit("progress", new Progress(40));
```

- `ctx` lleva la ventana que hizo la llamada (para abrir diálogos modales sobre ella) y un
  `CancellationToken` que se cancela si la ventana se cierra.
- **Fase posterior:** generador de código que lee `[GlueService]` / `[GlueMethod]` en una clase,
  genera el registro y produce un `.d.ts` con los tipos para el front.

## Binarios y datos grandes

No se mandan por el canal de mensajes (base64 en JSON). Se usa `fetch` contra el host propio:
`fetch("https://app.local/_glue/blob/<id>")`, servido por el mismo manejador de contenido. Sirve
para descargas desde C# y para subidas mediante POST (en Linux, WebKitGTK ≥ 2.40).

## Seguridad

- Solo se aceptan mensajes cuyo origen sea el host propio. Los mensajes desde cualquier otro origen
  se descartan.
- La navegación de la ventana principal a URLs externas se cancela y se abre en el navegador del sistema.
- DevTools y el menú contextual por defecto solo están activos en Debug o con una opción explícita.
- Los servicios expuestos son los que registra la aplicación; no hay acceso genérico a .NET desde JS.
