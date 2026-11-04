# Integración de htaW con el sistema

## Línea de comandos

```
htaw <archivo.htaw> [args...]     abre la app; args llegan a htaw.app.args
htaw --dev <carpeta|archivo>      modo desarrollo (ver abajo)
htaw pack <carpeta> [-o salida]   crea un .htaw (zip) y valida el manifiesto
htaw --register / --unregister    asocia la extensión .htaw para el usuario actual
htaw --forget <archivo|id>        olvida la confianza concedida a una app
htaw --version / --help
```

Sin argumentos (doble clic sobre `htaw` directamente): una ventana sencilla con la lista de apps
abiertas recientemente y un botón para abrir un archivo.

Idioma de los textos de htaW: según el locale, forzable con `HTAW_LANG=es|en`.

## Modelo de procesos

- **Un proceso por app abierta.** Si una app falla, no arrastra a las demás.
- `singleInstance: true`: la segunda ejecución de la misma app (mismo `id`) envía sus argumentos a
  la primera por un canal local (*named pipe* en Windows, socket Unix en el directorio de runtime del
  usuario) y termina. La app recibe el evento `htaw.app.on("secondInstance", args => …)`.

## Asociación de archivos (`--register`)

Siempre por usuario, sin permisos de administrador.

| SO | Mecanismo |
|---|---|
| Windows | `HKCU\Software\Classes\.htaw` + ProgID `htaW.App` con `shell\open\command` = `"<ruta>\htaw.exe" "%1"` e icono; aviso al shell con `SHChangeNotify` |
| Linux | `~/.local/share/applications/htaw.desktop` (`MimeType=application/x-htaw`), `~/.local/share/mime/packages/htaw.xml`, `update-mime-database` y `update-desktop-database` si existen |
| macOS | lo hace el bundle `.app` (`Info.plist` con `CFBundleDocumentTypes` / `UTExportedTypeDeclarations`); `--register` solo indica que hay que mover `htaW.app` a `/Applications` o `~/Applications` y abrirlo una vez |

### macOS: bundle obligatorio
Un ejecutable suelto no recibe archivos del Finder. El `build.sh` en macOS monta `htaW.app`
(`Contents/MacOS/htaw`, `Contents/Info.plist`, `Contents/Resources/htaw.icns`). Los archivos abiertos
llegan por `GlueApp.FilesOpened` (ver PLATAFORMAS.md), no por `argv`.

## Modo desarrollo (`--dev`)

- Acepta una **carpeta sin empaquetar** (con `htaw.json`) o un HTML suelto.
- DevTools activos (F12) y menú contextual del motor.
- Recarga automática al guardar cualquier archivo de la carpeta.
- No pide confirmación de permisos, pero **sí aplica** los permisos declarados, para que el
  comportamiento sea el mismo que en producción.
- Errores de manifiesto en la consola y en un aviso no bloqueante.

## Empaquetado (`pack`)

- Valida `htaw.json` (campos, `entry` existente, icono existente).
- Crea el zip con rutas relativas y `/` como separador. Excluye `.git`, `node_modules` y `*.htaw`.
- La salida por defecto es `<carpeta>.htaw` junto a la carpeta.
