# The manifest

The manifest describes the app: identity, window, look and permissions. It is JSON (it accepts `//` comments and
trailing commas) and lives in one of these places:

| App form | Where the manifest goes |
|---|---|
| Standalone HTML (`app.htaw`) | In the first `<script type="application/htaw+json">` inside `<head>` |
| Development folder | In `htaw.json`, at the root of the folder |
| `.htaw` package (zip) | In `htaw.json`, at the root of the zip |

An HTML file without a manifest also opens, with default values (and a warning in the console).

## Complete example

```jsonc
{
  "id": "com.example.app",
  "name": "My App",
  "version": "1.0.0",
  "entry": "index.html",
  "icon": "img/icon.ico",
  "window": {
    "width": 800, "height": 600,
    "minWidth": 400, "minHeight": 300,
    "maxWidth": null, "maxHeight": null,
    "resizable": true,
    "maximized": false,
    "alwaysOnTop": false,
    "background": "light-dark(#f3f3f3, #202020)",
    "fadeIn": 150
  },
  "theme": "native",
  "permissions": ["fs", "dialog", { "shell.exec": ["git"] }]
}
```

## Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `id` | text | derived from the path | Unique identifier. Lowercase letters, digits, `.` and `-` (`com.example.app`). Isolates the app's data: settings, `localStorage`, IndexedDB, cookies, remembered permissions. **Recommended**: without it, one is computed from the file path (`local-…`) and changes if the file moves. |
| `name` | text | file name | Visible name and title of the main window. Without it, the title is the page's `<title>`. |
| `version` | text | — | App version. Shown in the permission window and in `htaw.app.version`. |
| `entry` | path | — | Start page, relative to the root. **Required** in a folder or zip; ignored in a standalone HTML (the page is the file itself). |
| `icon` | path or `data:` | htaW icon | `.png` or `.ico` icon (up to 1 MB). See "The icon" below. |
| `lang` | text | — | App language (informative). |
| `window` | object | | Main window (next table). |
| `theme` | `"native"` \| `"none"` | `"native"` | `native`: htaW injects its CSS with a system look. `none`: the page looks like a normal website. See [Native look](06-style.md). |
| `permissions` | list | `[]` | What the app is allowed to do. See [Permissions](05-permissions.md). |
| `singleInstance` | yes/no | `false` | Reserved: read but has no effect yet. |

### `window`

Sizes are **logical units** (CSS units): on a 150 % display the window is 1.5 times as many physical pixels. They
refer to the **content area**, without the frame or the title bar.

| Field | Default | Description |
|---|---|---|
| `width`, `height` | 800, 600 | Initial size. Between 100×80 and 10000×10000. |
| `minWidth`, `minHeight` | 0 | Minimum size when resizing (0 = no minimum). |
| `maxWidth`, `maxHeight` | `null` | Maximum size (`null` = no maximum). |
| `resizable` | `true` | Whether it can be resized (without it, there is no maximize button either). |
| `maximized` | `false` | Open maximized. |
| `alwaysOnTop` | `false` | Always above the other windows. |
| `background` | `htaw.css` background | Colour the window shows while the page loads: `#rgb`, `#rrggbb` or `light-dark(#light, #dark)`. See "How the window appears". |
| `fadeIn` | 150 | Duration in ms of the content fade when it appears (0 = no fade; maximum 2000). |
| `center` | `true` | The window opens centred. |
| `frame` | `"native"` | Reserved: `"none"` is not supported yet (the system frame is used, with a warning). |

### The icon

- Formats: `.ico` (the size closest to what the system asks for is used) or `.png` (scaled; preferably square and
  256 px or more). To look sharp at 16 px, a multi-size `.ico` is best.
- As a **relative path**: inside the zip, in the development folder or next to the standalone HTML.
- As **`data:`**: `"icon": "data:image/png;base64,iVBORw0…"`. Useful in a standalone HTML so it stays a single file.
- It shows in the title bar and the taskbar of all the app's windows, and on the `.exe` of a portable executable.

### How the window appears

The window appears as soon as the page has loaded or, at the latest, after half a second, already painted with the
background colour; the page comes in with a short fade. To avoid a visible colour change, the initial background
should match the page's. It is chosen in this order:

1. `window.background` from the manifest;
2. the start page's `<meta name="theme-color">` (one per theme is supported with
   `media="(prefers-color-scheme: dark)"`);
3. with `theme: "native"`, the `htaw.css` background (`#f3f3f3` light, `#202020` dark);
4. white or `#202020` depending on the system theme.

If you use `theme: "native"` and do not change the background, there is nothing to set.

## Validation

When the app opens and when it is packed (`pack`, `bundle`), htaW validates the manifest:

- **Errors** (the app does not open and the reason is shown): invalid JSON; `id` with characters not allowed; `theme`
  other than `native`/`none`; sizes out of range or negative minimums; unknown `frame`; `background` in an unsupported
  format; `fadeIn` outside 0–2000; `entry` or `icon` leaving the root (`../`); malformed `data:` `icon`; unknown or
  misspelled permissions; in a folder or zip, missing `entry` or the page does not exist.
- **Warnings** (the app opens; they appear in the DevTools console): unknown fields, HTML without a manifest, `entry`
  in a standalone HTML, `frame: "none"`, and an `icon` that cannot be found or is not a valid `.png`/`.ico` (the htaW
  icon is used).
- When **packing**, an unusable icon is an error.
