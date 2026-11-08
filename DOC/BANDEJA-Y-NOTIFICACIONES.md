# Bandeja del sistema y notificaciones (versión 1.1)

> Estado: **notificaciones y bandeja implementadas en Windows** (`Native/Notifications.cs`, `Platform/Windows/WinToast.cs`; WolfGlue `app.Native.Notifications` y `glue.notifications`, htaW `htaw.notify` con permiso `notifications`). Bandeja: `Native/Tray.cs`, `Platform/Windows/WinTray.cs`; WolfGlue `app.Native.Tray` y `glue.tray`, htaW `htaw.tray` + `tray.keepRunning` + `htaw.window.show/hide` con permiso `tray`. Detalles de la versión de Windows: clic izquierdo y doble clic ejecutan la opción `default` una sola vez; el icono monocromo se tiñe según `SystemUsesLightTheme`; `keepRunning` solo oculta la ventana mientras haya icono puesto.

Mismo criterio que la barra de título ([BARRA-DE-TITULO.md](BARRA-DE-TITULO.md)): un modelo común limitado a lo que
**todos** los sistemas ofrecen de forma nativa. Lo que uno no puede hacer no se ofrece en ninguno; lo que un sistema no
tiene en absoluto se marca como no disponible y la app lo consulta.

## Bandeja del sistema (icono de estado)

### Qué hay en cada sistema

| | Windows | macOS | Linux |
|---|---|---|---|
| Dónde | Área de notificación (junto al reloj; puede quedar oculto en el desplegable) | Barra de menús, arriba a la derecha | StatusNotifierItem por D-Bus: KDE, Cinnamon, XFCE, Ubuntu (extensión AppIndicator). **GNOME sin extensiones no lo muestra** |
| API | `Shell_NotifyIconW` | `NSStatusItem` | D-Bus `org.kde.StatusNotifierItem` + `com.canonical.dbusmenu` (sin bibliotecas: el protocolo es sencillo) |
| Icono | Color, 16×16 lógicos; debe verse en barra clara y oscura | «Template» monocromo (18 pt) que el sistema tiñe; admite color | Nombre de icono o píxeles; los temas usan simbólicos monocromos |
| Clic izquierdo | Libre (suele abrir la app) | Abre el menú (convención) | `Activate` (KDE, Ubuntu); algunos entornos solo abren el menú |
| Clic derecho | Menú contextual (lo pinta la app) | Menú (Ctrl+clic) | Menú (lo pinta el sistema con dbusmenu) |
| Texto de ayuda | Sí | Sí (`toolTip` del botón) | Sí |
| Globos | Sí (obsoletos: son notificaciones) | No | No |

### Modelo común

```json
{
  "icon": "img/bandeja.png",
  "color": false,
  "tooltip": "Sincronizando…",
  "menu": [
    { "id": "abrir", "text": "Abrir", "default": true },
    { "type": "separator" },
    { "id": "pausa", "text": "Pausar", "checked": false },
    { "id": "salir", "text": "Salir" }
  ]
}
```

- **El menú es obligatorio** y es la única forma de interactuar: en macOS y en parte de Linux el clic izquierdo abre el
  menú, así que no puede haber acciones que solo existan en el clic.
- **Una opción `default`** (como mucho una): en Windows es lo que hace el clic izquierdo (y el doble clic); en Linux, el
  `Activate`; en macOS se muestra en negrita en el menú. Así «abrir la app con un clic» funciona donde se puede sin
  dejar fuera a nadie.
- Opciones como las de la barra de título: `id`, `text`, `enabled`, `checked`, separadores; sin submenús en la primera
  versión (los admiten los tres, pero no hacen falta para empezar).
- **Icono**: como en la barra de título, PNG/ICO monocromo teñido por el sistema (Windows: según el tema de la barra de
  tareas, `SystemUsesLightTheme`; macOS: imagen «template»; Linux: se pasa ya teñido según el tema) o `color: true`.
  Por defecto, el icono de la app.
- Un icono por app (varios iconos de una misma app son raros y en macOS ocupan sitio en una barra que no es tuya).
- `available`: `false` en un escritorio sin bandeja (GNOME sin extensión). La app decide qué hacer (normalmente, no
  ocultarse en la bandeja si no la hay).

### Seguir abierta sin ventanas

Con bandeja, cerrar la última ventana no debe cerrar la app. En htaW toda la lógica vive en las páginas, así que no
puede quedarse sin ninguna: **cerrar la ventana principal la oculta** en lugar de cerrarla (su JavaScript sigue vivo
y puede atender la bandeja), y la opción «Salir» del menú llama a `htaw.app.quit()`. `htaw.window.show()` la vuelve a
mostrar. En WolfGlue (C#) es más simple: `GlueAppOptions.QuitWhenLastWindowCloses = false`.

### API prevista

- WolfGlue: `app.Native.Tray.Set(GlueTray?)`, `Update(id, cambios)`, evento `Click(id)`; en la web
  `glue.tray.set / update / on("click", fn(id))`.
- htaW: manifiesto `"tray": { ..., "keepRunning": true }` y `htaw.tray.set / update / remove / on("click")`.
  **Permiso `tray`** («Mostrar un icono junto al reloj y seguir abierta en segundo plano»): una app que no se cierra al
  cerrar su ventana es algo que el usuario debe saber.

## Notificaciones

### Qué hay en cada sistema

| | Windows 10/11 | macOS | Linux |
|---|---|---|---|
| API | Notificaciones «toast» (WinRT `ToastNotificationManager`, se usan por COM sin WinRT proyectado) | `UNUserNotificationCenter` | D-Bus `org.freedesktop.Notifications` (lo que usa `notify-send`) |
| Requisito | AppUserModelID registrado (`HKCU\Software\Classes\AppUserModelId\<id>` con nombre e icono; htaW ya fija `htaW.<id>`) | App en un bundle `.app` con identificador; **permiso del usuario** la primera vez | Un servidor de notificaciones (todos los escritorios lo tienen) |
| Título y texto | Sí | Sí | Sí (el texto admite un subconjunto de HTML según el servidor) |
| Icono | Sí (el de la app; imagen grande opcional) | Siempre el de la app; imagen como adjunto | Sí |
| Botones | Hasta 5 | Sí (categorías registradas) | `actions` (GNOME muestra hasta 3) |
| Campo de texto para responder | Sí | Sí | **No** |
| Clic | Evento si la app está abierta; si no, la relanza (exige registro COM) | Evento al delegado; si está cerrada, la abre | Señal `ActionInvoked` solo mientras la app escucha |
| Sustituir una anterior | `Tag` | `identifier` | `replaces_id` |
| Sonido | Por defecto o silencio | Por defecto o silencio | Pista `suppress-sound` |

### Modelo común

`{ id?, title, body, icon?, silent?, buttons?: [{ id, text }] (máx. 2) }`

- **Título y texto** sencillo (sin HTML).
- **Hasta 2 botones**: los tres los admiten y GNOME muestra 3.
- **Sin campo de respuesta**: Linux no lo tiene.
- **`id`**: una notificación con el mismo `id` sustituye a la anterior (progreso, «3 mensajes nuevos»).
- Eventos: `click` (la notificación; la app se pone delante) y `button` (con su id).
- **Solo mientras la app está abierta**: al cerrarse, WolfGlue retira sus notificaciones (Windows:
  `History.Clear` de su AUMID; macOS: `removeAllDeliveredNotifications`; Linux: `CloseNotification`). Así nunca hay
  una notificación cuyo clic no lleve a ninguna parte, y no hace falta registrar la app en el sistema para que la
  relance (en Windows exigiría un servidor COM; en Linux no es posible).
- `permission()` → `"granted" | "denied" | "default"` y `requestPermission()` (en macOS pregunta el sistema; en Windows
  y Linux siempre `granted` salvo que el usuario las haya desactivado para la app, que se refleja como `denied`).

### API prevista

- WolfGlue: `app.Native.Notifications.Show(GlueNotification)` → id, `Close(id)`, eventos `Clicked` y `ButtonClicked`.
- htaW: `htaw.notify({ title, body, ... })` → id, `htaw.notify.close(id)`, `htaw.notify.on("click" | "button", fn)`.
  **Permiso `notifications`**. Además, la API estándar `new Notification(...)` de la web se puede redirigir a la
  nativa (WebView2 la muestra con su propio estilo y pide permiso por origen: mejor interceptar
  `PermissionRequested` y responder según el manifiesto).

## Orden propuesto

1. Notificaciones en Windows (sirven sin bandeja y son lo más pedido).
2. Bandeja en Windows con el modelo común y `keepRunning` en htaW.
3. macOS y Linux con sus backends.
