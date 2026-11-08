# Menú de ventana en Linux — punto abierto

Estudio previo, **sin decisión tomada y sin probar en máquinas reales**. Lo que se afirma sobre cada escritorio sale del
conocimiento general de GTK, GNOME, KDE y elementary OS y hay que **verificarlo en cada uno** antes de implementar; los puntos
que más dudas dan van marcados «a verificar». El resto del diseño del menú de ventana está en [MENUS.md](MENUS.md).

## El problema

macOS **obliga** a un menú (la barra global es parte del sistema). Pantheon (elementary OS) lo **rechaza por completo**: sus
guías de interfaz piden que las aplicaciones no tengan barra de menús. GNOME no tiene menú global desde hace años y sus guías
también la desaconsejan. KDE, Xfce, MATE y Cinnamon la aceptan como cosa normal. Queremos **la misma capacidad en todos los
sistemas** (menús con submenús, atajos mostrados, marcas, estados) y que cada escritorio la organice como mejor le encaje; en
Linux eso significa que **no hay una colocación válida para todos**.

## Qué hay en cada escritorio

| Escritorio | Barra de menús dentro de la ventana | Menú global | Convención propia |
|---|---|---|---|
| KDE Plasma | Aceptada (las apps de KDE la usan, se puede ocultar con Ctrl+M) | Sí, con el applet «menú global» (protocolo `com.canonical.AppMenu.Registrar`, dbusmenu); las apps GTK solo lo exportan con un módulo GTK de terceros | Barra de menús o botón de hamburguesa |
| GNOME | Desaconsejada; GTK la muestra dentro de la ventana si la aplicación la define | No (el «menú de aplicación» de la barra superior se retiró) | Cabecera con botón de menú principal («hamburguesa») que abre un popover |
| Pantheon (elementary) | A verificar: la Shell de GTK no la dibuja y la guía la rechaza | No | Cabecera con botones y, en sus propias apps, un botón de menú de aplicación; a verificar si es parte de la guía o una costumbre extendida |
| Xfce, MATE, Cinnamon | Aceptada | No (MATE y Cinnamon: no por defecto) | Barra de menús clásica |

Notas sobre Pantheon (a verificar):

- La hipótesis de trabajo es que la **hamburguesa de elementary es una convención de la comunidad y de sus propias apps**,
  no una definición del lenguaje de interfaz tan estricta como «sin menús». Conviene leer la guía actual (HIG) antes de
  apoyarse en ella.
- `GTK_CSD=0` fuerza decoración del lado del servidor en GTK3 para las ventanas que **no** fijan su propia barra de
  título. Se barajó para evitar que la Shell «se coma» el menú; **a verificar**: si Pantheon no lo dibuja por ser GTK con
  cabecera propia o por el gestor de ventanas, y si con `GTK_CSD=0` la barra del `GtkApplicationWindow` aparece.

## Opciones

| | Descripción | A favor | En contra |
|---|---|---|---|
| **A. Render propio, siempre** (como en Windows) | La misma franja de menús dibujada por WolfGlue dentro de la ventana, en todos los escritorios | Una sola implementación; se ve en cualquier escritorio, también Pantheon y GNOME; nada que detectar | No es nativo ni sigue el tema de GTK sin esfuerzo (colores, fuente, foco); choca con las guías de GNOME y Pantheon; accesibilidad (lectores de pantalla) a mano |
| **B. Nativo por escritorio** | `GtkMenuBar` donde se acepta (KDE, Xfce, MATE, Cinnamon) y hamburguesa en la cabecera (`GtkMenuButton` + `GtkPopoverMenu`) en GNOME y Pantheon | Respeta a cada escritorio; accesibilidad y tema gratis | Dos disposiciones y detección del escritorio (`XDG_CURRENT_DESKTOP`); lo que se pruebe en uno no vale en otro |
| **C. Solo hamburguesa en Linux** | El mismo `GMenuModel` siempre en un botón de menú de la cabecera | Una disposición; coincide con GNOME y la costumbre de Pantheon | En KDE y Xfce parece fuera de sitio; los submenús largos en un popover son incómodos |
| **D. Menú global donde exista** | Exportar el menú por dbusmenu para KDE (y Unity/Budgie) además de B o C | Integración real en KDE con menú global | Dependencia de un protocolo de terceros; hay que mantener otro camino |

**Orientación inicial (superada por la decisión provisional de abajo)**: B es lo más respetuoso y A lo más barato. Una ruta intermedia sensata: empezar por C (una sola
disposición, válida donde menos molesta), añadir la barra clásica en los escritorios que la aceptan solo si pruebas reales
muestran que la hamburguesa estorba, y dejar D para más adelante. A cobra sentido si la coherencia con el render propio de
Windows pesa más que lo nativo.

## Decisión provisional

**Hamburguesa en todos los casos** (opción C: un `GtkMenuButton` con un popover en la cabecera, construido desde el mismo
`GMenuModel`). **Excepción valorada: Xfce**, con la barra clásica (`GtkMenuBar`), **solo si la implementación dual tiene
poco coste**. Estimación (sin medir): bajo, porque las dos salen del mismo modelo y solo cambia el widget que se crea
(`gtk_menu_bar_new_from_model` frente a `gtk_menu_button_set_menu_model` / `gtk_popover_new_from_model`), elegido por
`XDG_CURRENT_DESKTOP` (que puede ser una lista: se busca `XFCE` dentro). El coste real está en probar las dos disposiciones
y en la barra propia de Linux (cabecera frente a fila). Si al probar en Xfce la hamburguesa no molesta, se descarta la
excepción y queda una sola.

## Lo que hay que averiguar (máquinas reales)

1. **Pantheon**: ¿se ve un `GtkMenuBar` en una ventana normal? ¿Y en una con cabecera propia? ¿Cambia algo con `GTK_CSD=0`?
   ¿Qué dicen la guía actual y las apps propias (Archivos, Código) sobre el botón de menú?
2. **GNOME**: popover de la hamburguesa con submenús de dos y tres niveles, con atajos mostrados y marcas; atajos de
   teclado con las teclas de acceso (`_` en GTK, no `&`).
3. **KDE**: barra de menús bajo el título frente a menú global; qué pasa con GTK3 + WebKitGTK en Wayland.
4. **Detección**: `XDG_CURRENT_DESKTOP` (puede ser una lista: `ubuntu:GNOME`), `GTK_CSD` y `DESKTOP_SESSION`; ¿se puede
   decidir sin depender de ellos, preguntando al portal o a `gtk-decoration-layout`?
5. **Teclas de acceso**: el modelo común usa `&` (Windows); en GTK es `_`. La traducción es mecánica, pero hay que decidir
   si `&&` y `__` se corresponden.
6. **Interacción con la barra propia**: en Linux la barra propia sería un `GtkHeaderBar`: ¿el menú va dentro de la cabecera
   (como botón) o en una fila aparte?

## Mientras tanto

El modelo (lista de menús con opciones, `id`, `text`, `shortcut`, `checked`, `enabled`, `items`) es el mismo para todos los
sistemas y se traduce a `GMenuModel` casi uno a uno (`g_menu_append_item`, secciones por separador, submenús, acciones con
estado para las marcas). **La colocación no forma parte del modelo**: una app declara el menú una vez y no sabe dónde se
muestra, como ya ocurre en macOS.
