# Native look (CSS)

With `"theme": "native"` (the usual case), htaW inserts its stylesheet (`/_htaw/htaw.css`) and the variables of the
real system theme at the start of the `<head>` of every page. The goal: **an HTML page with no classes and no CSS of
its own already looks like a system window**, light or dark and with the user's accent colour.

Your CSS comes after it, so **you can override anything**. With `"theme": "none"` htaW inserts nothing and the page
looks like a normal website (the `htaw` API is unchanged).

## Standard elements

Already styled, with no classes: `body` (system font and colours, no extra margins), `h1`–`h3`, `p`, `a`, `hr`,
`small`, `code`, `pre`, `kbd`, `button`, `input` (text, number, search, date, checkboxes, radio, range…), `select`,
`textarea`, `fieldset`/`legend`, `progress`, `meter`, `table` and the scrollbars.

- Text **cannot be selected** except in fields, `pre`, `code` and elements with the `.selectable` class, as in a
  desktop application.
- **Main button** (with the accent colour): `<button type="submit">`, `.default` or `.primary`.
- Disabled controls (`disabled`) look dimmed.

## Classes for desktop patterns

| Class | Use |
|---|---|
| `.layout-fill` | Full-window column layout: fixed children at the top/bottom and a `<main>` (or `.content`) that takes the rest with its own scroll |
| `.toolbar` | Toolbar (flat buttons); a `.spacer` inside pushes what follows to the right |
| `.statusbar` | Bottom status bar, small text |
| `.sidebar` | Side navigation panel; `.selected` marks the active item |
| `.tabs` + `.tab` | Tabs; the active one with `.selected` or `aria-selected="true"` |
| `.listview` | Selectable list (`ul` or `div` with children); `.selected` or `aria-selected="true"` |
| `.card` | Card: surface with a border and rounded corners |
| `.dialog-buttons` | Row of dialog buttons in the **system order**: write the main one first and htaW places it (OK/Cancel on Windows; Cancel/OK on macOS and Linux) |
| `.row`, `.column` | Row (wrapping) or column with an 8 px gap |
| `.muted` | Secondary text |
| `.selectable` | Allows selecting the text |

```html
<body>
  <div class="layout-fill">
    <div class="toolbar">
      <button id="open">Open…</button>
      <button id="save">Save</button>
      <span class="spacer"></span>
      <button id="settings">Settings</button>
    </div>
    <main>
      <div class="tabs">
        <button class="tab selected">General</button>
        <button class="tab">Advanced</button>
      </div>
      <div class="card">
        <label><input type="checkbox" checked> Save on exit</label>
      </div>
    </main>
    <div class="statusbar"><span id="status">Ready</span><span>UTF-8</span></div>
  </div>
</body>
```

## Variables

All of them can be used in your CSS (`color: var(--htaw-accent)`) or overridden in `:root`.

| Variable | Contents |
|---|---|
| `--htaw-accent` | System accent colour |
| `--htaw-accent-text` | Text readable on the accent (white or black) |
| `--htaw-font`, `--htaw-font-size` | System UI font and its size |
| `--htaw-bg` | Window background |
| `--htaw-surface` | Content surfaces (cards, lists, `pre`) |
| `--htaw-fg`, `--htaw-fg-muted` | Normal and secondary text |
| `--htaw-border`, `--htaw-border-strong` | Borders |
| `--htaw-control-bg`, `--htaw-control-hover`, `--htaw-control-active` | Control background at rest, on hover and when pressed |
| `--htaw-selection` | Selection background |
| `--htaw-link` | Links |
| `--htaw-radius`, `--htaw-radius-large` | Corner radii (controls and cards) |

Colours switch by themselves between light and dark (`prefers-color-scheme`), and the accent and font update if the
user changes them while the app is open.

```css
/* A style of your own that follows the theme */
.tag {
  background: color-mix(in srgb, var(--htaw-accent) 15%, transparent);
  border: 1px solid var(--htaw-accent);
  border-radius: var(--htaw-radius);
  color: var(--htaw-fg);
}
/* Change the background of the whole app */
:root { --htaw-bg: #fdf6e3; }
@media (prefers-color-scheme: dark) { :root { --htaw-bg: #002b36; } }
```

If you change the background, set the same colour in the manifest's `window.background` (or in a
`<meta name="theme-color">`) so the window appears with it already (see [The manifest](03-manifest.md)).

## Per system

`<html>` has `data-os="windows"`, `"macos"` or `"linux"`, in case you need to adjust something:

```css
html[data-os="macos"] .toolbar { padding-left: 80px; }
```

The current look is Windows 11's; macOS and Linux are provisional until those versions exist.
