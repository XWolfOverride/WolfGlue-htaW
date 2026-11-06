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
- Tamaños con AOT: `htaw.exe` ~8,4 MB; el ejemplo `hola.exe` ~3,9 MB. Sin avisos de AOT.
- `samples/htaw/notas.htaw` se genera con `htaw pack samples/htaw/notas` (está en `.gitignore`).

## Pruebas

### Automáticas (xUnit)
- `tests/WolfGlue.Tests` (86 pruebas, pasan): protocolo del bridge (resultados, tipos propios, errores con
  código, mensajes mal formados, ventana cerrada, eventos y reenvío, `UseJson`), tabla MIME, normalización de
  rutas, proveedores de contenido (recursos, carpeta, compuesto; rechazo de `..`), comprobación de origen, y
  tipos de los servicios nativos (patrones de filtro, forma JSON de opciones y resultados).
- `tests/htaW.Tests` (117 pruebas, pasan): línea de comandos, extracción del manifiesto del HTML, validación
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
segunda apertura sin preguntar; panel sin argumentos (estado de la asociación, lista y olvido de permisos, ayuda, cancelar un diálogo); carpeta pasada como argumento (como al arrastrarla sobre htaw) y recientes en el panel (Abrir); ejecutable portable (AOT) con argumentos y su icono en la barra de título, la de tareas y el `.exe`; `--version` y `pack` con salida
redirigida.

Sin probar todavía: los puntos de DPI y tema en caliente (el enlace externo ya está probado: abre el navegador); en htaW, `--register`, `fs.watch`,
`spawn` y `http.fetch`.
- Abrir, redimensionar y cerrar varias ventanas; modal sobre dueña.
- Mover la ventana entre monitores con distinta escala.
- Cambiar el tema claro/oscuro con la app abierta (contenido y barra de título).
- `window.open` interno y enlace externo.
- Diálogos de archivo y mensajes.
- Abrir `.htaw` con doble clic tras `--register`; HTML suelto y zip.
- Ventana de permisos con archivo descargado; `shell.exec` permitido y denegado.
- Publicación AOT: el binario arranca en una máquina sin .NET.
