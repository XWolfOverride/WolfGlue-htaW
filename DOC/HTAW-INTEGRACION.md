# Integración de htaW con el sistema

## Línea de comandos

```
htaw <archivo.htaw> [args...]     abre la app; args llegan a htaw.app.args (también los que empiezan por "-")
htaw --dev <carpeta|archivo>      modo desarrollo (ver abajo)
htaw <carpeta|htaw.json>          igual que --dev
htaw pack <carpeta> [-o salida]   crea un .htaw (zip) y valida el manifiesto
htaw bundle <carpeta|archivo> [-o salida] [--base htaw]
                                  crea un ejecutable portable con la app dentro (ver abajo)
htaw --register / --unregister    asocia la extensión .htaw para el usuario actual
htaw --forget <archivo|id>        olvida la confianza concedida a una app
htaw --version / --help
```

- Sin argumentos (doble clic sobre `htaw.exe`): **panel de htaW** con las mismas acciones (ver abajo).
  *(Versión 1.1, implementado: apps recientes en el panel. Las registra el propio `htaw` cada vez que abre una app, como ya
  hace con las de modo desarrollo (`recent-dev.json`), así que salen también las abiertas con doble clic o desde la
  línea de comandos, no solo desde el panel. Lista aparte (`recent.json`, las 10 últimas): nombre e icono del
  manifiesto, ruta y fecha; Abrir y Quitar; las que ya no existen se marcan. Los ejecutables portables no se anotan:
  no pasan por htaw.)*
- `htaw.exe` es una aplicación gráfica (`WinExe`): los comandos de consola (`--help`, `pack`…) escriben en la
  consola desde la que se lanzaron (`AttachConsole`) o en la salida redirigida si la hay.
- Códigos de salida: 0 bien; 1 error (formato, argumentos, E/S); 2 el usuario rechazó los permisos.
- Errores de formato al abrir una app: mensaje nativo con el motivo.
- Idioma: según el idioma del sistema, forzable con `HTAW_LANG=es|en` (y `WOLFGLUE_LANG` para los textos de WolfGlue).
- Datos de htaW en `%LOCALAPPDATA%\htaW` (`trust.json`, `apps/<id>/settings.json`, carpeta del webview);
  `HTAW_DATA` cambia la carpeta (útil en pruebas).

Implementación: `src/htaW/Program.cs` y `Cli.cs`.

## Panel de htaW (sin argumentos)

Ventana propia hecha con `htaw.css` (mismo aspecto que una app con `theme: native`), en el origen
`https://launcher.htaw.local/`. Página incrustada (`Assets/launcher.html`) y métodos `launcher.*` del bridge,
internos de htaW (no son API para las apps). Implementación: `src/htaW/Launcher.cs`.

| Sección | Equivale a | Detalle |
|---|---|---|
| Abrir una aplicación | `htaw <archivo> [args]` | Diálogo de abrir; campo opcional de argumentos (espacios separan, comillas agrupan). Lanza un proceso nuevo y **el panel se cierra**. |
| Modo desarrollo | `htaw --dev <carpeta\|archivo>` | Botones Carpeta… / Archivo… (`.htaw`, `.html`); usa los mismos argumentos. El panel **sigue abierto**. Debajo, los **recientes** (nombre del manifiesto y ruta) con Abrir y Quitar; se refrescan al volver al panel. |
| Crear un ejecutable portable | `htaw bundle` | Desde carpeta… / Desde archivo… → guardar `.exe` → Ejecutar / Mostrar en la carpeta. Necesita el `htaw.exe` publicado (con `dotnet run` da un error claro). |
| Crear un .htaw | `htaw pack <carpeta> -o <salida>` | Elegir carpeta → se valida antes de preguntar el destino → guardar (propone `<carpeta>.htaw` junto a ella). Al terminar: Ejecutar / Mostrar en la carpeta. |
| Extensión .htaw | `--register` / `--unregister` | Muestra el estado: sin asociar, asociada a este `htaw.exe`, a otra copia de htaW (con su ruta) o a otro programa. |
| Permisos recordados | `--forget <id>` | Lista de `trust.json` por id (permisos y archivos aceptados); Olvidar por app y Olvidar todo (pide una segunda pulsación). |
| Acerca de | `--version`, `--help` | Versión, ruta del ejecutable, carpeta de datos (se abre al pulsarla) y la ayuda de la línea de comandos. Al final, el copyright y la licencia MIT. |

Los errores (manifiesto no válido al empaquetar, etc.) salen en un aviso dentro del panel.

## Modelo de procesos

- **Un proceso por app abierta.** Si una app falla, no arrastra a las demás.
- En Windows cada proceso fija su AppUserModelID (`htaW.<id>`): cada app es su propio botón en la barra de tareas,
  con su icono, en vez de agruparse todas bajo `htaw.exe`. (Anclar una app a la barra de tareas necesitaría además un
  acceso directo con ese id; queda para más adelante.)
- Todos comparten la carpeta de datos del webview; cada app tiene su propio origen
  (`https://<id>.htaw.local/`), así que su almacenamiento web está aislado.
- `singleInstance: true` (`Api/SingleInstance.cs`): la primera ejecución toma un mutex con nombre (`Local\htaW-<hash>`,
  hash del id de la app, el usuario y la sesión) y atiende una tubería con nombre del mismo nombre (`CurrentUserOnly`;
  en Linux y macOS .NET la implementa con un socket de dominio Unix). Las siguientes, al ver el mutex ocupado, se
  conectan, reciben el PID de la primera (en Windows llaman a `AllowSetForegroundWindow` para que pueda ponerse
  delante), le pasan `{ args, cwd }` en JSON y terminan con código 0. La primera pone delante su ventana principal y
  emite `htaw.secondInstance`. Si la primera no contesta en 5 s, la segunda arranca normal. `relaunch()` suelta el
  mutex antes de lanzar la nueva instancia.

## Asociación de archivos (`--register`)

Por usuario, sin permisos de administrador.

| SO | Mecanismo | Estado |
|---|---|---|
| Windows | `HKCU\Software\Classes\.htaw` → ProgID `htaW.App` con `shell\open\command` = `"<ruta>\htaw.exe" "%1" %*` e icono del ejecutable; aviso al shell con `SHChangeNotify` | hecho (`FileAssociation.cs`) |
| Linux | `~/.local/share/applications/htaw.desktop` + tipo MIME en `~/.local/share/mime/packages/htaw.xml` | implementado |
| macOS | bundle `.app` con `CFBundleDocumentTypes`; los archivos llegan por el delegado de la app, no por `argv` | pendiente |

`--unregister` solo borra `.htaw` si sigue apuntando a htaW. Conviene registrar el ejecutable ya copiado en su
sitio definitivo (la ruta queda en el registro).

## Ejecutable portable (`bundle`)

Une un `htaw` publicado y una app (carpeta con `htaw.json`, `.htaw` zip o HTML suelto) en un solo ejecutable que se
puede copiar y lanzar sin instalar nada. Implementación: `src/htaW/Bundler.cs`, `Package/EmbeddedApp.cs`, `ExeIcon.cs`.

- **Formato**: `[ejecutable de htaW][app][longitud: int64][tipo: int32 (1 HTML, 2 zip)][versión: int32][`htaWapp\0`]`,
  todo en little-endian. El sistema carga el ejecutable por sus cabeceras y no lee lo que va detrás. Una carpeta se
  empaqueta antes como zip.
- **Arranque**: lo primero que hace `Main` es mirar los últimos 24 bytes de su propio ejecutable. Si hay app, la abre
  y **todos los argumentos son de la app** (la línea de comandos de htaW no existe en un portable). El zip se lee del
  propio `.exe` sin extraerlo; un HTML incrustado se sirve como `index.html` y sus rutas relativas, desde la carpeta
  del ejecutable. `htaw.app.file` es la ruta del `.exe`.
- **Seguridad**: igual que una app suelta. El hash de confianza es el de la app incrustada; el origen (MOTW) se mira en
  el `.exe`. Los datos van a la misma carpeta de htaW, por id de app.
- **Icono** (Windows): si la app declara `icon`, se cambian los recursos `RT_GROUP_ICON`/`RT_ICON` de la copia con
  `UpdateResourceW` (antes de añadir la app, porque reescribe el archivo). Se mantiene el nombre del grupo existente,
  así la clase de ventana también lo carga. Un `.ico` entra con todos sus tamaños; un `.png`, como imagen única (Windows
  admite PNG en iconos). Solo se puede hacer desde Windows; desde otro sistema queda el de htaW y se avisa.
- **Base**: por defecto, el propio `htaw` que se ejecuta, que tiene que estar publicado con Native AOT (con
  `dotnet run` el `.exe` es un lanzador que necesita sus DLL). `--base` indica otro: sirve para crear el portable de otra
  arquitectura o de otro sistema. Si la base ya lleva una app, se quita. Salida por defecto: junto al origen, con su
  nombre y `.exe` si la base es de Windows.
- Pendiente: el recurso de versión sigue diciendo htaW (el Administrador de tareas muestra esa descripción); un `.exe`
  sin firmar descargado de Internet hace que Windows avise antes de ejecutarlo, como cualquier programa sin firma.

### Modo portable: qué se desactiva (revisión para la versión 1.1)

Regla: un portable **es la app**, no htaW. Nunca muestra el panel ni la línea de comandos de htaW, así que **no guarda
nada cuyo único fin sea verse en el panel** ni toca la configuración de htaW. Solo guarda lo que la propia app
necesita para funcionar. Se decide en `Program.RunEmbedded` / `HtawPackage.Embedded`; cada función nueva tiene que
mirar esta tabla.

| Función | En un portable | Estado |
|---|---|---|
| Línea de comandos de htaW (`--dev`, `pack`, `bundle`, `--register`, `--forget`, `--help`…) | No existe: todos los argumentos son de la app | Ya es así |
| Panel de htaW | Nunca se abre | Ya es así |
| Recientes de desarrollo (`recent-dev.json`) | No se anotan (un portable no tiene modo desarrollo) | Ya es así |
| **Apps recientes** (`recent.json`, 1.1) | **No se anotan**: solo sirven para el panel | Implementado (`Security/AppRecents.cs`) |
| Modo desarrollo: DevTools, teclas de navegador, recarga automática, avisos del manifiesto | Desactivados | Ya es así (`dev: false`) |
| `htaw.app.relaunch()` | Relanza el `.exe` sin añadir la ruta de la app | Ya es así |
| Asociación de `.htaw` | No la toca nunca | Ya es así |
| Confianza (`trust.json`) | **Se mantiene**: evita volver a preguntar los permisos y tiene función de seguridad, no es solo para el panel. Se guarda con la ruta del `.exe` y el hash de la app incrustada | Ya es así. Para olvidarla hace falta un htaw instalado: `htaw --forget <portable.exe>` debe aceptar un ejecutable portable (hoy solo `.htaw`); pendiente |
| Datos de la app (`settings.json`, almacenamiento web en `WebView2/`) | Se mantienen en `%LOCALAPPDATA%\htaW` por id de app, como una app suelta (el portable es «sin instalar», no «con los datos al lado») | Ya es así |
| AppUserModelID (`htaW.<id>`) | Se mantiene (botón propio en la barra de tareas) | Ya es así |
| Instancia única | Igual que una app suelta (mismo id = misma instancia, se abra suelta o portable) | Ya es así |
| **Notificaciones** (1.1) | Funcionan. El registro de su AppUserModelID (`HKCU\Software\Classes\AppUserModelId\htaW.<id>`: nombre e icono) es necesario para que Windows muestre nombre e icono; el icono se copia a la carpeta de datos de la app | A implementar |
| **Bandeja** (1.1) | Funciona igual | A implementar |
| Comprobar actualizaciones de htaW (si algún día existe) | No: el portable lleva su htaw dentro | — |

### Linux y macOS (evaluación, sin implementar)

- **Linux**: el mismo formato funciona (un ELF tampoco lee lo que va detrás) y el icono de la ventana se pone en tiempo
  de ejecución (`gtk_window_set_icon`), así que barra de título y de tareas salen igual que en Windows. Lo que no existe es
  el icono *dentro* del ejecutable: los escritorios toman el icono de un `.desktop`. Opciones: (a) que el portable, con
  una opción como `--install`, escriba `~/.local/share/applications/<id>.desktop` y el icono en
  `~/.local/share/icons/hicolor/<tamaño>/apps/` (sencillo, por usuario); (b) generar un AppImage, que lleva icono y
  `.desktop` dentro, pero exige su runtime y un sistema de archivos SquashFS (más dependencias). Recomendado (a).
- **macOS**: el portable es un bundle `.app`: `Contents/MacOS/htaw`, `Contents/Info.plist` con `CFBundleIconFile`,
  `Contents/Resources/icon.icns` y la app en `Contents/Resources/app.htaw` (**no** añadida al binario: en Apple Silicon
  todo binario debe ir firmado, aunque sea con firma ad hoc, y añadirle datos invalida la firma). Un `.icns` puede llevar
  directamente imágenes PNG (`ic07`–`ic10`, de 128 a 1024 px), así que se puede generar sin herramientas de Apple; el
  bundle es una carpeta y se puede montar desde cualquier sistema. Ese bundle también resuelve los archivos abiertos
  desde el Finder (ver PLATAFORMAS.md).

## Icono de cada app en el explorador (evaluación, descartado por ahora)

Hoy todos los `.htaw` muestran el icono de htaW (`DefaultIcon` del ProgID). Para que cada archivo muestre el
`icon` de su manifiesto hace falta código que el explorador cargue por archivo: con solo el registro no se
puede (`DefaultIcon = "%1"` solo sirve para archivos con recursos de icono, como `.exe` o `.ico`).

### Opciones en Windows

| Opción | Cómo | Vistas en las que se ve | Riesgo | Coste |
|---|---|---|---|---|
| **A. Proveedor de miniaturas** (`IThumbnailProvider` + `IInitializeWithStream`) | DLL COM registrada en `HKCU\Software\Classes\.htaw\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}` | Iconos medianos, grandes y muy grandes, contenido, mosaicos, panel de vista previa | Bajo: el sistema la carga **fuera del explorador** (proceso sustituto) y cachea el resultado | Medio |
| B. Controlador de iconos (`IExtractIconW` + `IPersistFile`) | DLL COM en `HKCU\Software\Classes\htaW.App\shellex\IconHandler` y `DefaultIcon = %1` | Todas, también detalles y lista | Alto: se carga **dentro del explorador**; un fallo o una espera lo bloquea | Medio-alto |
| C. Accesos directos | `.lnk` en el escritorio o el menú Inicio con el icono de la app (extraído a `apps/<id>/icon.ico`) | Solo el acceso directo, no el `.htaw` | Ninguno | Bajo |

Las dos primeras son una **DLL nativa** (Native AOT admite bibliotecas compartidas: `NativeLib=Shared`, exportando
`DllGetClassObject` y `DllCanUnloadNow` con `[UnmanagedCallersOnly(EntryPoint = …)]` y la fábrica COM con
`ComWrappers` generado, como ya se hace con WebView2). Registro por usuario en
`HKCU\Software\Classes\CLSID\{…}\InProcServer32`, sin administrador, desde `--register` y el panel.

### Leer el icono de cada formato
- **Zip**: entrada indicada en `icon` dentro del zip. Leer el directorio central y una entrada es rápido y funciona
  con el `IStream` que da el sistema.
- **HTML suelto**: el manifiesto está al principio del archivo (basta leer los primeros KB). El problema es que un
  `icon` **relativo** apunta a un archivo junto al HTML, y el proveedor de miniaturas recibe un flujo, no la ruta.
  Propuesta: en el HTML admitir `icon` como `data:image/png;base64,…` (el archivo sigue siendo autosuficiente) o
  tomar el `<link rel="icon" href="data:…">` del `<head>`; con ruta relativa, usar `IInitializeWithItem` para
  saber la ruta (o caer en el icono de htaW).
- Formatos: `.png` y `.ico`. PNG a `HICON` con `CreateIconFromResourceEx` (acepta PNG) o WIC; a `HBITMAP` para la
  miniatura con WIC. SVG no (necesitaría un renderizador).
- Límites: tamaño máximo del icono (p. ej. 1 MB) y del manifiesto leído; ante cualquier error, nada (el sistema
  pone el icono de htaW). El código analiza archivos ajenos: mínimo y sin red.

### Recomendación
1. Primero el `icon` del manifiesto en la **ventana y la barra de tareas** (pendiente de la fase 2): da el lector
   de iconos de los dos formatos y la conversión a `HICON`, que luego reutiliza la extensión.
2. **Opción A** (miniaturas) con el valor `TypeOverlay` del ProgID para que en la esquina aparezca el icono de
   htaW: aspecto de "app de htaW" y sin riesgo para el explorador. En las vistas de detalles y lista seguiría el
   icono de htaW.
3. La **opción B** solo si hace falta el icono propio también en detalles y lista, sabiendo que corre dentro del
   explorador.
4. La C es independiente y útil por sí misma ("Crear acceso directo" en el panel).

Distribución: la DLL sería un segundo binario. Para mantener **un solo archivo**, `htaw.exe` puede llevarla
incrustada y escribirla en `%LOCALAPPDATA%\htaW\` al registrar (y borrarla al quitar el registro); `build.*` y
`publish.cmd` tendrían que publicar antes la DLL. Hay que decidir si se acepta así o como archivo aparte.

En Linux es mucho más sencillo: un archivo `.thumbnailer` (`Exec=htaw --thumbnail %i %o %s`) y el propio
`htaw` genera el PNG. En macOS haría falta una extensión de Quick Look dentro del bundle `.app`.

## Modo desarrollo (`--dev`)

- Acepta una **carpeta sin empaquetar** (con `htaw.json`) o un HTML suelto.
- `htaw <carpeta>` y `htaw <ruta>/htaw.json` (sin `--dev`) también abren en modo desarrollo. Así funciona arrastrar la
  carpeta sobre `htaw.exe` o sobre un acceso directo suyo, y «Abrir con» sobre `htaw.json`. `--dev`, `pack` y `bundle`
  aceptan igualmente la ruta del `htaw.json` en vez de la carpeta.
- Lo que se abre bien en modo desarrollo se anota en `recent-dev.json` (carpeta de datos de htaW; los 10 últimos, sin
  repetir), que el panel muestra como recientes. El panel solo lanza rutas de esa lista.

### Integración con el sistema (pendiente)

Anotado para más adelante, sin plugins:
- **Windows** (`HKCU`, desde `--register` y el panel): verbo en el menú contextual de las carpetas
  (`Directory\shell`, `%1`) y en su fondo (`Directory\Background\shell`, `%V`); en Windows 11 salen en «Mostrar más
  opciones». «Enviar a» con un `.lnk` en `shell:sendto` (`IShellLinkW`). El registro no puede mirar dentro de la
  carpeta, así que la opción sale en todas; `.json` es de otro programa, así que no hay doble clic en `htaw.json`.
- **Linux**: `.desktop` con `MimeType=inode/directory` (sale en «Abrir con» de cualquier carpeta) y un tipo MIME propio
  por nombre exacto `htaw.json`, asociado a ese `.desktop` (doble clic real). Menús propios de cada gestor con archivos
  de configuración: scripts de Nautilus, service menus de Dolphin, actions de Nemo, acciones de Thunar.
- **macOS**: en `htaw.app`, `CFBundleDocumentTypes` con carpetas (soltar en el Dock o en el icono) y un servicio
  (`NSServices`) para carpetas en el menú contextual del Finder.
- **Acceso directo de desarrollo** desde el panel (con el icono de la app): específico de cada sistema (`.lnk`,
  `.desktop`, lanzador `.app`); en duda.
- Plugins solo si se quiere que la opción salga únicamente en carpetas con `htaw.json` o en el primer nivel del menú de
  Windows 11: `IExplorerCommand` + paquete MSIX firmado (Windows), extensiones de Nautilus/Dolphin (Linux), Finder Sync
  (macOS). No compensan por ahora.
- DevTools (F12) y menú contextual del motor activos.
- Recarga todas las ventanas al guardar cualquier archivo de la carpeta (agrupando ráfagas de 300 ms).
- No pide confirmación de permisos, pero **sí los aplica**, igual que en producción.
- Los avisos del manifiesto salen en la consola de DevTools.

## Empaquetado (`pack`)

- Valida la carpeta exactamente como al abrirla (`htaw.json`, `entry` existente, permisos).
- Crea el zip con rutas relativas y `/`, en orden estable. Excluye `.git`, `.vs`, `.vscode`, `.idea`,
  `node_modules` y otros `.htaw`.
- Salida por defecto: `<carpeta>.htaw` junto a la carpeta. Se escribe en un temporal y se renombra.

Implementación: `src/htaW/Packer.cs`.
