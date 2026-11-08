# Mensajes y diálogos simples — evaluación y diseño, sin implementar

## El problema

`app.Native.Dialogs.MessageAsync` (y `htaw.dialog.message`) usa el mensaje nativo de cada sistema. En macOS (`NSAlert`)
y en Linux (`GtkMessageDialog`) sigue el tema claro/oscuro; en Windows **ni `MessageBoxW` ni `TaskDialogIndirect` lo
siguen** (probado en Windows 11 22H2: los dos salen en claro). Además, los mensajes nativos limitan lo que se puede
pedir: textos de botón fijos (Aceptar/Cancelar/Sí/No) en `MessageBoxW`, sin casilla, sin campo de texto.

## Opciones

| | A. Nativo en todos (como hoy) | B. Web solo en Windows | C. Diálogo propio en todos |
|---|---|---|---|
| Modo oscuro en Windows | No | Sí | Sí |
| Mismo resultado en todos los SO | Sí, pero pobre | **No**: dos implementaciones con distintas capacidades y fallos | Sí |
| Funciones extra (botones con texto propio, casilla «no volver a preguntar», campo de texto, detalle desplegable) | Limitadas a lo que da el más pobre | Solo si también se hacen nativas en macOS y Linux | Sí, iguales en todos |
| Aspecto | Exactamente el del sistema | Mezcla | Imitación cuidada del sistema (htaw.css por SO) |
| Coste de abrir | Inmediato | ~150–300 ms en Windows (un webview nuevo) | ~150–300 ms (mitigable) |
| Si el webview no funciona (falta el runtime de WebView2) | Funciona | Hay que mantener el nativo de reserva | Hay que mantener el nativo de reserva |

**B se descarta**: tendríamos dos diálogos distintos que mantener y probar, y cualquier función nueva habría que hacerla
dos veces o dejarla solo en Windows, que es justo lo que queremos evitar.

## Recomendación: C, con un modelo declarativo

Un diálogo propio de WolfGlue en **todos** los sistemas, pintado con su webview y el CSS nativo de cada SO, pero **no
HTML libre** (para eso ya están las ventanas modales con `htaw.window.open(..., { modal: true })`): una definición
cerrada que WolfGlue presenta siguiendo las convenciones de cada sistema. Así sigue pareciendo «un mensaje del
sistema», funciona igual en todos y admite propiedades propias.

```js
const r = await htaw.dialog.message({
  title: "Guardar cambios",
  text: "¿Guardar los cambios en «informe.md»?",          // instrucción principal
  detail: "Si no los guardas, se perderán.",              // texto secundario, más pequeño
  kind: "warning",                                          // info | warning | error | question
  buttons: [                                                // hasta 3, con texto propio
    { id: "guardar", text: "Guardar", default: true },
    { id: "no", text: "No guardar", destructive: true },
    { id: "cancelar", text: "Cancelar", cancel: true },
  ],
  checkbox: "No volver a preguntar",                        // opcional
  input: { placeholder: "Nombre", value: "" },             // opcional: un campo de texto (prompt)
  expandable: "Detalle técnico…",                           // opcional: texto desplegable (errores)
});
// → { button: "guardar", checked: false, value: "..." }
```

Las formas actuales (`message(texto, { kind, buttons: "yesNo" })` → `"yes"`) se mantienen y se traducen a esta.

### Convenciones por sistema (lo que hace que parezca nativo)

- **Orden de botones**: Windows y GNOME/KDE con la acción a la izquierda del cancelar (Windows: `[Guardar] [No guardar]
  [Cancelar]`); macOS con la acción por defecto a la derecha y «Cancelar» a su izquierda.
- **Teclas**: Intro = botón por defecto, Escape = el de `cancel`; en macOS además Cmd+D para el destructivo («No
  guardar») y Cmd+. para cancelar; en Windows, teclas de acceso subrayadas con Alt.
- **Colocación**: ventana modal centrada sobre su dueña (en macOS podría ser una «hoja» que baja de la barra de título;
  queda como mejora).
- **Iconos** del sistema (los mismos glifos que la barra de título) y tipografía/colores de htaw.css.
- **Accesibilidad**: el contenido es HTML semántico con roles (`alertdialog`), así que el lector de pantalla lo lee como
  un diálogo; el foco empieza en el botón por defecto.

### Coste y cómo reducirlo

Abrir un webview nuevo tarda del orden de 150–300 ms. Se puede bajar:
- reutilizando el entorno de WebView2 de la app (ya es compartido) y el proceso de renderizado del mismo origen;
- con una ventana de diálogo **precreada y oculta** al primer uso que se reutiliza para los siguientes.

### Lo que sigue siendo nativo

- **La confirmación de permisos de htaW** y los errores de arranque (por ejemplo, «falta el runtime de WebView2»):
  aparecen antes de que exista ningún webview o cuando no puede existir. Se quedan con `MessageBoxW` / `NSAlert` /
  GTK.
- Los diálogos de archivos y carpetas (esos sí siguen el tema en Windows).

### Seguridad

El diálogo es contenido interno de WolfGlue (`/_glue/dialog`), no de la app: la app solo pasa datos (textos, ids), que
se insertan como texto, nunca como HTML. Una página siempre puede imitar un diálogo con su propio HTML, pero no puede
alterar los diálogos de WolfGlue ni los de permisos de htaW.

## Decisión

Opción C para la **versión 1.2**; en la 1.1 se mantiene el comportamiento actual (A). Queda por decidir entonces si
`MessageAsync` de WolfGlue (C#) cambia para todas las apps o se ofrece como opción (`GlueAppOptions.Dialogs = Native | Glue`).
