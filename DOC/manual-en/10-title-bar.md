# Title bar with custom buttons

A window can carry its own controls in the title bar: buttons, groups, an information panel, an image and a search
field, as in modern system applications. htaW draws them with the system look (they are not HTML) and the app receives
their events in JavaScript.

The definition is **the same on every system** and is deliberately limited to what each system can offer natively
(the most restrictive one is the macOS toolbar): there are no custom colours, borders or styles. What is not on this
page cannot be done.

> Status: implemented on Windows. macOS and Linux will get it with their htaW versions, with the same definition.

## Where it is declared

Each window has its own bar, or none (the normal system bar):

1. **In the page**, inside the `<script type="application/htaw+json">` block of its `<head>`, as `window.titlebar`.
   On the start page that block is the manifest; on other pages only `window.titlebar` is read. This way every page
   opened in a window brings its own bar.
2. **When opening the window**: `htaw.window.open(page, { titlebar: {...} })` uses that bar instead of the page's;
   `titlebar: false` opens the window with the normal bar even if the page declares one.
3. **Live**, from the window itself: `htaw.window.titlebar.set(bar)` changes it and `set(null)` removes it.

In an app with `htaw.json`, the main window's bar can go in `htaw.json` (`window.titlebar`) or in its page's block.

```html
<script type="application/htaw+json">
{
  "window": {
    "titlebar": {
      "mode": "compact",
      "items": [
        { "type": "button", "id": "new", "icon": "add", "text": "New" },
        { "type": "button", "id": "save", "icon": "save", "text": "Save" },
        { "type": "space" },
        { "type": "search", "id": "find", "placeholder": "Search" }
      ]
    }
  }
}
</script>
```

## The bar

| Field | Default | Description |
|---|---|---|
| `mode` | `"compact"` | `"title"`: normal title bar height and up to 3 icon buttons next to the window buttons (like a "feedback" or account button). `"compact"`: toolbar with icons; each button's text shows as a tooltip. `"labels"`: taller bar, with the text under each icon. |
| `title` | `true` | Show the window icon and title on the left (if they fit). |
| `iconFont` | — | The app's own icon font (`.ttf` or `.otf`, relative path or `data:`) for `font:XXXX` icons. Loaded for the app only, without installing it. WOFF/WOFF2 are not supported. |
| `items` | `[]` | The items, in order. |

In `title` mode there are only buttons (3 at most).

## Items

All of them accept `id` (to receive their events and change them with `update`), `tooltip` and `enabled` (`true`).

| `type` | Fields | Description |
|---|---|---|
| `button` | `icon` and `text` **required**; `toggle`, `checked`, `menu`, `split` | Button. With `toggle: true` it is a toggle (shown as pressed with `checked`; the app changes it with `update` when it receives the click). With `menu`, clicking opens the menu. With `split: true` (and `menu`) it is a split button: the body fires the click and the arrow opens the menu. |
| `group` | `items` (buttons only), `text` | Groups buttons; in `labels` mode its buttons show only their icon and `text` appears underneath as the group label. |
| `panel` | `text` **required**; `subtitle`, `icon`, `menu`, `width` (120–480, 240) | Rounded box with one or two lines of information (for example, the current document or project). With an `id` it can be clicked; with a `menu`, it opens it. |
| `search` | `id` **required**; `placeholder`, `text`, `width` (120–480, 240) | Native search field. Always at the end of the bar; one at most. |
| `image` | `icon` (the app's PNG or ICO) **required**; `color` | Decorative image (a logo). With an `id` it can be clicked. |
| `space` | — | Flexible space: shares out the room left over. |

**Centring:** one item (and only one) can have `"center": true`: it sits in the centre of the window, the items before
it on the left and those after it on the right. A centred panel narrows (down to 120) if it does not fit. Search and
spaces are not centred.

If the window is narrow, items that do not fit are not shown.

### Menus

`menu` is a list of options `{ "id": "...", "text": "...", "enabled": true, "checked": false }` and separators
`{ "type": "separator" }`. Choosing an option fires a click with the option's `id`.

### Icons

`icon` can be:

- **A name from the common list**, turned into the native icon on each system: `add`, `remove`, `document`, `open`,
  `folder`, `save`, `print`, `undo`, `redo`, `cut`, `copy`, `paste`, `delete`, `edit`, `search`, `settings`,
  `refresh`, `sync`, `back`, `forward`, `up`, `down`, `home`, `share`, `download`, `upload`, `play`, `pause`, `stop`,
  `filter`, `sort`, `list`, `more`, `menu`, `sidebar`, `info`, `help`, `warning`, `star`, `heart`, `emoji`, `user`,
  `mail`, `link`, `lock`, `unlock`, `bell`, `calendar`, `clock`, `terminal`, `code`, `bold`, `italic`, `underline`,
  `zoomIn`, `zoomOut`, `fullscreen`, `pin`, `tag`, `flag`, `cloud`, `globe`, `image`, `chat`, `check`, `close`,
  `light`, `dark`.
- **A PNG or ICO image of the app** (relative path or `data:`). It is treated as **monochrome**: its transparency is
  used as a silhouette, painted in the text colour, so it adapts to the light or dark theme and to the state (disabled,
  inactive window). With `"color": true` it keeps its colours. Best square and 32 px or larger.
- **`font:XXXX`**: the character with hexadecimal code `XXXX` from the bar's `iconFont`.

## The API: `htaw.window.titlebar`

| Member | Description |
|---|---|
| `set(bar \| null)` | Puts another bar on this window, or removes it (`null`). |
| `update(id, changes)` | Changes an item: `text`, `subtitle`, `icon`, `tooltip`, `enabled`, `checked`, `menu`. |
| `focus(id)` | Focuses the search field and selects its text. |
| `on(type, fn)` | Listens to the events of this window's bar. Returns a function to stop listening. |

Events (`fn(id, e)`):

- `"click"`: a button, panel or image was clicked, or a menu option chosen. `e.button` is the `id` of the item whose
  menu was opened (`null` otherwise).
- `"input"`: the search text changed (quick keystrokes are grouped). `e.value` is the text.
- `"submit"`: Enter in the search field. `e.value` is the text.

In the search field, Escape clears the text (fires `input` with `""`); if it was already empty, focus goes back to the
page.

```js
const tb = htaw.window.titlebar;
let bold = false;
tb.on("click", (id, e) => {
  if (id === "bold") tb.update("bold", { checked: bold = !bold });
  if (id === "save") save();
});
tb.on("input", (id, e) => filter(e.value));
document.addEventListener("keydown", (ev) => {
  if (ev.ctrlKey && ev.key === "f") { ev.preventDefault(); tb.focus("find"); }
});
```

Opening another window with a different bar, or without a custom bar:

```js
htaw.window.open("help.html", { titlebar: { mode: "title", items: [
  { type: "button", id: "feedback", icon: "emoji", text: "Send feedback" } ] } });
htaw.window.open("about.html", { titlebar: false });
```

**Window icon**: with a custom bar, a click on the icon shows the system menu and a **double click closes the window**, as Windows has always done.

## Errors

A badly defined bar (a missing required field, an unknown type or icon, two centred items…) in `htaw.json` or in an
HTML file's manifest prevents the app from opening, with a message saying what is wrong. In `set` and in
`htaw.window.open` the promise is rejected with that message. In the block of a page other than the start page, a
badly defined bar is ignored and the window uses the normal bar.

Full example: `samples/htaw/barra` (`htaw samples/htaw/barra` opens it in development mode).

## Window menu

A window menu (File, Edit, View, Help…) that htaW places according to the system: on Windows, **inside the title bar**
(own rendering, light or dark following the theme); on macOS, in the global menu bar; on Linux, to be defined. The app
only declares the menu, not where it is shown. **No permission.**

```json
{ "window": { "menu": [
  { "text": "&File", "items": [
    { "id": "open", "text": "&Open…", "shortcut": "Ctrl+O" },
    { "type": "separator" },
    { "id": "quit", "text": "&Quit" }
  ] },
  { "text": "&Edit", "items": [ { "id": "copy", "text": "&Copy", "shortcut": "Ctrl+C" } ] }
] } }
```

```js
htaw.window.menu.on("click", (id) => { if (id === "quit") htaw.app.quit(); });
await htaw.window.menu.set(null);     // remove it; menu.set([...]) changes it on the fly
```

- Each top-level entry is a menu with `text` and `items`. The options are those of [`htaw.menu`](04-api.md#htawmenu--context-menus)
  (`id`, `text`, `enabled`, `checked`, `shortcut`, submenus up to 4 levels and separators). `&` marks the access key.
- It can be declared in the manifest (`window.menu`), in each page's `htaw+json` block, in `htaw.window.open(page, { menu })`
  or on the fly with `htaw.window.menu.set`. An invalid menu fails with `InvalidArgument` (or a manifest error).
- **Where it goes on Windows:** without a custom bar, with `title` and with `compact`, in a thin row below the bar (the bar's
  icons stay where they always are); with `labels`, in the top row: menus on the left and icon + title centered, with the
  labeled icons below.
- **Keyboard and mouse**: a lone Alt or F10 activates the bar (arrows, Enter, Esc); Alt+letter opens the menu with that access key
  (the `&` in the text: `"&File"` -> Alt+F, underlined when Alt is pressed). With a menu open, hovering another one or using the
  left and right arrows switches menus. If the menus do not fit in a narrow window, a "···" button appears that opens a menu with
  the leftovers. Keys work with focus in the page (not when focus is in a search field of the bar). `shortcut` is display only: your page handles the keys.
