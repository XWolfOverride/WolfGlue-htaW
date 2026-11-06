# La API `htaw`

htaW pone en cada página, antes de que se ejecute cualquier script, un objeto global `htaw` con todo lo que la app
puede usar del sistema. No hay que importar nada.

## Conceptos generales

- **Promesas.** Todas las funciones devuelven una `Promise`, salvo las propiedades (`htaw.app.id`, `htaw.theme.dark`…).
  Usa `await` (en un `<script type="module">` también en el nivel superior) o `.then()`.
- **Permisos.** Cada grupo indica el permiso que necesita. Sin él, la promesa se rechaza con
  `code: "PermissionDenied"`. Se declaran en el manifiesto (ver [Permisos](05-permisos.md)).
- **Errores.** Los fallos llegan como `Error` con un `code` y un `message` en el idioma de la interfaz:

  | `code` | Cuándo |
  |---|---|
  | `PermissionDenied` | Falta el permiso, o la operación no está permitida (un programa fuera de la lista de `shell.exec`) |
  | `NotFound` | El archivo, la carpeta o el programa no existe |
  | `AccessDenied` | El sistema deniega el acceso (archivo protegido, en uso…) |
  | `Timeout` | Se superó el tiempo límite (`exec`) |
  | `Cancelled` | La operación se canceló |
  | `InvalidArgument` | Un parámetro no es válido (ruta vacía, URL no permitida, codificación desconocida…) |
  | `IOError` | Error de entrada/salida |
  | `MethodNotFound` | La función no existe en esta versión de htaW |
  | `Error` | Cualquier otro |

  ```js
  try {
    await htaw.fs.readText("datos.json");
  } catch (e) {
    if (e.code === "NotFound") { /* primera vez: no hay datos */ }
    else await htaw.dialog.message(e.message, { kind: "error" });
  }
  ```

- **Rutas.** Son rutas del sistema (`C:\carpeta\archivo.txt`, `/home/…`). Las **relativas** se resuelven desde la
  carpeta de la app (HTML suelto, carpeta de desarrollo o la del ejecutable portable) o, en un `.htaw` empaquetado,
  desde la carpeta del usuario. `~` al principio es la carpeta del usuario (`~/Documentos/notas.txt`).
- **Tipos para el editor.** `src/htaW/Assets/htaw.d.ts` describe toda la API. Cópialo junto a tu app y añade al
  principio de tus scripts `/// <reference path="htaw.d.ts" />` para tener autocompletado en editores como VS Code.

Índice: [`app`](#htawapp) · [`window`](#htawwindow) · [`dialog`](#htawdialog) · [`fs`](#htawfs) ·
[`shell`](#htawshell) · [`settings`](#htawsettings) · [`http`](#htawhttp) · [`clipboard`](#htawclipboard) ·
[`env`](#htawenv) · [`theme`](#htawtheme) · [`permissions`](#htawpermissions-y-htawhas)

---

## `htaw.app`

Datos de la aplicación. **Sin permiso.**

| Miembro | Tipo | Descripción |
|---|---|---|
| `id` | `string` | Identificador (del manifiesto o derivado). |
| `name` | `string` | Nombre visible. |
| `version` | `string \| null` | Versión del manifiesto. |
| `args` | `string[]` | Argumentos con los que se abrió: `htaw app.htaw uno "dos tres"` → `["uno", "dos tres"]`. En un ejecutable portable, todos sus argumentos. |
| `dir` | `string \| null` | Carpeta de la app (HTML suelto, carpeta de desarrollo, ejecutable portable con HTML); `null` en un zip. |
| `file` | `string` | Ruta del `.htaw`, de la carpeta o del `.exe` portable. |
| `dev` | `boolean` | `true` en modo desarrollo. |
| `quit(code?)` | `Promise<void>` | Cierra la app entera con ese código de salida (0 por defecto). |
| `relaunch()` | `Promise<void>` | Vuelve a abrir la app con los mismos argumentos (y modo) y cierra esta. |

```js
document.title = `${htaw.app.name} ${htaw.app.version ?? ""}`;
if (htaw.app.args.length) abrirArchivo(htaw.app.args[0]);   // p. ej. un archivo arrastrado sobre la app
if (htaw.app.dev) console.log("Modo desarrollo");
```

---

## `htaw.window`

La ventana de la página. **Sin permiso.** Más detalle y ejemplos en [Ventanas y comunicación](07-ventanas.md).

| Miembro | Descripción |
|---|---|
| `id` | Identificador de esta ventana. |
| `setTitle(texto)` | Cambia el título. |
| `setSize(ancho, alto)` | Cambia el tamaño del área de contenido (unidades lógicas). |
| `center()` | Centra la ventana en su pantalla. |
| `minimize()`, `maximize()`, `restore()` | Minimiza, maximiza o restaura. |
| `close(resultado?)` | Cierra la ventana. En una ventana modal, `resultado` (cualquier valor JSON) llega a quien la abrió. Cerrar la última ventana cierra la app. |
| `open(pagina, opciones?)` | Abre otra página de la app en una ventana nueva. Devuelve `{ id, result }`. |

Opciones de `open`: `title`, `width`, `height` (por defecto, los de la ventana que la abre), `resizable` (`true`) y
`modal` (`false`). Con `modal: true` la ventana se abre encima de la actual, la bloquea mientras está abierta, y
`result` es una promesa con lo que la otra página pase a `htaw.window.close(valor)` (`null` si se cerró sin valor).

```js
const { result } = await htaw.window.open("ajustes.html", { modal: true, width: 380, height: 220, resizable: false });
const r = await result;            // p. ej. { nombre: "Ana" }, o null si se canceló
```

---

## `htaw.dialog`

Diálogos nativos del sistema. **Permiso `dialog`.** Se muestran sobre la ventana de la página y la bloquean mientras
están abiertos.

### `open(opciones?)` → `Promise<string[] | null>`

Abrir uno o varios archivos. Devuelve las rutas elegidas o `null` si se cancela.

| Opción | Descripción |
|---|---|
| `title` | Título del diálogo. |
| `filters` | Lista de filtros: `{ name: "Imágenes", patterns: ["png", "jpg"] }`. Los patrones pueden ser `"png"`, `".png"` o `"*.png"`. Atajo: un texto suelto (`"*.txt"`) es un filtro con ese nombre y patrón. |
| `defaultPath` | Carpeta inicial (o ruta de archivo). |
| `multiple` | Permitir elegir varios archivos. |

```js
const files = await htaw.dialog.open({
  title: "Elige imágenes",
  filters: [{ name: "Imágenes", patterns: ["png", "jpg", "gif"] }, { name: "Todos", patterns: ["*"] }],
  multiple: true,
});
if (files) for (const f of files) console.log(f);
```

### `save(opciones?)` → `Promise<string | null>`

Elegir dónde guardar. Mismas opciones que `open` (sin `multiple`) y además `fileName` (nombre propuesto) y
`defaultExtension` (sin punto; se añade si el usuario no escribe ninguna). El diálogo del sistema ya pregunta si se
quiere sobrescribir un archivo existente.

```js
const ruta = await htaw.dialog.save({ fileName: "informe.md", defaultExtension: "md", filters: [{ name: "Markdown", patterns: ["md"] }] });
if (ruta) await htaw.fs.writeText(ruta, texto);
```

### `folder(opciones?)` → `Promise<string | null>`

Elegir una carpeta. Opciones: `title`, `defaultPath`.

### `message(texto, opciones?)` → `Promise<"ok" | "cancel" | "yes" | "no">`

Mensaje nativo.

| Opción | Valores | Por defecto |
|---|---|---|
| `title` | texto | el de la ventana |
| `kind` | `"info"`, `"warning"`, `"error"`, `"question"` | `"info"` |
| `buttons` | `"ok"`, `"okCancel"`, `"yesNo"`, `"yesNoCancel"` | `"ok"` |
| `defaultButton` | índice del botón por defecto (0 = el primero) | 0 |

```js
const r = await htaw.dialog.message("¿Guardar los cambios antes de salir?",
  { kind: "question", buttons: "yesNoCancel" });
if (r === "yes") await guardar();
if (r !== "cancel") await htaw.app.quit();
```

> Para preguntas peligrosas («¿Borrar todo?») pon `defaultButton` en el botón de cancelar.

---

## `htaw.fs`

Archivos y carpetas. **Permiso `fs`.** Acceso a cualquier ruta a la que tenga acceso el usuario.

| Función | Devuelve | Descripción |
|---|---|---|
| `readText(ruta)` | `string` | Lee un archivo de texto (UTF-8; respeta BOM UTF-16). |
| `writeText(ruta, texto, { append })` | — | Escribe texto en UTF-8 (crea o reemplaza; con `append: true`, añade al final). |
| `readBytes(ruta)` | `Uint8Array` | Lee un archivo binario. |
| `writeBytes(ruta, datos)` | — | Escribe un `Uint8Array` o `ArrayBuffer`. |
| `list(carpeta)` | `FsEntry[]` | Contenido de una carpeta: primero las carpetas, luego los archivos, por nombre. |
| `stat(ruta)` | `FsStat` | Información de un archivo o carpeta (no falla si no existe: `exists: false`). |
| `exists(ruta)` | `boolean` | Si existe (archivo o carpeta). |
| `mkdir(ruta)` | — | Crea la carpeta y las intermedias que falten. |
| `remove(ruta, { recursive })` | — | Borra un archivo, o una carpeta (vacía, o con todo su contenido si `recursive: true`). **No pasa por la papelera.** |
| `rename(de, a, { overwrite })` | — | Mueve o renombra un archivo o carpeta. |
| `copy(de, a, { overwrite })` | — | Copia un archivo. |
| `watch(ruta, fn, { recursive })` | `{ close() }` | Avisa de cambios en una carpeta o un archivo. |

```ts
FsEntry = { name, path, isDirectory, size, modified }        // modified: fecha ISO (UTC)
FsStat  = { exists, isDirectory, size, modified, created, readOnly }
```

```js
// Leer y escribir JSON
const ruta = "~/Documentos/mi-app/datos.json";
const datos = await htaw.fs.exists(ruta) ? JSON.parse(await htaw.fs.readText(ruta)) : {};
datos.ultimaVez = new Date().toISOString();
await htaw.fs.mkdir("~/Documentos/mi-app");
await htaw.fs.writeText(ruta, JSON.stringify(datos, null, 2));

// Listar solo imágenes
const imagenes = (await htaw.fs.list(carpeta)).filter(e => !e.isDirectory && /\.(png|jpe?g)$/i.test(e.name));

// Binario: mostrar una imagen del disco
const bytes = await htaw.fs.readBytes(imagenes[0].path);
img.src = URL.createObjectURL(new Blob([bytes]));
```

### `watch`

```js
const vigilancia = await htaw.fs.watch(carpeta, (e) => {
  // e.type: "change" | "create" | "delete" | "rename";  e.path;  e.oldPath (solo en rename)
  console.log(e.type, e.path);
}, { recursive: true });
// ...
await vigilancia.close();
```

Los editores suelen guardar en varios pasos, así que un guardado puede producir varios avisos seguidos: agrúpalos
con un temporizador si vas a recargar algo pesado.

---

## `htaw.shell`

Abrir cosas con otras aplicaciones y ejecutar programas.

### `open(urlORuta)` · `reveal(ruta)` — **permiso `shell.open`**

- `open`: abre una URL (`http`, `https`, `mailto`) en el navegador o cliente de correo, o un archivo o carpeta con
  la aplicación que el sistema tenga asignada. **No ejecuta programas**: abrir un `.exe`, `.bat`, `.ps1`, `.lnk`,
  un script… se rechaza salvo que la app tenga `shell.exec: "*.*"`.
- `reveal`: abre el explorador de archivos con el archivo seleccionado.

```js
await htaw.shell.open("https://ejemplo.com/ayuda");
await htaw.shell.open(rutaDelPdf);         // con el visor de PDF del usuario
await htaw.shell.reveal(rutaGuardada);     // "Mostrar en la carpeta"
```

### `exec(programa, args?, opciones?)` → `{ code, stdout, stderr }` — **permiso `shell.exec`**

Ejecuta un programa, espera a que termine y devuelve su código de salida y su salida completa.

| Opción | Descripción |
|---|---|
| `cwd` | Carpeta de trabajo (por defecto, la de la app o la del usuario). |
| `env` | Variables de entorno **añadidas** a las heredadas: `{ LANG: "C" }`. |
| `timeout` | Milisegundos. Si se supera, se mata el proceso (con sus hijos) y falla con `Timeout`. |
| `encoding` | Codificación de la salida: `utf-8` (por defecto), `oem` (consola de Windows) o un nombre como `latin1`. |
| `input` | Texto que se envía a la entrada estándar. |

```js
const r = await htaw.shell.exec("git", ["log", "-5", "--oneline"], { cwd: repo, timeout: 10000 });
if (r.code === 0) mostrar(r.stdout);
else mostrarError(r.stderr);
```

- `programa` es un nombre que se busca en el `PATH` (`"git"`, sin `.exe`) o una ruta. Tiene que estar permitido en
  el manifiesto (`{ "shell.exec": ["git"] }`; ver [Permisos](05-permisos.md)).
- Los **argumentos van siempre como lista** y llegan tal cual al programa: no hay un shell intermedio, así que
  espacios, comillas, `&` o `|` dentro de un argumento no tienen ningún efecto especial. Para usar tuberías o
  redirecciones, hazlo en JavaScript con la salida.
- Un código distinto de 0 **no** es un error de la promesa: compruébalo en `r.code`. La promesa falla si el programa
  no se puede iniciar (`NotFound`), no está permitido (`PermissionDenied`) o se agota el tiempo (`Timeout`).

### `spawn(programa, args?, opciones?)` → proceso — **permiso `shell.exec`**

Inicia un programa y deja que siga funcionando; su salida llega mientras se produce. Opciones: `cwd`, `env`,
`encoding`.

```js
const p = await htaw.shell.spawn("ping", ["-n", "5", "ejemplo.com"]);
p.on("stdout", (texto) => consola.textContent += texto)
 .on("stderr", (texto) => consola.textContent += texto)
 .on("exit", (code) => estado.textContent = "Terminó con código " + code);

await p.write("texto\n");    // escribir en su entrada estándar
await p.closeInput();        // cerrar la entrada (fin de datos)
await p.kill();              // terminarlo, con todos sus procesos hijos
const code = await p.exited; // promesa que se resuelve con el código de salida
```

- La salida llega **por líneas**, cada una con su `\n` final.
- Al cerrar la app, htaW termina los procesos que sigan vivos.

---

## `htaw.settings`

Ajustes persistentes de la app, guardados como JSON en la carpeta de datos de htaW (`apps/<id>/settings.json`).
**Sin permiso**: cada app solo ve los suyos.

| Función | Descripción |
|---|---|
| `get(clave, porDefecto?)` | Valor guardado, o `porDefecto` (o `null`) si no existe. |
| `set(clave, valor)` | Guarda cualquier valor JSON (número, texto, lista, objeto…). Se escribe al momento. |
| `remove(clave)` | Borra una clave. |
| `all()` | Objeto con todas las claves. |

```js
const prefs = await htaw.settings.get("prefs", { fuente: 14, recientes: [] });
prefs.recientes = [ruta, ...prefs.recientes.filter(r => r !== ruta)].slice(0, 10);
await htaw.settings.set("prefs", prefs);
```

> También funcionan `localStorage` e IndexedDB: cada app tiene su propio origen (`https://<id>.htaw.local/`), así que
> sus datos web tampoco se mezclan con los de otras apps. `settings` es más cómodo para preferencias y se puede leer
> desde fuera (es un archivo JSON).

---

## `htaw.http`

Peticiones HTTP **sin las restricciones CORS** del navegador (las hace htaW, no la página). **Permiso `http`.**

### `fetch(url, opciones?)` → respuesta

| Opción | Descripción |
|---|---|
| `method` | `"GET"` por defecto. |
| `headers` | Objeto de cabeceras. |
| `body` | Texto; objeto (se envía como JSON y se pone `Content-Type: application/json` si no se indicó); o `Uint8Array`/`ArrayBuffer` (binario). |
| `responseType` | `"text"` (por defecto) o `"base64"` para respuestas binarias. |
| `timeout` | Milisegundos (100 000 por defecto). |

Respuesta: `{ status, statusText, ok, headers, body, json() }`. Las claves de `headers` van en minúsculas. Un
estado 4xx/5xx **no** rechaza la promesa: comprueba `ok` o `status`.

```js
const r = await htaw.http.fetch("https://api.github.com/repos/dotnet/runtime", {
  headers: { "User-Agent": "mi-app" },
});
if (!r.ok) throw new Error("HTTP " + r.status);
const repo = r.json();
console.log(repo.stargazers_count);

// POST con JSON
await htaw.http.fetch("https://ejemplo.com/api/notas", { method: "POST", body: { texto: "hola" } });

// Descargar un binario y guardarlo
const img = await htaw.http.fetch(url, { responseType: "base64" });
await htaw.fs.writeBytes(destino, Uint8Array.from(atob(img.body), c => c.charCodeAt(0)));
```

Solo se admiten URLs `http` y `https`. El `fetch` estándar del navegador también funciona, pero con CORS: para APIs
propias o públicas que lo permitan no hace falta el permiso `http`.

---

## `htaw.clipboard`

Portapapeles del sistema (texto). **Permiso `clipboard`.**

```js
await htaw.clipboard.writeText(resultado);
const pegado = await htaw.clipboard.readText();   // null si no hay texto
```

---

## `htaw.env`

Entorno del sistema. **Permiso `env`.**

| Miembro | Descripción |
|---|---|
| `get(nombre)` | Valor de una variable de entorno, o `null`. |
| `home` | Carpeta del usuario. |
| `temp` | Carpeta temporal. |
| `documents` | Carpeta de documentos. |
| `os` | `"windows"`, `"macos"` o `"linux"`. |
| `arch` | Arquitectura (`"x64"`, `"arm64"`…). |

Sin el permiso, las propiedades no existen y `get` falla con `PermissionDenied`. (El sistema operativo también está,
sin permiso, en `htaw.theme.os`.)

```js
const destino = htaw.env.documents + "\\Exportaciones";
const proxy = await htaw.env.get("HTTPS_PROXY");
```

---

## `htaw.theme`

Tema del sistema. **Sin permiso.** Se actualiza solo si el usuario cambia el tema con la app abierta.

| Miembro | Descripción |
|---|---|
| `dark` | `true` si el sistema está en modo oscuro. |
| `accent` | Color de acento `#rrggbb`. |
| `fontFamily`, `fontSize` | Fuente de interfaz del sistema y su tamaño en píxeles CSS. |
| `highContrast` | Modo de alto contraste activo. |
| `os` | `"windows"`, `"macos"` o `"linux"`. |
| `on("change", fn)` | Avisa de cada cambio con el tema nuevo. Devuelve una función para dejar de escuchar. |

```js
const pintar = (t) => grafico.setColores({ fondo: t.dark ? "#202020" : "#fff", serie: t.accent });
pintar(htaw.theme);
const dejar = htaw.theme.on("change", pintar);
```

Con `theme: "native"` el CSS ya se adapta solo; esto es para lo que se dibuja con código (canvas, gráficos…). En CSS
también sirve `@media (prefers-color-scheme: dark)`.

---

## `htaw.permissions` y `htaw.has()`

- `htaw.permissions`: lista de permisos concedidos, en forma canónica: `"fs"`, `"dialog"`, `"shell.exec:git"`,
  `"shell.exec:*"` (todos)…
- `htaw.has(permiso)`: si la app lo tiene. `htaw.has("shell.exec")` es `true` si puede ejecutar algún programa.

```js
document.getElementById("exportar").hidden = !htaw.has("fs");
```

---

## Otras capacidades de la página

- **`window.open(url)`** y los enlaces con `target="_blank"`: una página de la propia app se abre en una ventana de
  htaW; una URL externa (`http`, `https`, `mailto`) se abre en el navegador del sistema. Otros esquemas se ignoran.
- **Mensajes entre ventanas**: `glue.emit` / `glue.on`, `BroadcastChannel` y el evento `storage`; ver
  [Ventanas y comunicación](07-ventanas.md).
- **Objeto `glue`**: es la capa de WolfGlue sobre la que se construye `htaw`. De él solo conviene usar `glue.emit`,
  `glue.on` y `glue.off`; sus diálogos, portapapeles y shell están desactivados en htaW (usa los de `htaw`, que
  comprueban los permisos).

## Lo que aún no existe

- `htaw.window.onClosing(fn)`: avisar antes de cerrar la ventana para poder cancelarlo (por ejemplo, con cambios sin
  guardar).
- Instancia única (`singleInstance`) y el evento de «segunda instancia».
- Ventana sin marco (`window.frame: "none"`).
