# Using htaW

## What a `.htaw` application is

A `.htaw` file is a desktop application made with web technologies. It can be:

- a **single HTML file** with the `.htaw` extension, or
- a **package** (a zip with the `.htaw` extension) with several pages, images and scripts.

For you as a user there is no difference: both open the same way. htaW provides the window, the system look (light
or dark theme, accent colour) and controlled access to your computer.

There are also **portable** applications: a single `.exe` that already contains htaW. They are used like any
program: copy them and double-click, nothing to install.

## Opening an application

- **Double-click** the `.htaw`, if the extension is associated with htaW (see "Associating the extension" below).
- **Drag** the file onto `htaw.exe` or onto a shortcut to it.
- From the **htaW panel** (open `htaw.exe` with nothing): *Open .htaw…* button.
- From a console: `htaw path\to\the\app.htaw`.

Each application opens in its own process: if one fails, the others keep working. Each one has its own taskbar
button with its icon.

## The permission window

Applications can only do what they declare (read files, run programs, connect to the Internet…). If you open an
application **downloaded from the Internet**, or a new version of one you already knew **asks for more permissions**,
htaW first shows a window with:

- the name, identifier, version and path of the application;
- where it comes from (downloaded from the Internet or local);
- the list of what it asks for, in plain language.

The default button is **Cancel**: read the list before accepting. If the application can run any program, you will
see a prominent warning: that is the same as giving it your own permissions.

Once you accept, htaW remembers it and does not ask again while the file does not change. Local applications (not
from the Internet) open without asking, but **are still limited** to what they declare.

## The htaW panel

Opening `htaw.exe` without any file shows the panel, with these sections:

| Section | What it is for |
|---|---|
| Open an application | Choose a `.htaw` and open it, optionally with arguments for the app. The panel closes when it opens.  Below it, the **last 10 applications opened** with htaW (also by double click or from the command line; not development-mode ones nor portable executables), with name, icon and path: *Open* and *Remove*; those that no longer exist are marked. |

| Development mode | Open a folder or a file in development mode, and the **Recent** list (Open / Remove). See [Development](08-development-and-distribution.md). |
| Create a .htaw | Pack a development folder into a single `.htaw`. |
| Create a portable executable | Turn an app (folder, `.htaw` or HTML) into an `.exe` that contains htaW. |
| .htaw extension | See whether `.htaw` is associated with this htaW, and associate it or remove the association. |
| Remembered permissions | See which applications you gave permission to and forget them (you will be asked again). |
| About | Version, program path, data folder and command-line help. |

## Associating the `.htaw` extension

To open `.htaw` files with a double click, press **Associate** in the panel (or run `htaw --register`). It applies
only to your user and needs no administrator rights. Do it with `htaw.exe` already in its final place, because the
path is stored. **Remove association** (or `htaw --unregister`) undoes it.

## Your data

htaW stores in `%LOCALAPPDATA%\htaW` (Windows):

- `trust.json`: the applications you gave permission to;
- `apps\<id>\settings.json`: the settings of each application;
- `recent.json`: the last applications opened (name, icon, path and date);
- `recent-dev.json`: the recent development folders;
- `WebView2\`: the data of the built-in browser (web storage of each app, cache).

To forget an app's permissions: panel → *Remembered permissions* → *Forget*, or `htaw --forget <file|id>` (the file can be a `.htaw` or a portable executable).

## Command line

```
htaw <file.htaw> [args...]         open the application (what follows is for the app)
htaw <folder|htaw.json>            open a folder in development mode
htaw --dev <folder|file>           development mode
htaw pack <folder> [-o output]     create a .htaw (zip) and validate the manifest
htaw bundle <folder|file> [-o output] [--base htaw.exe]
                                   create a portable executable with the app inside
htaw --register | --unregister     associate (or not) the .htaw extension
htaw --forget <file|id>            forget the granted permissions
htaw --version | --help
htaw                               open the panel
```

Exit codes: `0` success, `1` error, `2` the user rejected the permissions.

htaW follows the system language (Spanish or English). It can be forced with the environment variable
`HTAW_LANG=en` or `HTAW_LANG=es`.
