# Development and distribution

## Forms of an app

| Form | When | How it opens |
|---|---|---|
| **Standalone HTML** (`app.htaw`) | Small single-file apps. It can use files from its own folder (scripts, images). | Double click, `htaw app.htaw` |
| **Folder** with `htaw.json` | While developing a multi-page app | `htaw folder` (always in development mode) |
| **Package** `.htaw` (zip) | To distribute the folder as a single file | Double click, `htaw app.htaw` |
| **Portable executable** (`.exe`) | For people without htaW: a single program, nothing to install | Double click |

## Development mode

It opens with any of these:

- `htaw --dev <folder|file>`;
- `htaw <folder>` or `htaw <folder>\htaw.json`: a folder always opens in development mode;
- dragging the folder onto `htaw.exe` or onto a shortcut to it;
- from the htaW panel (*Development mode* → *Folder…* / *File…*, or an item under *Recent*).

In development mode:

- **Automatic reload**: saving any file in the folder reloads all the windows (changes within 300 ms are grouped,
  because editors save in several steps). `.git` folders are ignored.
- **F12** opens DevTools (console, inspector, network, debugger with breakpoints) and the engine's context menu is
  enabled.
- Manifest **warnings** appear in the console with the `[htaW]` prefix.
- **It does not ask about permissions, but they are enforced**: a call without permission fails just as in the final
  version.
- `htaw.app.dev` is `true`, in case you want to show debugging information.
- The path is stored under *Recent* in the panel (the last 10).

Typical cycle: the panel open with the app under *Recent*, the app open in development mode and the editor beside it;
every time you save, the window reloads.

### Debugging

- `console.log` and JavaScript errors appear in DevTools.
- Failing API calls reject their promise: an `await` without `try` shows the error in the console as
  "Uncaught (in promise)", with its `code` and message.
- To test with arguments: `htaw --dev folder one two` → `htaw.app.args = ["one", "two"]`. In the panel, the
  *Arguments* field is also used when opening in development mode.
- To test the permission window as the user will see it, pack the app and open the `.htaw` marked as downloaded (for
  example, by actually downloading it).

### Autocompletion

Copy `htaw.d.ts` (in `src/htaW/Assets/`) to your folder and add at the top of your `.js` files:

```js
/// <reference path="htaw.d.ts" />
```

Editors that understand TypeScript (VS Code, for example) will complete `htaw.` with all the members and their types.
`htaw pack` packs it like any other file; if you do not want it included, keep it outside the app folder.

## Packing: `.htaw`

```
htaw pack <folder> [-o output.htaw]
```

- Validates the folder exactly as when opening it (manifest, `entry`, permissions, **icon**). If anything fails, it
  creates nothing and explains why.
- Creates a zip with all the files, excluding `.git`, `.vs`, `.vscode`, `.idea`, `node_modules` and other `.htaw`.
- Default output: `<folder>.htaw` next to the folder.
- Also from the panel: *Create a .htaw* → *Pack folder…*.

The `.htaw` is served straight from the zip, without extracting it. In a zip, `htaw.app.dir` is `null` and relative
`htaw.fs` paths are resolved from the user's folder.

## Portable executable

```
htaw bundle <folder|file.htaw|file.html> [-o output.exe] [--base htaw.exe]
```

Joins htaW and the app into **a single program** that is copied and opened with a double click, nothing to install:

- If the app declares an `icon`, the `.exe` carries it (the file manager shows it).
- All of the `.exe`'s arguments are for the app (`htaw.app.args`); the htaW command line does not exist in it.
- A standalone-HTML app inside a portable serves its relative files from the `.exe`'s folder; a folder or a zip carries
  everything inside.
- Permissions work the same: if the `.exe` is downloaded from the Internet, htaW asks before opening it (on top of
  Windows' own warning for unsigned programs).
- The app's data (settings, web storage) is stored in the user's htaW data folder, by `id`.
- It needs the **published** `htaw.exe` (`publish.cmd` or the build command in the README); `--base` lets you give
  another one, for example one for a different architecture.
- Also from the panel: *Create a portable executable* → *From folder…* / *From file…*.

The portable weighs the same as htaW (about 9 MB) plus the app.

## Checklist before publishing

- Your own, stable `id` (if it changes, the app loses its settings and data, and users see the permission window
  again).
- Updated `version`.
- Minimal `permissions`: remove the ones you no longer use.
- `icon` as a multi-size `.ico`.
- Tested in light and dark mode (with `theme: "native"` it works by itself, except for your own colours).
- `htaw pack` (or `bundle`) without errors.
