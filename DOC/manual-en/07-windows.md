# Windows and messaging

An htaW app can have several windows. They are all pages of the same app: they share the origin, `localStorage`,
settings and permissions. The app ends when the main window closes (or with `htaw.app.quit()`); see [Lifecycle](#lifecycle).

## Opening a window

```js
const { id } = await htaw.window.open("history.html", { title: "History", width: 500, height: 400 });
```

- `page`: path of a page of the app, relative to the root (`"history.html"`, `"views/detail.html?id=3"`). Pages from
  outside the app cannot be opened in an htaW window.
- Options: `title` (otherwise, the page's `<title>`), `width`, `height` (by default, those of the current window),
  `resizable` (`true`), `modal` (`false`), `titlebar` (a custom title bar, or `false`).
- `window.open("history.html")` and links with `target="_blank"` do the same (external URLs open in the system
  browser when the user asks with a click or a key press).

The new window has the same icon and the same look as the main one. Its title bar is the one its page declares (or
the one in the `titlebar` option); each window has its own or the normal system one. See [Title bar](10-title-bar.md).

## Custom dialogs with a result (modal windows)

With `modal: true` the window opens above the current one, disables it until it closes and returns a result:

**The opener** (`index.html`):

```js
const { result } = await htaw.window.open("rename.html?name=" + encodeURIComponent(current),
  { modal: true, width: 360, height: 180, resizable: false, title: "Rename" });
const r = await result;          // whatever the dialog passes to close(), or null
if (r) await rename(current, r.name);
```

**The dialog** (`rename.html`):

```html
<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>Rename</title></head>
<body>
  <label>New name <input id="name" autofocus></label>
  <div class="dialog-buttons">
    <button type="submit" id="ok">OK</button>
    <button id="cancel">Cancel</button>
  </div>
  <script>
    const field = document.getElementById("name");
    field.value = new URLSearchParams(location.search).get("name") ?? "";
    field.select();
    const accept = () => htaw.window.close({ name: field.value.trim() });
    const cancel = () => htaw.window.close();           // no value: the result is null
    document.getElementById("ok").onclick = accept;
    document.getElementById("cancel").onclick = cancel;
    addEventListener("keydown", (e) => {
      if (e.key === "Enter") accept();
      if (e.key === "Escape") cancel();
    });
  </script>
</body>
</html>
```

- The result can be any JSON value (text, number, object, list).
- Closing with the window's ✕ button is the same as `close()` with no value: `null`.
- Input data is passed through the URL (`?name=…`), `htaw.settings` or `localStorage`.
- `.dialog-buttons` puts the buttons in each system's order; always write the main one first.

## Messages between windows

### `glue.emit` / `glue.on`

htaW includes an event channel between the app's windows:

```js
// In any window: notify all the others
glue.emit("note-saved", { path, title });

// To a specific window (its htaw.window.id)
glue.emit("go-to", { line: 42 }, { to: editorId });

// Listen (returns a function to stop listening)
const stop = glue.on("note-saved", (data, from) => reloadList());
```

- Data is any JSON value.
- `to: "*"` (default) sends to all the other windows; with an id, only to that one.
- The emitting window does not receive its own event.

### Standard alternatives

Since all windows share the origin, these work too, with nothing from htaW:

```js
const channel = new BroadcastChannel("my-app");
channel.postMessage({ type: "refresh" });
channel.onmessage = (e) => console.log(e.data);

addEventListener("storage", (e) => { if (e.key === "app-theme") apply(e.newValue); });
```

## Warning before closing

```js
let modified = false;
htaw.window.onClosing(async () => {
  if (!modified) return;                         // nothing to lose: it closes
  const r = await htaw.dialog.message("There are unsaved changes. Close anyway?",
    { kind: "warning", buttons: "yesNo" });      // needs the "dialog" permission
  return r === "yes";                            // false: the window stays open
});
```

It is called when the user closes the window (button, Alt+F4, taskbar) or the system asks; not when the page itself
calls `htaw.window.close()`. If the page does not respond (hung), a second close attempt after 2 seconds closes it
anyway.

## Controlling the current window

```js
await htaw.window.setTitle(`${fileName}${modified ? " •" : ""} — My editor`);
await htaw.window.setSize(900, 600);
await htaw.window.center();
await htaw.window.maximize();     // minimize(), restore()
await htaw.window.close();
```

The initial title is the manifest's `name` in the main window; otherwise, the page's `<title>` (which follows the
document if it changes).

## Lifecycle

The **main window** is the first one opened. By default (`"lifecycle": "main"`), **closing it closes the app and all other windows**, as in Delphi: no forgotten window keeps the app alive. With `"lifecycle": "last"` the app lives while any window remains open (useful for apps with several independent windows).

- `htaw.app.quit()` closes the app in either mode.
- With `tray.keepRunning`, closing the main window hides it (see [`htaw.tray`](04-api.md#htawtray--permission-tray)).
- Modal windows always close with their owner.

## About

An app can show its About window with `htaw.app.showAboutBox()`:

- If the manifest defines `about`, that page opens in a small, non-resizable window that closes with the main one.
- Otherwise htaW shows a **generic window**: icon (the app's or, if it has none, htaW's), name, version and author if present, and at the bottom, small and grey, "Powered by htaW". A custom page has no such footer.
- If it is already open, it is brought to the front instead of opening another.

Nothing is shown on its own: on Windows and Linux the app decides when (usually from its *Help* menu or a button). To make it a standard option, use the `about` role:

```json
{ "text": "&Help", "items": [ { "role": "about", "text": "&About…" } ] }      // in the window menu
{ "type": "button", "role": "about" }                                          // in the title bar: a "?" icon and default text
```

Options or buttons with `role: "about"` open the About window without your page handling any event. On macOS it will be the "About" option of the application menu.
