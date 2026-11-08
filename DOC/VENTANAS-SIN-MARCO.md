# Ventanas sin marco y transparentes — diseño, sin implementar (sin versión asignada)

En **WolfGlue** (C#) y en **htaW** con un permiso propio (`overlay`, ver el final).

**Incompatible con la barra de título propia** ([BARRA-DE-TITULO.md](BARRA-DE-TITULO.md)): esa barra son elementos
pintados por WolfGlue en la franja del título del sistema; una ventana sin marco no tiene esa franja, así que no hay
dónde ponerlos. Pedir las dos cosas a la vez (`titlebar` y `frame: "none"`) es un error de validación. Una ventana sin
marco que quiera una barra de herramientas la hace en HTML, con zonas de arrastre (`app-region: drag`).

## 1. Sin marco (`Frame = None`)

La ventana no tiene barra de título ni bordes del sistema; la página pinta todo y dice qué zonas arrastran la ventana.

| | Windows | macOS | Linux (GTK) |
|---|---|---|---|
| Ventana | Marco conservado con `WM_NCCALCSIZE` (sombra, esquinas redondeadas y ajuste de Windows 11 siguen funcionando) | `NSWindowStyleMaskFullSizeContentView` + barra de título oculta y transparente (los tres botones pueden ocultarse) | `gtk_window_set_decorated(FALSE)` (sin sombra en X11 sin compositor) |
| Arrastrar | `WM_NCHITTEST` → `HTCAPTION` en las zonas de arrastre | `performWindowDragWithEvent:` | `gtk_window_begin_move_drag` (X11 y Wayland) |
| Redimensionar | Bordes del marco conservado | Bordes del sistema | `gtk_window_begin_resize_drag` desde un borde que deja WolfGlue |

**Zonas de arrastre**: como hacen otros sistemas de ventanas web, con CSS: `app-region: drag` / `app-region: no-drag`
(WebView2 ya entiende `-webkit-app-region` si se activa `IsNonClientRegionSupportEnabled`; en macOS y Linux WolfGlue
lee las zonas que le pasa el shim al cambiar el DOM). Doble clic en una zona de arrastre = maximizar/restaurar.

Botones de ventana: los pinta la página y llaman a `glue.window.minimize/maximize/close`. En Windows 11, para el menú de
distribuciones al pasar sobre «maximizar», la página marca su botón con `app-region: maximize` (o una clase) y WolfGlue
responde `HTMAXBUTTON` sobre él, como ya hace la barra de título propia.

## 2. Ventana transparente al escritorio (avatar, mascota, widget)

**Sí es posible** con los tres webviews: los píxeles transparentes de la página dejan ver **lo que hay detrás de la
ventana** (el escritorio u otras ventanas), no un color de fondo. La página es una página normal: `div`, imágenes
posicionadas, animaciones CSS, `<canvas>`, SVG…

Ojo con el formato: **JPG no tiene transparencia**. Hay que usar PNG, WebP, GIF o APNG, SVG, o dibujar en `<canvas>`.
Vídeo con transparencia: WebM VP9 con alfa en Windows (Chromium) y Linux (según GStreamer); en macOS, HEVC con alfa.
Para un avatar animado, lo más portable son imágenes por fotogramas (sprites PNG/WebP) o SVG/canvas.

### Cómo se hace en cada sistema

| | Windows | macOS | Linux |
|---|---|---|---|
| Fondo transparente | Ventana sin marco + `DwmExtendFrameIntoClientArea` (−1) sin material de fondo, y WebView2 con `DefaultBackgroundColor` alfa 0 | `NSWindow` con `opaque = NO`, `backgroundColor = clearColor`, `hasShadow = NO`; WKWebView sin fondo | Visual RGBA (`gdk_screen_get_rgba_visual`) y WebKitGTK con color de fondo transparente; necesita compositor (siempre en Wayland; en X11, casi siempre) |
| Siempre encima | `HWND_TOPMOST` | `level = NSFloatingWindowLevel` (o `statusBar`) | X11: sí. **Wayland: no** (el protocolo no deja que una app se ponga encima de las demás) |
| Colocar en cualquier sitio y moverla por código | Sí | Sí | X11: sí. **Wayland: no** (la app no elige su posición; solo el usuario al arrastrar) |
| Clics a través de lo transparente | `WS_EX_TRANSPARENT` alternado (ver abajo) | `ignoresMouseEvents` alternado | Región de entrada (`gtk_widget_input_shape_combine_region`), en X11 y Wayland |
| Sin botón en la barra de tareas / Alt+Tab | `WS_EX_TOOLWINDOW` | `NSApplicationActivationPolicyAccessory` | `skip_taskbar_hint` (X11; en Wayland depende del escritorio) |

### Clics: que lo transparente no tape el escritorio

Una ventana transparente sigue recibiendo los clics en toda su superficie, aunque no se vea nada. Para un avatar, los
clics sobre las zonas vacías deben llegar a lo de detrás. Propuesta:

- La página marca qué es «sólido» con CSS (`glue-hit: auto` en el avatar; todo lo demás es atravesable), o con una
  llamada `glue.window.setHitRegions([...rectángulos])` si lo prefiere por código.
- WolfGlue consulta la posición del ratón (temporizador ligero o gancho de ratón de bajo nivel en Windows) y, cuando el
  cursor está fuera de las zonas sólidas, hace la ventana atravesable (`WS_EX_TRANSPARENT` / `ignoresMouseEvents`);
  dentro, la vuelve normal. En Linux basta con fijar la región de entrada a las zonas sólidas (sin sondeo).
- Opción simple para widgets que no necesitan clics: `ClickThrough = true` (toda la ventana atravesable).

### Excepción aceptada: Wayland

Se persigue el mismo comportamiento que en el resto de opciones (Windows, macOS, X11), pero **Wayland es una excepción
aceptada**: el protocolo de ventanas (xdg-shell) no deja que una aplicación se ponga encima de las demás ni elija su
posición. No se inventan trucos (capas de shell específicas de un compositor, ventanas de pantalla completa
transparentes con clics a través, X11 forzado): si el sistema no puede, la opción **no se aplica** y la app lo sabe.

| Opción | Windows / macOS / X11 (también XWayland) | Wayland |
|---|---|---|
| `AlwaysOnTop` | Se aplica | **Se ignora** (sin error). Solo el usuario puede fijarla, desde el menú de la ventana de su escritorio o con una regla de ventana (KDE) |
| `setPosition(x, y)` y colocar al abrir | Se aplica | **Se ignora**: la app no elige ni conoce su posición global; el compositor decide |
| Arrastrar la ventana | Zonas de arrastre | **Funciona**: `gtk_window_begin_move_drag` pide al compositor que empiece el movimiento, siempre que lo dispare un clic o toque real |
| Recordar la posición entre sesiones | Posible (la app la guarda y la restaura) | **No** |
| Transparencia y clics a través de lo transparente | Se aplica | **Se aplica** (la región de entrada es por superficie) |

Consecuencias para una app:

- `GlueWindow.Capabilities` (`CanPosition`, `CanStayOnTop`) y `htaw.window.capabilities` lo dicen con antelación; son
  `false` en Wayland. Una app no debe depender de ellos para funcionar, solo adaptarse: por ejemplo, avisar «puedes fijar
  esta ventana siempre encima desde el menú de la ventana».
- El avatar que se pasea por la pantalla por sí solo (`setPosition` repetido) **no existe en Wayland**: se queda donde lo
  deje el usuario al arrastrarlo. Un avatar que solo se arrastra a mano funciona igual en todos los sistemas.
- `alwaysOnTop` y `setPosition` nunca dan error por el hecho de ser Wayland: cumplen «mejor esfuerzo». La opción `overlay`
  de htaW sigue siendo necesaria para pedirlas, también en Wayland (el permiso describe lo que la app intenta, no lo que
  el sistema concede).
- Este límite es del protocolo y puede cambiar (hay propuestas de protocolos de posición y de sesiones): la regla es
  consultar `Capabilities`, no el nombre del sistema.

### Rendimiento

Una ventana transparente se compone con alfa en cada fotograma: animaciones CSS con `transform`/`opacity` o un canvas a
30–60 fps van bien. Conviene que la ventana tenga el tamaño justo del avatar (no a pantalla completa) y moverla
(`glue.window.setPosition`) en lugar de mover el avatar dentro de una ventana enorme: menos píxeles que componer y menos
superficie que tapa el escritorio. (En Wayland, por la excepción de arriba, el avatar no puede pasearse por la pantalla.)

### API prevista (WolfGlue)

```csharp
app.CreateWindow(new GlueWindowOptions
{
    Frame = GlueFrame.None,
    Transparent = true,      // píxeles transparentes de la página = escritorio
    AlwaysOnTop = true,
    ShowInTaskbar = false,
    ClickThrough = GlueClickThrough.Transparent,   // None | Transparent (zonas marcadas) | All
    Width = 200, Height = 260,
});
```

En la web: `glue.window.setPosition(x, y)` (unidades lógicas, pantalla virtual), `glue.window.setHitRegions(...)` y
`glue.screens()` (ya existe) para saber dónde está el escritorio. `GlueWindow.Capabilities` dice qué no se puede en el
sistema actual (`CanPosition`, `CanStayOnTop`) para que la app se adapte en Wayland.

### En htaW

Con el **permiso `overlay`** («Mostrar ventanas sin marco, transparentes o siempre encima»): una ventana así puede
tapar o imitar otras aplicaciones, y el usuario debe saberlo al aceptar los permisos.

```json
"window": { "frame": "none", "transparent": true, "alwaysOnTop": true, "taskbar": false, "clickThrough": "transparent" }
```

- `frame: "none"` deja de estar reservado; `transparent`, `taskbar` y `clickThrough` son nuevos. Sin el permiso, el
  manifiesto es válido pero la ventana se abre normal (con marco y opaca) y sale un aviso en la consola.
- `alwaysOnTop` ya existe y sigue sin necesitar permiso con marco normal; sin marco y transparente, sí (es lo que
  permite el avatar sobre todo lo demás).
- Las mismas opciones en `htaw.window.open` (para abrir el avatar desde la ventana principal) y
  `htaw.window.setPosition(x, y)`, `htaw.window.setHitRegions(...)`, `htaw.window.capabilities` (Wayland).
- Incompatible con `window.titlebar` (error de validación, como en WolfGlue).
