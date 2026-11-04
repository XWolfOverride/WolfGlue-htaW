# Compilación y pruebas

> Estado actual: **no hay código**. Este documento describe lo previsto.

## Requisitos de desarrollo

- SDK de .NET 10.
- Compilador nativo para Native AOT:
  - Windows: Visual Studio Build Tools con «Desarrollo para escritorio con C++» (localizado con `vswhere`).
  - Linux: `clang`, enlazador (`ld`/`lld`), cabeceras de zlib.
  - macOS: Xcode Command Line Tools.
- Para ejecutar: runtime de WebView2 (Windows), `libwebkit2gtk-4.1` + GTK3 (Linux).

Native AOT no compila de un sistema a otro: cada binario se compila en su sistema de destino.

## Comandos previstos

```
dotnet build                          # todo
dotnet test                           # pruebas xUnit
dotnet run --project src/htaW -- samples/hola.htaw
```

- `run.cmd` / `run.sh`: lanzan `dotnet run` de htaW con doble clic.
- `publish.cmd` (Windows, doble clic): solo `dotnet publish` AOT y copia de `htaw.exe` a la raíz, sin
  comprobaciones ni git. Evita el bloqueo de la directiva de ejecución al lanzar `build.ps1` como archivo.
- `build.ps1` / `build.sh`: comprueban requisitos (sin instalar nada), obtienen el código, ejecutan
  `dotnet publish src/htaW/htaW.csproj -c Release -r <RID>` con AOT, copian el binario a la raíz y
  explican cómo instalarlo en el `PATH` y cómo asociar `.htaw`. En macOS montan `htaW.app`.
  Se basan en las plantillas habituales (un solo ejecutable). Para forks: `WOLFGLUE_REPO_URL`.

## Pruebas

### Automáticas (xUnit)
- `tests/WolfGlue.Tests`: protocolo del bridge (serialización, ids, errores, cancelación), tabla MIME,
  proveedores de contenido (recursos, zip, carpeta; rechazo de `..`), lógica independiente del SO.
- `tests/htaW.Tests`: detección de formato, extracción del manifiesto del HTML, validación del
  manifiesto, reglas de permisos y de `shell.exec` (resolución en `PATH`, intérpretes, rutas),
  detección de origen externo, `pack`.
- Acceso desde las pruebas a lo interno con `InternalsVisibleTo`.
- Las pruebas que tocan variables de entorno van en una colección sin paralelismo.
- La parte nativa (ventanas, webview) no se prueba de forma automática en CI.

### Manuales (lista de comprobación por SO)
- Abrir, redimensionar y cerrar varias ventanas; modal sobre dueña.
- Mover la ventana entre monitores con distinta escala.
- Cambiar el tema claro/oscuro con la app abierta (contenido y barra de título).
- `window.open` interno y enlace externo.
- Diálogos de archivo y mensajes.
- Abrir `.htaw` con doble clic tras `--register`; HTML suelto y zip.
- Ventana de permisos con archivo descargado; `shell.exec` permitido y denegado.
- Publicación AOT: el binario arranca en una máquina sin .NET.
