# Recipes

Complete examples. Each one is a single `.htaw` (standalone HTML): save it with that extension and open it with htaW.

## Text editor

Open, save, "Save as", unsaved-changes marker in the title, Ctrl+S and remembered last file.

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.example.editor",
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
      <button id="open">Open…</button>
      <button id="save">Save</button>
      <button id="saveAs">Save as…</button>
    </div>
    <textarea id="text" spellcheck="false"></textarea>
    <div class="statusbar"><span id="status">Untitled</span></div>
  </div>
  <script type="module">
    const text = document.getElementById("text");
    const status = document.getElementById("status");
    const filters = [{ name: "Text", patterns: ["txt", "md"] }, { name: "All", patterns: ["*"] }];
    let path = null, modified = false;

    function update() {
      const name = path ? path.split(/[\\/]/).pop() : "Untitled";
      htaw.window.setTitle(`${modified ? "• " : ""}${name} — Editor`);
      status.textContent = path ?? "Untitled";
    }

    async function open(p) {
      text.value = await htaw.fs.readText(p);
      path = p; modified = false; update();
      await htaw.settings.set("last", p);
    }

    async function save(as) {
      if (!path || as) {
        const p = await htaw.dialog.save({ fileName: "note.txt", defaultExtension: "txt", filters });
        if (!p) return;
        path = p;
      }
      await htaw.fs.writeText(path, text.value);
      modified = false; update();
      await htaw.settings.set("last", path);
    }

    document.getElementById("open").onclick = async () => {
      const p = await htaw.dialog.open({ filters });
      if (p) await open(p[0]);
    };
    document.getElementById("save").onclick = () => save(false);
    document.getElementById("saveAs").onclick = () => save(true);
    text.oninput = () => { if (!modified) { modified = true; update(); } };
    addEventListener("keydown", (e) => {
      if (e.ctrlKey && e.key === "s") { e.preventDefault(); save(e.shiftKey); }
    });

    // A file passed as an argument (htaw editor.htaw note.txt) or the last one used.
    const initial = htaw.app.args[0] ?? await htaw.settings.get("last");
    if (initial && await htaw.fs.exists(initial)) await open(initial);
    else update();
  </script>
</body>
</html>
```

## Live output of a program

Runs `git` in a chosen folder and shows its output as it arrives, with a button to stop it.

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.example.gitlog",
    "name": "Git history",
    "window": { "width": 720, "height": 480 },
    "permissions": ["dialog", { "shell.exec": ["git"] }]
  }
  </script>
</head>
<body>
  <div class="layout-fill">
    <div class="toolbar">
      <button id="folder" class="primary">Choose repository…</button>
      <button id="stop" disabled>Stop</button>
      <span class="spacer"></span><span id="status" class="muted"></span>
    </div>
    <main><pre id="output"></pre></main>
  </div>
  <script type="module">
    const output = document.getElementById("output");
    const status = document.getElementById("status");
    const stop = document.getElementById("stop");
    let proc = null;

    document.getElementById("folder").onclick = async () => {
      const repo = await htaw.dialog.folder({ title: "Git repository" });
      if (!repo) return;
      output.textContent = "";
      try {
        proc = await htaw.shell.spawn("git", ["log", "--stat", "-50"], { cwd: repo });
      } catch (e) {
        status.textContent = e.code === "NotFound" ? "git is not installed" : e.message;
        return;
      }
      stop.disabled = false;
      status.textContent = "Reading…";
      proc.on("stdout", (t) => output.append(t))
          .on("stderr", (t) => output.append(t))
          .on("exit", (code) => {
            status.textContent = code === 0 ? "Done" : `Exited with code ${code}`;
            stop.disabled = true;
          });
    };
    stop.onclick = () => proc?.kill();
  </script>
</body>
</html>
```

For something short that does not need to be seen live, `exec` is simpler:

```js
const { code, stdout, stderr } = await htaw.shell.exec("git", ["rev-parse", "--abbrev-ref", "HEAD"], { cwd: repo });
const branch = code === 0 ? stdout.trim() : null;
```

## Querying an Internet API

`htaw.http.fetch` does not have the browser's CORS restrictions.

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.example.repos",
    "name": "Repositories",
    "window": { "width": 640, "height": 480 },
    "permissions": ["http", "shell.open"]
  }
  </script>
</head>
<body>
  <div class="row">
    <input id="user" placeholder="GitHub user" value="dotnet">
    <button id="search" type="submit">Search</button>
  </div>
  <table>
    <thead><tr><th>Repository</th><th>Stars</th></tr></thead>
    <tbody id="list"></tbody>
  </table>
  <script type="module">
    const list = document.getElementById("list");
    document.getElementById("search").onclick = async () => {
      const user = document.getElementById("user").value.trim();
      const r = await htaw.http.fetch(`https://api.github.com/users/${encodeURIComponent(user)}/repos?per_page=20`, {
        headers: { "User-Agent": "htaw-example", Accept: "application/vnd.github+json" },
        timeout: 15000,
      });
      list.replaceChildren();
      if (!r.ok) { list.insertRow().insertCell().textContent = `Error ${r.status}`; return; }
      for (const repo of r.json().sort((a, b) => b.stargazers_count - a.stargazers_count)) {
        const row = list.insertRow();
        const link = Object.assign(document.createElement("a"), { textContent: repo.name });
        link.onclick = () => htaw.shell.open(repo.html_url);
        row.insertCell().append(link);
        row.insertCell().textContent = repo.stargazers_count;
      }
    };
  </script>
</body>
</html>
```

## A folder that updates by itself

Lists a folder and refreshes when something inside changes.

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.example.folder",
    "name": "Watcher",
    "window": { "width": 560, "height": 440 },
    "permissions": ["fs", "dialog", "shell.open"]
  }
  </script>
</head>
<body>
  <div class="row"><button id="choose">Choose folder…</button><span id="path" class="muted"></span></div>
  <ul id="list" class="listview" style="margin-top:8px; height: 340px"></ul>
  <script type="module">
    const list = document.getElementById("list");
    let folder = null, watcher = null, pending = null;

    async function paint() {
      list.replaceChildren();
      for (const e of await htaw.fs.list(folder)) {
        const li = document.createElement("li");
        li.textContent = (e.isDirectory ? "📁 " : "📄 ") + e.name;
        li.ondblclick = () => htaw.shell.open(e.path);   // open with its application
        list.append(li);
      }
    }

    document.getElementById("choose").onclick = async () => {
      const f = await htaw.dialog.folder();
      if (!f) return;
      folder = f;
      document.getElementById("path").textContent = f;
      await watcher?.close();
      watcher = await htaw.fs.watch(f, () => {
        // Group bursts of changes into a single repaint.
        clearTimeout(pending);
        pending = setTimeout(paint, 200);
      });
      await paint();
    };
  </script>
</body>
</html>
```

## Dropping files on the window

Dragging files from the file manager onto the page works with the standard web APIs: their contents can be read,
although their path on disk is not known.

```js
addEventListener("dragover", (e) => e.preventDefault());   // without this, the engine would try to open the file
addEventListener("drop", async (e) => {
  e.preventDefault();
  for (const f of e.dataTransfer.files) {
    const contents = await f.text();           // or await f.arrayBuffer() for binaries
    console.log(f.name, f.size, contents.length);
  }
});
```

If you need the path (to save back to the same place), use `htaw.dialog.open`. Another option: drop the file onto the
app's portable executable, or onto a shortcut to `htaw.exe` with the app as argument; the path then arrives in
`htaw.app.args`.

## More examples

In `samples/htaw`:

- `hola.htaw`: controls with the native style, a message, `exec` of git, a denied permission and a modal dialog with
  a result (`ajustes.html`).
- `markdown.htaw`: Markdown editor with live preview, open and save, copy the HTML; a draggable splitter between editor
  and preview, light/dark preview (automatic from the system) and a collapsible stylesheet panel, all remembered with
  `htaw.settings`.
- `notas/`: multi-page app (folder with `htaw.json`), ready for `htaw pack`.
