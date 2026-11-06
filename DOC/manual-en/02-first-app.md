# Your first application

This walkthrough goes from an empty HTML file to an application with permissions, an icon, several pages and ready
to distribute. All you need is a text editor and `htaw.exe`.

## 1. A single file

Create `counter.htaw` with this content:

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <script type="application/htaw+json">
  {
    "id": "com.example.counter",
    "name": "Counter",
    "version": "1.0.0",
    "window": { "width": 360, "height": 240, "resizable": false }
  }
  </script>
  <title>Counter</title>
</head>
<body>
  <h1 id="n">0</h1>
  <div class="row">
    <button id="add" type="submit">Add</button>
    <button id="reset">Reset</button>
  </div>
  <script>
    const n = document.getElementById("n");
    let value = 0;
    document.getElementById("add").onclick = () => n.textContent = ++value;
    document.getElementById("reset").onclick = () => n.textContent = value = 0;
  </script>
</body>
</html>
```

Open it with a double click (or drag it onto `htaw.exe`). Notice that:

- it is **plain HTML**: it also opens in a browser;
- the `<script type="application/htaw+json">` block is the **manifest**: identifier, name, window size… (all the
  fields in [The manifest](03-manifest.md));
- there is **no CSS** and it still looks like a system window: htaW injects its stylesheet, which follows the light
  or dark theme and the accent colour (see [Native look](06-style.md)). `type="submit"` marks the main button.

> The `id` isolates the app's data (settings, `localStorage`…). Use a dotted name of your own, in lowercase:
> `com.yourname.app`. Without it, htaW derives one from the file path, and moving the file would change its data.

## 2. Remembering the value: settings

`htaw.settings` keeps app data between runs. It needs no permission:

```js
let value = await htaw.settings.get("value", 0);   // 0 if it does not exist yet
n.textContent = value;
document.getElementById("add").onclick = async () => {
  n.textContent = ++value;
  await htaw.settings.set("value", value);
};
```

(Put the code in a `<script type="module">` to be able to use top-level `await`.)

## 3. Asking for permissions: saving to a file

To read or write files, the app has to **declare it**. Add `"permissions"` to the manifest:

```json
"permissions": ["fs", "dialog"]
```

and a button that exports the value:

```js
document.getElementById("export").onclick = async () => {
  const path = await htaw.dialog.save({ fileName: "counter.txt", filters: [{ name: "Text", patterns: ["txt"] }] });
  if (!path) return;                     // the user cancelled
  await htaw.fs.writeText(path, String(value));
  await htaw.dialog.message("Saved to " + path);
};
```

If you call `htaw.fs.writeText` **without** declaring `fs`, the promise fails with `e.code === "PermissionDenied"`.
So an app can only do what its manifest says, and the user sees it before opening the app if it comes from the
Internet (see [Permissions](05-permissions.md)).

## 4. Developing comfortably

Open the app in **development mode**:

```
htaw --dev counter.htaw
```

- The window **reloads by itself** when you save the file (or any file in its folder).
- **F12** opens the developer tools (console, inspector, network).
- Manifest warnings (unknown fields, icon not found…) appear in the console.

The htaW panel keeps the apps opened in development mode under *Recent*, to relaunch them with one click.

## 5. Several pages: a folder

When the app grows, move it to a folder:

```
counter/
├─ htaw.json        ← the manifest (same JSON, plus "entry")
├─ index.html
├─ app.js
├─ style.css
└─ icon.ico
```

`htaw.json`:

```json
{
  "id": "com.example.counter",
  "name": "Counter",
  "version": "1.1.0",
  "entry": "index.html",
  "icon": "icon.ico",
  "window": { "width": 360, "height": 240, "resizable": false },
  "permissions": ["fs", "dialog"]
}
```

`index.html` no longer needs the manifest block; relative paths (`app.js`, `style.css`) work like on a website. To try
it: `htaw counter` (or drag the folder onto `htaw.exe`): a folder always opens in development mode.

The `icon` (`.ico` or `.png`) shows in the title bar, in the taskbar and, if you build a portable executable, on the
`.exe`.

## 6. Distributing it

- **A `.htaw`**: `htaw pack counter` creates `counter.htaw` (a zip) after validating the manifest. Anyone with htaW
  opens it with a double click.
- **A portable executable**: `htaw bundle counter` creates `counter.exe`, with htaW inside and your icon. It needs
  nothing installed.

Details in [Development and distribution](08-development-and-distribution.md).

## Next steps

- Everything the app can use: [The `htaw` API](04-api.md).
- Custom dialogs with a result and several windows: [Windows and messaging](07-windows.md).
- Complete examples: [Recipes](09-recipes.md) and `samples/htaw`.
