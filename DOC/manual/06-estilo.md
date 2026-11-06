# Aspecto nativo (CSS)

Con `"theme": "native"` (lo normal), htaW inserta al principio del `<head>` de cada página su hoja de estilo
(`/_htaw/htaw.css`) y las variables del tema real del sistema. El objetivo: **un HTML sin clases ni CSS propio ya
parece una ventana del sistema**, en claro u oscuro y con el color de acento del usuario.

Tu CSS va después, así que **puedes sobrescribir cualquier cosa**. Con `"theme": "none"` htaW no inserta nada y la
página se ve como una web normal (la API `htaw` sigue igual).

## Elementos estándar

Ya tienen estilo, sin clases: `body` (fuente y colores del sistema, sin márgenes extra), `h1`–`h3`, `p`, `a`, `hr`,
`small`, `code`, `pre`, `kbd`, `button`, `input` (texto, número, búsqueda, fecha, casillas, radio, rango…), `select`,
`textarea`, `fieldset`/`legend`, `progress`, `meter`, `table` y las barras de desplazamiento.

- El texto **no se puede seleccionar** salvo en campos, `pre`, `code` y elementos con la clase `.selectable`, como en
  una aplicación de escritorio.
- **Botón principal** (con el color de acento): `<button type="submit">`, `.default` o `.primary`.
- Los controles deshabilitados (`disabled`) se ven atenuados.

## Clases para patrones de escritorio

| Clase | Uso |
|---|---|
| `.layout-fill` | Ventana a pantalla completa en columna: hijos fijos arriba/abajo y un `<main>` (o `.content`) que ocupa el resto con su propio scroll |
| `.toolbar` | Barra de herramientas (botones planos); `.spacer` dentro empuja lo que sigue a la derecha |
| `.statusbar` | Barra de estado inferior, texto pequeño |
| `.sidebar` | Panel lateral de navegación; `.selected` marca el elemento activo |
| `.tabs` + `.tab` | Pestañas; la activa con `.selected` o `aria-selected="true"` |
| `.listview` | Lista seleccionable (`ul` o `div` con hijos); `.selected` o `aria-selected="true"` |
| `.card` | Tarjeta: superficie con borde y esquinas redondeadas |
| `.dialog-buttons` | Fila de botones de diálogo en el **orden del sistema**: escribe el principal primero y htaW lo coloca (Aceptar/Cancelar en Windows; Cancelar/Aceptar en macOS y Linux) |
| `.row`, `.column` | Fila (con ajuste de línea) o columna con separación de 8 px |
| `.muted` | Texto secundario |
| `.selectable` | Permite seleccionar el texto |

```html
<body>
  <div class="layout-fill">
    <div class="toolbar">
      <button id="abrir">Abrir…</button>
      <button id="guardar">Guardar</button>
      <span class="spacer"></span>
      <button id="ajustes">Ajustes</button>
    </div>
    <main>
      <div class="tabs">
        <button class="tab selected">General</button>
        <button class="tab">Avanzado</button>
      </div>
      <div class="card">
        <label><input type="checkbox" checked> Guardar al salir</label>
      </div>
    </main>
    <div class="statusbar"><span id="estado">Listo</span><span>UTF-8</span></div>
  </div>
</body>
```

## Variables

Todas se pueden usar en tu CSS (`color: var(--htaw-accent)`) o sobrescribir en `:root`.

| Variable | Contenido |
|---|---|
| `--htaw-accent` | Color de acento del sistema |
| `--htaw-accent-text` | Texto legible sobre el acento (blanco o negro) |
| `--htaw-font`, `--htaw-font-size` | Fuente de interfaz del sistema y su tamaño |
| `--htaw-bg` | Fondo de la ventana |
| `--htaw-surface` | Superficies de contenido (tarjetas, listas, `pre`) |
| `--htaw-fg`, `--htaw-fg-muted` | Texto normal y secundario |
| `--htaw-border`, `--htaw-border-strong` | Bordes |
| `--htaw-control-bg`, `--htaw-control-hover`, `--htaw-control-active` | Fondo de controles en reposo, al pasar el ratón y al pulsar |
| `--htaw-selection` | Fondo de la selección |
| `--htaw-link` | Enlaces |
| `--htaw-radius`, `--htaw-radius-large` | Radios de esquina (controles y tarjetas) |

Los colores cambian solos entre claro y oscuro (`prefers-color-scheme`), y el acento y la fuente se actualizan si el
usuario los cambia con la app abierta.

```css
/* Un estilo propio que sigue el tema */
.etiqueta {
  background: color-mix(in srgb, var(--htaw-accent) 15%, transparent);
  border: 1px solid var(--htaw-accent);
  border-radius: var(--htaw-radius);
  color: var(--htaw-fg);
}
/* Cambiar el fondo de toda la app */
:root { --htaw-bg: #fdf6e3; }
@media (prefers-color-scheme: dark) { :root { --htaw-bg: #002b36; } }
```

Si cambias el fondo, pon el mismo color en `window.background` del manifiesto (o en un
`<meta name="theme-color">`) para que la ventana aparezca ya con él (ver [El manifiesto](03-manifiesto.md)).

## Según el sistema

`<html>` lleva `data-os="windows"`, `"macos"` o `"linux"`, por si necesitas ajustar algo:

```css
html[data-os="macos"] .toolbar { padding-left: 80px; }
```

El aspecto actual es el de Windows 11; los de macOS y Linux son provisionales hasta que existan esas versiones.
