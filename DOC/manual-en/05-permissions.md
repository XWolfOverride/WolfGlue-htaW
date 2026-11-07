# Permissions and security

An htaW app runs with the user's permissions, so htaW limits what it can do: **only what it declares in
`permissions`**. Ask only for what you really use: it is what the user will see before opening your app if they
download it.

## Permission list

| Permission | Gives access to | How the user sees it |
|---|---|---|
| `fs` | `htaw.fs` (read, write, list, delete, watch files) | Read and write files |
| `dialog` | `htaw.dialog` (open, save, folder, messages) | Show system dialogs |
| `clipboard` | `htaw.clipboard` | Read and write the clipboard (text and images) |
| `http` | `htaw.http.fetch` (without CORS) | Connect to the Internet without restrictions |
| `env` | `htaw.env` (environment variables and user folders) | Read environment variables and user folders |
| `shell.open` | `htaw.shell.open` and `htaw.shell.reveal` | Open files and addresses with other applications |
| `links.auto` | Links and `window.open` to other websites open without a user click (for example, from a timer) | Open web pages in the browser without you asking |
| `shell.exec` | `htaw.shell.exec` and `htaw.shell.spawn`, with a list of programs | Run these programs: … |

Always available, without permission: `htaw.app`, `htaw.window`, `htaw.settings`, `htaw.theme`, `htaw.permissions`,
the standard `fetch` (with CORS), `localStorage`/IndexedDB and messages between windows. They only affect the app
itself.

```json
"permissions": ["fs", "dialog", "clipboard", { "shell.exec": ["git", "ffmpeg"] }]
```

An unknown or misspelled permission is an **error**: the app does not open and the reason is explained.

## `shell.exec`: the program list

```jsonc
{ "shell.exec": ["git", "ffmpeg"] }        // only these programs
{ "shell.exec": ["C:\\Tools\\conv.exe"] }  // a specific path
{ "shell.exec": "*.*" }                    // any program (also "")
```

How each `exec`/`spawn` call is checked:

- **Name without a path** (`"git"`): looked up in the `PATH` (on Windows, with or without `.exe`) and allowed if the
  name is in the list. Only the `PATH` is searched, not the app folder.
- **With a path** (`"C:\\Tools\\conv.exe"`, `"./tool"`): only if that exact path is in the list (or with `*.*`).
  Having `"git"` does not allow running a different `git.exe` from another folder.
- **No intermediate shell**: arguments reach the program as they are, with nothing interpreting them. An argument
  containing `& del *` is just text.
- **Interpreters = everything.** Allowing `cmd`, `powershell`, `pwsh`, `bash`, `sh`, `python`, `node`, `perl`,
  `ruby`, `wscript`, `cscript`, `mshta`, `osascript`, `env`, `sudo`… is the same as allowing any program (they can run
  anything), and the user sees it with the same warning as `*.*`.
- **`.bat`/`.cmd` scripts** (Windows): only with `*.*`, because `cmd` reinterprets the arguments.

Recommendation: a closed list with the specific programs. If you need a script, prefer a program of your own that the
app calls directly.

## `shell.open` does not run programs

`htaw.shell.open` opens files with their application (a PDF with the viewer, a folder with the file manager) and
URLs in the browser. But opening an executable or a script (`.exe`, `.bat`, `.cmd`, `.ps1`, `.vbs`, `.js`, `.msi`,
`.lnk`…) means running it, so it is rejected unless the app has `shell.exec: "*.*"`.

## What the user sees

- If the app file comes **from the Internet** (Windows marks it when downloading), htaW asks before opening it,
  showing the permission list. The default button is Cancel.
- If a new version of an already accepted app (same `id`) **asks for more permissions**, it asks again.
- Local apps open without asking, but with the same limits.
- In **development mode** it never asks, but permissions **are still enforced**: what works in development will work
  the same in the final version.

That is why the `id` should stay stable across versions and permissions should not grow without need.

## The app's pages

- Only pages of the app itself are loaded (its origin `https://<id>.htaw.local/`). Links and `window.open` to other
  websites open in the system browser (only `http`, `https` and `mailto`); anything else is ignored. No permission is
  needed, but **only when the user starts it** (a click or a key press): if the page navigates to another website on
  its own (`location.href = …` in a timer, a simulated `click()`), nothing opens. That way an app cannot open tabs
  the user did not ask for. An app that really needs it declares the `links.auto` permission; and `htaw.shell.open`
  (with its permission) always opens URLs.
- DevTools exist only in development mode.
- Each app has its own origin: other apps cannot see its `localStorage`, IndexedDB or cookies.
- The `/_htaw/` path is reserved for htaW: do not put your own files in a folder named `_htaw`.
