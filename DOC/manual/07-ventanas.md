# Ventanas y comunicación

Una app htaW puede tener varias ventanas. Todas son páginas de la misma app: comparten origen, `localStorage`,
ajustes y permisos. La app termina al cerrar la última ventana (o con `htaw.app.quit()`).

## Abrir una ventana

```js
const { id } = await htaw.window.open("historial.html", { title: "Historial", width: 500, height: 400 });
```

- `pagina`: ruta de una página de la app, relativa a la raíz (`"historial.html"`, `"vistas/detalle.html?id=3"`).
  Las páginas de fuera de la app no se pueden abrir en una ventana de htaW.
- Opciones: `title` (si no, el `<title>` de la página), `width`, `height` (por defecto, los de la ventana actual),
  `resizable` (`true`), `modal` (`false`), `titlebar` (barra de título propia, o `false`).
- `window.open("historial.html")` y los enlaces con `target="_blank"` hacen lo mismo (las URL externas se abren en el
  navegador del sistema si las pide el usuario con un clic o una tecla).

La ventana nueva tiene el mismo icono y el mismo aspecto que la principal. Su barra de título es la que declare su página
(o la de la opción `titlebar`); cada ventana tiene la suya o la normal del sistema. Ver [Barra de título](10-barra-de-titulo.md).

## Diálogos propios con resultado (ventanas modales)

Con `modal: true` la ventana se abre encima de la actual, la deshabilita hasta cerrarse y devuelve un resultado:

**Quien abre** (`index.html`):

```js
const { result } = await htaw.window.open("renombrar.html?nombre=" + encodeURIComponent(actual),
  { modal: true, width: 360, height: 180, resizable: false, title: "Renombrar" });
const r = await result;          // lo que el diálogo pase a close(), o null
if (r) await renombrar(actual, r.nombre);
```

**El diálogo** (`renombrar.html`):

```html
<!doctype html>
<html lang="es">
<head><meta charset="utf-8"><title>Renombrar</title></head>
<body>
  <label>Nuevo nombre <input id="nombre" autofocus></label>
  <div class="dialog-buttons">
    <button type="submit" id="ok">Aceptar</button>
    <button id="cancelar">Cancelar</button>
  </div>
  <script>
    const campo = document.getElementById("nombre");
    campo.value = new URLSearchParams(location.search).get("nombre") ?? "";
    campo.select();
    const aceptar = () => htaw.window.close({ nombre: campo.value.trim() });
    const cancelar = () => htaw.window.close();          // sin valor: el resultado es null
    document.getElementById("ok").onclick = aceptar;
    document.getElementById("cancelar").onclick = cancelar;
    addEventListener("keydown", (e) => {
      if (e.key === "Enter") aceptar();
      if (e.key === "Escape") cancelar();
    });
  </script>
</body>
</html>
```

- El resultado puede ser cualquier valor JSON (texto, número, objeto, lista).
- Cerrar con el botón ✕ de la ventana equivale a `close()` sin valor: `null`.
- Los datos de entrada se pasan por la URL (`?nombre=…`), por `htaw.settings` o por `localStorage`.
- `.dialog-buttons` pone los botones en el orden de cada sistema; escribe siempre el principal primero.

## Mensajes entre ventanas

### `glue.emit` / `glue.on`

htaW incluye un canal de eventos entre las ventanas de la app:

```js
// En cualquier ventana: avisar a todas las demás
glue.emit("nota-guardada", { ruta, titulo });

// A una ventana concreta (su htaw.window.id)
glue.emit("ir-a", { linea: 42 }, { to: idEditor });

// Escuchar (devuelve una función para dejar de escuchar)
const dejar = glue.on("nota-guardada", (datos, deQuien) => recargarLista());
```

- Los datos son cualquier valor JSON.
- `to: "*"` (por defecto) envía a todas las demás ventanas; con un id, solo a esa.
- La ventana que emite no recibe su propio evento.

### Alternativas estándar

Como todas las ventanas comparten origen, también funcionan sin nada de htaW:

```js
const canal = new BroadcastChannel("mi-app");
canal.postMessage({ tipo: "refrescar" });
canal.onmessage = (e) => console.log(e.data);

addEventListener("storage", (e) => { if (e.key === "tema-app") aplicar(e.newValue); });
```

## Avisar antes de cerrar

```js
let modificado = false;
htaw.window.onClosing(async () => {
  if (!modificado) return;                       // nada que perder: se cierra
  const r = await htaw.dialog.message("Hay cambios sin guardar. ¿Cerrar igualmente?",
    { kind: "warning", buttons: "yesNo" });      // necesita el permiso "dialog"
  return r === "yes";                            // false: la ventana sigue abierta
});
```

Se llama cuando el usuario cierra la ventana (botón, Alt+F4, barra de tareas) o lo pide el sistema; no cuando la propia
página llama a `htaw.window.close()`. Si la página no responde (bloqueada), un segundo intento de cierre pasados 2
segundos la cierra igualmente.

## Controlar la ventana actual

```js
await htaw.window.setTitle(`${nombreArchivo}${modificado ? " •" : ""} — Mi editor`);
await htaw.window.setSize(900, 600);
await htaw.window.center();
await htaw.window.maximize();     // minimize(), restore()
await htaw.window.close();
```

El título inicial es `name` del manifiesto en la ventana principal; si no hay, el `<title>` de la página (que sigue
al documento si cambia).
