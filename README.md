# WolfGlue

>
> ⚠️ 🚧 Proyecto en fase de diseño. (pre-pre-alpha)
>

Interfaces web para aplicaciones de escritorio en C#, sin frameworks de UI y con Native AOT.

WolfGlue crea ventanas nativas en Windows, Linux y macOS y las llena con el **webview que ya trae
el sistema** (WebView2, WebKitGTK o WKWebView). La interfaz se escribe en HTML, CSS y JavaScript;
la lógica, en C#. No se incluye ningún navegador: el ejecutable es pequeño y no necesita .NET instalado.

El repositorio incluye **htaW**, un lanzador de aplicaciones `.htaw`: aplicaciones de escritorio
hechas solo con web, con una API para archivos, diálogos, procesos y ventanas, y un estilo por
defecto que imita el del sistema donde se ejecutan.

> **Estado:** la biblioteca funciona en Windows (con un ejemplo en `samples/Hola`); htaW, Linux y
> macOS están en diseño. Detalle en [DOC/](DOC/README.md).

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

Detalles en [DOC/HTAW-FORMATO.md](DOC/HTAW-FORMATO.md), [DOC/HTAW-API.md](DOC/HTAW-API.md) y
[DOC/HTAW-SEGURIDAD.md](DOC/HTAW-SEGURIDAD.md).

## Compilar desde fuentes

*(Previsto; los scripts aún no existen.)*

htaW se compila en tu máquina con un solo comando. El script comprueba que tienes lo necesario
(**no instala nada**; si falta algo, te dice qué y cómo instalarlo), descarga la última versión,
compila con Native AOT y deja el ejecutable en la raíz del proyecto.

Necesitas: git, el SDK de .NET 10 y un compilador nativo (Visual Studio Build Tools con
«Desarrollo para escritorio con C++» en Windows; `clang`, `zlib` y un enlazador en Linux;
Xcode Command Line Tools en macOS). Para **ejecutar** en Linux hace falta WebKitGTK 4.1
(`libwebkit2gtk-4.1`); en Windows, el runtime de WebView2 (incluido en Windows 11).

```powershell
irm https://raw.githubusercontent.com/XWolfOverride/WolfGlue/main/build.ps1 | iex           # Windows
```
```bash
curl -fsSL https://raw.githubusercontent.com/XWolfOverride/WolfGlue/main/build.sh | bash    # Linux y macOS
```

**Para actualizar a una nueva versión, vuelve a ejecutar el mismo comando.** Para compilar un
fork, define `WOLFGLUE_REPO_URL` con la URL de tu repositorio.

El script explica cómo añadir `htaw` al `PATH` (para tu usuario o para todo el sistema) y cómo
asociar la extensión `.htaw` (`htaw --register`).

## Licencia

MIT. Ver [LICENSE](LICENSE).
