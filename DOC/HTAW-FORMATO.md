# Formato de las aplicaciones .htaw

Una aplicación `.htaw` no lleva código C#: es solo web. Hay dos formas con la **misma extensión**.

## Detección

htaW lee los primeros bytes del archivo:
- `PK\x03\x04` → **zip**.
- Cualquier otra cosa → **HTML** (UTF-8, con o sin BOM).

## Forma 1: HTML suelto

El manifiesto va en un bloque JSON dentro del `<head>`. Es HTML válido y se puede leer sin un parser
HTML completo: basta un escáner que localice el primer `<script type="application/htaw+json">`
antes de `</head>`.

```html
<!doctype html>
<html>
<head>
  <script type="application/htaw+json">
  {
    "id": "com.ejemplo.renombrador",
    "name": "Renombrador",
    "window": { "width": 640, "height": 420, "resizable": true },
    "permissions": ["fs", "dialog"]
  }
  </script>
  <title>Renombrador</title>
</head>
<body>…</body>
</html>
```

- Los recursos relativos (`app.js`, `img/x.png`) se sirven **desde la carpeta del archivo** y solo
  desde ella: las rutas que salen con `..` o son absolutas se rechazan.
- Si no hay bloque de manifiesto, se aplican valores por defecto. El `id` se deriva de la ruta
  absoluta del archivo (hash), para que dos HTML distintos no compartan almacenamiento.

## Forma 2: zip renombrado

```
mi-app.htaw (zip)
├─ htaw.json        ← obligatorio, en la raíz
├─ index.html       ← indicado en "entry"
├─ app.js
└─ img/…
```

- Se sirve **directamente desde el zip** (`System.IO.Compression`), sin extraerlo a disco.
- Se rechazan las entradas con rutas absolutas o con `..`.
- `entry` es obligatorio en el zip.

## Manifiesto

Mismo esquema en los dos formatos (en el HTML suelto `entry` no se usa).

```jsonc
{
  "id": "com.ejemplo.app",          // recomendado; aísla datos y origen (minúsculas, [a-z0-9.-])
  "name": "Mi App",                 // título por defecto de la ventana
  "version": "1.0.0",
  "entry": "index.html",            // solo zip
  "icon": "icon.png",               // .png o .ico: ruta relativa dentro de la app o data:...;base64
  "lang": "es",                     // opcional
  "window": {
    "width": 800, "height": 600,    // unidades lógicas
    "minWidth": 400, "minHeight": 300,
    "maxWidth": null, "maxHeight": null,
    "resizable": true,
    "center": true,
    "frame": "native",              // native | none
    "maximized": false,
    "alwaysOnTop": false,
    "background": "#f3f3f3",        // color mientras carga: #rgb, #rrggbb o light-dark(#claro, #oscuro)
    "fadeIn": 150                   // fundido de entrada en ms (0 = sin fundido)
  },
  "singleInstance": false,
  "theme": "native",                // native | none (CSS de htaW, ver HTAW-ESTILO.md)
  "permissions": [                  // ver HTAW-SEGURIDAD.md
    "fs", "dialog", "clipboard", "http",
    { "shell.exec": ["git", "ffmpeg"] }
  ]
}
```

### Validación
- Campos desconocidos: se ignoran con un aviso en la consola de DevTools (compatibilidad futura).
- Valores no válidos: error claro en un diálogo nativo y la app no se abre.
- `htaw pack` valida el manifiesto antes de empaquetar (ver HTAW-INTEGRACION.md).

## Origen y almacenamiento

- Cada app se sirve en `https://<id>.htaw.local/`. `localStorage`, IndexedDB y cookies quedan
  aislados por app sin necesidad de una carpeta de datos del webview distinta.
- `htaw.settings` (ver HTAW-API.md) guarda en `<datos de usuario>/htaW/apps/<id>/settings.json`.

## Estado de la implementación

Implementado en `src/htaW/Package/` (`HtawManifest.cs`, `HtawPackage.cs`). Detalles fijados al implementar:

- Id derivado (sin `id` en el manifiesto): `local-` + 16 caracteres hex del SHA-256 de la ruta completa
  (en Windows, en minúsculas). Estable para el mismo archivo; distinto para archivos distintos.
- El JSON del manifiesto admite comentarios y comas finales.
- `window.background`: sin él, se usa el `<meta name="theme-color">` del HTML de entrada si lo hay y, si no, con
  `theme: "native"` el fondo de `htaw.css` (`--htaw-bg`), así que una app sin CSS propio no necesita indicarlo.
- Se rechazan como error: JSON inválido, `id` inválido, `theme` distinto de `native`/`none`, tamaños fuera de
  100–10000 × 80–10000, mínimos negativos, `frame` desconocido, `background` con formato no admitido, `fadeIn` fuera de 0–2000, `entry` que sale de la raíz, permisos
  desconocidos o mal formados.
- Avisos (no error): campos desconocidos (también dentro de `window`), HTML sin bloque de manifiesto,
  `entry` en un HTML suelto (se ignora), `window.frame: "none"` (aún no soportado: se usa el marco del sistema).
- El título de la ventana es `name`; si no hay, el `<title>` del documento.
- `icon`: `.png` o `.ico` (hasta 1 MB), como ruta relativa (dentro del zip, o junto al HTML / en la carpeta) o como
  `data:image/png;base64,…` (útil en un HTML suelto para que siga siendo un solo archivo). Se usa en la barra de
  título y en la de tareas de todas las ventanas de la app y, en un ejecutable portable, como icono del `.exe`. Una ruta
  que sale de la raíz o un `data:` mal formado es error; un icono que no se encuentra o no es `.png`/`.ico` válido es un
  aviso al abrir (queda el de htaW) y un error en `pack` y `bundle`. Para tamaños pequeños nítidos, mejor un `.ico`
  con varios tamaños; un `.png` se escala (conviene cuadrado y de 256 px o más).
- `singleInstance`: instancia única por app y usuario (ver HTAW-INTEGRACION.md).
- El prefijo de ruta `/_htaw/` está reservado para htaW (CSS y acceso binario a archivos): la app no debe usarlo.
- Con `"theme": "native"` (por defecto), htaW inserta en cada respuesta HTML, justo tras `<head>`, su hoja
  `/_htaw/htaw.css` y un `<style id="htaw-theme">` con el tema real. Nunca antes del `<!doctype>`.
