# Permisos y seguridad

Una app htaW se ejecuta con los permisos del usuario, así que htaW limita lo que puede hacer: **solo lo que declara
en `permissions`**. Pide solo lo que de verdad uses: es lo que verá el usuario antes de abrir tu app si la descarga.

## Lista de permisos

| Permiso | Da acceso a | Cómo lo ve el usuario |
|---|---|---|
| `fs` | `htaw.fs` (leer, escribir, listar, borrar, vigilar archivos) | Leer y escribir archivos |
| `dialog` | `htaw.dialog` (abrir, guardar, carpeta, mensajes) | Mostrar diálogos del sistema |
| `clipboard` | `htaw.clipboard` | Leer y escribir el portapapeles (texto e imágenes) |
| `http` | `htaw.http.fetch` (sin CORS) | Conectarse a Internet sin restricciones |
| `env` | `htaw.env` (variables de entorno y carpetas del usuario) | Leer variables de entorno y carpetas del usuario |
| `shell.open` | `htaw.shell.open` y `htaw.shell.reveal` | Abrir archivos y direcciones con otras aplicaciones |
| `links.auto` | Que los enlaces y `window.open` a otras webs se abran sin que el usuario haga clic (por ejemplo, desde un temporizador) | Abrir paginas web en el navegador sin que lo pidas |
| `shell.exec` | `htaw.shell.exec` y `htaw.shell.spawn`, con lista de programas | Ejecutar estos programas: … |

Sin permiso, siempre disponibles: `htaw.app`, `htaw.window`, `htaw.settings`, `htaw.theme`, `htaw.permissions`, el
`fetch` estándar (con CORS), `localStorage`/IndexedDB y los mensajes entre ventanas. Solo afectan a la propia app.

```json
"permissions": ["fs", "dialog", "clipboard", { "shell.exec": ["git", "ffmpeg"] }]
```

Un permiso desconocido o mal escrito es un **error**: la app no se abre y se explica el motivo.

## `shell.exec`: la lista de programas

```jsonc
{ "shell.exec": ["git", "ffmpeg"] }        // solo estos programas
{ "shell.exec": ["C:\\Tools\\conv.exe"] }  // una ruta concreta
{ "shell.exec": "*.*" }                    // cualquier programa (también "")
```

Cómo se comprueba cada llamada a `exec`/`spawn`:

- **Nombre sin ruta** (`"git"`): se busca en el `PATH` (en Windows, con o sin `.exe`) y se permite si el nombre está
  en la lista. Solo se busca en el `PATH`, no en la carpeta de la app.
- **Con ruta** (`"C:\\Tools\\conv.exe"`, `"./tool"`): solo si esa ruta exacta está en la lista (o con `*.*`). Tener
  `"git"` no permite ejecutar otro `git.exe` de otra carpeta.
- **Sin shell intermedio**: los argumentos llegan tal cual al programa, sin que nada los interprete. Un argumento con
  `& del *` es solo texto.
- **Intérpretes = todo.** Permitir `cmd`, `powershell`, `pwsh`, `bash`, `sh`, `python`, `node`, `perl`, `ruby`,
  `wscript`, `cscript`, `mshta`, `osascript`, `env`, `sudo`… equivale a permitir cualquier programa (con ellos se
  puede ejecutar lo que sea), y el usuario lo ve con el mismo aviso que `*.*`.
- **Scripts `.bat`/`.cmd`** (Windows): solo con `*.*`, porque `cmd` reinterpreta los argumentos.

Recomendación: lista cerrada con los programas concretos. Si necesitas un script, mejor un programa propio que la
app llame directamente.

## `shell.open` no ejecuta programas

`htaw.shell.open` abre archivos con su aplicación (un PDF con el visor, una carpeta con el explorador) y URLs en el
navegador. Pero abrir un ejecutable o un script (`.exe`, `.bat`, `.cmd`, `.ps1`, `.vbs`, `.js`, `.msi`, `.lnk`…) es
ejecutarlo, así que se rechaza salvo que la app tenga `shell.exec: "*.*"`.

## Qué ve el usuario

- Si el archivo de la app viene **de Internet** (Windows lo marca al descargarlo), htaW pregunta antes de abrirla,
  enseñando la lista de permisos. El botón por defecto es Cancelar.
- Si una versión nueva de una app ya aceptada (mismo `id`) **pide más permisos**, vuelve a preguntar.
- Las apps locales se abren sin preguntar, pero con los mismos límites.
- En **modo desarrollo** nunca se pregunta, pero los permisos **sí se aplican**: lo que funciona en desarrollo
  funcionará igual en la versión final.

Por eso conviene mantener el `id` estable entre versiones y no ampliar permisos sin necesidad.

## Las páginas de la app

- Solo se cargan páginas de la propia app (su origen `https://<id>.htaw.local/`). Los enlaces y `window.open` a otras
  webs se abren en el navegador del sistema (solo `http`, `https` y `mailto`); el resto se ignora. No hace falta ningún
  permiso, pero **solo si los inicia el usuario** (un clic o una tecla): si la página navega a otra web por su
  cuenta (`location.href = …` en un temporizador, un `click()` simulado), no se abre nada. Así una app no puede abrir
  pestañas sin que el usuario lo pida. La app que lo necesite de verdad declara el permiso `links.auto`; y
  `htaw.shell.open` (con su permiso) abre URLs siempre.
- DevTools solo existen en modo desarrollo.
- Cada app tiene su propio origen: su `localStorage`, IndexedDB y cookies no los ven las demás.
- La ruta `/_htaw/` está reservada para htaW: no pongas archivos tuyos en una carpeta llamada `_htaw`.
