# Hoja de ruta

Orden previsto. Cada fase termina con algo que se puede ejecutar y probar.

## Versiones

### 1.0 (publicada)
WolfGlue y htaW en Windows (fases 1 y 2, salvo lo indicado en ellas).

### 1.1 (Windows; funcionalidad hecha y probada a mano; falta publicar el AOT y comprobarlo)
Hecho (sin publicar):
- ✅ Barra de título con elementos propios (BARRA-DE-TITULO.md).
- ✅ Enlaces externos solo por acción del usuario y permiso `links.auto`.
- ✅ Instancia única (`singleInstance`, evento `secondInstance`) y `htaw.window.onClosing`.
- ✅ Imágenes en el portapapeles; teclas de navegador desactivadas fuera del modo desarrollo.
- ✅ Generador del bridge `[GlueService]` y `.d.ts` (BRIDGE.md).

Por hacer en esta versión:
- ✅ **Bandeja del sistema** (BANDEJA-Y-NOTIFICACIONES.md): modelo común con menú obligatorio y opción `default`;
  `keepRunning` en htaW (cerrar oculta la ventana); permiso `tray`.
- ✅ **Notificaciones** (BANDEJA-Y-NOTIFICACIONES.md): título, texto, hasta 2 botones, sustituir por `id`; se retiran al
  cerrar la app; permiso `notifications`.
- ✅ **Apps recientes en el panel** (HTAW-INTEGRACION.md): las anota `htaw` en cada apertura (también con doble clic);
  revisión de todo lo que no debe hacer el modo portable (HTAW-INTEGRACION.md, «Modo portable»).
- ✅ **Fondo del sistema `backdrop`** (FONDO-DE-VENTANA.md): `solid`, `tinted` (Mica) y `blurred` (Acrylic). Sin repintar
  la barra de título propia: si se nota el corte entre la franja y el material, se acepta; si el sistema lo resuelve
  solo, mejor.
- ✅ **Ciclo de vida por ventana principal y ventana «Acerca de»** (CICLO-DE-VIDA.md): por defecto, cerrar la principal cierra la
  app y el resto (`lifecycle: "last"` para el modo antiguo); autor en los metadatos; `showAboutBox()`; ventana genérica con
  «Desarrollado con»; papel `about` en menús y barra. Sin implementar: el menú de aplicación de macOS.
- ✅ **Menús contextuales nativos** (MENUS.md): modelo común con submenús, `htaw.menu.popup`, tema oscuro en los menús
  emergentes de Windows. ✅ **Menú de ventana** (Archivo/Editar…, MENUS.md): render propio en la barra de título de Windows (sin barra, `title` y `compact`: fila fina bajo la barra; `labels`: fila superior con menús a la izquierda y título centrado; desbordamiento con «···»); probado a mano en todos los modos de barra y con fondo; teclas Alt/F10, Alt+letra, flechas y cambio de menú con el ratón implementados y probados a mano. **Punto abierto de Linux**: MENUS-LINUX.md (Pantheon
  rechaza la barra de menús, GNOME la desaconseja, KDE la acepta).

### 1.2 (siguiente)
- **Ventanas sin marco y transparentes al escritorio** (avatar, widgets), en WolfGlue y en htaW con permiso propio
  `overlay` (VENTANAS-SIN-MARCO.md; Wayland es una excepción aceptada). Incompatibles con la barra de título propia.
- **Mensajes propios en todos los sistemas** (MENSAJES.md): diálogo declarativo con botones de texto propio, casilla,
  campo de texto y modo oscuro. Hasta entonces se mantiene el comportamiento actual (mensaje nativo, en claro en
  Windows).

### Sin versión asignada
- Linux y macOS (fases 3 y 4); otras plataformas (fase 6).
- **Extensión nativa de htaW** (idea, sin decidir; solo se revisitaría si htaW tiene éxito y los usuarios lo piden: para más
  contacto con el sistema ya está WolfGlue). Dos vías evaluadas:
  - **Plugins**: librerías nativas (C, Rust o C# AOT) con una API C mínima y estable (`init`, `call(nombre, json)`,
    `free`), declaradas por nombre en el manifiesto y empaquetadas por sistema y arquitectura en el `.htaw` (se extraen a
    la carpeta de datos con su hash antes de cargarse). Abstraen el SO una vez para todas las apps.
  - **FFI desde JavaScript** (`dlopen` y llamadas con firmas): sin compilar nada, pero cada app asume la abstracción del SO
    y el riesgo de firmas mal escritas; con Native AOT solo admite un conjunto fijo de firmas o llevar libffi.
  - Ambas rompen el modelo de permisos finos: serían un permiso propio con aviso destacado, como `shell.exec "*"`, y
    confianza fijada al hash de la librería. Un fallo nativo cierra toda la app. Primero habría que diseñar el permiso y
    comprobar que dos Native AOT conviven en un proceso. Si se hace una, plugins antes que FFI. Alternativas ya
    disponibles: `shell.exec`/`spawn` con un proceso auxiliar (aislado) y WebAssembly en el webview (solo cálculo).
- **Base de datos local para apps htaW**: descartada por ahora. Basta IndexedDB (documentos con índices y consultas por
  rango, parecido a un MongoDB pequeño), `localStorage` y `htaw.settings`, aislados por app; SQLite (SQL, un fichero que
  el usuario ve y copia) solo se reevaluaría si una app real lo necesita. Ver el manual (`htaw.settings` y
  almacenamiento web).

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

Marco `none`: sin versión (VENTANAS-SIN-MARCO.md). Hecho: instancia
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
- Ventanas sin marco con zonas de arrastre y ventanas transparentes al escritorio (versión 1.2; VENTANAS-SIN-MARCO.md).
- ✅ Elementos propios en la barra de título (hecho en Windows; ver la sección siguiente).
- ✅ Bandeja, notificaciones y fondo del sistema en Windows (versión 1.1).
- Mensajes propios en todos los sistemas (MENSAJES.md). Versión 1.2.
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
- API C# exacta de `GlueApp` / `GlueWindow` (se fija en la fase 1).
- Icono de app: decidido `.png` o `.ico` en el manifiesto (en macOS se convertirá a `.icns` con PNG dentro).
- Base de datos local para apps htaW: decidido no ofrecerla; se usa IndexedDB (ver «Sin versión asignada»).
- Valores exactos del CSS por sistema.
