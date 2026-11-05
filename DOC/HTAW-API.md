# API JavaScript de htaW

Objeto global `htaw`, inyectado antes de que cargue la página (después del shim `glue` de WolfGlue).
Implementación: `src/htaW/Assets/htaw.js` (JS) y `src/htaW/Api/HtawHost.cs` (C#). Tipos para el editor en
`src/htaW/Assets/htaw.d.ts` (copiarlo junto a la app y referenciarlo con `/// <reference path="htaw.d.ts" />`).

Las funciones devuelven promesas salvo las propiedades. Cada grupo exige el permiso indicado
(ver HTAW-SEGURIDAD.md); sin él, la promesa se rechaza con `code: "PermissionDenied"`.

| Grupo | Permiso | Contenido |
|---|---|---|
| `htaw.app` | — | `id`, `name`, `version`, `args`, `dir` (carpeta de la app; null en un zip), `file`, `dev`, `quit(code?)`, `relaunch()` |
| `htaw.window` | — | `id`, `setTitle`, `setSize(w,h)`, `center`, `minimize`, `maximize`, `restore`, `close(resultado?)`, `open(pagina, opciones)` |
| `htaw.dialog` | `dialog` | `open(o)` → `string[] \| null`, `save(o)` → `string \| null`, `folder(o)` → `string \| null`, `message(texto, o)` → `"ok"\|"cancel"\|"yes"\|"no"` |
| `htaw.fs` | `fs` | `readText`, `writeText(ruta, texto, {append})`, `readBytes` → `Uint8Array`, `writeBytes(ruta, datos)`, `list`, `stat`, `exists`, `mkdir`, `remove(ruta, {recursive})`, `rename(de, a, {overwrite})`, `copy(de, a, {overwrite})`, `watch(ruta, fn, {recursive})` → `{close()}` |
| `htaw.shell` | `shell.open` / `shell.exec` | `open(urlOArchivo)`, `reveal(ruta)`, `exec(nombre, args, o)`, `spawn(nombre, args, o)` |
| `htaw.settings` | — | `get(clave, porDefecto?)`, `set(clave, valor)`, `remove(clave)`, `all()` |
| `htaw.http` | `http` | `fetch(url, o)` sin CORS |
| `htaw.clipboard` | `clipboard` | `readText()`, `writeText(texto)` |
| `htaw.theme` | — | `dark`, `accent`, `fontFamily`, `fontSize`, `highContrast`, `os`, `on("change", fn)` |
| `htaw.env` | `env` | `get(nombre)`; con el permiso, además `home`, `temp`, `documents`, `os`, `arch` |
| `htaw.permissions` | — | permisos concedidos en forma canónica (`"fs"`, `"shell.exec:git"`, `"shell.exec:*"`); `htaw.has(p)` |

## Rutas

- Rutas del sistema. Las relativas se resuelven desde la carpeta de la app (HTML suelto o `--dev`) o desde la
  carpeta del usuario (zip). `~` al principio es la carpeta del usuario.
- `readBytes`/`writeBytes` no pasan por JSON: usan `fetch` contra `/_htaw/fs?path=…` (GET lee, POST escribe),
  con el mismo permiso `fs`.

## Procesos

```js
const r = await htaw.shell.exec("git", ["status", "--short"], { cwd, env, timeout: 10000, input: "…" });
// → { code, stdout, stderr }

const p = await htaw.shell.spawn("ffmpeg", ["-i", entrada, salida], { cwd });
p.on("stdout", t => …).on("stderr", t => …).on("exit", code => …);
await p.write("q");      // entrada estándar
await p.closeInput();
await p.kill();          // con todo su árbol de procesos
const code = await p.exited;
```

- Argumentos **siempre como array**; nunca hay un shell intermedio (ver HTAW-SEGURIDAD.md).
- `encoding`: `utf-8` (por defecto), `oem` (página de códigos de consola de Windows) o un nombre de codificación.
- La salida de `spawn` llega por líneas. Al cerrar la app se terminan los procesos que sigan vivos.
- `cwd` por defecto: la carpeta de la app (o la del usuario en un zip).

## HTTP

```js
const r = await htaw.http.fetch("https://api.ejemplo.com/x", {
  method: "POST", headers: { Authorization: "…" }, body: { a: 1 }, // objeto → JSON
  responseType: "text", timeout: 30000,                             // o "base64"
});
r.status; r.ok; r.headers["content-type"]; r.body; r.json();
```

`body` puede ser texto, objeto (se envía como JSON) o `Uint8Array`/`ArrayBuffer`. Solo `http` y `https`.

## Ventanas

```js
const w = await htaw.window.open("ajustes.html", { width: 400, height: 300, modal: true, resizable: false });
const resultado = await w.result;   // lo que la otra página pase a htaw.window.close(valor); null si se cerró sin valor
const n = await htaw.window.open("otra.html");   // no modal: { id, result: null }
```

`window.open` estándar también crea una ventana de htaW.

## Ajustes

JSON persistente por app en `<datos de htaW>/apps/<id>/settings.json`. Los valores son cualquier JSON.

## Tema

`htaw.theme` refleja el tema del sistema y se actualiza solo; `htaw.theme.on("change", fn)` avisa de los
cambios. El CSS de htaW ya lo aplica (ver HTAW-ESTILO.md).

## Errores

`Error` con `code` (`PermissionDenied`, `NotFound`, `AccessDenied`, `Timeout`, `Cancelled`, `InvalidArgument`,
`IOError`, `MethodNotFound`, `Error`) y `message` en el idioma de la interfaz.

## Pendiente

- `htaw.window.onClosing(fn)` (cancelar el cierre desde la página).
- Evento `secondInstance` (depende de la instancia única, ver HTAW-INTEGRACION.md).
