# Desarrollo y distribución

## Formas de una app

| Forma | Cuándo | Cómo se abre |
|---|---|---|
| **HTML suelto** (`app.htaw`) | Apps pequeñas de un solo archivo. Puede usar archivos de su misma carpeta (scripts, imágenes). | Doble clic, `htaw app.htaw` |
| **Carpeta** con `htaw.json` | Mientras desarrollas una app de varias páginas | `htaw carpeta` (siempre en modo desarrollo) |
| **Paquete** `.htaw` (zip) | Para distribuir la carpeta como un único archivo | Doble clic, `htaw app.htaw` |
| **Ejecutable portable** (`.exe`) | Para quien no tiene htaW: un único programa sin instalar nada | Doble clic |

## Modo desarrollo

Se abre con cualquiera de estas formas:

- `htaw --dev <carpeta|archivo>`;
- `htaw <carpeta>` o `htaw <carpeta>\htaw.json`: una carpeta siempre se abre en modo desarrollo;
- arrastrando la carpeta sobre `htaw.exe` o sobre un acceso directo suyo;
- desde el panel de htaW (*Modo desarrollo* → *Carpeta…* / *Archivo…*, o un elemento de *Recientes*).

En modo desarrollo:

- **Recarga automática**: al guardar cualquier archivo de la carpeta se recargan todas las ventanas (se agrupan los
  cambios de 300 ms, porque los editores guardan en varios pasos). Las carpetas `.git` se ignoran.
- **F12** abre DevTools (consola, inspector, red, depurador con puntos de ruptura) y el menú contextual del motor
  está activo.
- Los **avisos** del manifiesto salen en la consola con el prefijo `[htaW]`.
- **No se pregunta por los permisos, pero se aplican**: una llamada sin permiso falla igual que en la versión final.
- `htaw.app.dev` es `true`, por si quieres mostrar información de depuración.
- La ruta se guarda en *Recientes* del panel (las 10 últimas).

Ciclo típico: el panel abierto con la app en *Recientes*, la app abierta en modo desarrollo y el editor al lado;
cada vez que guardas, la ventana se recarga.

### Depurar

- `console.log` y los errores de JavaScript aparecen en DevTools.
- Las llamadas a la API que fallan rechazan su promesa: un `await` sin `try` muestra el error en la consola como
  «Uncaught (in promise)», con su `code` y su mensaje.
- Para probar con argumentos: `htaw --dev carpeta uno dos` → `htaw.app.args = ["uno", "dos"]`. En el panel, el campo
  *Argumentos* se usa también al abrir en modo desarrollo.
- Para probar la ventana de permisos tal como la verá el usuario, empaqueta la app y abre el `.htaw` marcado como
  descargado (por ejemplo, descargándolo de verdad).

### Autocompletado

Copia `htaw.d.ts` (en `src/htaW/Assets/`) a tu carpeta y añade al principio de tus `.js`:

```js
/// <reference path="htaw.d.ts" />
```

Los editores que entienden TypeScript (VS Code, por ejemplo) completarán `htaw.` con todos los miembros y sus tipos.
`htaw pack` lo empaqueta como cualquier archivo; si no quieres incluirlo, déjalo fuera de la carpeta de la app.

## Empaquetar: `.htaw`

```
htaw pack <carpeta> [-o salida.htaw]
```

- Valida la carpeta exactamente como al abrirla (manifiesto, `entry`, permisos, **icono**). Si algo falla, no crea
  nada y explica el motivo.
- Crea un zip con todos los archivos, excluyendo `.git`, `.vs`, `.vscode`, `.idea`, `node_modules` y otros `.htaw`.
- Salida por defecto: `<carpeta>.htaw` junto a la carpeta.
- También desde el panel: *Crear un .htaw* → *Empaquetar carpeta…*.

El `.htaw` se sirve directamente desde el zip, sin extraerlo. En un zip, `htaw.app.dir` es `null` y las rutas
relativas de `htaw.fs` se resuelven desde la carpeta del usuario.

## Ejecutable portable

```
htaw bundle <carpeta|archivo.htaw|archivo.html> [-o salida.exe] [--base htaw.exe]
```

Une htaW y la app en **un único programa** que se copia y se abre con doble clic, sin instalar nada:

- Si la app declara `icon`, el `.exe` lleva ese icono (lo verá el explorador de archivos).
- Todos los argumentos del `.exe` son para la app (`htaw.app.args`); la línea de comandos de htaW no existe en él.
- Una app de HTML suelto dentro de un portable sirve sus archivos relativos desde la carpeta del `.exe`; una carpeta o
  un zip lo llevan todo dentro.
- Los permisos funcionan igual: si el `.exe` se descarga de Internet, htaW pregunta antes de abrirlo (además del aviso
  propio de Windows para programas sin firmar).
- Los datos de la app (ajustes, almacenamiento web) se guardan en la carpeta de datos de htaW del usuario, por `id`.
- Hace falta el `htaw.exe` **publicado** (`publish.cmd` o el comando de compilación del README); con `--base` se
  puede indicar otro, por ejemplo uno de otra arquitectura.
- También desde el panel: *Crear un ejecutable portable* → *Desde carpeta…* / *Desde archivo…*.

El portable pesa lo mismo que htaW (unos 9 MB) más la app.

## Lista de comprobación antes de publicar

- `id` propio y estable (si cambia, la app pierde sus ajustes y sus datos, y los usuarios vuelven a ver la ventana de
  permisos).
- `version` actualizada.
- `permissions` mínimos: quita los que ya no uses.
- `icon` en `.ico` con varios tamaños.
- Probada en modo claro y oscuro (con `theme: "native"` sale sola, salvo colores propios).
- `htaw pack` (o `bundle`) sin errores.
