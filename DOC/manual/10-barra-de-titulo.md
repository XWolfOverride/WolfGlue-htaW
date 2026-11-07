# Barra de título con botones propios

Una ventana puede llevar sus propios controles en la barra de título: botones, grupos, un panel de información, una
imagen y un campo de búsqueda, como en las aplicaciones modernas del sistema. Los dibuja htaW con el aspecto del
sistema (no son HTML) y la app recibe sus eventos en JavaScript.

La definición es **la misma en todos los sistemas** y está limitada a propósito a lo que cada sistema puede ofrecer de
forma nativa (el más restrictivo es la barra de herramientas de macOS): no hay colores, bordes ni estilos propios. Lo
que no está en esta página no se puede hacer.

> Estado: implementado en Windows. En macOS y Linux llegará con sus versiones de htaW, con la misma definición.

## Dónde se declara

Cada ventana tiene su barra, o ninguna (la barra normal del sistema):

1. **En la página**, dentro del bloque `<script type="application/htaw+json">` de su `<head>`, como
   `window.titlebar`. En la página inicial ese bloque es el manifiesto; en las demás páginas solo se lee
   `window.titlebar`. Así, cada página que se abra en una ventana trae su barra.
2. **Al abrir la ventana**: `htaw.window.open(pagina, { titlebar: {...} })` usa esa barra en lugar de la de la página;
   `titlebar: false` abre la ventana con la barra normal aunque la página declare una.
3. **En caliente**, desde la propia ventana: `htaw.window.titlebar.set(barra)` la cambia y `set(null)` la quita.

En una app con `htaw.json`, la barra de la ventana principal puede ir en `htaw.json` (`window.titlebar`) o en el bloque
de su página.

```html
<script type="application/htaw+json">
{
  "window": {
    "titlebar": {
      "mode": "compact",
      "items": [
        { "type": "button", "id": "nuevo", "icon": "add", "text": "Nuevo" },
        { "type": "button", "id": "guardar", "icon": "save", "text": "Guardar" },
        { "type": "space" },
        { "type": "search", "id": "buscar", "placeholder": "Buscar" }
      ]
    }
  }
}
</script>
```

## La barra

| Campo | Por defecto | Descripción |
|---|---|---|
| `mode` | `"compact"` | `"title"`: alto normal de la barra de título y hasta 3 botones de icono junto a los botones de la ventana (como un botón de «opinar» o de cuenta). `"compact"`: barra de herramientas con iconos; el texto de cada botón sale como ayuda al pasar el ratón. `"labels"`: barra más alta, con el texto bajo cada icono. |
| `title` | `true` | Mostrar el icono y el título de la ventana a la izquierda (si caben). |
| `iconFont` | — | Fuente de iconos propia de la app (`.ttf` u `.otf`, ruta relativa o `data:`) para usar iconos `font:XXXX`. Se carga solo para la app, sin instalarla. WOFF/WOFF2 no se admiten. |
| `items` | `[]` | Los elementos, en orden. |

En el modo `title` solo hay botones (como mucho 3).

## Elementos

Todos admiten `id` (para recibir sus eventos y cambiarlos con `update`), `tooltip` y `enabled` (`true`).

| `type` | Campos | Descripción |
|---|---|---|
| `button` | `icon` y `text` **obligatorios**; `toggle`, `checked`, `menu`, `split` | Botón. Con `toggle: true` es un conmutador (se ve marcado con `checked`; la app lo cambia con `update` al recibir el clic). Con `menu`, el clic abre el menú. Con `split: true` (y `menu`), es un botón partido: el cuerpo lanza el clic y la flecha abre el menú. |
| `group` | `items` (solo botones), `text` | Agrupa botones; en el modo `labels` sus botones se ven solo con icono y `text` sale debajo como etiqueta del grupo. |
| `panel` | `text` **obligatorio**; `subtitle`, `icon`, `menu`, `width` (120–480, 240) | Recuadro redondeado con una o dos líneas de información (por ejemplo, el documento o el proyecto actual). Si tiene `id` se puede pulsar; si tiene `menu`, lo abre. |
| `search` | `id` **obligatorio**; `placeholder`, `text`, `width` (120–480, 240) | Campo de búsqueda nativo. Siempre va al final de la barra; como mucho uno. |
| `image` | `icon` (PNG o ICO de la app) **obligatorio**; `color` | Imagen decorativa (un logotipo). Si tiene `id`, se puede pulsar. |
| `space` | — | Espacio flexible: reparte el sitio que sobra. |

**Centrado:** un elemento (y solo uno) puede llevar `"center": true`: queda en el centro de la ventana, los anteriores
a la izquierda y los posteriores a la derecha. Un panel centrado se estrecha (hasta 120) si no cabe. La búsqueda y los
espacios no se centran.

Si la ventana es estrecha, los elementos que no caben no se muestran.

### Menús

`menu` es una lista de opciones `{ "id": "...", "text": "...", "enabled": true, "checked": false }` y separadores
`{ "type": "separator" }`. Elegir una opción lanza un clic con el `id` de la opción.

### Iconos

`icon` puede ser:

- **Un nombre de la lista común**, que en cada sistema se convierte en su icono nativo: `add`, `remove`, `document`,
  `open`, `folder`, `save`, `print`, `undo`, `redo`, `cut`, `copy`, `paste`, `delete`, `edit`, `search`, `settings`,
  `refresh`, `sync`, `back`, `forward`, `up`, `down`, `home`, `share`, `download`, `upload`, `play`, `pause`, `stop`,
  `filter`, `sort`, `list`, `more`, `menu`, `sidebar`, `info`, `help`, `warning`, `star`, `heart`, `emoji`, `user`,
  `mail`, `link`, `lock`, `unlock`, `bell`, `calendar`, `clock`, `terminal`, `code`, `bold`, `italic`, `underline`,
  `zoomIn`, `zoomOut`, `fullscreen`, `pin`, `tag`, `flag`, `cloud`, `globe`, `image`, `chat`, `check`, `close`,
  `light`, `dark`.
- **Una imagen PNG o ICO de la app** (ruta relativa o `data:`). Se trata como **monocroma**: se usa su transparencia
  como silueta y se pinta del color del texto, así se adapta al tema claro u oscuro y al estado (desactivado, ventana
  inactiva). Con `"color": true` se muestra con sus colores. Mejor cuadrada y de 32 px o más.
- **`font:XXXX`**: el carácter con código hexadecimal `XXXX` de la fuente `iconFont` de la barra.

## La API: `htaw.window.titlebar`

| Miembro | Descripción |
|---|---|
| `set(barra \| null)` | Pone otra barra en esta ventana, o la quita (`null`). |
| `update(id, cambios)` | Cambia un elemento: `text`, `subtitle`, `icon`, `tooltip`, `enabled`, `checked`, `menu`. |
| `focus(id)` | Pone el foco en el campo de búsqueda y selecciona su texto. |
| `on(tipo, fn)` | Escucha los eventos de la barra de esta ventana. Devuelve una función para dejar de escuchar. |

Eventos (`fn(id, e)`):

- `"click"`: un botón, panel o imagen pulsado, o una opción de menú elegida. `e.button` es el `id` del elemento cuyo
  menú se abrió (`null` si no).
- `"input"`: el texto de la búsqueda cambió (se agrupan las pulsaciones seguidas). `e.value` es el texto.
- `"submit"`: Intro en la búsqueda. `e.value` es el texto.

En la búsqueda, Escape borra el texto (lanza `input` con `""`); si ya estaba vacía, devuelve el foco a la página.

```js
const tb = htaw.window.titlebar;
let negrita = false;
tb.on("click", (id, e) => {
  if (id === "negrita") tb.update("negrita", { checked: negrita = !negrita });
  if (id === "guardar") guardar();
});
tb.on("input", (id, e) => filtrar(e.value));
document.addEventListener("keydown", (ev) => {
  if (ev.ctrlKey && ev.key === "f") { ev.preventDefault(); tb.focus("buscar"); }
});
```

Abrir otra ventana con una barra distinta, o sin barra propia:

```js
htaw.window.open("ayuda.html", { titlebar: { mode: "title", items: [
  { type: "button", id: "opinar", icon: "emoji", text: "Enviar opinión" } ] } });
htaw.window.open("acerca.html", { titlebar: false });
```

## Errores

Una barra mal definida (un campo obligatorio que falta, un tipo o icono desconocido, dos elementos centrados…) en
`htaw.json` o en el manifiesto de un HTML impide abrir la app, con un mensaje que dice qué falla. En `set` y en
`htaw.window.open` la promesa se rechaza con ese mensaje. En el bloque de una página que no es la inicial, una barra mal
definida se ignora y la ventana usa la barra normal.

Ejemplo completo: `samples/htaw/barra` (`htaw samples/htaw/barra` para abrirlo en modo desarrollo).
