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
  "icon": "icon.png",               // ruta relativa dentro de la app
  "lang": "es",                     // opcional
  "window": {
    "width": 800, "height": 600,    // unidades lógicas
    "minWidth": 400, "minHeight": 300,
    "maxWidth": null, "maxHeight": null,
    "resizable": true,
    "center": true,
    "frame": "native",              // native | none
    "maximized": false,
    "alwaysOnTop": false
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
