# WolfGlue — AGENTS.md

Repositorio con dos piezas:
- **WolfGlue**: biblioteca C# que crea ventanas nativas (Windows, Linux, macOS) ocupadas por
  completo por el webview del sistema, para hacer las interfaces en HTML/CSS/JS con la logica en C#.
  Varias ventanas por aplicacion, gestionadas como una sola app.
- **htaW**: ejecutable construido sobre WolfGlue que lanza aplicaciones `.htaw` (solo web, sin C#),
  inspiradas en el concepto de las HTML Applications, con una API JS propia y un CSS con aspecto nativo.

## Decisiones tomadas
- C# / .NET 10 con Native AOT. Sin WinForms, WPF ni frameworks de UI: P/Invoke directo
  (`user32`, GTK3, runtime de Objective-C). Sin reflexion; `System.Text.Json` con generador.
- Webview del sistema, nunca un Chromium propio: WebView2 (Windows), WebKitGTK 4.1 (Linux),
  WKWebView (macOS). El DPI lo gestiona el webview; la ventana solo aplica el tamano sugerido.
- Interfaz COM de WebView2 declarada a mano con `[GeneratedComInterface]`; `WebView2LoaderStatic.lib`
  enlazado estaticamente (un solo `.exe`).
- Un unico entorno de webview compartido por todas las ventanas de la app.
- Contenido servido por esquema/host propio (`https://<host>.local/`) desde recursos incrustados o zip; sin servidor HTTP local.
- Comunicacion web <-> C#: JSON-RPC sobre el canal de mensajes del webview + `fetch` al host propio para binarios.
- WolfGlue se consume **solo como `ProjectReference`** (submodulo git en otros repos); no se publica en NuGet.
  El consumidor importa `WolfGlue.targets` con una linea.
- htaW: dos formatos con la misma extension `.htaw` (HTML con manifiesto JSON en `<head>`, o zip con `htaw.json`).
- htaW: permisos declarados en el manifiesto + confirmacion si el archivo viene de fuera (MOTW / quarantine).
- htaW: `shell.exec` permitido, con permiso por nombre de proceso; `"*.*"` o `""` = todos, con aviso destacado.
- htaW: un proceso por app; origen `https://<id>.htaw.local/` para aislar almacenamiento.

## Estado
- Solo documentacion de diseno. **No hay codigo ni proyectos C# todavia.**
- Repositorio git sin remoto y sin commits.

## Documentacion
Indice y hechos clave en [DOC/README.md](DOC/README.md). Leer solo el documento que haga falta:
arquitectura, plataformas, bridge, consumo como ProjectReference, formato/API/seguridad/estilo de htaW,
compilacion y hoja de ruta.
