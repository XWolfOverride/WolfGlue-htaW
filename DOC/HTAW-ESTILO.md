# CSS con aspecto nativo

Objetivo: **un HTML sin clases ni CSS propio debe parecer ya una ventana del sistema**. Con
`"theme": "none"` en el manifiesto, htaW no inyecta nada y la página se ve como una web normal.

## Capas

### 1. Variables inyectadas por el host
Se calculan con el servicio `Theme` de WolfGlue y se actualizan en vivo cuando cambia el tema.

```css
:root {
  --htaw-accent: …;          /* color de acento del sistema */
  --htaw-accent-text: …;     /* texto legible sobre el acento */
  --htaw-font: …;            /* fuente de interfaz del sistema */
  --htaw-font-size: …;
  --htaw-radius: …;
  --htaw-bg, --htaw-fg, --htaw-border, --htaw-control-bg, --htaw-control-hover: …;
}
html[data-os="windows" | "macos" | "linux"]
html[data-theme="light" | "dark"]
```

Valores base por sistema (los ajustes finos se decidirán al implementar):

| | Windows 11 | macOS | Linux (GNOME/Adwaita como referencia) |
|---|---|---|---|
| Fuente | `"Segoe UI Variable Text", "Segoe UI"` | `-apple-system` / `system-ui` | `system-ui` (fuente de GTK) |
| Tamaño | 14px | 13px | 14px aprox. |
| Radio | 4px controles, 8px ventanas/tarjetas | 5–6px | 6px |
| Acento | ajuste de personalización | `controlAccentColor` | portal `accent-color` o azul de Adwaita |

### 2. `htaw.css` base
Estilos para los elementos **estándar**, siempre con `color-scheme: light dark` para que los
controles propios del motor sigan el tema:

- `body`: fuente, tamaño, colores, sin márgenes, `user-select: none` salvo en campos de texto
  (comportamiento de app, no de documento), cursor por defecto.
- `button`, `input` (texto, número, búsqueda, rango, checkbox, radio), `select`, `textarea`,
  `fieldset`/`legend`, `progress`, `meter`, `table`, `hr`, `a`.
- Barras de desplazamiento con el estilo del sistema donde el motor lo permita.
- `:focus-visible` con el anillo de foco del sistema.
- `button[type=submit]` / `.default` como botón principal (con el acento).

### 3. Clases para patrones de escritorio
Lo que HTML no trae y toda app de escritorio necesita:

| Clase | Uso |
|---|---|
| `.toolbar` | barra de herramientas superior |
| `.statusbar` | barra de estado inferior |
| `.sidebar` | panel lateral de navegación |
| `.tabs` + `.tab` | pestañas |
| `.listview` | lista seleccionable con teclado (con un poco de JS opcional) |
| `.dialog-buttons` | fila de botones de diálogo con el **orden del sistema** (Aceptar/Cancelar en Windows; Cancelar/Aceptar en macOS y GNOME) |
| `.layout-fill` | estructura de ventana a pantalla completa (cabecera, contenido con scroll, pie) |

El orden de botones se resuelve con CSS (`flex-direction` según `data-os`), así el HTML es el mismo
en todos los sistemas.

## Fondo transparente (Windows 11)

Con el fondo Mica activo (opción de ventana), `--htaw-bg` pasa a ser transparente en las zonas de
"ventana" y las superficies de contenido usan un color semitransparente. Es opcional (`"window":
{ "backdrop": "mica" }`, por definir).

## Reglas

- Sin imágenes ni fuentes externas: todo incrustado en htaW.
- No imitar a la perfección los controles del sistema. Basta con que sean coherentes (fuente,
  tamaños, colores, radios, foco).
- Las clases y variables públicas se documentan en `htaw.d.ts` / en este archivo y no se renombran
  sin dejar alias.

## Estado de la implementación

`src/htaW/Assets/htaw.css`, con el aspecto de Windows 11 (los valores de macOS y Linux son provisionales hasta
probar en esos sistemas).

- htaW inserta la hoja tras `<head>` y, detrás, `<style id="htaw-theme">` con `--htaw-accent`,
  `--htaw-accent-text` (blanco o negro según la luminancia del acento) y `--htaw-font`. El shim los actualiza
  si el tema cambia con la app abierta. La hoja de la app va después y puede sobreescribir cualquier variable.
- Claro/oscuro: por `prefers-color-scheme`, que el webview sigue solo.
- `html[data-os]` lo pone el shim al crearse el documento.
- Variables públicas: `--htaw-accent`, `--htaw-accent-text`, `--htaw-font`, `--htaw-font-size`, `--htaw-radius`,
  `--htaw-radius-large`, `--htaw-bg`, `--htaw-surface`, `--htaw-fg`, `--htaw-fg-muted`, `--htaw-border`,
  `--htaw-border-strong`, `--htaw-control-bg`, `--htaw-control-hover`, `--htaw-control-active`,
  `--htaw-selection`, `--htaw-link`.
- Clases: las de la tabla anterior más `.card`, `.row`, `.column`, `.muted`, `.selectable`, y `.toolbar .spacer`.
- Botón principal: `button[type=submit]`, `.default` o `.primary`.
- Fondo Mica: pendiente.
