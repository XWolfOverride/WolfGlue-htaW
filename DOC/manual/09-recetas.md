# Recetas

Ejemplos completos. Cada uno es un único `.htaw` (HTML suelto): guárdalo con esa extensión y ábrelo con htaW.

## Editor de texto

Abrir, guardar, «Guardar como», aviso de cambios sin guardar en el título, Ctrl+S y último archivo recordado.

```html
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.ejemplo.editor",
    "name": "Editor",
    "window": { "width": 760, "height": 520, "minWidth": 400, "minHeight": 260 },
    "permissions": ["fs", "dialog"]
  }
  </script>
  <style>
    textarea { flex: 1; margin: 0; border-radius: 0; border: 0; resize: none; font-family: "Cascadia Mono", Consolas, monospace; }
  </style>
</head>
<body>
  <div class="layout-fill">
    <div class="toolbar">
      <button id="abrir">Abrir…</button>
      <button id="guardar">Guardar</button>
      <button id="guardarComo">Guardar como…</button>
    </div>
    <textarea id="texto" spellcheck="false"></textarea>
    <div class="statusbar"><span id="estado">Sin título</span></div>
  </div>
  <script type="module">
    const texto = document.getElementById("texto");
    const estado = document.getElementById("estado");
    const filtros = [{ name: "Texto", patterns: ["txt", "md"] }, { name: "Todos", patterns: ["*"] }];
    let ruta = null, modificado = false;

    function actualizar() {
      const nombre = ruta ? ruta.split(/[\\/]/).pop() : "Sin título";
      htaw.window.setTitle(`${modificado ? "• " : ""}${nombre} — Editor`);
      estado.textContent = ruta ?? "Sin título";
    }

    async function abrir(r) {
      texto.value = await htaw.fs.readText(r);
      ruta = r; modificado = false; actualizar();
      await htaw.settings.set("ultimo", r);
    }

    async function guardar(como) {
      if (!ruta || como) {
        const r = await htaw.dialog.save({ fileName: "nota.txt", defaultExtension: "txt", filters: filtros });
        if (!r) return;
        ruta = r;
      }
      await htaw.fs.writeText(ruta, texto.value);
      modificado = false; actualizar();
      await htaw.settings.set("ultimo", ruta);
    }

    document.getElementById("abrir").onclick = async () => {
      const r = await htaw.dialog.open({ filters: filtros });
      if (r) await abrir(r[0]);
    };
    document.getElementById("guardar").onclick = () => guardar(false);
    document.getElementById("guardarComo").onclick = () => guardar(true);
    texto.oninput = () => { if (!modificado) { modificado = true; actualizar(); } };
    addEventListener("keydown", (e) => {
      if (e.ctrlKey && e.key === "s") { e.preventDefault(); guardar(e.shiftKey); }
    });

    // Un archivo pasado como argumento (htaw editor.htaw nota.txt) o el último que se usó.
    const inicial = htaw.app.args[0] ?? await htaw.settings.get("ultimo");
    if (inicial && await htaw.fs.exists(inicial)) await abrir(inicial);
    else actualizar();
  </script>
</body>
</html>
```

## Salida de un programa en directo

Lanza `git` en una carpeta elegida y muestra su salida según llega, con un botón para detenerlo.

```html
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.ejemplo.gitlog",
    "name": "Historial de git",
    "window": { "width": 720, "height": 480 },
    "permissions": ["dialog", { "shell.exec": ["git"] }]
  }
  </script>
</head>
<body>
  <div class="layout-fill">
    <div class="toolbar">
      <button id="carpeta" class="primary">Elegir repositorio…</button>
      <button id="parar" disabled>Detener</button>
      <span class="spacer"></span><span id="estado" class="muted"></span>
    </div>
    <main><pre id="salida"></pre></main>
  </div>
  <script type="module">
    const salida = document.getElementById("salida");
    const estado = document.getElementById("estado");
    const parar = document.getElementById("parar");
    let proceso = null;

    document.getElementById("carpeta").onclick = async () => {
      const repo = await htaw.dialog.folder({ title: "Repositorio de git" });
      if (!repo) return;
      salida.textContent = "";
      try {
        proceso = await htaw.shell.spawn("git", ["log", "--stat", "-50"], { cwd: repo });
      } catch (e) {
        estado.textContent = e.code === "NotFound" ? "git no está instalado" : e.message;
        return;
      }
      parar.disabled = false;
      estado.textContent = "Leyendo…";
      proceso.on("stdout", (t) => salida.append(t))
             .on("stderr", (t) => salida.append(t))
             .on("exit", (code) => {
               estado.textContent = code === 0 ? "Listo" : `Terminó con código ${code}`;
               parar.disabled = true;
             });
    };
    parar.onclick = () => proceso?.kill();
  </script>
</body>
</html>
```

Para algo corto que no necesita verse en directo, `exec` es más sencillo:

```js
const { code, stdout, stderr } = await htaw.shell.exec("git", ["rev-parse", "--abbrev-ref", "HEAD"], { cwd: repo });
const rama = code === 0 ? stdout.trim() : null;
```

## Consultar una API de Internet

`htaw.http.fetch` no tiene las restricciones CORS del navegador.

```html
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.ejemplo.repos",
    "name": "Repositorios",
    "window": { "width": 640, "height": 480 },
    "permissions": ["http", "shell.open"]
  }
  </script>
</head>
<body>
  <div class="row">
    <input id="usuario" placeholder="Usuario de GitHub" value="dotnet">
    <button id="buscar" type="submit">Buscar</button>
  </div>
  <table>
    <thead><tr><th>Repositorio</th><th>Estrellas</th></tr></thead>
    <tbody id="lista"></tbody>
  </table>
  <script type="module">
    const lista = document.getElementById("lista");
    document.getElementById("buscar").onclick = async () => {
      const usuario = document.getElementById("usuario").value.trim();
      const r = await htaw.http.fetch(`https://api.github.com/users/${encodeURIComponent(usuario)}/repos?per_page=20`, {
        headers: { "User-Agent": "htaw-ejemplo", Accept: "application/vnd.github+json" },
        timeout: 15000,
      });
      lista.replaceChildren();
      if (!r.ok) { lista.insertRow().insertCell().textContent = `Error ${r.status}`; return; }
      for (const repo of r.json().sort((a, b) => b.stargazers_count - a.stargazers_count)) {
        const fila = lista.insertRow();
        const enlace = Object.assign(document.createElement("a"), { textContent: repo.name });
        enlace.onclick = () => htaw.shell.open(repo.html_url);
        fila.insertCell().append(enlace);
        fila.insertCell().textContent = repo.stargazers_count;
      }
    };
  </script>
</body>
</html>
```

## Carpeta que se actualiza sola

Lista una carpeta y se refresca cuando cambia algo dentro.

```html
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.ejemplo.carpeta",
    "name": "Vigilante",
    "window": { "width": 560, "height": 440 },
    "permissions": ["fs", "dialog", "shell.open"]
  }
  </script>
</head>
<body>
  <div class="row"><button id="elegir">Elegir carpeta…</button><span id="ruta" class="muted"></span></div>
  <ul id="lista" class="listview" style="margin-top:8px; height: 340px"></ul>
  <script type="module">
    const lista = document.getElementById("lista");
    let carpeta = null, vigilancia = null, pendiente = null;

    async function pintar() {
      lista.replaceChildren();
      for (const e of await htaw.fs.list(carpeta)) {
        const li = document.createElement("li");
        li.textContent = (e.isDirectory ? "📁 " : "📄 ") + e.name;
        li.ondblclick = () => htaw.shell.open(e.path);   // abrir con su aplicación
        lista.append(li);
      }
    }

    document.getElementById("elegir").onclick = async () => {
      const c = await htaw.dialog.folder();
      if (!c) return;
      carpeta = c;
      document.getElementById("ruta").textContent = c;
      await vigilancia?.close();
      vigilancia = await htaw.fs.watch(c, () => {
        // Agrupa ráfagas de cambios en un solo repintado.
        clearTimeout(pendiente);
        pendiente = setTimeout(pintar, 200);
      });
      await pintar();
    };
  </script>
</body>
</html>
```

## Soltar archivos sobre la ventana

Arrastrar archivos desde el explorador a la página funciona con las API web estándar: se puede leer su contenido,
aunque no se conoce su ruta en el disco.

```js
addEventListener("dragover", (e) => e.preventDefault());   // sin esto, el motor intentaría abrir el archivo
addEventListener("drop", async (e) => {
  e.preventDefault();
  for (const f of e.dataTransfer.files) {
    const contenido = await f.text();          // o await f.arrayBuffer() para binarios
    console.log(f.name, f.size, contenido.length);
  }
});
```

Si necesitas la ruta (para volver a guardar en el mismo sitio), usa `htaw.dialog.open`. Otra opción: soltar el archivo
sobre el ejecutable portable de la app, o sobre un acceso directo a `htaw.exe` con la app como argumento; la ruta llega
entonces en `htaw.app.args`.

## Más ejemplos

En `samples/htaw`:

- `hola.htaw`: controles con el estilo nativo, mensaje, `exec` de git, permiso denegado y diálogo modal con resultado
  (`ajustes.html`).
- `markdown.htaw`: editor de Markdown con vista previa en vivo, abrir y guardar, copiar el HTML; separador ajustable
  entre editor y vista previa, vista previa clara/oscura (automática según el sistema) y panel de estilo plegable, todo
  recordado con `htaw.settings`.
- `notas/`: app de varias páginas (carpeta con `htaw.json`), lista para `htaw pack`.
