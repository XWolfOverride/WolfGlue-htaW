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

Pendiente de probar a mano: mover entre monitores con distinta escala, cambiar el tema con la app abierta,
enlace externo. Por decidir: desactivar las teclas de navegador (F5, Ctrl+P…) con `ICoreWebView2Settings3`;
mensajes con `TaskDialogIndirect` en vez de `MessageBoxW` (botones con texto propio; el modo oscuro tampoco
lo soporta); imagen en el portapapeles.

## Fase 2: htaW en Windows
1. ✅ Detección de formato, manifiesto, proveedores HTML suelto y zip.
2. ✅ API `htaw.*` sobre el bridge, con permisos.
3. ✅ Ventana de permisos, MOTW, `trust.json`, reglas de `shell.exec`.
4. ✅ `htaw.css` para Windows 11.
5. ⏳ Hechos `--register`, `--dev`, `pack`, `--forget`; falta la instancia única.
6. ✅ `build.ps1`, `run.cmd`, `publish.cmd` (`build.sh` llega con los backends de Linux y macOS).

Pendiente de la fase 2: instancia única (`singleInstance`) y evento `secondInstance`;
`htaw.window.onClosing`; ventana de apps recientes (en el panel); marco `none`. Hecho: panel de htaW sin argumentos,
`icon` en barra de título y de tareas, ejecutable portable (`bundle`) con icono, carpeta o `htaw.json` como modo desarrollo y recientes en el panel. Anotado: integración de `--dev` con los menús de cada sistema. Evaluado: icono de cada `.htaw` en el
explorador (descartado por ahora) y portables en Linux y macOS (HTAW-INTEGRACION.md). Sin probar a mano:
`--register` (cambia el registro del usuario), `fs.watch`, `spawn`, `http.fetch`.

## Fase 3: macOS
Backend Cocoa (runtime de ObjC, delegados en tiempo de ejecución), `FilesOpened`, bundle `.app`,
CSS de macOS, `build.sh` para macOS.

## Fase 4: Linux
Backend GTK3 + WebKitGTK 4.1, tema por portal, CSS de Linux, `.desktop`/MIME, `build.sh` para Linux.

## Fase 5: mejoras
- Generador de código del bridge (`[GlueService]`) y generación de `.d.ts`.
- Mica en Windows 11, ventanas sin marco con zonas de arrastre.
- Elementos propios en la barra de título (ver la sección siguiente).
- Menús nativos, bandeja del sistema, notificaciones.
- Evaluar GTK4 + WebKitGTK 6.0.

## Fase 6: otras plataformas (evaluado, sin implementar)
Ver HTAW-OTRAS-PLATAFORMAS.md. Orden propuesto: portables cruzados de escritorio (`bundle --target` y carpeta
`runtimes/`, junto con los backends de Linux y macOS); htaW de Android (solo ejecución) y generación de APK desde
cualquier sistema; iOS después de macOS, solo ejecución y con distribución de desarrollo.

## Barra de título con elementos propios (diseño, sin implementar)

Objetivo: que la app ponga botones y otros controles en la barra de título, integrados como en las
aplicaciones modernas del sistema, con **la misma definición en todos los sistemas**. Ni la app C# ni el
`.htaw` cambian su código según el SO (pueden consultar en qué sistema están, pero no les hace falta).

### Decisiones tomadas
- **La barra la dibuja WolfGlue, no la web.** En ese espacio solo van elementos propios de WolfGlue:
  botones (texto y/o icono), tiradores de arrastre, separadores y agrupadores. La app los declara y recibe sus
  eventos (clic, menú) por el bridge.
- **Estilo limitado y fácil de implementar en todos los SO:** colores (fondo, texto, hover, pulsado), bordes,
  grosores, radios, márgenes y separación. Un subconjunto de propiedades tipo CSS, no CSS completo.
- **Windows:** la ventana mantiene su marco y DWM sigue dibujando los botones de minimizar, maximizar y cerrar.
  El área de cliente se extiende a la barra de título (`WM_NCCALCSIZE`) y `WM_NCHITTEST` dice qué es zona de
  arrastre, qué es elemento propio y qué es botón del sistema (para conservar el menú de ajuste de Windows 11
  sobre maximizar). WolfGlue pinta los elementos propios en esa franja.
- **Linux:** imitar lo más posible el comportamiento de Windows, también dibujado por WolfGlue (no HTML). Con
  decoraciones del lado del cliente (GTK), la barra la compone WolfGlue con los botones de ventana en el orden
  y lado que indique el sistema (`gtk-decoration-layout`); el arrastre y el redimensionado se piden al gestor
  de ventanas (`gtk_window_begin_move_drag` / `begin_resize_drag`), que funciona en X11 y Wayland.
- **macOS:** contenido bajo una barra transparente, con los tres botones nativos en su sitio y los elementos
  propios a su derecha.
- Si un sistema no puede ofrecerlo, WolfGlue vuelve a la barra normal con los elementos en una franja propia
  bajo ella, sin que la app tenga que hacer nada.

### Por definir
- Declaración: en C# (`GlueWindowOptions`) y en el manifiesto de htaW (`window.titlebar`), con el mismo esquema
  JSON en ambos (lista de elementos con id, tipo, texto/icono, alineación, estilo).
- Cambios en caliente desde la web (`glue.window.titlebar.update(id, …)`): texto, habilitado, marcado.
- Formato de los iconos (SVG simple, PNG, o glifos de una fuente del sistema).
- Altura de la barra por SO y por DPI, y cómo se avisa a la página del espacio que queda (variables CSS).
- Idea abierta: reservar un rectángulo de la barra para contenido web. Pintar ahí un elemento oculto del HTML
  reenviando los eventos de ratón no es viable de forma directa; la alternativa realista sería un segundo
  webview pequeño en ese rectángulo (cuesta un proceso de renderizado más, ~25 MB).

## Decisiones pendientes
- API C# exacta de `GlueApp` / `GlueWindow` (se fija en la fase 1).
- Icono de app: decidido `.png` o `.ico` en el manifiesto (en macOS se convertirá a `.icns` con PNG dentro).
- Si htaW ofrece una API de base de datos local (por ejemplo SQLite) o se deja a IndexedDB.
- Valores exactos del CSS por sistema.
