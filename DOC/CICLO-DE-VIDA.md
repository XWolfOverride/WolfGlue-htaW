# Ciclo de vida de la aplicación y ventana «Acerca de» (versión 1.1)

## Qué define una aplicación

En macOS, un menú (el de la aplicación: «Acerca de», Ajustes, Ocultar, Salir). En Windows y Linux, nada. WolfGlue toma de
**Delphi** el concepto de **ventana principal** como lo mínimo común: la primera ventana que se abre.

## Modos (`GlueAppOptions.QuitMode`, `lifecycle` en htaW)

| Modo | Valor | La app termina cuando… |
|---|---|---|
| **Ventana principal** (por defecto) | `GlueQuitMode.MainWindowClosed`, `"lifecycle": "main"` | se cierra la ventana principal; **el resto de ventanas se cierra con ella** (sin preguntar a sus confirmaciones de cierre) |
| Última ventana | `GlueQuitMode.LastWindowClosed`, `"lifecycle": "last"` | se cierra la última ventana |
| Explícito | `GlueQuitMode.Explicit` | se llama a `GlueApp.Quit` (solo WolfGlue) |

- Antes el modo por defecto era «última ventana»: una ventana olvidada mantenía viva la app. Ahora manda la principal
  (`GlueApp.MainWindow`, la primera que se muestra; se puede cambiar).
- Las ventanas modales se cierran siempre con su dueña. Con `tray.keepRunning`, cerrar la principal la oculta
  (ver [BANDEJA-Y-NOTIFICACIONES.md](BANDEJA-Y-NOTIFICACIONES.md)).

## Ventana «Acerca de»

- **Metadatos**: `GlueAppOptions.AppName`, `Version`, `Author` y `Icon`; en htaW, `name`, `version`, `author` e `icon` del
  manifiesto.
- **Marcar una ventana**: `GlueWindowOptions.IsAbout`. `GlueApp.ShowAboutBox()` trae al frente la que esté abierta, abre la
  página propia (`GlueAppOptions.AboutPage`; htaW: `about` en el manifiesto, texto o `{ page, width, height }`) o la
  **genérica**: icono (el de la app, o el de htaW si no tiene, `AboutIcon`), nombre, versión y autor si los hay y, abajo,
  pequeño y en gris, «Desarrollado con {PoweredBy}» (WolfGlue por defecto, htaW en htaW). La página propia **no** lleva ese pie.
  Ventana pequeña (420×320 por defecto), no redimensionable, dueña la principal.
- **Invocación**: `glue.aboutBox()` / `htaw.app.showAboutBox()`. Nada se abre solo en Windows y Linux (el «Acerca de»
  automático no tiene sentido allí).
- **macOS** (sin implementar): `ShowAboutBox` es lo que ejecuta la opción «Acerca de» del menú de la aplicación, que macOS
  espera en un sitio concreto (primer menú de la barra global). Ese menú lo compone el sistema/WolfGlue con «Acerca de»,
  Ajustes (si la app los declara), Ocultar y Salir; los menús declarados por la app van a continuación.

## Papel `about` (botón y opción de menú estándar)

Sin botones automáticos: abrir una barra propia solo por esto cambiaría la ventana (la barra del sistema pasaría a ser
nuestra) y un «?» junto a los botones de ventana se lee como «Ayuda», no como «Acerca de». En su lugar, un **papel**:

- Opción de menú de ventana: `{ "role": "about", "text": "&Acerca de…" }` (el `id` es `glue:about`).
- Botón de la barra propia: `{ "type": "button", "role": "about" }` (icono de ayuda, texto «Acerca de» e `id` por defecto; se
  pueden cambiar `icon`, `text`).
- Al elegirla se abre la ventana «Acerca de» **sin evento** para la página. En macOS, la opción con este papel se mueve al
  menú de la aplicación.
- Una barra propia **no lo lleva sola**: la app lo da de alta.
