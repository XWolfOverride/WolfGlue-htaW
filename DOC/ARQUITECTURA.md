# Arquitectura

## Estructura prevista del repositorio

```
WolfGlue/
├─ src/
│  ├─ WolfGlue/                 biblioteca: núcleo + backends Windows/Linux/macOS
│  │  └─ WolfGlue.targets       lo que debe importar el proyecto consumidor (ver CONSUMO.md)
│  ├─ WolfGlue.Generators/      generador de código del bridge y de .d.ts (fase posterior)
│  └─ htaW/                     ejecutable htaw (WinExe, Native AOT)
│     ├─ Program.cs, Cli.cs     línea de comandos
│     ├─ Launcher.cs            panel sin argumentos (Assets/launcher.html)
│     ├─ Package/               manifiesto, apertura de HTML/zip/carpeta/app incrustada, contenido, icono
│     ├─ Security/              permisos, shell.exec, origen (MOTW) y trust.json
│     ├─ Api/                   HtawHost (GlueApp + API htaw.* + CSS), procesos, tipos JSON
│     ├─ Packer.cs              htaw pack
│     ├─ Bundler.cs, ExeIcon.cs htaw bundle (ejecutable portable) e icono en los recursos del .exe
│     ├─ FileAssociation.cs     --register / --unregister
│     └─ Assets/                htaw.js, htaw.css, launcher.html (incrustados), htaw.d.ts
├─ samples/Hola/                ejemplo C# de WolfGlue (llamadas, eventos, ventanas, modal, servicios nativos)
├─ samples/htaw/                hola.htaw y markdown.htaw (HTML suelto) y notas/ (carpeta para empaquetar)
├─ tests/
│  ├─ WolfGlue.Tests/           xUnit
│  └─ htaW.Tests/               xUnit
├─ DOC/
├─ build.ps1 / build.sh         autocompilan htaW con AOT (build.sh: previsto)
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
  la misma caché y la misma carpeta de datos. Cada ventana es como una pestaña: solo añade su propio proceso
  de renderizado. Medido en Windows con el ejemplo: 6 procesos y ~156 MB privados con una ventana; la segunda
  añade un renderer y ~30 MB en total.

### GlueWindow (N por app)
- Opciones: título, icono (`Icon`: bytes de un `.ico` o `.png`; si no, `GlueAppOptions.Icon`; si no, el del ejecutable), tamaño (en unidades lógicas), mínimo/máximo, posición o centrado,
  redimensionable, sin marco, siempre encima, `Owner`, `Modal`.
- Aparición: `Show()` muestra la ventana cuando la página inicial ha cargado o, como mucho, a los `ShowDelayMs`
  (500 ms por defecto), ya pintada con su **color de fondo inicial**; la página entra con un **fundido** corto
  (`FadeInMs`, 150 ms; se omite si el sistema pide reducir animaciones). Así nunca se ve un marco vacío ni un
  destello de otro color, y si la carga se alarga, la ventana ya está en pantalla. Un diálogo modal deshabilita a
  su dueña desde el `Show()`.
- Color de fondo inicial, por orden: `GlueWindowOptions.Background`; el `<meta name="theme-color">` de la página
  inicial (admite uno por tema con `media="(prefers-color-scheme: dark)"`); `GlueAppOptions.Background`; blanco
  o `#202020` según el tema. Formatos: `#rgb`, `#rrggbb` o `light-dark(#claro, #oscuro)`. Conviene que coincida
  con el fondo de la página: el fundido es del `body`, y el fondo de la página aparece de golpe.
- Sin comprobación de reputación de URL (SmartScreen) salvo `GlueAppOptions.UrlReputationCheck`: medido, retrasa
  cada navegación unos 2 s (de ~0,5 s a ~2,5 s hasta ver la página) y el contenido es propio.
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

Implementación:
- Fachada pública `WolfGlue.Native.GlueNative` (`app.Native`) en `src/WolfGlue/Native/`; contrato interno
  `IPlatformServices` en `Platform/IPlatform.cs`; Windows en `Platform/Windows/WinServices.cs` (+ `ShellInterop.cs`
  con `IFileOpenDialog`/`IFileSaveDialog` declaradas para AOT).
- Los diálogos se **encolan** siempre en el hilo de UI y nunca se abren dentro de un callback del webview: el bucle
  modal anidado dentro de un evento de WebView2 puede dar problemas de reentrada.
- `Theme.Changed` (C#) y el evento `glue.theme` (web) se disparan una sola vez por cambio real, aunque el aviso del
  sistema llegue a todas las ventanas.
- Pantallas en píxeles físicos del escritorio virtual + `Scale` de cada monitor.
- Tema en Windows: claro/oscuro y acento del registro de usuario, fuente de mensajes del sistema
  (`SystemParametersInfoForDpi` a 96 ppp, "Segoe UI Variable Text" en Windows 11), alto contraste.
