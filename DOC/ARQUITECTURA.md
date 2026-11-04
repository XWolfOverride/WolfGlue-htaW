# Arquitectura

## Estructura prevista del repositorio

```
WolfGlue/
├─ src/
│  ├─ WolfGlue/                 biblioteca: núcleo + backends Windows/Linux/macOS
│  │  └─ WolfGlue.targets       lo que debe importar el proyecto consumidor (ver CONSUMO.md)
│  ├─ WolfGlue.Generators/      generador de código del bridge y de .d.ts (fase posterior)
│  └─ htaW/                     ejecutable (WinExe, Native AOT)
│     └─ assets/                htaw.css, shim JS, htaw.d.ts (recursos incrustados)
├─ samples/                     ejemplo C# con varias ventanas + aplicaciones .htaw de ejemplo
├─ tests/
│  ├─ WolfGlue.Tests/           xUnit
│  └─ htaW.Tests/               xUnit
├─ DOC/
├─ build.ps1 / build.sh         compilan htaW con AOT
├─ run.cmd / run.sh             dotnet run de htaW (doble clic)
├─ publish.cmd                  solo publish AOT + copia a la raíz (doble clic, Windows)
└─ AGENTS.md / CLAUDE.md
```

## Capas

```
┌──────────────────────────────────────────────────────────────┐
│ Aplicación (C# propio o htaW)                                │
├──────────────────────────────────────────────────────────────┤
│ API pública: GlueApp, GlueWindow, Bridge, servicios nativos  │  independiente del SO
├──────────────────────────────────────────────────────────────┤
│ Backend: IPlatform, IPlatformWindow, IPlatformWebView        │  contrato interno
├───────────────────┬────────────────────┬─────────────────────┤
│ Win32 + WebView2  │ GTK3 + WebKitGTK   │ Cocoa + WKWebView   │  una implementación por SO
└───────────────────┴────────────────────┴─────────────────────┘
```

El backend se elige en tiempo de ejecución con `OperatingSystem.IsWindows()`, `IsLinux()` e
`IsMacOS()`. Cada backend solo carga sus bibliotecas nativas cuando se usa.

## Modelo de aplicación

### GlueApp (una por proceso)
- `Run()` arranca el bucle de mensajes del sistema en el hilo actual, que debe ser el principal.
  Bloquea hasta `Quit()`.
- `QuitMode`: `LastWindowClosed` (por defecto), `MainWindowClosed` o `Explicit`.
- Registro de ventanas abiertas (`Windows`, `MainWindow`).
- `Invoke(Action)` / `InvokeAsync(Func<Task>)`: ejecutar en el hilo de UI desde cualquier hilo.
- `Services`: servicios del bridge disponibles para todas las ventanas.
- `Native`: diálogos, portapapeles, shell, tema, pantallas.
- Evento `FilesOpened`: archivos que el sistema pide abrir (necesario en macOS, ver PLATAFORMAS.md).
- Dueña del **entorno de webview compartido**: todas las ventanas usan el mismo proceso de navegador,
  la misma caché y la misma carpeta de datos.

### GlueWindow (N por app)
- Opciones: título, icono, tamaño (en unidades lógicas), mínimo/máximo, posición o centrado,
  redimensionable, sin marco, siempre encima, `Owner`, `Modal`.
- Eventos: `Closing` (cancelable), `Closed`, `Resized`, `Moved`, `Activated`, `Deactivated`.
- Contenido: `Navigate(url)`, o un proveedor de contenido para el host propio (recursos incrustados,
  zip, carpeta).
- Bridge propio: servicios solo de esa ventana y `Emit(evento, datos)`.
- `ShowDialogAsync<T>()`: abre la ventana como modal sobre su `Owner` y devuelve el resultado que la
  página indique al cerrarse.

### Ventanas abiertas desde la web
`window.open` y los enlaces con `target="_blank"` se interceptan:
- Si la URL es del host propio, se crea una `GlueWindow` nueva (las opciones de `window.open`
  se traducen a opciones de ventana).
- Si es externa, se abre en el navegador del sistema y no se navega dentro de la app.

### Comunicación entre ventanas
- Por el bridge: `host.emit("evento", datos, { to: "*" | idVentana })`.
- Como todas las ventanas comparten origen, `BroadcastChannel` y `localStorage` funcionan entre ellas sin hacer nada.

## Hilos

- Un solo hilo de UI, el principal. Las ventanas y los webviews solo se tocan desde él.
- Los manejadores del bridge pueden ser `async`. Las respuestas se devuelven al webview desde el hilo de UI.
- Mecanismo de `Invoke` por SO: mensaje propio con `PostMessageW` (Windows), `g_idle_add` (GTK),
  `dispatch_async_f` sobre la cola principal (macOS).

## Servicios nativos (contrato común)

| Servicio | Funciones |
|---|---|
| `Dialogs` | abrir archivo(s), guardar, elegir carpeta, mensaje (info/aviso/error/pregunta) |
| `Shell` | abrir URL o archivo con la app por defecto, mostrar en el explorador de archivos |
| `Theme` | claro/oscuro, color de acento, fuente y tamaño del sistema, evento de cambio |
| `Screens` | monitores, área de trabajo, escala de cada uno |
| `Clipboard` | texto (imagen más adelante) |

Los diálogos reciben la ventana dueña para mostrarse modales sobre ella.
