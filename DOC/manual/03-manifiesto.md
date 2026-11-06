# El manifiesto

El manifiesto describe la app: identidad, ventana, aspecto y permisos. Es JSON (admite comentarios `//` y comas
finales) y va en uno de estos sitios:

| Forma de la app | Dónde va el manifiesto |
|---|---|
| HTML suelto (`app.htaw`) | En el primer `<script type="application/htaw+json">` dentro del `<head>` |
| Carpeta de desarrollo | En `htaw.json`, en la raíz de la carpeta |
| Paquete `.htaw` (zip) | En `htaw.json`, en la raíz del zip |

Un HTML sin manifiesto también se abre, con valores por defecto (y un aviso en la consola).

## Ejemplo completo

```jsonc
{
  "id": "com.ejemplo.app",
  "name": "Mi App",
  "version": "1.0.0",
  "entry": "index.html",
  "icon": "img/icono.ico",
  "window": {
    "width": 800, "height": 600,
    "minWidth": 400, "minHeight": 300,
    "maxWidth": null, "maxHeight": null,
    "resizable": true,
    "maximized": false,
    "alwaysOnTop": false,
    "background": "light-dark(#f3f3f3, #202020)",
    "fadeIn": 150
  },
  "theme": "native",
  "permissions": ["fs", "dialog", { "shell.exec": ["git"] }]
}
```

## Campos

| Campo | Tipo | Por defecto | Descripción |
|---|---|---|---|
| `id` | texto | derivado de la ruta | Identificador único. Minúsculas, dígitos, `.` y `-` (`com.ejemplo.app`). Aísla los datos de la app: ajustes, `localStorage`, IndexedDB, cookies, permisos recordados. **Recomendado**: sin él, se calcula a partir de la ruta del archivo (`local-…`) y cambia si se mueve. |
| `name` | texto | nombre del archivo | Nombre visible y título de la ventana principal. Sin él, el título es el `<title>` de la página. |
| `version` | texto | — | Versión de la app. Se muestra en la ventana de permisos y en `htaw.app.version`. |
| `entry` | ruta | — | Página inicial, relativa a la raíz. **Obligatorio** en carpeta y zip; en un HTML suelto se ignora (la página es el propio archivo). |
| `icon` | ruta o `data:` | icono de htaW | Icono `.png` o `.ico` (hasta 1 MB). Ver «El icono» más abajo. |
| `lang` | texto | — | Idioma de la app (informativo). |
| `window` | objeto | | Ventana principal (tabla siguiente). |
| `theme` | `"native"` \| `"none"` | `"native"` | `native`: htaW inyecta su CSS con aspecto de sistema. `none`: la página se ve como una web normal. Ver [Aspecto nativo](06-estilo.md). |
| `permissions` | lista | `[]` | Lo que la app puede hacer. Ver [Permisos](05-permisos.md). |
| `singleInstance` | sí/no | `false` | Reservado: se lee pero aún no tiene efecto. |

### `window`

Los tamaños son **unidades lógicas** (las de CSS): en una pantalla al 150 % la ventana mide 1,5 veces más píxeles
físicos. Se refieren al **área de contenido**, sin el marco ni la barra de título.

| Campo | Por defecto | Descripción |
|---|---|---|
| `width`, `height` | 800, 600 | Tamaño inicial. Entre 100×80 y 10000×10000. |
| `minWidth`, `minHeight` | 0 | Tamaño mínimo al redimensionar (0 = sin mínimo). |
| `maxWidth`, `maxHeight` | `null` | Tamaño máximo (`null` = sin máximo). |
| `resizable` | `true` | Si se puede redimensionar (sin él, tampoco hay botón de maximizar). |
| `maximized` | `false` | Abrir maximizada. |
| `alwaysOnTop` | `false` | Siempre encima de las demás ventanas. |
| `background` | fondo de `htaw.css` | Color con el que aparece la ventana mientras carga la página: `#rgb`, `#rrggbb` o `light-dark(#claro, #oscuro)`. Ver «La aparición de la ventana». |
| `fadeIn` | 150 | Duración en ms del fundido del contenido al aparecer (0 = sin fundido; máximo 2000). |
| `center` | `true` | La ventana se abre centrada. |
| `frame` | `"native"` | Reservado: `"none"` aún no está soportado (se usa el marco del sistema, con un aviso). |

### El icono

- Formatos: `.ico` (se usa el tamaño más cercano al que pide el sistema) o `.png` (se escala; mejor cuadrado y de
  256 px o más). Para que se vea nítido a 16 px, lo ideal es un `.ico` con varios tamaños.
- Como **ruta relativa**: dentro del zip, en la carpeta de desarrollo o junto al HTML suelto.
- Como **`data:`**: `"icon": "data:image/png;base64,iVBORw0…"`. Útil en un HTML suelto para que siga siendo un único
  archivo.
- Se ve en la barra de título y en la barra de tareas de todas las ventanas de la app, y en el `.exe` de un
  ejecutable portable.

### La aparición de la ventana

La ventana aparece en cuanto la página ha cargado o, como mucho, al medio segundo, ya pintada con el color de fondo;
la página entra con un fundido corto. Para que no se note el cambio de color, el fondo inicial debe coincidir con el
de la página. Se decide así, por orden:

1. `window.background` del manifiesto;
2. el `<meta name="theme-color">` de la página inicial (admite uno por tema con
   `media="(prefers-color-scheme: dark)"`);
3. con `theme: "native"`, el fondo de `htaw.css` (`#f3f3f3` claro, `#202020` oscuro);
4. blanco o `#202020` según el tema del sistema.

Si usas `theme: "native"` y no cambias el fondo, no hace falta indicar nada.

## Validación

Al abrir la app y al empaquetarla (`pack`, `bundle`), htaW valida el manifiesto:

- **Errores** (la app no se abre y se muestra el motivo): JSON no válido; `id` con caracteres no permitidos; `theme`
  distinto de `native`/`none`; tamaños fuera de rango o mínimos negativos; `frame` desconocido; `background` con
  formato no admitido; `fadeIn` fuera de 0–2000; `entry` o `icon` que salen de la raíz (`../`); `icon` `data:` mal
  formado; permisos desconocidos o mal escritos; en carpeta o zip, falta `entry` o la página no existe.
- **Avisos** (la app se abre; salen en la consola de DevTools): campos desconocidos, HTML sin manifiesto, `entry` en un
  HTML suelto, `frame: "none"`, y un `icon` que no se encuentra o no es un `.png`/`.ico` válido (se usa el de htaW).
- Al **empaquetar**, un icono que no se puede usar sí es un error.
