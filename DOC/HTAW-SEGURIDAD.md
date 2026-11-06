# Seguridad de htaW

Las aplicaciones `.htaw` se ejecutan con los permisos del usuario. Para evitar que un archivo
descargado haga lo que quiera, htaW aplica dos medidas: **permisos declarados** y **confirmación
para archivos de origen externo**.

## Permisos declarados

Lo que no aparece en `permissions` del manifiesto, el bridge lo rechaza (`PermissionDenied`).

| Permiso | Concede |
|---|---|
| `fs` | lectura y escritura de archivos (`htaw.fs`) |
| `dialog` | diálogos nativos |
| `clipboard` | portapapeles |
| `http` | `htaw.http.fetch` sin CORS |
| `env` | variables de entorno y carpetas del usuario |
| `shell.open` | abrir URLs y archivos con la app por defecto, mostrar en el explorador |
| `links.auto` | que los enlaces y `window.open` a otras webs se abran sin acción del usuario |
| `shell.exec` | ejecutar procesos, con la lista de procesos permitidos |

Sin permiso: `htaw.app`, `htaw.window`, `htaw.settings`, `htaw.theme`. Afectan solo a la propia app.

## `shell.exec`

```jsonc
{ "shell.exec": ["git", "ffmpeg"] }   // solo estos procesos
{ "shell.exec": "*.*" }               // cualquier proceso
{ "shell.exec": "" }                  // igual que "*.*"
```

Reglas de comprobación:

1. **Se compara el ejecutable resuelto, no el texto recibido.**
   - Nombre sin ruta (`"git"`): se busca en el `PATH`. En Windows la extensión es opcional y se prueba
     con `PATHEXT`. Se permite si el nombre (sin extensión, sin distinguir mayúsculas en Windows)
     está en la lista.
   - Ruta absoluta o relativa (`"C:\x\git.exe"`, `"./tool"`): solo se permite si esa ruta exacta, una
     vez normalizada, está en la lista, o si el permiso es `*.*`. Un permiso `"git"` no autoriza un
     `git.exe` de otra carpeta.
2. **Sin shell intermedio:** los argumentos se pasan como array al proceso, sin concatenarlos
   en una línea de comandos interpretada.
3. **Intérpretes y shells cuentan como "todos".** Pedir `cmd`, `powershell`, `pwsh`, `bash`, `sh`,
   `zsh`, `fish`, `python`, `node`, `perl`, `ruby`, `wscript`, `cscript`, `mshta`, `osascript`,
   `env`, `sudo`, `xargs` o equivalentes permite ejecutar cualquier cosa. La ventana de permisos
   los muestra con el mismo aviso que `*.*`. La lista vive en un solo sitio del código y se
   puede ampliar.
4. **`shell.open` no ejecuta programas.** Abrir con la app por defecto un ejecutable o script (`.exe`,
   `.bat`, `.cmd`, `.ps1`, `.vbs`, `.js`, `.msi`, `.lnk`, `.sh`, `.app`, `.command`, archivos con bit
   de ejecución…) se rechaza salvo que la app tenga `shell.exec: "*.*"`.

## Confirmación de primera ejecución

### Cuándo se pregunta
- El archivo viene de fuera:
  - Windows: tiene *Mark of the Web* (flujo alternativo `Zone.Identifier` con `ZoneId` ≥ 3).
  - macOS: tiene el atributo extendido `com.apple.quarantine`.
  - Linux: no hay un marcador estándar fiable. Se pregunta si el archivo está en la carpeta de
    descargas del usuario (`XDG_DOWNLOAD_DIR`) o si tiene el atributo `user.xdg.origin.url`.
- O la app ya se había aceptado y una versión nueva **pide más permisos** que los aceptados.

Las apps locales sin marca de origen externo se abren sin preguntar. Aun así, solo pueden hacer lo
que declaran.

### Ventana de permisos (nativa, no web)
- Nombre, `id`, versión y ruta del archivo; origen (descargado de Internet / local).
- Lista de permisos en lenguaje claro, en español o inglés.
- Para `shell.exec` con lista: los nombres de los procesos.
- Para `shell.exec` con `*.*`, `""` o un intérprete: aviso destacado
  «Esta aplicación puede ejecutar cualquier programa con tus permisos».
- Botones: Permitir y abrir / Cancelar. Por defecto, Cancelar.

### Recuerdo de la decisión
- Se guarda en `<datos de usuario>/htaW/trust.json` con clave = hash SHA-256 del archivo, y el
  conjunto de permisos aceptado.
- Si el archivo cambia, el hash cambia y se vuelve a preguntar si tiene marca de origen externo.
- `htaw --forget <archivo|id>` borra la decisión guardada.

## Contenido web

- Solo se cargan páginas del origen propio de la app. Las navegaciones externas (enlaces, `window.open`) se abren en el navegador del sistema solo si son `http`, `https` o `mailto`; el resto se descarta. Así una app sin `shell.open` no puede abrir archivos ni programas con un `file:` u otro protocolo.
- Además, solo si las **inició el usuario** (clic o tecla, `IsUserInitiated` de WebView2); las que la página lanza por su cuenta se descartan, salvo con el permiso `links.auto` (`GlueAppOptions.ExternalLinksRequireUserAction = false`). Evita que una app abra pestañas sin parar. Para que el dato sea fiable, WolfGlue no usa `ExecuteScript` en la página (le daría activación de usuario, como un clic): el fundido de entrada se arranca con un mensaje del webview.
- `htaw.http` existe para que la app no tenga que salir de su origen para hablar con Internet.
- DevTools solo con `htaw --dev`.

## Estado de la implementación

Implementado en `src/htaW/Security/` (`Permissions.cs`, `Trust.cs`) y `Api/HtawHost.cs`. Detalles fijados al
implementar:

- **Scripts `.bat`/`.cmd`** (Windows): `cmd.exe` los interpreta con sus propias reglas de comillas, así que un
  argumento podría inyectar órdenes. Solo se ejecutan si el permiso ya equivale a "todos" (`*.*` o un intérprete).
- Para `shell.exec` el nombre se busca solo en el `PATH` (no en la carpeta actual ni en la de la app).
- La ventana de permisos es un mensaje nativo con Aceptar/Cancelar y **Cancelar por defecto**; con icono de
  aviso si el archivo viene de fuera o si los permisos equivalen a ejecutar cualquier cosa.
- `trust.json` guarda dos cosas: `files` (hash → permisos aceptados para ese contenido exacto) y `apps`
  (id → unión de permisos aceptados). Las apps locales se registran en `apps` al abrirse sin preguntar, para
  detectar después una versión que pida más.
- Origen externo: hecho en Windows (`Zone.Identifier`); macOS y Linux, pendientes con sus backends.
- `--dev` no pregunta, pero aplica los permisos.
- htaW crea la `GlueApp` con `NativeServicesInWeb = false`: la web no ve `glue.dialog`, `glue.shell`… solo las
  versiones `htaw.*` con permisos. `glue.window.*` sí está disponible (no necesita permiso).
