# Compilación y pruebas

> Estado actual: WolfGlue y htaW funcionan en Windows, con sus pruebas y ejemplos. `build.sh` (Linux y
> macOS) llegará con esos backends: lo que se refiere a él es lo previsto.

## Requisitos de desarrollo

- SDK de .NET 10.
- Compilador nativo para Native AOT:
  - Windows: Visual Studio Build Tools con «Desarrollo para escritorio con C++» (localizado con `vswhere`).
  - Linux: `clang`, enlazador (`ld`/`lld`), cabeceras de zlib.
  - macOS: Xcode Command Line Tools.
- Para ejecutar: runtime de WebView2 (Windows), `libwebkit2gtk-4.1` + GTK3 (Linux).

Native AOT no compila de un sistema a otro: cada binario se compila en su sistema de destino.

## Comandos

```
dotnet build                                   # solución WolfGlue.slnx
dotnet test                                    # pruebas xUnit
dotnet run --project src/htaW -- samples/htaw/hola.htaw     # htaW
dotnet run --project src/htaW -- --dev samples/htaw/notas    # htaW en modo desarrollo
dotnet run --project samples/Hola                           # ejemplo C# de WolfGlue
dotnet publish src/htaW -c Release -r win-x64               # htaW AOT (vswhere en el PATH)
```

- `run.cmd` / `run.sh`: `dotnet run` de htaW con doble clic (sin argumentos abre el panel de htaW).
- `publish.cmd` (Windows, doble clic): solo `dotnet publish` AOT y copia de `htaw.exe` a la raíz, sin
  comprobaciones ni git. Evita el bloqueo de la directiva de ejecución al lanzar `build.ps1` como archivo.
- `build.ps1` (hecho): comprueba git, SDK de .NET 10 y Build Tools de C++ (sin instalar nada) y avisa si falta el
  runtime de WebView2; usa el repo actual o clona/actualiza; publica `src/htaW/htaW.csproj` con AOT, copia
  `htaw.exe` a la raíz y explica cómo instalarlo en el `PATH` y asociar `.htaw`. Para forks: `WOLFGLUE_REPO_URL`.
- `build.sh` (previsto, con los backends de Linux y macOS): lo mismo; en macOS montará `htaW.app`.
- Tamaños con AOT: `htaw.exe` ~9,3 MB; el ejemplo `hola.exe` ~3,9 MB. Sin avisos de AOT.
- `samples/htaw/notas.htaw` se genera con `htaw pack samples/htaw/notas` (está en `.gitignore`).

## Pruebas

### Automáticas (xUnit)
- `tests/WolfGlue.Tests` (130 pruebas, pasan): generador de servicios del bridge, imagen del portapapeles (PNG/DIB), barra de título (validación, iconos, nombre de fuente), protocolo del bridge (resultados, tipos propios, errores con
  código, mensajes mal formados, ventana cerrada, eventos y reenvío, `UseJson`), tabla MIME, normalización de
  rutas, proveedores de contenido (recursos, carpeta, compuesto; rechazo de `..`), comprobación de origen, y
  tipos de los servicios nativos (patrones de filtro, forma JSON de opciones y resultados).
- `tests/htaW.Tests` (127 pruebas, pasan): instancia única, línea de comandos, barra de título del manifiesto y de cada página, extracción del manifiesto del HTML, validación
  del manifiesto e ids, permisos y forma canónica, reglas de `shell.exec` (resolución en `PATH` pasado como
  parámetro, nombres frente a rutas, intérpretes, `*.*`), archivos ejecutables para `shell.open`, Mark of the
  Web (flujo alternativo real en NTFS), reglas de `trust.json`, apertura de HTML/zip/carpeta, `pack`
  (exclusiones y errores), inserción del CSS en el HTML (nunca antes del doctype), contraste del acento y
  codificaciones de `exec`, y del panel: división de argumentos, ruta del programa asociado y olvidar todo; app incrustada (cola, HTML y zip), `bundle` (salida, base reutilizada, icono en un `.exe` real), y `icon` del manifiesto (`data:`, ruta, avisos y errores). Carpeta o `htaw.json` como modo desarrollo y lista de recientes. En WolfGlue, lectura de `.ico`/`.png` y `HICON` al tamaño pedido.
- Acceso desde las pruebas a lo interno con `InternalsVisibleTo`.
- Las pruebas que tocan variables de entorno van en una colección sin paralelismo.
- La parte nativa (ventanas, webview) no se prueba de forma automática en CI.

### Manuales (lista de comprobación por SO)

Probado en Windows 11 (JIT y AOT) con `samples/Hola`: ventana con barra de título oscura, llamadas
síncronas y asíncronas, error con código, eventos desde otro hilo, segunda ventana, avisos entre ventanas,
diálogo modal con resultado y salida con código 0 al cerrar la última ventana; servicios nativos: abrir
(selección múltiple y filtros), guardar (nombre propuesto, cancelar), elegir carpeta, mensaje con
Sí/No/Cancelar, copiar y pegar texto, pantallas (varios monitores con distinta escala), tema y mostrar en el
explorador.

Probado en Windows 11 (JIT y AOT) con htaW: `hola.htaw` con CSS nativo y el acento del sistema, `exec` de un
programa permitido, permiso no declarado rechazado, diálogo modal con resultado; `notas.htaw` (zip) marcado
como descargado: ventana de permisos (Cancelar por defecto), guardar con `fs`/`dialog`, ajustes persistentes y
segunda apertura sin preguntar; panel sin argumentos (estado de la asociación, lista y olvido de permisos, ayuda, cancelar un diálogo); carpeta pasada como argumento (como al arrastrarla sobre htaw) y recientes en el panel (Abrir); ejecutable portable (AOT) con argumentos y su icono en la barra de título, la de tareas y el `.exe`; barra de título propia con `samples/htaw/barra` (ver BARRA-DE-TITULO.md); `--version` y `pack` con salida
redirigida.

Probado también: mover ventanas entre monitores con distinta escala (con la barra de título propia), `--register` y abrir una carpeta en modo desarrollo con su lista de recientes. Sin probar todavía: el tema en caliente (el enlace externo ya está probado: abre el navegador); en htaW, `fs.watch`,
`spawn` y `http.fetch`.
- Abrir, redimensionar y cerrar varias ventanas; modal sobre dueña.
- Mover la ventana entre monitores con distinta escala.
- Cambiar el tema claro/oscuro con la app abierta (contenido y barra de título).
- `window.open` interno y enlace externo.
- Diálogos de archivo y mensajes.
- Abrir `.htaw` con doble clic tras `--register`; HTML suelto y zip.
- Ventana de permisos con archivo descargado; `shell.exec` permitido y denegado.
- Publicación AOT: el binario arranca en una máquina sin .NET.

## Registro de diagnóstico (solo Debug, Windows)

En las compilaciones **Debug** (`dotnet run`, `run.cmd`, las pruebas) WolfGlue escribe siempre un registro, **`wolfglue-debug.log` en el
directorio de trabajo del proceso al arrancar** (con `run.cmd`, la raíz del repositorio; está en `.gitignore`). En **Release** (lo que
publica `publish.cmd`) no existe: las llamadas se eliminan al compilar. Implementación: `Platform/Windows/DebugLog.cs`.

- Cada ejecución empieza con una línea `--- sesion <pid> <fecha>`; si el archivo pasa de 2 MB se empieza de nuevo.
- `WOLFGLUE_DEBUG_LOG=<archivo>` cambia el archivo y `WOLFGLUE_DEBUG_LOG=0` lo desactiva (útil con `$env:` en PowerShell o `set` en `cmd`).
- Contenido: un resumen por segundo con los contadores `contenedor-pinta` (el contenedor del webview se repinta) y `ventana-borra`, y
  una línea `EVENTO` por cada repintado forzado bajo el webview (tamaño, DPI, barra o fondo con el efecto activo), cada tecla que llega
  al menú de ventana indicando por qué vía (`tecla (webview)`, `tecla (ventana)` o `tecla (pagina)`) y el registro de
  `AcceleratorKeyPressed`.
- Para comprobar que las animaciones de la página no provocan repintados: `samples\htaw\repintado`.

## Canal de control para pruebas automáticas (solo Debug, Windows)

Para comprobar la interfaz con números y archivos, sin controlar el escritorio ni mirar capturas. **Solo compilaciones Debug** y solo
si se arranca con `WOLFGLUE_DEBUG_CONTROL=1` (en Release no existe). La app abre una tubería con nombre `wolfglue-debug-<pid>` (accesible
solo por el usuario actual) y deja su nombre en `wolfglue-debug.pipe` (directorio de trabajo). Se le mandan comandos JSON, uno por línea,
y responde un JSON por línea (`{"ok":true,"result":…}` o `{"ok":false,"error":"…"}`). Implementación: `Platform/Windows/WinDebugControl.cs`;
cliente: `tools/debug/wg.ps1`.

```powershell
$env:WOLFGLUE_DEBUG_CONTROL = "1"
.\run.cmd --dev samples\htaw\fondo            # en otra consola, o en segundo plano
.\tools\debug\wg.ps1 estado
.\tools\debug\wg.ps1 eval "document.title"
.\tools\debug\wg.ps1 eval "await htaw.window.setBackdrop('blurred'); return htaw.theme.backdrop;"
.\tools\debug\wg.ps1 captura -File C:\temp\ventana.png
.\tools\debug\wg.ps1 pixel -X 10 -Y 90 -W 100 -H 20
```

| Comando | Qué hace |
|---|---|
| `estado` | Vuelca en JSON el estado de la app y de cada ventana: posición, tamaño de ventana y de cliente, DPI, visible, minimizada, tema; fondo pedido y activo y si el contenedor del webview lleva `WS_CLIPCHILDREN`; barra de título (modo, alto, rectángulos del título, de los botones de ventana, de cada elemento y de cada menú, con su visibilidad, hover, menú abierto, modo teclado). Todos los rectángulos en píxeles físicos. |
| `eval` | Ejecuta JavaScript en la página (`-Window <id>` para otra ventana; por defecto la principal). Vale una **expresión** (también con `await`) o un **bloque con `return`**. Devuelve el resultado como JSON; un error de la página vuelve como `ok:false`. `-Timeout` en ms. |
| `captura` | Guarda el área de cliente en un PNG (`-File`; por defecto `wolfglue-debug-<pid>-<n>.png` en el directorio de trabajo). `-Mode screen` (por defecto) copia lo que se ve en pantalla (la ventana debe estar visible y sin tapar); `-Mode print` usa `PrintWindow`. |
| `pixel` | Color medio de una zona del área de cliente (`-X -Y -W -H`, píxeles físicos): `avg`, `first`, `distinctColors` (hasta 64) y `uniform`. Sirve para comprobar fondos, cristal o restos sin mirar la imagen. |

Ejemplos de comprobaciones: que tras cambiar de barra el menú esté donde dice `estado`; que una zona bajo la barra sea `uniform`
(sin restos de un menú anterior); que `eval "htaw.theme.backdrop"` coincida con `estado.backdrop.active`.

Límites: no simula teclado ni ratón reales (para el menú de ventana, `eval` con eventos sintéticos o `htaw.window.menu`), ni
cambios de DPI entre monitores.
