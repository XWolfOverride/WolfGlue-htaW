# WolfGlue

> **English:** this README is in Spanish. Read the [English summary](#english-summary) below, and the
> [htaW manual in English](DOC/manual-en/README.md).

Interfaces web para aplicaciones de escritorio en C#, sin frameworks de UI y con Native AOT.

WolfGlue crea ventanas nativas en Windows, Linux y macOS y las llena con el **webview que ya trae
el sistema** (WebView2, WebKitGTK o WKWebView). La interfaz se escribe en HTML, CSS y JavaScript;
la lógica, en C#. No se incluye ningún navegador: el ejecutable es pequeño y no necesita .NET instalado.

El repositorio incluye **htaW**, un lanzador de aplicaciones `.htaw`: aplicaciones de escritorio
hechas solo con web, con una API para archivos, diálogos, procesos y ventanas, y un estilo por
defecto que imita el del sistema donde se ejecutan.

> **Estado:** la biblioteca y htaW funcionan en Windows (ejemplos en `samples/Hola` y `samples/htaw`);
> Linux y macOS están en diseño. Detalle en [DOC/](DOC/README.md).

## WolfGlue (biblioteca)

- Ventanas nativas sin WinForms: `user32` en Windows, GTK3 en Linux, Cocoa en macOS.
- Varias ventanas por aplicación, con dueño y modales, gestionadas como una sola app.
- El webview gestiona el DPI y los cambios de escala entre monitores.
- Puente JavaScript ↔ C# basado en promesas y eventos, sin reflexión.
- Contenido web incrustado en el ejecutable (un solo binario).
- Servicios nativos comunes: diálogos de archivo, mensajes, portapapeles, tema del sistema
  (claro/oscuro, color de acento), pantallas y apertura de archivos o URLs.

Se usa como `ProjectReference` (ver [DOC/CONSUMO.md](DOC/CONSUMO.md)).

## htaW (lanzador de aplicaciones .htaw)

Una aplicación `.htaw` puede ser:

- **Un único HTML** con un bloque de manifiesto en el `<head>` (nombre, tamaño de ventana, permisos…).
- **Un zip renombrado a `.htaw`** con un `htaw.json` y los archivos de la aplicación.

htaW las abre con doble clic, aplica un CSS con aspecto nativo y ofrece la API `htaw.*`
(archivos, diálogos, procesos, ventanas, ajustes, HTTP sin CORS…). Lo que puede hacer cada
aplicación se declara en su manifiesto y, si el archivo viene de Internet, se pide confirmación
antes de abrirla por primera vez.

Ejemplo mínimo (`hola.htaw`):

```html
<!doctype html>
<html>
<head>
  <script type="application/htaw+json">
  { "id": "com.ejemplo.hola", "name": "Hola", "window": { "width": 480, "height": 320 },
    "permissions": ["dialog"] }
  </script>
</head>
<body>
  <h1>Hola</h1>
  <button type="submit" onclick="htaw.dialog.message('¡Hola!')">Saludar</button>
</body>
</html>
```

Uso:

```
htaw app.htaw [argumentos]     abre la aplicación
htaw --dev carpeta             desarrollo: DevTools y recarga al guardar
htaw pack carpeta              empaqueta una carpeta como .htaw
htaw --register                abre los .htaw con doble clic (para tu usuario)
```

**Manual de htaW** (uso y desarrollo de aplicaciones, con ejemplos): [en español](DOC/manual/README.md) ·
[in English](DOC/manual-en/README.md).

Detalles técnicos en [DOC/HTAW-FORMATO.md](DOC/HTAW-FORMATO.md), [DOC/HTAW-API.md](DOC/HTAW-API.md),
[DOC/HTAW-SEGURIDAD.md](DOC/HTAW-SEGURIDAD.md) y [DOC/HTAW-INTEGRACION.md](DOC/HTAW-INTEGRACION.md).

## Ejemplos

La carpeta [samples](samples) reúne programas de ejemplo, y irá creciendo con más:

- [samples/htaw](samples/htaw): aplicaciones `.htaw` listas para abrir con htaW.
  - `hola.htaw`: controles con el estilo nativo, mensajes, ejecución de un programa, un permiso denegado y un
    diálogo propio con resultado.
  - `markdown.htaw`: editor de Markdown con vista previa en vivo (clara u oscura), separador ajustable y panel de estilo
    plegable.
  - `notas/`: app de varias páginas en carpeta (se abre en modo desarrollo o se empaqueta con `htaw pack`).
- [samples/Hola](samples/Hola): ejemplo de WolfGlue desde C# (`dotnet run --project samples/Hola`).

## Compilar desde fuentes

htaW se compila en tu máquina con un solo comando. El script comprueba que tienes lo necesario
(**no instala nada**; si falta algo, te dice qué y cómo instalarlo), descarga la última versión,
compila con Native AOT y deja el ejecutable en la raíz del proyecto.

Necesitas: git, el SDK de .NET 10 y un compilador nativo (Visual Studio Build Tools con
«Desarrollo para escritorio con C++» en Windows; `clang`, `zlib` y un enlazador en Linux;
Xcode Command Line Tools en macOS). Para **ejecutar** en Linux hace falta WebKitGTK 4.1
(`libwebkit2gtk-4.1`); en Windows, el runtime de WebView2 (incluido en Windows 11).

```powershell
irm https://raw.githubusercontent.com/XWolfOverride/WolfGlue-htaW/main/build.ps1 | iex           # Windows
```
```bash
curl -fsSL https://raw.githubusercontent.com/XWolfOverride/WolfGlue-htaW/main/build.sh | bash    # Linux y macOS (cuando estén soportados)
```

Dentro de la carpeta del proyecto también puedes usar `publish.cmd` (doble clic, solo compila).

**Para actualizar a una nueva versión, vuelve a ejecutar el mismo comando.** Para compilar un
fork, define `WOLFGLUE_REPO_URL` con la URL de tu repositorio.

El script explica cómo añadir `htaw` al `PATH` (para tu usuario o para todo el sistema) y cómo
asociar la extensión `.htaw` (`htaw --register`).

## English summary

**WolfGlue** is a C# library for web-based desktop user interfaces, without UI frameworks and with Native AOT. It
creates native windows (Windows today; Linux and macOS in design) filled with the **webview the system already
ships** (WebView2, WebKitGTK or WKWebView): the interface is HTML, CSS and JavaScript, the logic is C#. No browser is
bundled, so the executable is small and does not need .NET installed. It offers several windows per app, a
promise-based JavaScript ↔ C# bridge without reflection, web content embedded in the executable and common native
services (file dialogs, messages, clipboard, system theme, screens, opening files and URLs). It is consumed as a
`ProjectReference`.

**htaW** is a launcher for `.htaw` applications: desktop apps made only with web technologies, either a single HTML
file with a manifest block in its `<head>` or a renamed zip with an `htaw.json`. htaW opens them with a double
click, applies a native-looking stylesheet and provides the `htaw.*` API (files, dialogs, processes, windows,
settings, HTTP without CORS…). What each app may do is declared in its manifest, and apps downloaded from the
Internet ask for confirmation before their first run. It also has a development mode (DevTools, reload on save),
packaging (`htaw pack`) and portable executables with the app and its icon inside (`htaw bundle`). Running
`htaw` with no arguments opens a panel with all these actions.

- htaW manual (usage and full developer guide, with examples): [DOC/manual-en](DOC/manual-en/README.md).
- Examples: [samples](samples) (`.htaw` apps in [samples/htaw](samples/htaw), a C# WolfGlue app in
  [samples/Hola](samples/Hola)); more will be added over time.
- Technical documentation (Spanish): [DOC](DOC/README.md).

**Building from source.** One command builds htaW on your machine with Native AOT. The script checks the
requirements (git, .NET 10 SDK and a native compiler: Visual Studio Build Tools with C++ on Windows) without
installing anything, downloads the latest version and leaves the executable in the project root:

```powershell
irm https://raw.githubusercontent.com/XWolfOverride/WolfGlue-htaW/main/build.ps1 | iex           # Windows
```

To update, run the same command again. Running `.htaw` apps needs the WebView2 runtime (included in Windows 11).

License: MIT, see [LICENSE](LICENSE).

## Licencia

MIT. Ver [LICENSE](LICENSE).
