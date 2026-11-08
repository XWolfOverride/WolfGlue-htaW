# Barra de título con elementos propios

Referencia para desarrolladores y agentes de IA. La guía para quien hace apps htaW está en el manual
([manual/10-barra-de-titulo.md](manual/10-barra-de-titulo.md)).

## Principio: el sistema más restrictivo manda

La misma definición sirve en todos los sistemas, sin código por SO en la app. Para que eso sea posible sin forzar
ningún sistema, el modelo se limita a lo que la barra de herramientas **nativa de macOS** (`NSToolbar` en una ventana
con barra unificada) ofrece tal cual. Si algo exige saltarse el comportamiento de algún sistema, no se ofrece en
ninguno. Consecuencias:

- **Sin estilos propios** (colores, bordes, radios, márgenes): la barra tiene el aspecto del sistema.
- Iconos de una lista común con nombre, PNG/ICO monocromos teñidos, o una fuente de iconos propia.
- Descartado: el icono grande de app ocupando varias filas (estilo «orbe»), separadores, alto libre, contenido HTML
  en la barra.

## Modelo (`src/WolfGlue/GlueTitleBar.cs`)

`GlueTitleBar { Mode, Title, IconFont, Items }` con `Parse(json)` (generador de JSON, AOT) y `Validate()`, que lanza
`FormatException` con un mensaje en español que dice qué campo falla.

| Modo | Windows | macOS (previsto) | Linux (previsto) |
|---|---|---|---|
| `title` (≤ 3 botones) | alto 32, botones de 40 a toda altura junto a los de la ventana | barra de título normal + accesorio de barra de título | `GtkHeaderBar` con botones al final |
| `compact` | alto 40, botones 32×32, texto como ayuda | `NSToolbar` `iconOnly` | `GtkHeaderBar` |
| `labels` | alto 56, icono 20 con texto debajo; botones de ventana de 32 arriba | `NSToolbar` `iconAndLabel` | `GtkHeaderBar` con etiquetas |

Elementos: `button` (icono y texto obligatorios; `toggle`/`checked`, `menu`, `split`), `group` (solo botones, sin
anidar; `text` = etiqueta del grupo), `panel` (`text`, `subtitle`, `icon`, `menu`, ancho 120–480), `search` (id
obligatorio, siempre al final, como mucho uno), `image` (PNG/ICO), `space` (flexible). Como mucho un elemento
`center`; los anteriores van a la izquierda y los posteriores a la derecha.

Iconos (`GlueTitleBarIcons.cs`): tabla nombre → (código Segoe Fluent/MDL2, SF Symbol, icono simbólico freedesktop).
Los nombres no cambian nunca de significado; la lista solo crece. Rutas de imagen relativas (sin `..` ni `:`) o
`data:`. `font:XXXX` exige `iconFont` (TTF/OTF; el nombre de familia lo lee `GlueFontInfo` de la tabla `name`,
registro 1, el que usa GDI).

## Dónde se declara y cómo llega a cada ventana

- `GlueWindowOptions.TitleBar` (C#) o, en htaW, `window.titlebar` del manifiesto para la ventana principal.
- `GlueWindowOptions.TitleBarFromPage` (`true`) + `GlueAppOptions.PageTitleBar` (`Func<string, GlueTitleBar?>`):
  si la ventana no trae barra, `GlueWindow.Show` pide la página inicial al contenido propio y le pasa el HTML. htaW lo
  implementa leyendo solo `window.titlebar` del bloque `application/htaw+json` (`HtawHost.PageTitleBar`). Si el
  contenido no responde al momento (carpeta, disco), la barra se aplica cuando llega la página (la ventana aún no se
  ve: aparece al cargar o a los 500 ms).
- `glue.window.open(url, { titlebar })`: objeto = esa barra; `false` = ninguna (no se mira la página).
- En caliente: `glue.window.titlebar.set(barra | null)`, `update(id, cambios)` (se aplica sobre una copia y se valida;
  si falla, no cambia nada), `focus(id)`.
- Eventos: `glue.titlebar` → `{ type: click | input | submit, id, button, value }`; en C#, `GlueWindow.TitleBarEvent`.
  `glue.window.titlebar.on(tipo, fn)` filtra por tipo. Los archivos (imágenes, fuente) se piden con
  `IWindowHost.LoadTitleBarFileAsync` (contenido propio o `data:`).

## Implementación en Windows (`Platform/Windows/WinTitleBar.cs`)

- La ventana conserva su marco (sombra, bordes, esquinas redondeadas, ajuste de Windows 11). `WM_NCCALCSIZE` quita
  solo la franja superior del área no cliente; maximizada se suma `SM_CYFRAME + SM_CXPADDEDBORDER` para que no se
  salga de la pantalla. El webview se coloca bajo la franja.
- `WM_NCHITTEST`: primero `DefWindowProc` (bordes); en la franja, borde superior para redimensionar, `HTMAXBUTTON` en
  el botón de maximizar (menú de distribuciones de Windows 11; el clic lo gestiona la barra), `HTCLIENT` en los
  elementos y `HTCAPTION` en el resto (arrastrar, doble clic, menú del sistema).
- Pintado GDI con doble búfer; rectángulos redondeados y escalado/teñido de imágenes con GDI+ (`Gdip` en
  `Win32TitleBar.cs`: carga desde memoria, `PARGB`, recoloreado con el alfa como silueta). Iconos con nombre en
  Segoe Fluent Icons (Windows 11) o Segoe MDL2 Assets (Windows 10). Fuente propia con `AddFontMemResourceEx` (solo
  para el proceso).
- Botones de ventana propios (glifos E921/E922/E923/E8BB; cerrar en rojo #C42B1C al pasar). Colores derivados del
  fondo de la ventana y del tema; ventana inactiva atenuada.
- Menús con `TrackPopupMenu` (`TPM_RETURNCMD`); ayudas con `tooltips_class32`.
- Búsqueda: control `EDIT` hijo (cursor, selección, portapapeles, IME y accesibilidad del sistema), texto de ayuda con
  `EM_SETCUEBANNER` (comctl v6 por el manifiesto), colores con `WM_CTLCOLOREDIT`, Intro/Escape con
  `SetWindowSubclass`, `EN_CHANGE` agrupado con un temporizador de 250 ms.
- Colocación: título a la izquierda si cabe; en `compact`/`labels` los elementos tras el título, búsqueda al final y
  espacios repartiendo el sobrante; con un elemento centrado, centrado en la ventana (un panel centrado se encoge hasta
  120). Lo que no cabe no se muestra.
- **Icono de la ventana**: la zona del icono devuelve `HTSYSMENU`, así que Windows hace lo de siempre: clic = menú del sistema, doble
  clic = cerrar la ventana.

## macOS y Linux (previsto)

- macOS: `NSToolbar` con `NSToolbarItem` (botón), `NSToolbarItemGroup` (grupo, con `label`), `NSMenuToolbarItem`
  (menú y partido), `NSSearchToolbarItem` (búsqueda), un `NSToolbarItem` con vista propia para el panel y la imagen,
  `flexibleSpace` y `centeredItemIdentifiers`. Iconos con SF Symbols; PNG como `template image`.
- Linux: `GtkHeaderBar` con `GtkButton`, `GtkMenuButton`, `GtkSearchEntry`, cajas enlazadas para los grupos y
  `set_custom_title` para el elemento centrado; botones de ventana según `gtk-decoration-layout`.

## Pruebas

`tests/WolfGlue.Tests/TitleBarTests.cs` (validación, iconos, `GlueFontInfo`) y
`tests/htaW.Tests/TitleBarManifestTests.cs` (manifiesto y bloque de página). A mano en Windows 11 con
`samples/htaw/barra`: los tres modos, clic, conmutador, menú partido, búsqueda (input, submit, Escape,
`focus`), imagen a color e icono PNG teñido, barra de la página, de `window.open`, `false` y cambio en caliente
(poner y quitar).
