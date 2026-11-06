# The `htaw` API

On every page, before any script runs, htaW provides a global `htaw` object with everything the app can use from the
system. Nothing to import.

## General concepts

- **Promises.** Every function returns a `Promise`, except properties (`htaw.app.id`, `htaw.theme.dark`…). Use
  `await` (in a `<script type="module">` also at the top level) or `.then()`.
- **Permissions.** Each group states the permission it needs. Without it, the promise is rejected with
  `code: "PermissionDenied"`. They are declared in the manifest (see [Permissions](05-permissions.md)).
- **Errors.** Failures arrive as an `Error` with a `code` and a `message` in the interface language:

  | `code` | When |
  |---|---|
  | `PermissionDenied` | The permission is missing, or the operation is not allowed (a program outside the `shell.exec` list) |
  | `NotFound` | The file, folder or program does not exist |
  | `AccessDenied` | The system denies access (protected file, in use…) |
  | `Timeout` | The time limit was exceeded (`exec`) |
  | `Cancelled` | The operation was cancelled |
  | `InvalidArgument` | A parameter is not valid (empty path, URL not allowed, unknown encoding…) |
  | `IOError` | Input/output error |
  | `MethodNotFound` | The function does not exist in this version of htaW |
  | `Error` | Anything else |

  ```js
  try {
    await htaw.fs.readText("data.json");
  } catch (e) {
    if (e.code === "NotFound") { /* first run: no data yet */ }
    else await htaw.dialog.message(e.message, { kind: "error" });
  }
  ```

- **Paths.** They are system paths (`C:\folder\file.txt`, `/home/…`). **Relative** ones are resolved from the app
  folder (standalone HTML, development folder or the portable executable's folder) or, in a packaged `.htaw`, from
  the user's folder. A leading `~` is the user's folder (`~/Documents/notes.txt`).
- **Editor types.** `src/htaW/Assets/htaw.d.ts` describes the whole API. Copy it next to your app and add
  `/// <reference path="htaw.d.ts" />` at the top of your scripts to get autocompletion in editors such as VS Code.

Index: [`app`](#htawapp) · [`window`](#htawwindow) · [`dialog`](#htawdialog) · [`fs`](#htawfs) ·
[`shell`](#htawshell) · [`settings`](#htawsettings) · [`http`](#htawhttp) · [`clipboard`](#htawclipboard) ·
[`env`](#htawenv) · [`theme`](#htawtheme) · [`permissions`](#htawpermissions-and-htawhas)

---

## `htaw.app`

Application data. **No permission.**

| Member | Type | Description |
|---|---|---|
| `id` | `string` | Identifier (from the manifest or derived). |
| `name` | `string` | Visible name. |
| `version` | `string \| null` | Manifest version. |
| `args` | `string[]` | Arguments it was opened with: `htaw app.htaw one "two three"` → `["one", "two three"]`. In a portable executable, all its arguments. |
| `dir` | `string \| null` | App folder (standalone HTML, development folder, portable executable with HTML); `null` in a zip. |
| `file` | `string` | Path of the `.htaw`, the folder or the portable `.exe`. |
| `dev` | `boolean` | `true` in development mode. |
| `quit(code?)` | `Promise<void>` | Closes the whole app with that exit code (0 by default). |
| `relaunch()` | `Promise<void>` | Opens the app again with the same arguments (and mode) and closes this one. |

```js
document.title = `${htaw.app.name} ${htaw.app.version ?? ""}`;
if (htaw.app.args.length) openFile(htaw.app.args[0]);   // e.g. a file dropped onto the app
if (htaw.app.dev) console.log("Development mode");
```

---

## `htaw.window`

The page's window. **No permission.** More detail and examples in [Windows and messaging](07-windows.md).

| Member | Description |
|---|---|
| `id` | Identifier of this window. |
| `setTitle(text)` | Changes the title. |
| `setSize(width, height)` | Changes the size of the content area (logical units). |
| `center()` | Centres the window on its screen. |
| `minimize()`, `maximize()`, `restore()` | Minimizes, maximizes or restores. |
| `close(result?)` | Closes the window. In a modal window, `result` (any JSON value) reaches whoever opened it. Closing the last window closes the app. |
| `open(page, options?)` | Opens another page of the app in a new window. Returns `{ id, result }`. |

`open` options: `title`, `width`, `height` (by default, those of the window that opens it), `resizable` (`true`) and
`modal` (`false`). With `modal: true` the window opens above the current one, blocks it while open, and `result` is a
promise with whatever the other page passes to `htaw.window.close(value)` (`null` if it closed without a value).

```js
const { result } = await htaw.window.open("settings.html", { modal: true, width: 380, height: 220, resizable: false });
const r = await result;            // e.g. { name: "Ann" }, or null if cancelled
```

---

## `htaw.dialog`

Native system dialogs. **Permission `dialog`.** They show above the page's window and block it while open.

### `open(options?)` → `Promise<string[] | null>`

Open one or more files. Returns the chosen paths or `null` if cancelled.

| Option | Description |
|---|---|
| `title` | Dialog title. |
| `filters` | List of filters: `{ name: "Images", patterns: ["png", "jpg"] }`. Patterns can be `"png"`, `".png"` or `"*.png"`. Shortcut: a plain string (`"*.txt"`) is a filter with that name and pattern. |
| `defaultPath` | Initial folder (or file path). |
| `multiple` | Allow choosing several files. |

```js
const files = await htaw.dialog.open({
  title: "Choose images",
  filters: [{ name: "Images", patterns: ["png", "jpg", "gif"] }, { name: "All", patterns: ["*"] }],
  multiple: true,
});
if (files) for (const f of files) console.log(f);
```

### `save(options?)` → `Promise<string | null>`

Choose where to save. Same options as `open` (without `multiple`) plus `fileName` (suggested name) and
`defaultExtension` (without the dot; added if the user types none). The system dialog already asks before
overwriting an existing file.

```js
const path = await htaw.dialog.save({ fileName: "report.md", defaultExtension: "md", filters: [{ name: "Markdown", patterns: ["md"] }] });
if (path) await htaw.fs.writeText(path, text);
```

### `folder(options?)` → `Promise<string | null>`

Choose a folder. Options: `title`, `defaultPath`.

### `message(text, options?)` → `Promise<"ok" | "cancel" | "yes" | "no">`

Native message.

| Option | Values | Default |
|---|---|---|
| `title` | text | the window's |
| `kind` | `"info"`, `"warning"`, `"error"`, `"question"` | `"info"` |
| `buttons` | `"ok"`, `"okCancel"`, `"yesNo"`, `"yesNoCancel"` | `"ok"` |
| `defaultButton` | index of the default button (0 = the first one) | 0 |

```js
const r = await htaw.dialog.message("Save changes before leaving?",
  { kind: "question", buttons: "yesNoCancel" });
if (r === "yes") await save();
if (r !== "cancel") await htaw.app.quit();
```

> For dangerous questions ("Delete everything?") set `defaultButton` to the cancel button.

---

## `htaw.fs`

Files and folders. **Permission `fs`.** Access to any path the user can access.

| Function | Returns | Description |
|---|---|---|
| `readText(path)` | `string` | Reads a text file (UTF-8; honours a UTF-16 BOM). |
| `writeText(path, text, { append })` | — | Writes text as UTF-8 (creates or replaces; with `append: true`, appends). |
| `readBytes(path)` | `Uint8Array` | Reads a binary file. |
| `writeBytes(path, data)` | — | Writes a `Uint8Array` or `ArrayBuffer`. |
| `list(folder)` | `FsEntry[]` | Contents of a folder: folders first, then files, by name. |
| `stat(path)` | `FsStat` | Information about a file or folder (does not fail if it does not exist: `exists: false`). |
| `exists(path)` | `boolean` | Whether it exists (file or folder). |
| `mkdir(path)` | — | Creates the folder and any missing parents. |
| `remove(path, { recursive })` | — | Deletes a file, or a folder (empty, or with all its contents with `recursive: true`). **Does not use the recycle bin.** |
| `rename(from, to, { overwrite })` | — | Moves or renames a file or folder. |
| `copy(from, to, { overwrite })` | — | Copies a file. |
| `watch(path, fn, { recursive })` | `{ close() }` | Reports changes in a folder or a file. |

```ts
FsEntry = { name, path, isDirectory, size, modified }        // modified: ISO date (UTC)
FsStat  = { exists, isDirectory, size, modified, created, readOnly }
```

```js
// Read and write JSON
const path = "~/Documents/my-app/data.json";
const data = await htaw.fs.exists(path) ? JSON.parse(await htaw.fs.readText(path)) : {};
data.lastTime = new Date().toISOString();
await htaw.fs.mkdir("~/Documents/my-app");
await htaw.fs.writeText(path, JSON.stringify(data, null, 2));

// List only images
const images = (await htaw.fs.list(folder)).filter(e => !e.isDirectory && /\.(png|jpe?g)$/i.test(e.name));

// Binary: show an image from disk
const bytes = await htaw.fs.readBytes(images[0].path);
img.src = URL.createObjectURL(new Blob([bytes]));
```

### `watch`

```js
const watcher = await htaw.fs.watch(folder, (e) => {
  // e.type: "change" | "create" | "delete" | "rename";  e.path;  e.oldPath (rename only)
  console.log(e.type, e.path);
}, { recursive: true });
// ...
await watcher.close();
```

Editors usually save in several steps, so one save can produce several notifications in a row: group them with a
timer if you are going to reload something heavy.

---

## `htaw.shell`

Open things with other applications and run programs.

### `open(urlOrPath)` · `reveal(path)` — **permission `shell.open`**

- `open`: opens a URL (`http`, `https`, `mailto`) in the browser or mail client, or a file or folder with the
  application the system assigns to it. **It does not run programs**: opening an `.exe`, `.bat`, `.ps1`, `.lnk`, a
  script… is rejected unless the app has `shell.exec: "*.*"`.
- `reveal`: opens the file manager with the file selected.

```js
await htaw.shell.open("https://example.com/help");
await htaw.shell.open(pdfPath);            // with the user's PDF viewer
await htaw.shell.reveal(savedPath);        // "Show in folder"
```

### `exec(program, args?, options?)` → `{ code, stdout, stderr }` — **permission `shell.exec`**

Runs a program, waits for it to finish and returns its exit code and its whole output.

| Option | Description |
|---|---|
| `cwd` | Working folder (by default, the app's or the user's). |
| `env` | Environment variables **added** to the inherited ones: `{ LANG: "C" }`. |
| `timeout` | Milliseconds. If exceeded, the process (and its children) is killed and the call fails with `Timeout`. |
| `encoding` | Output encoding: `utf-8` (default), `oem` (Windows console) or a name such as `latin1`. |
| `input` | Text sent to the standard input. |

```js
const r = await htaw.shell.exec("git", ["log", "-5", "--oneline"], { cwd: repo, timeout: 10000 });
if (r.code === 0) show(r.stdout);
else showError(r.stderr);
```

- `program` is a name looked up in the `PATH` (`"git"`, without `.exe`) or a path. It has to be allowed in the
  manifest (`{ "shell.exec": ["git"] }`; see [Permissions](05-permissions.md)).
- **Arguments are always a list** and reach the program as they are: there is no intermediate shell, so spaces,
  quotes, `&` or `|` inside an argument have no special effect. For pipes or redirections, do it in JavaScript with
  the output.
- A non-zero code is **not** a promise error: check `r.code`. The promise fails if the program cannot start
  (`NotFound`), is not allowed (`PermissionDenied`) or times out (`Timeout`).

### `spawn(program, args?, options?)` → process — **permission `shell.exec`**

Starts a program and lets it keep running; its output arrives as it is produced. Options: `cwd`, `env`, `encoding`.

```js
const p = await htaw.shell.spawn("ping", ["-n", "5", "example.com"]);
p.on("stdout", (text) => console_.textContent += text)
 .on("stderr", (text) => console_.textContent += text)
 .on("exit", (code) => status.textContent = "Exited with code " + code);

await p.write("text\n");     // write to its standard input
await p.closeInput();        // close the input (end of data)
await p.kill();              // terminate it, with all its child processes
const code = await p.exited; // promise resolved with the exit code
```

- Output arrives **line by line**, each with its trailing `\n`.
- When the app closes, htaW terminates the processes that are still alive.

---

## `htaw.settings`

Persistent app settings, stored as JSON in the htaW data folder (`apps/<id>/settings.json`). **No permission**: each
app only sees its own.

| Function | Description |
|---|---|
| `get(key, fallback?)` | Stored value, or `fallback` (or `null`) if it does not exist. |
| `set(key, value)` | Stores any JSON value (number, text, list, object…). Written immediately. |
| `remove(key)` | Deletes a key. |
| `all()` | Object with all the keys. |

```js
const prefs = await htaw.settings.get("prefs", { fontSize: 14, recent: [] });
prefs.recent = [path, ...prefs.recent.filter(r => r !== path)].slice(0, 10);
await htaw.settings.set("prefs", prefs);
```

> `localStorage` and IndexedDB also work: each app has its own origin (`https://<id>.htaw.local/`), so its web data
> does not mix with other apps' either. `settings` is handier for preferences and can be read from outside (it is a
> JSON file).

---

## `htaw.http`

HTTP requests **without the browser's CORS restrictions** (htaW makes them, not the page). **Permission `http`.**

### `fetch(url, options?)` → response

| Option | Description |
|---|---|
| `method` | `"GET"` by default. |
| `headers` | Object of headers. |
| `body` | Text; object (sent as JSON, with `Content-Type: application/json` if none was given); or `Uint8Array`/`ArrayBuffer` (binary). |
| `responseType` | `"text"` (default) or `"base64"` for binary responses. |
| `timeout` | Milliseconds (100,000 by default). |

Response: `{ status, statusText, ok, headers, body, json() }`. `headers` keys are lowercase. A 4xx/5xx status does
**not** reject the promise: check `ok` or `status`.

```js
const r = await htaw.http.fetch("https://api.github.com/repos/dotnet/runtime", {
  headers: { "User-Agent": "my-app" },
});
if (!r.ok) throw new Error("HTTP " + r.status);
const repo = r.json();
console.log(repo.stargazers_count);

// POST with JSON
await htaw.http.fetch("https://example.com/api/notes", { method: "POST", body: { text: "hello" } });

// Download a binary and save it
const img = await htaw.http.fetch(url, { responseType: "base64" });
await htaw.fs.writeBytes(target, Uint8Array.from(atob(img.body), c => c.charCodeAt(0)));
```

Only `http` and `https` URLs are allowed. The browser's standard `fetch` also works, but with CORS: for your own or
public APIs that allow it, the `http` permission is not needed.

---

## `htaw.clipboard`

System clipboard (text). **Permission `clipboard`.**

```js
await htaw.clipboard.writeText(result);
const pasted = await htaw.clipboard.readText();   // null if there is no text
```

---

## `htaw.env`

System environment. **Permission `env`.**

| Member | Description |
|---|---|
| `get(name)` | Value of an environment variable, or `null`. |
| `home` | User folder. |
| `temp` | Temporary folder. |
| `documents` | Documents folder. |
| `os` | `"windows"`, `"macos"` or `"linux"`. |
| `arch` | Architecture (`"x64"`, `"arm64"`…). |

Without the permission, the properties do not exist and `get` fails with `PermissionDenied`. (The operating system
is also available, without permission, in `htaw.theme.os`.)

```js
const target = htaw.env.documents + "\\Exports";
const proxy = await htaw.env.get("HTTPS_PROXY");
```

---

## `htaw.theme`

System theme. **No permission.** It updates by itself if the user changes the theme while the app is open.

| Member | Description |
|---|---|
| `dark` | `true` if the system is in dark mode. |
| `accent` | Accent colour `#rrggbb`. |
| `fontFamily`, `fontSize` | System UI font and its size in CSS pixels. |
| `highContrast` | High-contrast mode active. |
| `os` | `"windows"`, `"macos"` or `"linux"`. |
| `on("change", fn)` | Reports every change with the new theme. Returns a function to stop listening. |

```js
const paint = (t) => chart.setColours({ background: t.dark ? "#202020" : "#fff", series: t.accent });
paint(htaw.theme);
const stop = htaw.theme.on("change", paint);
```

With `theme: "native"` the CSS already adapts by itself; this is for what you draw in code (canvas, charts…). In CSS,
`@media (prefers-color-scheme: dark)` also works.

---

## `htaw.permissions` and `htaw.has()`

- `htaw.permissions`: list of granted permissions, in canonical form: `"fs"`, `"dialog"`, `"shell.exec:git"`,
  `"shell.exec:*"` (all)…
- `htaw.has(permission)`: whether the app has it. `htaw.has("shell.exec")` is `true` if it can run any program at all.

```js
document.getElementById("export").hidden = !htaw.has("fs");
```

---

## Other page capabilities

- **`window.open(url)`** and links with `target="_blank"`: a page of the app itself opens in an htaW window; an
  external URL (`http`, `https`, `mailto`) opens in the system browser. Other schemes are ignored.
  External URLs only open when the user asks with a click or a key press (unless the app has the `links.auto`
  permission, see [Permissions](05-permissions.md#the-apps-pages)).
- **Messages between windows**: `glue.emit` / `glue.on`, `BroadcastChannel` and the `storage` event; see
  [Windows and messaging](07-windows.md).
- **The `glue` object**: the WolfGlue layer `htaw` is built on. Only `glue.emit`, `glue.on` and `glue.off` are meant
  to be used; its dialogs, clipboard and shell are disabled in htaW (use the `htaw` ones, which check permissions).

## Not available yet

- `htaw.window.onClosing(fn)`: notify before the window closes so it can be cancelled (for example, with unsaved
  changes).
- Single instance (`singleInstance`) and the "second instance" event.
- Frameless window (`window.frame: "none"`).
