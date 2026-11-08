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
- Generador de Roslyn `src/WolfGlue.Generators` (enlazado por WolfGlue.targets): `[GlueService]` en una clase ->
  `bridge.Register(servicio)` con parametros posicionales y `GlueServicesTypeScript` (.d.ts); en JS `glue.services.x.y()`.
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
- Enlaces y `window.open` a otras webs (http, https, mailto): navegador del sistema sin permiso, solo si los inicia el
  usuario (`IsUserInitiated`); el permiso `links.auto` quita esa condicion (`ExternalLinksRequireUserAction`). El host no
  usa `ExecuteScript` en las paginas (daria activacion de usuario falsa): el fundido se arranca con un mensaje.
- Barra de titulo con elementos propios (DOC/BARRA-DE-TITULO.md): modelo comun limitado a lo que permite la barra de
  herramientas nativa de macOS (sin estilos propios): modos `title`/`compact`/`labels`; botones (conmutador, menu,
  partido), grupos, panel, busqueda nativa, imagen, espacio, un centrado; iconos con nombre comun, PNG/ICO tenidos o
  fuente propia. Por ventana: `GlueWindowOptions.TitleBar`, bloque `htaw+json` de la pagina (`PageTitleBar`),
  `window.open({ titlebar })` o en caliente (`glue.window.titlebar`). Windows: marco del sistema + franja pintada.
- Teclas de navegador del webview (F5, Ctrl+P, Ctrl+F, zoom, F12) desactivadas (`GlueAppOptions.BrowserKeys`, htaW solo
  en modo desarrollo): macOS y Linux no las tienen. Portapapeles con imagenes (siempre PNG hacia la web).
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
- htaW `recent.json`: las 10 ultimas apps abiertas con `htaw` (nombre, icono hasta 64 KB como `data:`, ruta, fecha), anotadas al abrir bien
  una app (tambien con doble clic) y nunca en modo desarrollo (`recent-dev.json`) ni en un portable. `htaw --forget` acepta tambien un portable.
- Notificaciones (`app.Native.Notifications`, `glue.notifications`, `htaw.notify` con permiso `notifications`): toasts de Windows por COM
  sin WinRT proyectado (`WinToast.cs`, llamadas por tabla virtual, manejador del evento Activated como objeto COM nativo); AUMID
  `htaW.<id>` registrado en `HKCUSoftwareClassesAppUserModelId`; como mucho 2 botones; mismo `id` sustituye; se retiran al terminar `Run`.
- Bandeja (`app.Native.Tray`, `glue.tray`, `htaw.tray` con permiso `tray`): `Shell_NotifyIconW` en la ventana de mensajes; menu obligatorio, una
  opcion `default` (clic izquierdo, una vez aunque sea doble clic); icono monocromo teñido segun `SystemUsesLightTheme` o `color: true`;
  `tray.keepRunning` (manifiesto) oculta la ventana principal al cerrar solo mientras haya icono; `htaw.window.show/hide`.
- Fondo del sistema (`GlueBackdrop`, `window.backdrop`, `htaw.window.setBackdrop`, `htaw.theme.backdrop`): `DWMWA_SYSTEMBACKDROP_TYPE` +
  `DwmExtendFrameIntoClientArea(-1)` (compilacion 22621+, y efectos de transparencia activos); con el efecto activo el webview es
  transparente (alfa 0), el fondo propio se pinta negro y `<html>` lleva `glue-backdrop-tinted|blurred`; evento `glue.backdrop`.
  La barra de titulo propia no se repinta (se acepta el corte).
  El repintado bajo el webview (con fondo activo) se aisla: solo al cambiar tamano, DPI, barra o fondo se quita `WS_CLIPCHILDREN` del contenedor,
  se repinta en negro (`RepaintUnderWebView`) y se repone; el contenido de la pagina no lo provoca (registro `wolfglue-debug.log`, solo Debug, `DebugLog.cs`; DOC/COMPILACION-Y-PRUEBAS.md).
- Canal de control de depuracion (solo Debug, `WOLFGLUE_DEBUG_CONTROL=1`, `WinDebugControl.cs`, cliente `tools/debug/wg.ps1`): `estado` (JSON de ventanas, barra,
  menus, fondo), `eval` (JS en la pagina, con await), `captura` (PNG) y `pixel` (color de una zona): permite probar sin controlar el escritorio.

## Estado
- Fase 1 (WolfGlue en Windows) completa: `src/WolfGlue` (API, bridge, contenido, servicios nativos `app.Native`,
  backend Win32 + WebView2), `samples/Hola` (ejemplo) y `tests/WolfGlue.Tests` (xUnit, 163 pruebas, pasan).
- Probado a mano en Windows 11, con JIT y con AOT (un `.exe` de ~3,9 MB): llamadas sincronas/asincronas, errores con
  codigo, eventos desde otro hilo, varias ventanas, avisos entre ventanas, dialogo modal con resultado, cierre limpio,
  dialogos de abrir (multiple), guardar y carpeta, mensaje con botones, portapapeles, pantallas, tema y mostrar en
  el explorador.
- Probado a mano: cambio de DPI entre monitores (tambien con la barra de titulo propia). Sin probar: cambio de tema en
  caliente (evento `glue.theme`).
- Probado a mano (htaW, JIT y AOT): enlaces y `window.open` externos se abren con un clic real y se ignoran si los lanza
  la página sola (temporizador, `click()` simulado); con `links.auto`, se abren igualmente.
- Limitacion conocida: el mensaje nativo (`MessageBoxW`) sale en modo claro aunque el sistema este en oscuro.
- Fase 2 (htaW en Windows) casi completa: `src/htaW` (paquete y manifiesto, permisos y confianza, API `htaw.*`,
  CSS nativo, linea de comandos), `samples/htaw` (`hola.htaw`, `markdown.htaw` y las carpetas `notas` y `barra`) y `tests/htaW.Tests`
  (139 pruebas, pasan). AOT: un `htaw.exe` de ~9,3 MB, sin avisos.
- Probado a mano (JIT y AOT): HTML suelto con CSS nativo y acento del sistema, `exec` permitido, permiso denegado,
  dialogo modal con resultado, zip con confirmacion por Mark of the Web, guardar con `fs` y `dialog`, ajustes
  persistentes, segunda apertura sin preguntar, panel sin argumentos (estado de la asociacion, olvidar permisos, ayuda), `--version` y `pack` con salida redirigida.
- Probado a mano (htaW, JIT y AOT): barra de titulo propia en sus tres modos, varias ventanas cada una con su barra o sin
  ella, busqueda, menus, conmutador y cambio en caliente (`samples/htaw/barra`).
- Probado a mano (AOT): ejecutable portable con argumentos e icono propio (barra de titulo, barra de tareas y el del .exe).
- Hecho y probado a mano: instancia unica (`singleInstance`, mutex + tuberia con nombre, evento `secondInstance`) y
  `htaw.window.onClosing` (confirmacion de cierre por la pagina). Probado a mano: `--register`, abrir una carpeta en modo
  desarrollo y su lista de recientes. `fs.watch`, `spawn` y `http.fetch`: prueba automatica de humo correcta (eventos create/change/rename/delete, ping con stdout y exit, exec permitido y rechazado, GET/POST con JSON); ejemplo `samples/htaw/pruebas` probado a mano (las tildes de ping se arreglan con `encoding: "oem"`).
- Ciclo de vida (DOC/CICLO-DE-VIDA.md): por defecto `GlueQuitMode.MainWindowClosed` (la primera ventana es la principal; al cerrarla se
  cierra todo, `lifecycle: "last"` en htaW para vivir mientras haya ventanas). Ventana Acerca de: `GlueApp.ShowAboutBox()`,
  `GlueWindowOptions.IsAbout`, `AboutPage` (htaW `about`), generica con icono, nombre, version, autor y pie «Desarrollado con»
  (`GlueAbout.cs`); `htaw.app.showAboutBox()`; papel `about` en opciones de menu y botones de barra. Autor en el manifiesto.
  Linux: hamburguesa en todos los casos, Xfce con barra clasica solo si la implementacion dual sale barata (DOC/MENUS-LINUX.md).
- Versiones en DOC/HOJA-DE-RUTA.md. **1.1 (Windows)**: hechas y probadas a mano bandeja, notificaciones, apps recientes (y revision del modo
  portable) y fondo `backdrop` (ejemplos `samples/htaw/avisos`, `bandeja` y `fondo`); hechos menus contextuales (`htaw.menu.popup`, `app.Native.Menus`, sin permiso; menus emergentes en oscuro via uxtheme ordinales 135/136; DOC/MENUS.md; ejemplo `samples/htaw/menus`, probados a mano). Menu de ventana (`window.menu`, `htaw.window.menu`): render propio en `WinTitleBar` (no HMENU: no sigue el oscuro), fila fina bajo la barra en sin barra/`title`/`compact`, fila superior en `labels` (menus a la izquierda, icono y titulo centrados); boton «···» con los menus que no caben; desplegable con `TrackPopupMenu`; probado el uso basico (`samples/htaw/acerca`); probado en todos los modos de barra y con fondo. Teclado (Alt/F10, Alt+letra, flechas) por `AcceleratorKeyPressed`, mensajes de la ventana y reenvio desde la pagina (`glue.window.menuKey`/`menuState`) y cambio de menu con raton/flechas por gancho `WH_MSGFILTER` (`WinMenu.IBarHook`): implementado y probado a mano (Alt, F10, Alt+letra, flechas y raton). Linux: punto abierto en DOC/MENUS-LINUX.md. Resuelto: con fondo activo y barra
  propia el cristal deja libre la franja superior (`ApplyFrameMargins`) y DWM ya no duplica los botones de ventana. **1.2**: ventanas sin marco
  y transparentes (avatar/widget, permiso `overlay`, incompatibles con la barra de titulo propia; DOC/VENTANAS-SIN-MARCO.md) y mensajes propios en
  todos los SO (DOC/MENSAJES.md; hasta entonces, mensaje nativo). Wayland es una excepcion aceptada: `alwaysOnTop` y `setPosition` se ignoran
  alli (mejor esfuerzo, con `Capabilities`), el arrastre por clic si funciona.
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
