# Fondo de ventana del sistema (`backdrop`) (versión 1.1)

> Estado: **implementado en Windows 11** (`GlueBackdrop.cs`, `WinWindow.UpdateBackdrop`; WolfGlue `GlueAppOptions.Backdrop`, `GlueWindowOptions.Backdrop`, `GlueWindow.SetBackdrop`, clase `glue-backdrop-*` y evento `glue.backdrop`; htaW `window.backdrop`, bloque de página, `open({ backdrop })`, `setBackdrop`, `theme.backdrop` y `htaw.css`). Windows 10 y sistemas sin soporte: sólido. macOS y Linux: sin implementar. El webview es transparente y el fondo propio se pinta negro (cristal para DWM) solo mientras el efecto está activo; la barra de título propia no se repinta (se acepta el corte). Requiere compilación 22621 o posterior.

Que una ventana use el material de fondo que el sistema ofrece para las aplicaciones modernas (en Windows 11, «Mica»
y «Acrylic»), con **un nombre común** que no dependa del sistema y que cada uno traduzca a lo suyo.

## Qué es cada cosa

| Efecto | Qué se ve detrás | Windows 11 | macOS | Linux |
|---|---|---|---|---|
| **Teñido** | No se ve lo de detrás: el fondo toma un tinte suave del fondo de escritorio y apenas cambia al mover la ventana. Barato (se calcula una vez). | Mica (`DWMSBT_MAINWINDOW`) | `NSVisualEffectView`, material `.windowBackground` o `.underWindowBackground`, `behindWindow` (el «tinte del fondo de escritorio» de las ventanas normales) | No hay equivalente estándar |
| **Difuminado** | Lo que hay detrás de la ventana, en vivo y muy desenfocado (como un cristal esmerilado). Más caro. | Acrylic (`DWMSBT_TRANSIENTWINDOW`) | Materiales `.hudWindow`, `.popover`, `.sidebar` con `behindWindow` | Solo KDE (protocolo de desenfoque de KWin); GNOME no lo ofrece |

- Windows 10 no tiene ninguno de los dos de forma documentada (el «acrílico» de Windows 10 usaba una API no pública que
  daba problemas al mover la ventana): fondo sólido.
- El sistema lo **desactiva solo** cuando el usuario quita los efectos de transparencia (Windows: Personalización >
  Colores > Efectos de transparencia; macOS: Accesibilidad > Reducir transparencia) y, en Windows, cuando la ventana
  está inactiva (Mica se vuelve sólido).

## Nombre y valores

Propiedad **`backdrop`** («fondo»), con tres valores semánticos, no de marca:

| Valor | Significado | Windows 11 | macOS | Linux |
|---|---|---|---|---|
| `"solid"` (por defecto) | Fondo normal de la ventana | color de fondo | opaco | opaco |
| `"tinted"` | Fondo **teñido** por el escritorio, para ventanas principales | Mica | `.windowBackground` | sólido |
| `"blurred"` | Lo de detrás **difuminado**, para ventanas auxiliares, paneles flotantes, utilidades | Acrylic | `.hudWindow` | sólido (KDE: desenfoque, si se implementa) |

Regla: si el sistema no puede, **la ventana queda sólida** y la app no tiene que hacer nada. Nunca se inventa el
efecto con trucos (capturas del escritorio, transparencias sin desenfoque), porque se ve mal y gasta.

## Dónde se declara

- WolfGlue: `GlueAppOptions.Backdrop` (todas las ventanas) y `GlueWindowOptions.Backdrop` (la que lo tenga gana).
- htaW: `window.backdrop` en el manifiesto (ventana principal) y en el bloque `htaw+json` de cada página (como la
  barra de título); `htaw.window.open(pagina, { backdrop })`.
- En caliente: `glue.window.setBackdrop(valor)` / `htaw.window.setBackdrop(valor)` (DWM y `NSVisualEffectView` lo
  permiten sin recrear la ventana).

## La página tiene que dejar ver el fondo

El material está **detrás del webview**: si la página pinta un fondo opaco, no se ve nada. Por eso:

- El webview se crea con fondo transparente (WebView2: `DefaultBackgroundColor` con alfa 0; WKWebView:
  `drawsBackground = NO`; WebKitGTK: `webkit_web_view_set_background_color` transparente) solo cuando el efecto
  está **activo**.
- WolfGlue avisa a la página: clase `glue-backdrop-tinted` / `glue-backdrop-blurred` en `<html>` y evento
  `glue.backdrop` (`{ requested, active }`) cuando cambia (el usuario quita la transparencia, la ventana pasa a
  inactiva en Windows, el sistema no lo soporta).
- htaW: `htaw.css` hace transparente `--htaw-bg` cuando el efecto está activo y usa superficies semitransparentes
  para tarjetas y paneles (`--htaw-surface` con alfa), como las apps del sistema. Con el efecto inactivo, todo vuelve a
  los colores sólidos de siempre, así que la app se ve bien en todos los casos sin escribir CSS propio.
- `htaw.theme` gana `backdrop` (el efecto activo ahora mismo), para quien quiera ajustar su CSS.

## Detalles por sistema

- **Windows 11**: `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE)` (22H2 o posterior; en 21H2,
  `DWMWA_MICA_EFFECT` no documentado: no se usa) + `DwmExtendFrameIntoClientArea` con márgenes −1 para que el material
  llegue al área de cliente.
- **Decisión: la barra de título propia no se repinta a mano** para dejar ver el material. Con barra del sistema, el
  material cubre la barra y el contenido sin corte (lo pinta DWM). Con la barra propia (WinTitleBar, pintada con GDI),
  la franja sigue con su color sólido y debajo empieza el material: **se acepta ese corte**. Se puede suavizar sin
  repintar eligiendo como color de la franja el más parecido al material (el fondo del tema), pero no se persigue más.
- macOS no tiene ese problema: la barra de herramientas nativa ya va sobre el material, sin corte.
- **macOS**: `NSVisualEffectView` como vista de contenido, con el WKWebView encima; `state = followsWindowActiveState`
  para imitar el comportamiento de Windows al desactivar la ventana.
- **Linux**: sólido. Si algún día se quiere el desenfoque de KDE, sería `org_kde_kwin_blur` (Wayland) o la propiedad
  `_KDE_NET_WM_BLUR_BEHIND_REGION` (X11), con ventana de visual RGBA.

## Pruebas previstas

Activar y desactivar «efectos de transparencia» con la app abierta; ventana activa e inactiva; tema claro y oscuro;
barra de título propia; ventana maximizada; mover entre monitores con distinto fondo de escritorio.

## Repintado bajo el webview (restos de la barra con el efecto activo) — resuelto, por si reaparece

**Síntoma**: solo con `tinted` o `blurred` (nunca en `solid`), tras pasar la ventana a otro monitor con distinto DPI o cambiar de
tipo de barra o de fondo, se queda un resto de la barra o del menú anterior en la zona bajo la barra, mientras el HTML se pinta bien
encima. Desaparece al encoger y ensanchar la ventana o al volver a sólido.

**Causa**: con el efecto activo el webview es transparente y compone (DirectComposition) sobre la superficie de la ventana, que sigue
siendo GDI. Esa zona la cubre el contenedor del webview, que tiene `WS_CLIPCHILDREN`, así que no puede pintar bajo las ventanas del
webview y lo que hubiera debajo nunca se invalidaba. En sólido el webview es opaco y lo tapa.

**Solución** (`WinWindow.RepaintUnderWebView`): solo cuando cambia la disposición (tamaño, DPI, barra, fondo) con el efecto activo se
quita `WS_CLIPCHILDREN` del contenedor, se fuerza el repintado de toda la zona con `RedrawWindow` (en negro, que para DWM es cristal)
y se repone. El resto del tiempo el contenedor conserva el recorte, así que el contenido de la página (animaciones, canvas) no
provoca repintados. Con el efecto desactivado solo se asegura que el recorte esté puesto.

**Si alguien lo reporta**: pedir el `wolfglue-debug.log` de una ejecución Debug (`run.cmd`; ver [COMPILACION-Y-PRUEBAS.md](COMPILACION-Y-PRUEBAS.md)) y
comprobar el ejemplo `samples/htaw/repintado`. Lo normal:

- Sin tocar la ventana, **ninguna** línea ni contador durante segundos (con animaciones CSS y canvas en marcha).
- Cada línea `EVENTO` va seguida de `contenedor-pinta=1` o `2`: es el repintado forzado; no debe haber `contenedor-pinta` sueltos.
- Los eventos salen en ráfagas al arrastrar o cambiar de monitor, y a veces dos casi seguidos (el cambio de DPI recoloca el webview
  explícitamente y otra vez por `WM_SIZE`); es inofensivo.
- Un `contenedor-pinta` constante sin interacción indicaría que algo invalida el contenedor en cada fotograma.

Comprobado a mano con dos monitores de distinto DPI y con la barra en todos los modos. Si vuelve a verse el resto, el siguiente paso
previsto es quitar también el recorte de la ventana principal.
