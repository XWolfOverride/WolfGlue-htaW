# Uso de htaW

## Qué es una aplicación `.htaw`

Un archivo `.htaw` es una aplicación de escritorio hecha con tecnologías web. Puede ser:

- un **único archivo HTML** con extensión `.htaw`, o
- un **paquete** (un zip con extensión `.htaw`) con varias páginas, imágenes y scripts.

Para ti como usuario no hay diferencia: se abre igual. htaW pone la ventana, el aspecto del sistema (tema claro u
oscuro, color de acento) y el acceso controlado a tu equipo.

También hay aplicaciones **portables**: un único `.exe` que ya lleva htaW dentro. Se usan como cualquier programa:
se copian y se abren con doble clic, sin instalar nada.

## Abrir una aplicación

- **Doble clic** sobre el `.htaw`, si la extensión está asociada a htaW (ver «Asociar la extensión» más abajo).
- **Arrastrar** el archivo sobre `htaw.exe` o sobre un acceso directo suyo.
- Desde el **panel de htaW** (abrir `htaw.exe` sin nada): botón *Abrir .htaw…*.
- Desde una consola: `htaw ruta\a\la\app.htaw`.

Cada aplicación se abre en su propio proceso: si una falla, las demás siguen funcionando. En la barra de tareas cada
una tiene su propio botón con su icono.

## La ventana de permisos

Las aplicaciones solo pueden hacer lo que declaran (leer archivos, ejecutar programas, conectarse a Internet…). Si
abres una aplicación **descargada de Internet**, o una versión nueva de una que ya conocías **pide más permisos**,
htaW te muestra antes una ventana con:

- el nombre, el identificador, la versión y la ruta de la aplicación;
- de dónde viene (descargada de Internet o local);
- la lista de lo que pide, en lenguaje claro.

El botón por defecto es **Cancelar**: lee la lista antes de aceptar. Si la aplicación puede ejecutar cualquier
programa, verás un aviso destacado: eso equivale a darle tus mismos permisos.

Cuando aceptas, htaW lo recuerda y no vuelve a preguntar mientras el archivo no cambie. Las aplicaciones locales (que
no vienen de Internet) se abren sin preguntar, pero **siguen limitadas** a lo que declaran.

## El panel de htaW

Abriendo `htaw.exe` sin ningún archivo aparece el panel, con estas secciones:

| Sección | Para qué sirve |
|---|---|
| Abrir una aplicación | Elegir un `.htaw` y abrirlo. Opcionalmente, con argumentos para la app. El panel se cierra al abrirla. |
| Modo desarrollo | Abrir una carpeta o un archivo en modo desarrollo, y la lista de **recientes** (Abrir / Quitar). Ver [Desarrollo](08-desarrollo-y-distribucion.md). |
| Crear un .htaw | Empaquetar una carpeta de desarrollo en un único `.htaw`. |
| Crear un ejecutable portable | Convertir una app (carpeta, `.htaw` o HTML) en un `.exe` que lleva htaW dentro. |
| Extensión .htaw | Ver si `.htaw` está asociada a este htaW y asociarla o quitar la asociación. |
| Permisos recordados | Ver a qué aplicaciones diste permiso y olvidarlas (se volverá a preguntar). |
| Acerca de | Versión, ruta del programa, carpeta de datos y ayuda de la línea de comandos. |

## Asociar la extensión `.htaw`

Para abrir los `.htaw` con doble clic, en el panel pulsa **Asociar** (o ejecuta `htaw --register`). Es solo para tu
usuario y no necesita permisos de administrador. Conviene hacerlo con `htaw.exe` ya en su sitio definitivo, porque la
ruta queda guardada. **Quitar asociación** (o `htaw --unregister`) lo deshace.

## Tus datos

htaW guarda en `%LOCALAPPDATA%\htaW` (Windows):

- `trust.json`: las aplicaciones a las que diste permiso;
- `apps\<id>\settings.json`: los ajustes de cada aplicación;
- `recent-dev.json`: las carpetas de desarrollo recientes;
- `WebView2\`: los datos del navegador interno (almacenamiento web de cada app, caché).

Para olvidar los permisos de una app: panel → *Permisos recordados* → *Olvidar*, o `htaw --forget <archivo|id>`.

## Línea de comandos

```
htaw <archivo.htaw> [args...]      abre la aplicación (lo que va detrás es para la app)
htaw <carpeta|htaw.json>           abre una carpeta en modo desarrollo
htaw --dev <carpeta|archivo>       modo desarrollo
htaw pack <carpeta> [-o salida]    crea un .htaw (zip) y valida el manifiesto
htaw bundle <carpeta|archivo> [-o salida] [--base htaw.exe]
                                   crea un ejecutable portable con la app dentro
htaw --register | --unregister     asocia (o no) la extensión .htaw
htaw --forget <archivo|id>         olvida los permisos concedidos
htaw --version | --help
htaw                               abre el panel
```

Códigos de salida: `0` bien, `1` error, `2` el usuario rechazó los permisos.

El idioma de htaW sigue al del sistema (español o inglés). Se puede forzar con la variable de entorno
`HTAW_LANG=es` o `HTAW_LANG=en`.
