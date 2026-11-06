# WolfGlue — AGENTS.md

Repositorio con dos piezas:
- **WolfGlue**: biblioteca C# que crea ventanas nativas (Windows, Linux, macOS) ocupadas por
  completo por el webview del sistema, para hacer las interfaces en HTML/CSS/JS con la logica en C#.
  Varias ventanas por aplicacion, gestionadas como una sola app.
- **htaW**: ejecutable construido sobre WolfGlue que lanza aplicaciones `.htaw` (solo web, sin C#),
  inspiradas en el concepto de las HTML Applications, con una API JS propia y un CSS con aspecto nativo.

## Decisiones tomadas
- C# / .NET 10 con Native AOT. Sin WinForms, WPF ni frameworks de UI: P/Invoke directo
  (`user32`, GTK3, runtime de Objective-C). Sin reflexion; `System.Text.Json` con generador.
- Webview del sistema, nunca un Chromium propio: WebView2 (Windows), WebKitGTK 4.1 (Linux),
  WKWebView (macOS). El DPI lo gestiona el webview; la ventana solo aplica el tamano sugerido.
- Interfaz COM de WebView2 declarada a mano con `[GeneratedComInterface]`; `WebView2LoaderStatic.lib`
  enlazado estaticamente (un solo `.exe`).
- Un unico entorno de webview compartido por todas las ventanas de la app.
- Contenido servido por host propio (`https://app.local/` por defecto, `GlueAppOptions.Host`) desde recursos
  incrustados, carpeta o zip (`IContentProvider`); sin servidor HTTP local.
- `Main` con `[STAThread]`; un solo hilo de UI; `SynchronizationContext` propio para que los `await` vuelvan a el.
- Tamanos de ventana en unidades logicas y referidos al area de contenido (no al marco).
- Tipos JSON del bridge: la app registra su `JsonSerializerContext` con `Bridge.UseJson` (antes de `Run`);
  los tipos se resuelven en la primera llamada.
- Servicios nativos en `app.Native` (dialogos, portapapeles, pantallas, tema, shell), usables desde cualquier hilo;
  los dialogos se encolan en el hilo de UI (nunca dentro de un callback del webview). Se exponen a la web como
  `glue.dialog/clipboard/screens/theme/shell` salvo `NativeServicesInWeb = false` (htaW lo desactivara y pondra
  sus versiones con permisos).
- Aparicion de ventanas: se muestran al cargar la pagina o, como mucho, a los 500 ms (`ShowDelayMs`), pintadas con
  un color de fondo inicial (opcion, `theme-color` de la pagina, app o tema) y con fundido del `body` (`FadeInMs`).
  El webview va en una hija oculta hasta la carga (WebView2 pinta su gris antes del primer fotograma).
- SmartScreen del webview desactivado por defecto (`UrlReputationCheck`): retrasaba cada navegacion ~2 s.
- Comunicacion web <-> C#: JSON-RPC sobre el canal de mensajes del webview + `fetch` al host propio para binarios.
- WolfGlue se consume **solo como `ProjectReference`** (submodulo git en otros repos); no se publica en NuGet.
  El consumidor importa `WolfGlue.targets` con una linea.
- htaW: dos formatos con la misma extension `.htaw` (HTML con manifiesto JSON en `<head>`, o zip con `htaw.json`).
- htaW: permisos declarados en el manifiesto + confirmacion si el archivo viene de fuera (MOTW / quarantine).
- htaW: `shell.exec` permitido, con permiso por nombre de proceso; `"*.*"` o `""` = todos, con aviso destacado.
- htaW: un proceso por app; origen `https://<id>.htaw.local/` para aislar almacenamiento.
- htaW: `.bat`/`.cmd` solo con shell.exec "todos" (cmd reinterpreta los argumentos); la busqueda de
  ejecutables es solo en el PATH; nunca hay shell intermedio.
- htaW: la ventana de permisos es un mensaje nativo con Cancelar por defecto; decisiones en `trust.json`
  (por hash de archivo y por id de app). Datos en `%LOCALAPPDATA%\htaW` (forzable con `HTAW_DATA`).
- htaW sin argumentos abre su panel (`Launcher.cs` + `Assets/launcher.html`, metodos `launcher.*`) con las acciones de la
  linea de comandos: abrir (el panel se cierra), `--dev` (sigue abierto), `pack`, asociar .htaw y olvidar permisos.
- Icono de app: `icon` del manifiesto (.png/.ico, ruta o `data:`) en barra de titulo y de tareas (`GlueAppOptions.Icon`).
  Icono por archivo en el explorador: descartado por ahora. Cada app fija su AppUserModelID (`htaW.<id>`) para tener su
  propio boton en la barra de tareas.
- Modo desarrollo: `htaw <carpeta|htaw.json>` equivale a `--dev` (arrastrar la carpeta sobre htaw); recientes en
  `recent-dev.json` y en el panel. Integracion con menus de cada SO: anotada, sin implementar (DOC/HTAW-INTEGRACION.md).
- Iconos de app: cuadrado redondeado, vector plano, colores vivos; generados y convertidos con `tools/iconos`
  (DOC/ICONOS.md). Los ejemplos `hola`, `markdown` y `notas` llevan el suyo.
- htaW `bundle`: ejecutable portable = htaw publicado + app al final (cola de 24 bytes `htaWapp\0`) y, en Windows, el
  icono de la app en los recursos del .exe. En un portable todos los argumentos son de la app.
- htaW inserta `/_htaw/htaw.css` + `<style id="htaw-theme">` tras `<head>` en cada HTML (prefijo `/_htaw/` reservado).

## Estado
- Fase 1 (WolfGlue en Windows) completa: `src/WolfGlue` (API, bridge, contenido, servicios nativos `app.Native`,
  backend Win32 + WebView2), `samples/Hola` (ejemplo) y `tests/WolfGlue.Tests` (xUnit, 86 pruebas, pasan).
- Probado a mano en Windows 11, con JIT y con AOT (un `.exe` de ~3,9 MB): llamadas sincronas/asincronas, errores con
  codigo, eventos desde otro hilo, varias ventanas, avisos entre ventanas, dialogo modal con resultado, cierre limpio,
  dialogos de abrir (multiple), guardar y carpeta, mensaje con botones, portapapeles, pantallas, tema y mostrar en
  el explorador.
- Sin probar: cambio de DPI entre monitores, cambio de tema en caliente (evento `glue.theme`), enlace externo.
- Limitacion conocida: el mensaje nativo (`MessageBoxW`) sale en modo claro aunque el sistema este en oscuro.
- Fase 2 (htaW en Windows) casi completa: `src/htaW` (paquete y manifiesto, permisos y confianza, API `htaw.*`,
  CSS nativo, linea de comandos), `samples/htaw` (`hola.htaw`, `markdown.htaw` y la carpeta `notas`) y `tests/htaW.Tests`
  (116 pruebas, pasan). AOT: un `htaw.exe` de ~8,4 MB, sin avisos.
- Probado a mano (JIT y AOT): HTML suelto con CSS nativo y acento del sistema, `exec` permitido, permiso denegado,
  dialogo modal con resultado, zip con confirmacion por Mark of the Web, guardar con `fs` y `dialog`, ajustes
  persistentes, segunda apertura sin preguntar, panel sin argumentos (estado de la asociacion, olvidar permisos, ayuda), `--version` y `pack` con salida redirigida.
- Probado a mano (AOT): ejecutable portable con argumentos e icono propio (barra de titulo, barra de tareas y el del .exe).
- Falta de la fase 2: instancia unica, `onClosing`, ventana de recientes. Sin probar: `--register`,
  `fs.watch`, `spawn`, `http.fetch`; en el panel, empaquetar, abrir y modo desarrollo (los dialogos).
- Evaluado, sin implementar (DOC/HTAW-INTEGRACION.md): icono de cada .htaw en el explorador (descartado por ahora) y
  portables en Linux (.desktop por usuario) y macOS (bundle .app con .icns).
- macOS y Linux: sin empezar (sin `build.sh` hasta entonces).
- Evaluado (DOC/HTAW-OTRAS-PLATAFORMAS.md): htaW en Android e iOS (solo ejecucion) y portables para otros sistemas
  desde cualquiera (`bundle --target` con binarios en `runtimes/`, APK firmado en C#; IPA descartado por la firma).
- Repositorio git sin remoto y sin commits.

## Compilar
`dotnet build`, `dotnet test`, `run.cmd` (htaW con `dotnet run`), `publish.cmd` (htaW AOT -> `htaw.exe` en la raiz),
`build.ps1` (autocompilacion). Ejemplo C#: `dotnet run --project samples/Hola`.
Detalle en [DOC/COMPILACION-Y-PRUEBAS.md](DOC/COMPILACION-Y-PRUEBAS.md).

## Documentacion
- **Manual de htaW** en [DOC/manual/](DOC/manual/README.md) (espanol) y [DOC/manual-en/](DOC/manual-en/README.md)
  (ingles, mismas paginas): uso y guia para desarrolladores de apps (todo lo que htaW expone, con ejemplos). **Regla:** cada capacidad nueva o cambiada de htaW (campo del manifiesto, funcion de la
  API, permiso, clase o variable CSS, opcion de la linea de comandos o del panel) se documenta en el manual en el mismo
  cambio, en los dos idiomas. Lo que no este en el manual no existe para quien hace apps.

Indice y hechos clave en [DOC/README.md](DOC/README.md). Leer solo el documento que haga falta:
arquitectura, plataformas, bridge, consumo como ProjectReference, formato/API/seguridad/estilo de htaW,
compilacion y hoja de ruta.
