# API JavaScript de htaW

Objeto global `htaw`, inyectado antes de que cargue la página. Todas las funciones devuelven
promesas salvo que se indique otra cosa. Se publica `htaw.d.ts` para tener autocompletado en el editor.

Cada grupo exige el permiso indicado (ver HTAW-SEGURIDAD.md). Si se llama sin permiso, se rechaza con
`code: "PermissionDenied"`.

| Grupo | Permiso | Funciones |
|---|---|---|
| `htaw.app` | — | `args`, `name`, `id`, `version`, `dir` (solo HTML suelto), `quit(code?)`, `relaunch()`, evento `secondInstance` |
| `htaw.window` | — | `title`, `setSize(w,h)`, `center()`, `minimize()`, `maximize()`, `close()`, `open(pagina, opciones)` → ventana, `onClosing(fn)` |
| `htaw.dialog` | `dialog` | `open(opciones)`, `save(opciones)`, `folder(opciones)`, `message(texto, opciones)` |
| `htaw.fs` | `fs` | `readText`, `readBytes`, `writeText`, `writeBytes`, `list`, `stat`, `exists`, `mkdir`, `remove`, `rename`, `copy`, `watch` |
| `htaw.shell` | `shell.open` / `shell.exec` | `open(urlOArchivo)`, `reveal(ruta)`, `exec(nombre, args, opciones)`, `spawn(nombre, args, opciones)` |
| `htaw.settings` | — | `get(clave)`, `set(clave, valor)`, `remove(clave)`, `all()` (JSON persistente por app) |
| `htaw.http` | `http` | `fetch(url, opciones)` sin restricciones de CORS |
| `htaw.clipboard` | `clipboard` | `readText()`, `writeText(texto)` |
| `htaw.theme` | — | `dark`, `accent`, `os`, evento `change` |
| `htaw.env` | `env` | `get(nombre)`, `home`, `temp`, `documents`, `os`, `arch` |

## Procesos

```js
// Ejecución completa: espera a que termine
const r = await htaw.shell.exec("git", ["status", "--short"], { cwd, env, timeout: 10000 });
// → { code, stdout, stderr }

// Procesos largos
const p = htaw.shell.spawn("ffmpeg", ["-i", entrada, salida], { cwd });
p.on("stdout", texto => ...);
p.on("stderr", texto => ...);
p.on("exit", codigo => ...);
await p.write("q");        // stdin
p.kill();
```

- Los argumentos van **siempre como array**. No hay modo de cadena única ni shell intermedio.
- Codificación de la salida: UTF-8 por defecto; `encoding` en las opciones para otras.
- Al cerrar la app se terminan los procesos de `spawn` que sigan vivos.

## Archivos

- Rutas absolutas del sistema (es una app de escritorio). Los separadores `/` y `\` se aceptan en
  Windows.
- `readBytes` / `writeBytes` usan `Uint8Array`. Por debajo van por el canal de binarios del host
  propio, no por JSON (ver BRIDGE.md).
- `watch(ruta, fn)` devuelve un objeto con `close()`.

## Ventanas

```js
const w = await htaw.window.open("ajustes.html", { width: 400, height: 300, modal: true });
const resultado = await w.result;          // lo que la otra página pase a htaw.window.close(valor)
```

`window.open` estándar también funciona y crea una ventana de htaW.

## Errores

Rechazos con `Error` que tiene `code` (`PermissionDenied`, `NotFound`, `AccessDenied`, `Timeout`,
`Cancelled`, `InvalidArgument`, `IOError`) y `message` en el idioma de la interfaz.
