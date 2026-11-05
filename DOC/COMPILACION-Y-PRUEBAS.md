# Compilación y pruebas

> Estado actual: WolfGlue (Windows), el ejemplo `samples/Hola` y `tests/WolfGlue.Tests` existen y compilan.
> htaW y los scripts `build.*` aún no: lo que se refiere a ellos es lo previsto.

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
dotnet run --project samples/Hola              # ejemplo
dotnet publish samples/Hola -c Release -r win-x64   # ejemplo AOT (vswhere en el PATH)
```

- `run.cmd` / `run.sh`: `dotnet run` del ejemplo con doble clic (pasarán a htaW cuando exista).
- `publish.cmd` (Windows, doble clic): solo `dotnet publish` AOT y copia de `hola.exe` a la raíz, sin
  comprobaciones ni git (pasará a `htaw.exe`). Evita el bloqueo de la directiva de ejecución al lanzar
  `build.ps1` como archivo.
- Hay que ejecutar en Windows con el runtime de WebView2. El ejemplo con AOT ocupa ~3,3 MB en un único `.exe`.
- `build.ps1` / `build.sh`: comprueban requisitos (sin instalar nada), obtienen el código, ejecutan
  `dotnet publish src/htaW/htaW.csproj -c Release -r <RID>` con AOT, copian el binario a la raíz y
  explican cómo instalarlo en el `PATH` y cómo asociar `.htaw`. En macOS montan `htaW.app`.
  Se basan en las plantillas habituales (un solo ejecutable). Para forks: `WOLFGLUE_REPO_URL`.

## Pruebas

### Automáticas (xUnit)
- `tests/WolfGlue.Tests` (48 pruebas, pasan): protocolo del bridge (resultados, tipos propios, errores con
  código, mensajes mal formados, ventana cerrada, eventos y reenvío, `UseJson`), tabla MIME, normalización de
  rutas, proveedores de contenido (recursos, carpeta, compuesto; rechazo de `..`), comprobación de origen, y
  tipos de los servicios nativos (patrones de filtro, forma JSON de opciones y resultados).
- `tests/htaW.Tests`: detección de formato, extracción del manifiesto del HTML, validación del
  manifiesto, reglas de permisos y de `shell.exec` (resolución en `PATH`, intérpretes, rutas),
  detección de origen externo, `pack`.
- Acceso desde las pruebas a lo interno con `InternalsVisibleTo`.
- Las pruebas que tocan variables de entorno van en una colección sin paralelismo.
- La parte nativa (ventanas, webview) no se prueba de forma automática en CI.

### Manuales (lista de comprobación por SO)

Probado en Windows 11 (JIT y AOT) con `samples/Hola`: ventana con barra de título oscura, llamadas
síncronas y asíncronas, error con código, eventos desde otro hilo, segunda ventana, avisos entre ventanas,
diálogo modal con resultado y salida con código 0 al cerrar la última ventana; servicios nativos: abrir
(selección múltiple y filtros), guardar (nombre propuesto, cancelar), elegir carpeta, mensaje con
Sí/No/Cancelar, copiar y pegar texto, pantallas (varios monitores con distinta escala), tema y mostrar en el
explorador. Sin probar todavía: los puntos de DPI, tema en caliente y enlace externo, y todo lo de htaW.
- Abrir, redimensionar y cerrar varias ventanas; modal sobre dueña.
- Mover la ventana entre monitores con distinta escala.
- Cambiar el tema claro/oscuro con la app abierta (contenido y barra de título).
- `window.open` interno y enlace externo.
- Diálogos de archivo y mensajes.
- Abrir `.htaw` con doble clic tras `--register`; HTML suelto y zip.
- Ventana de permisos con archivo descargado; `shell.exec` permitido y denegado.
- Publicación AOT: el binario arranca en una máquina sin .NET.
