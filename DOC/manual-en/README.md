# htaW manual

htaW runs **desktop applications made with HTML, CSS and JavaScript**: `.htaw` files that open with a double click like
any program, with their own window, a native look and controlled access to the system (files, dialogs, processes,
the Internet…).

*Versión en español: [DOC/manual](../manual/README.md).*

This manual has two parts:

**Using htaW**

1. [Using htaW](01-usage.md): opening applications, the permission window, the htaW panel and the command line.

**Building htaW applications**

2. [Your first application](02-first-app.md): from an empty HTML file to an app with permissions, an icon, packaged.
3. [The manifest](03-manifest.md): every field of `htaw.json` / of the HTML block.
4. [The `htaw` API](04-api.md): full reference of what the app can use from JavaScript, with examples.
5. [Permissions and security](05-permissions.md): what each permission grants, how it is checked and what the user sees.
6. [Native look (CSS)](06-style.md): the htaW stylesheet, its variables and its classes.
7. [Windows and messaging](07-windows.md): several windows, modal dialogs with a result and messages between windows.
8. [Development and distribution](08-development-and-distribution.md): development mode, debugging, packaged `.htaw`
   and portable executable.
9. [Recipes](09-recipes.md): complete examples ready to copy.

The examples in `samples/htaw` (`hola.htaw`, `markdown.htaw` and the `notas` folder) are real apps that use what is
explained here.

> **Maintenance.** This manual describes everything htaW offers to users and to applications. Whenever htaW gains or
> changes a capability (a manifest field, an API function, a permission, a CSS class, a command-line or panel option),
> the matching page must be updated **in the same change**, in this manual and in the Spanish one. What is not here
> does not exist for app developers.
