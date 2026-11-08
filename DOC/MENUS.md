# Menús nativos (versión 1.1)

Mismo criterio que la barra de título ([BARRA-DE-TITULO.md](BARRA-DE-TITULO.md)) y la bandeja
([BANDEJA-Y-NOTIFICACIONES.md](BANDEJA-Y-NOTIFICACIONES.md)): **las capacidades son las mismas en todos los sistemas, la
organización visual la decide cada uno**. Hay dos clases de menú: el contextual (implementado en Windows) y el menú de
ventana (diseño abierto).

## Modelo común

Una lista de opciones, como en la barra de título y la bandeja, más submenús:

```json
[
  { "id": "copiar", "text": "Copiar", "shortcut": "Ctrl+C" },
  { "type": "separator" },
  { "id": "negrita", "text": "Negrita", "checked": true, "enabled": true },
  { "text": "Tema", "items": [ { "id": "claro", "text": "Claro" }, { "id": "oscuro", "text": "Oscuro" } ] }
]
```

- `id` (obligatorio salvo en submenús; único en todo el menú), `text`, `enabled` (`true`), `checked` (`false`),
  `shortcut` (solo se muestra; la página atiende las teclas), `items` (submenú, hasta 4 niveles) y separadores.
- Validación común (`GlueMenuItem.Validate`): menú no vacío, ni solo separadores, texto obligatorio, ids únicos, submenús
  no vacíos.
- La tecla de acceso se marca con `&` en `text` (`&&` para una `&` literal), como en Windows.

## Menú contextual — implementado en Windows

- WolfGlue: `app.Native.Menus.PopupAsync(items, ventana?, posición?)` → `id` o `null`; web `glue.menu.popup(items, { x, y })`.
- htaW: `htaw.menu.popup(items, { x, y })`, **sin permiso** (solo devuelve a la página lo que el usuario elige).
- Es **modal**: se pide desde el hilo de UI encolado (nunca dentro de un callback del webview, como los diálogos) y
  la promesa se resuelve al elegir o al cerrar. Sin posición, aparece junto al ratón; con ella, relativa al área de
  contenido de la ventana (píxeles CSS, se escalan con el DPI y se descuenta la franja de la barra propia).
- Windows: `TrackPopupMenu` con `TPM_RETURNCMD`. **Tema oscuro**: los menús emergentes de Win32 son claros por defecto;
  `WinDarkMenus` llama a `SetPreferredAppMode` / `FlushMenuThemes` de uxtheme (ordinales 135 y 136, no documentados pero
  estables desde Windows 10 1903) con el tema de la app (`ForceDark` / `ForceLight`) al arrancar y al cambiar el tema.
  Afecta también a los menús de la bandeja y de la barra de título. Si las funciones no existen, quedan en claro.
- Los menús por defecto del webview (clic derecho en un campo de texto) siguen desactivados salvo en modo desarrollo: la
  página ofrece los suyos con este menú.
- macOS: `NSMenu` con `popUpMenuPositioningItem`. Linux: `GtkMenu` (`gtk_menu_popup_at_pointer`) o `GtkPopoverMenu` (GTK4).

## Menú de ventana (Archivo / Editar / Ver / Ayuda) — implementado en Windows

Misma capacidad en todos los sistemas (menús con submenús, atajos mostrados, marcas, estados), con la colocación que mejor
encaje en cada uno. **Render propio en Windows**, no `HMENU`: la barra clásica no sigue el tema oscuro y no se integra con
la barra propia. La barra de menús la pinta `WinTitleBar` con el mismo estilo que los botones; el desplegable que se abre
al pulsar una entrada es el menú nativo del sistema (`TrackPopupMenu`, que ya sigue el tema, ver arriba).

Declaración, por ventana y con el mismo esquema que la barra de título:

- WolfGlue: `GlueWindowOptions.Menu`, `GlueWindow.SetMenu(menu)`, `GlueAppOptions.PageMenu` (menú declarado por la página),
  `glue.window.menu.set(menus | null)` y `glue.window.menu.on(fn(id))` (evento `glue.window.menu`), `window.open({ menu })`.
- htaW: `window.menu` en el manifiesto (ventana principal) y en el bloque `htaw+json` de cada página, `htaw.window.menu.set()`
  y `htaw.window.menu.on("click", fn(id))`, `htaw.window.open(pagina, { menu })`. Sin permiso.
- Cada entrada de primer nivel es un menú con `text` e `items` (ni separadores ni opciones sueltas). `ValidateBar` lo comprueba.

```json
{ "window": { "menu": [ { "text": "&Archivo", "items": [ { "id": "abrir", "text": "&Abrir…", "shortcut": "Ctrl+O" } ] } ] } }
```

### Colocación en Windows (implementada)

| Modo de barra | Dónde van los menús |
|---|---|
| Sin barra propia | La ventana pasa a tener una franja propia de modo `title` **sin elementos** y los menús van en una fila **debajo** de la barra de título |
| `title` y `compact` | Fila fina de menús **debajo** de la barra (los iconos de la barra se quedan donde siempre, a la izquierda tras el título). En `compact` el alto de la barra no cambia: se suma la fila de menús |
| `labels` | **Fila superior**: menús a la izquierda, **icono + título centrados** en la ventana y los botones de ventana a la derecha; los iconos con etiqueta quedan **debajo**, en su fila de siempre |

- **Desbordamiento**: si no caben todos los menús, aparece un botón «···» al final de los que caben; al pulsarlo se abre un menú
  contextual con los menús que sobran (cada uno como submenú). Con el título centrado de `labels`, los menús ceden antes que él.
- Altura: la fila de menús suma 28 px lógicos (`title` y `compact`) y la fila superior de `labels` 32 px lógicos.
- **Teclado y ratón** (como un menú de Windows de toda la vida): Alt suelto o F10 activan la barra (resalta la primera entrada; flechas
  izquierda y derecha cambian, Abajo, Intro o Espacio abren, Esc sale); Alt+letra, o la letra estando en la barra, abre el menú con esa
  tecla de acceso (`&` en el texto; se subraya solo con Alt o en modo teclado). Con un menú abierto, el ratón sobre otra entrada o las
  flechas izquierda y derecha (en el menú de primer nivel, no dentro de un submenú) pasan al menú contiguo. Las teclas se leen en
  `AcceleratorKeyPressed` de WebView2 (antes que la página) y el cambio de menú con un gancho de mensajes mientras el menú modal está
  abierto (`WinMenu.IBarHook`). Como el webview se queda con Alt, F10 y las flechas cuando tiene el foco, la propia página (el shim de WolfGlue) también reenvía esas teclas al host (`glue.window.menuKey`) y el host le avisa del estado del menú (`glue.window.menuState`: puesto, modo teclado y letras de acceso); así funciona aunque `AcceleratorKeyPressed` no llegue. Las vías (evento de WebView2, mensajes de la ventana y página) comparten la misma lógica (`WinTitleBar.OnAccelerator`); el registro Debug (`wolfglue-debug.log`) indica por cuál llega cada tecla.
- Descartado: `compact` con el menú en la misma fila (al estilo VS Code): allí no hay título y los botones van a la derecha; en htaW
  el título y los iconos de la barra compiten con el menú por la misma fila.
- **Icono de la ventana**: el clic muestra el menú del sistema y el **doble clic cierra la ventana**, como desde Windows 1
  (`HTSYSMENU` en la zona del icono de la barra propia, que Windows trata como siempre).
- **Composición específica (abierto)**: la colocación estándar depende del sistema (ver arriba). Se deja abierta la posibilidad de
  añadir más adelante **opciones de composición específicas** (colocar el menú, el título o los iconos de otra manera) sin
  tocar el modelo del menú, que es el mismo en todos los sistemas.
- Pendiente de ajuste visual (estilo, separación, altura) tras probarlo.

### macOS y Linux

- **macOS**: barra de menús global (`NSMenu` como menú principal de la aplicación); no ocupa sitio en la ventana, igual
  con cualquier barra propia.
- **Linux**: **punto abierto**, estudio en [MENUS-LINUX.md](MENUS-LINUX.md): Pantheon rechaza la barra de menús y GNOME la
  desaconseja; KDE y Xfce la aceptan. No hay una colocación válida para todos.

- **Papel `about`**: una opción con `role: "about"` abre la ventana «Acerca de» (ver [CICLO-DE-VIDA.md](CICLO-DE-VIDA.md)); en
  macOS se mueve al menú de la aplicación.

### Dudas abiertas

- **Atajos**: ¿los gestiona el sistema (aceleradores, solo en menús nativos) o la página? Hoy la página: `shortcut` solo se muestra.
- Linux (ver arriba).
