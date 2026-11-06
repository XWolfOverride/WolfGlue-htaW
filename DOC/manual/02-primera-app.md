# Tu primera aplicación

Este recorrido lleva de un HTML vacío a una aplicación con permisos, icono, varias páginas y lista para distribuir.
Solo hace falta un editor de texto y `htaw.exe`.

## 1. Un único archivo

Crea `contador.htaw` con este contenido:

```html
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.ejemplo.contador",
    "name": "Contador",
    "version": "1.0.0",
    "window": { "width": 360, "height": 240, "resizable": false }
  }
  </script>
  <title>Contador</title>
</head>
<body>
  <h1 id="n">0</h1>
  <div class="row">
    <button id="mas" type="submit">Sumar</button>
    <button id="reset">Poner a cero</button>
  </div>
  <script>
    const n = document.getElementById("n");
    let valor = 0;
    document.getElementById("mas").onclick = () => n.textContent = ++valor;
    document.getElementById("reset").onclick = () => n.textContent = valor = 0;
  </script>
</body>
</html>
```

Ábrelo con doble clic (o arrástralo sobre `htaw.exe`). Fíjate en que:

- es un **HTML normal**: se puede abrir también en un navegador;
- el bloque `<script type="application/htaw+json">` es el **manifiesto**: identificador, nombre, tamaño de la
  ventana… (todos los campos en [El manifiesto](03-manifiesto.md));
- **no hay CSS** y aun así parece una ventana del sistema: htaW inyecta su hoja de estilo, que sigue el tema claro u
  oscuro y el color de acento (ver [Aspecto nativo](06-estilo.md)). `type="submit"` marca el botón principal.

> El `id` aísla los datos de la app (ajustes, `localStorage`…). Usa un nombre tuyo con puntos, en minúsculas:
> `com.tunombre.app`. Si no lo pones, htaW deriva uno de la ruta del archivo, y moverlo cambiaría sus datos.

## 2. Recordar el valor: ajustes

`htaw.settings` guarda datos de la app entre ejecuciones. No necesita permiso:

```js
let valor = await htaw.settings.get("valor", 0);   // 0 si aún no existe
n.textContent = valor;
document.getElementById("mas").onclick = async () => {
  n.textContent = ++valor;
  await htaw.settings.set("valor", valor);
};
```

(Pon el código en un `<script type="module">` para poder usar `await` en el nivel superior.)

## 3. Pedir permisos: guardar en un archivo

Para leer o escribir archivos, la app tiene que **declararlo**. Añade `"permissions"` al manifiesto:

```json
"permissions": ["fs", "dialog"]
```

y un botón que exporte el valor:

```js
document.getElementById("exportar").onclick = async () => {
  const ruta = await htaw.dialog.save({ fileName: "contador.txt", filters: [{ name: "Texto", patterns: ["txt"] }] });
  if (!ruta) return;                     // el usuario canceló
  await htaw.fs.writeText(ruta, String(valor));
  await htaw.dialog.message("Guardado en " + ruta);
};
```

Si llamas a `htaw.fs.writeText` **sin** haber declarado `fs`, la promesa falla con `e.code === "PermissionDenied"`.
Así una app solo puede hacer lo que su manifiesto dice, y el usuario lo ve antes de abrirla si viene de Internet
(ver [Permisos](05-permisos.md)).

## 4. Desarrollar cómodamente

Abre la app en **modo desarrollo**:

```
htaw --dev contador.htaw
```

- La ventana se **recarga sola** al guardar el archivo (o cualquier archivo de su carpeta).
- **F12** abre las herramientas de desarrollo (consola, inspector, red).
- Los avisos del manifiesto (campos desconocidos, icono que no se encuentra…) salen en la consola.

El panel de htaW guarda las apps abiertas en modo desarrollo en *Recientes* para relanzarlas con un clic.

## 5. Varias páginas: una carpeta

Cuando la app crece, pásala a una carpeta:

```
contador/
├─ htaw.json        ← el manifiesto (el mismo JSON, más "entry")
├─ index.html
├─ app.js
├─ estilo.css
└─ icono.ico
```

`htaw.json`:

```json
{
  "id": "com.ejemplo.contador",
  "name": "Contador",
  "version": "1.1.0",
  "entry": "index.html",
  "icon": "icono.ico",
  "window": { "width": 360, "height": 240, "resizable": false },
  "permissions": ["fs", "dialog"]
}
```

En `index.html` ya no hace falta el bloque del manifiesto; las rutas relativas (`app.js`, `estilo.css`) funcionan
como en una web. Para probarla: `htaw contador` (o arrastra la carpeta sobre `htaw.exe`): una carpeta siempre se
abre en modo desarrollo.

El `icon` (`.ico` o `.png`) aparece en la barra de título, en la barra de tareas y, si se hace un ejecutable
portable, en el `.exe`.

## 6. Distribuirla

- **Un `.htaw`**: `htaw pack contador` crea `contador.htaw` (un zip) después de validar el manifiesto. Quien tenga
  htaW lo abre con doble clic.
- **Un ejecutable portable**: `htaw bundle contador` crea `contador.exe`, con htaW dentro y tu icono. No necesita
  nada instalado.

Detalles en [Desarrollo y distribución](08-desarrollo-y-distribucion.md).

## Siguientes pasos

- Todo lo que puede usar la app: [La API `htaw`](04-api.md).
- Diálogos propios con resultado y varias ventanas: [Ventanas y comunicación](07-ventanas.md).
- Ejemplos completos: [Recetas](09-recetas.md) y `samples/htaw`.
