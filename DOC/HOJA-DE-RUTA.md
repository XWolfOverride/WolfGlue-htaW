# Hoja de ruta

Orden previsto. Cada fase termina con algo que se puede ejecutar y probar.

## Fase 1: WolfGlue en Windows
1. ✅ Estructura del repo, solución, proyectos y pruebas; `WolfGlue.targets`.
2. ✅ `GlueApp` + `GlueWindow` con `user32` (bucle, `Invoke`, DPI PerMonitorV2, cierre).
3. ✅ WebView2 por COM generado y loader estático; un solo entorno compartido.
4. ✅ Host propio con recursos incrustados; shim JS; bridge JSON-RPC y eventos.
5. ✅ Varias ventanas, `window.open`, ventanas con dueño y modales.
6. ✅ Servicios nativos (`app.Native` y `glue.*`): diálogos de abrir/guardar/carpeta, mensajes, portapapeles
   (texto), pantallas, tema con evento de cambio, shell (abrir URL, abrir archivo, mostrar en el explorador).
7. ✅ Ejemplo C# en `samples/Hola`.

Pendiente de probar a mano: mover entre monitores con distinta escala, cambiar el tema con la app abierta.
Hecho: teclas de navegador (F5, Ctrl+P…) desactivadas con `ICoreWebView2Settings3` salvo
`GlueAppOptions.BrowserKeys` (htaW: solo en modo desarrollo); imagen en el portapapeles (PNG). Probado y descartado por
ahora: `TaskDialogIndirect` (sale en claro igual que `MessageBoxW` en Windows 11 22H2; ver «Decisiones pendientes»).

## Fase 2: htaW en Windows
1. ✅ Detección de formato, manifiesto, proveedores HTML suelto y zip.
2. ✅ API `htaw.*` sobre el bridge, con permisos.
3. ✅ Ventana de permisos, MOTW, `trust.json`, reglas de `shell.exec`.
4. ✅ `htaw.css` para Windows 11.
5. ⏳ Hechos `--register`, `--dev`, `pack`, `--forget`; falta la instancia única.
6. ✅ `build.ps1`, `run.cmd`, `publish.cmd` (`build.sh` llega con los backends de Linux y macOS).

Pendiente de la fase 2: ventana de apps recientes (en el panel). Descartado por ahora: marco `none`. Hecho: instancia
única (`singleInstance`) con el evento `secondInstance` y `htaw.window.onClosing`. Hecho: panel de htaW sin argumentos,
`icon` en barra de título y de tareas, ejecutable portable (`bundle`) con icono, carpeta o `htaw.json` como modo desarrollo y recientes en el panel. Anotado: integración de `--dev` con los menús de cada sistema. Evaluado: icono de cada `.htaw` en el
explorador (descartado por ahora) y portables en Linux y macOS (HTAW-INTEGRACION.md). Probados a mano: `--register`,
DPI entre monitores y modo desarrollo con recientes. Sin probar a mano: `fs.watch`, `spawn`, `http.fetch`.

## Fase 3: macOS
Backend Cocoa (runtime de ObjC, delegados en tiempo de ejecución), `FilesOpened`, bundle `.app`,
CSS de macOS, `build.sh` para macOS.

## Fase 4: Linux
Backend GTK3 + WebKitGTK 4.1, tema por portal, CSS de Linux, `.desktop`/MIME, `build.sh` para Linux.

## Fase 5: mejoras
- ✅ Generador de código del bridge (`[GlueService]`) y generación de `.d.ts` (BRIDGE.md).
- Mica en Windows 11, ventanas sin marco con zonas de arrastre.
- ✅ Elementos propios en la barra de título (hecho en Windows; ver la sección siguiente).
- Menús nativos, bandeja del sistema, notificaciones.
- Evaluar GTK4 + WebKitGTK 6.0.

## Fase 6: otras plataformas (evaluado, sin implementar)
Ver HTAW-OTRAS-PLATAFORMAS.md. Orden propuesto: portables cruzados de escritorio (`bundle --target` y carpeta
`runtimes/`, junto con los backends de Linux y macOS); htaW de Android (solo ejecución) y generación de APK desde
cualquier sistema; iOS después de macOS, solo ejecución y con distribución de desarrollo.

## Barra de título con elementos propios

✅ Hecha en Windows (botones, conmutadores, menús, botones partidos, grupos, panel, búsqueda nativa, imagen, espacio
flexible y un elemento centrado; tres modos: `title`, `compact` y `labels`). Declarada por ventana: en la página
(bloque `htaw+json`), en `window.open` o en caliente. Modelo limitado a lo que la barra de herramientas nativa de
macOS permite, sin estilos propios. Diseño, implementación y lo previsto en macOS y Linux:
[BARRA-DE-TITULO.md](BARRA-DE-TITULO.md). Descartado: alto libre, estilos, separadores, icono grande tipo «orbe» y
contenido HTML en la barra.

## Decisiones pendientes
- Mensajes en modo oscuro en Windows: ni `MessageBoxW` ni `TaskDialogIndirect` siguen el tema. Alternativa: diálogo
  propio (ventana de WolfGlue con su webview y el CSS nativo), como hacen macOS (`NSAlert`) y GTK de serie.
- API C# exacta de `GlueApp` / `GlueWindow` (se fija en la fase 1).
- Icono de app: decidido `.png` o `.ico` en el manifiesto (en macOS se convertirá a `.icns` con PNG dentro).
- Si htaW ofrece una API de base de datos local (por ejemplo SQLite) o se deja a IndexedDB.
- Valores exactos del CSS por sistema.
