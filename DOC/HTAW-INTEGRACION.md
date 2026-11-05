# Integración de htaW con el sistema

## Línea de comandos

```
htaw <archivo.htaw> [args...]     abre la app; args llegan a htaw.app.args (también los que empiezan por "-")
htaw --dev <carpeta|archivo>      modo desarrollo (ver abajo)
htaw pack <carpeta> [-o salida]   crea un .htaw (zip) y valida el manifiesto
htaw --register / --unregister    asocia la extensión .htaw para el usuario actual
htaw --forget <archivo|id>        olvida la confianza concedida a una app
htaw --version / --help
```

- Sin argumentos (doble clic sobre `htaw.exe`): diálogo para elegir un `.htaw`; htaW se relanza con él.
  *(La ventana de apps recientes que se pensó en el diseño queda para más adelante.)*
- `htaw.exe` es una aplicación gráfica (`WinExe`): los comandos de consola (`--help`, `pack`…) escriben en la
  consola desde la que se lanzaron (`AttachConsole`) o en la salida redirigida si la hay.
- Códigos de salida: 0 bien; 1 error (formato, argumentos, E/S); 2 el usuario rechazó los permisos.
- Errores de formato al abrir una app: mensaje nativo con el motivo.
- Idioma: según el idioma del sistema, forzable con `HTAW_LANG=es|en` (y `WOLFGLUE_LANG` para los textos de WolfGlue).
- Datos de htaW en `%LOCALAPPDATA%\htaW` (`trust.json`, `apps/<id>/settings.json`, carpeta del webview);
  `HTAW_DATA` cambia la carpeta (útil en pruebas).

Implementación: `src/htaW/Program.cs` y `Cli.cs`.

## Modelo de procesos

- **Un proceso por app abierta.** Si una app falla, no arrastra a las demás.
- Todos comparten la carpeta de datos del webview; cada app tiene su propio origen
  (`https://<id>.htaw.local/`), así que su almacenamiento web está aislado.
- *(Pendiente)* `singleInstance: true`: la segunda ejecución pasaría sus argumentos a la primera por un canal
  local y terminaría. Hoy el campo se lee pero no tiene efecto.

## Asociación de archivos (`--register`)

Por usuario, sin permisos de administrador.

| SO | Mecanismo | Estado |
|---|---|---|
| Windows | `HKCU\Software\Classes\.htaw` → ProgID `htaW.App` con `shell\open\command` = `"<ruta>\htaw.exe" "%1" %*` e icono del ejecutable; aviso al shell con `SHChangeNotify` | hecho (`FileAssociation.cs`) |
| Linux | `~/.local/share/applications/htaw.desktop` + tipo MIME en `~/.local/share/mime/packages/htaw.xml` | pendiente |
| macOS | bundle `.app` con `CFBundleDocumentTypes`; los archivos llegan por el delegado de la app, no por `argv` | pendiente |

`--unregister` solo borra `.htaw` si sigue apuntando a htaW. Conviene registrar el ejecutable ya copiado en su
sitio definitivo (la ruta queda en el registro).

## Modo desarrollo (`--dev`)

- Acepta una **carpeta sin empaquetar** (con `htaw.json`) o un HTML suelto.
- DevTools (F12) y menú contextual del motor activos.
- Recarga todas las ventanas al guardar cualquier archivo de la carpeta (agrupando ráfagas de 300 ms).
- No pide confirmación de permisos, pero **sí los aplica**, igual que en producción.
- Los avisos del manifiesto salen en la consola de DevTools.

## Empaquetado (`pack`)

- Valida la carpeta exactamente como al abrirla (`htaw.json`, `entry` existente, permisos).
- Crea el zip con rutas relativas y `/`, en orden estable. Excluye `.git`, `.vs`, `.vscode`, `.idea`,
  `node_modules` y otros `.htaw`.
- Salida por defecto: `<carpeta>.htaw` junto a la carpeta. Se escribe en un temporal y se renombra.

Implementación: `src/htaW/Packer.cs`.
