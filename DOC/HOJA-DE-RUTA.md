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
1. Detección de formato, manifiesto, proveedores HTML suelto y zip.
2. API `htaw.*` sobre el bridge, con permisos.
3. Ventana de permisos, MOTW, `trust.json`, reglas de `shell.exec`.
4. `htaw.css` para Windows 11.
5. `--register`, `--dev`, `pack`, instancia única.
6. `build.ps1`, `run.cmd`.

## Fase 3: macOS
Backend Cocoa (runtime de ObjC, delegados en tiempo de ejecución), `FilesOpened`, bundle `.app`,
CSS de macOS, `build.sh` para macOS.

## Fase 4: Linux
Backend GTK3 + WebKitGTK 4.1, tema por portal, CSS de Linux, `.desktop`/MIME, `build.sh` para Linux.

## Fase 5: mejoras
- Generador de código del bridge (`[GlueService]`) y generación de `.d.ts`.
- Mica en Windows 11, ventanas sin marco con zonas de arrastre.
- Menús nativos, bandeja del sistema, notificaciones.
- Evaluar GTK4 + WebKitGTK 6.0.

## Decisiones pendientes
- API C# exacta de `GlueApp` / `GlueWindow` (se fija en la fase 1).
- Formato del icono de app (`.png` único convertido en cada SO o un icono por SO).
- Si htaW ofrece una API de base de datos local (por ejemplo SQLite) o se deja a IndexedDB.
- Valores exactos del CSS por sistema.
