# Plataformas

Detalle de cada backend. Todos cumplen el mismo contrato interno (ver ARQUITECTURA.md).

## Resumen

| SO | Ventana | Webview | Disponibilidad del webview |
|---|---|---|---|
| Windows | `user32` (Win32) | WebView2 (Chromium), COM | Incluido en Windows 11; en Windows 10 normalmente presente, se comprueba |
| Linux | GTK3 | WebKitGTK 4.1 | Dependencia de la distribución (`libwebkit2gtk-4.1`) |
| macOS | Cocoa (AppKit) | WKWebView (WebKit) | Siempre presente |

La web se pinta con dos motores distintos (Chromium y WebKit), así que hay que probar en los dos.

## Windows

### Ventana
- `RegisterClassExW` + `CreateWindowExW`, con un `WndProc` `[UnmanagedCallersOnly]`.
- Bucle `GetMessageW` / `TranslateMessage` / `DispatchMessageW`.
- Icono desde los recursos del ejecutable o desde un archivo (`.ico` / `.png` convertido).

### WebView2 con Native AOT
- El interop del paquete oficial usa `ComImport`, que no es compatible con AOT. Las interfaces
  necesarias se declaran a mano con `[GeneratedComInterface]` (`ComWrappers` generado):
  `ICoreWebView2Environment`, `ICoreWebView2Controller`, `ICoreWebView2`, `ICoreWebView2Settings`,
  `ICoreWebView2WebResourceRequestedEventArgs`, etc., y los handlers de cada evento como
  `[GeneratedComClass]`. Solo se declaran las que se usan.
- Creación del entorno: `CreateCoreWebView2EnvironmentWithOptions` desde `WebView2LoaderStatic.lib`,
  **enlazado estáticamente** (`<NativeLibrary>` + `<DirectPInvoke>`), así no hace falta distribuir
  `WebView2Loader.dll`. Lo aporta `WolfGlue.targets` (ver CONSUMO.md).
- Si no hay runtime de WebView2, se muestra un mensaje nativo (`MessageBoxW`) con el enlace al
  instalador y la app termina con un código de error.
- Carpeta de datos del webview: `%LOCALAPPDATA%\<idApp>\WebView2`, nunca junto al ejecutable.
- Aparición: la ventana se crea oculta y `ShowWindow` se llama en el primer `NavigationCompleted` (con éxito o
  error) o al saltar un temporizador de `ShowDelayMs` (`SetTimer` + `WM_TIMER`). La ventana pinta su color de
  fondo en `WM_ERASEBKGND`. El controlador de WebView2 vive en una ventana hija (`STATIC`) oculta hasta el primer
  `NavigationCompleted`: hasta tener el primer fotograma, WebView2 pinta su propio gris (`#121212` en oscuro) y
  ni `DefaultBackgroundColor` ni la variable `WEBVIEW2_DEFAULT_BACKGROUND_COLOR` lo evitan (comprobado). Al
  mostrarla se llama por `ExecuteScript` a `__glueFadeIn()`, que arranca el fundido del `body` (Web Animations;
  arranca solo a los 3 s si nadie lo llama). Se funde el `body` y no la raíz porque bajo una raíz transparente el
  lienzo se pinta con el color de `color-scheme` del motor, no con el de la ventana.
- `ICoreWebView2Settings8.IsReputationCheckingRequired = false` (salvo `UrlReputationCheck`): con SmartScreen
  activo, medido, pasan ~2 s entre servir el HTML y que el renderer pida el primer recurso.
  Pendiente opcional: un webview precalentado para que las ventanas nuevas aparezcan casi al instante
  (cuesta ~25 MB mientras espera).

### DPI
- Manifiesto con `PerMonitorV2`.
- En `WM_DPICHANGED` se aplica el rectángulo sugerido (`SetWindowPos`). El controlador de WebView2
  ajusta su escala solo (`ShouldDetectMonitorScaleChanges` activo por defecto).
- El tamaño inicial de la ventana se pide en unidades lógicas y se convierte con
  `GetDpiForWindow` / `GetDpiForMonitor`.

### Tema
- Barra de título oscura: `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)`, que se vuelve a
  aplicar cuando llega `WM_SETTINGCHANGE` con `ImmersiveColorSet`.
- Fondo Mica opcional: webview con `DefaultBackgroundColor` transparente +
  `DWMWA_SYSTEMBACKDROP_TYPE`. Solo en Windows 11; en versiones anteriores se ignora.
- Color de acento: valor de DWM / ajustes de personalización del usuario.

### Diálogos
- `IFileOpenDialog` / `IFileSaveDialog` (COM, también con `[GeneratedComInterface]`).
- Mensajes: `TaskDialogIndirect` (necesita Common Controls v6 en el manifiesto) o `MessageBoxW`.

## Linux

### Bibliotecas
- `libgtk-3.so.0`, `libwebkit2gtk-4.1.so.0`, `libgobject-2.0.so.0`, `libglib-2.0.so.0`, cargadas con
  `NativeLibrary.TryLoad`. Si faltan, se muestra en consola qué paquete instalar (apt, dnf, pacman).
- Como la carga es dinámica, para **compilar** no hacen falta las cabeceras de desarrollo.
- Familias de WebKitGTK: 4.0 (libsoup2, en retirada), **4.1** (GTK3 + libsoup3, la elegida) y
  6.0 (GTK4). Pasar a GTK4/6.0 queda como opción futura.

### Ventana y señales
- `gtk_init`, `gtk_window_new`, `webkit_web_view_new_with_context` con un `WebKitWebContext` compartido.
- Señales con `g_signal_connect_data` y punteros a función `[UnmanagedCallersOnly]`.
- Bucle: `gtk_main` / `gtk_main_quit`.

### Host propio y mensajes
- `webkit_web_context_register_uri_scheme` para el contenido. Para leer el cuerpo de las peticiones
  POST hace falta WebKitGTK ≥ 2.40 (`webkit_uri_scheme_request_get_http_body`).
- Mensajes: `webkit_user_content_manager_register_script_message_handler`. Respuestas con
  `webkit_web_view_evaluate_javascript`.

### DPI y tema
- La escala la gestionan GTK y WebKit (factor de escala del monitor).
- Modo oscuro: WebKitGTK no lo propaga solo a `prefers-color-scheme`. Se lee la preferencia del
  portal `org.freedesktop.appearance` (`color-scheme`, `accent-color`) por D-Bus y se aplica
  (`gtk-application-prefer-dark-theme`). Sin portal: tema claro y acento por defecto.

### Problemas conocidos
- Con Wayland y algunos drivers (sobre todo NVIDIA) el webview puede quedar en blanco. Se arregla
  con `WEBKIT_DISABLE_DMABUF_RENDERER=1`. Se puede fijar desde la app al detectar ese caso o desde
  una opción.

### Diálogos
- `GtkFileChooserNative` (usa el portal de archivos si existe) y `GtkMessageDialog`.

## macOS

### Runtime de Objective-C
- Todo pasa por `libobjc.dylib`: `objc_getClass`, `sel_registerName` y `objc_msgSend`, declarado
  varias veces con `EntryPoint = "objc_msgSend"`, una por firma (`IntPtr`, `bool`, `double`, `CGRect`…).
- En x64, los métodos que devuelven estructuras grandes usan `objc_msgSend_stret`; en arm64 no existe.
- Las clases delegadas (`NSApplicationDelegate`, `NSWindowDelegate`, `WKScriptMessageHandler`,
  `WKURLSchemeHandler`, `WKNavigationDelegate`, `WKUIDelegate`) se crean en tiempo de ejecución con
  `objc_allocateClassPair` + `class_addMethod` + `objc_registerClassPair`, con métodos `[UnmanagedCallersOnly]`.
- Gestión de memoria: `NSAutoreleasePool` alrededor de cada callback; `retain`/`release` explícitos.

### Ventana y webview
- `NSApplication`, `NSWindow`, `WKWebView` con un `WKWebViewConfiguration` común
  (`WKProcessPool` y `WKWebsiteDataStore` compartidos).
- El bucle corre en el hilo principal (`[NSApp run]`), obligatorio en macOS.

### Archivos abiertos desde el Finder
- **No llegan por `argv`**, llegan por `application:openURLs:` del delegado de la aplicación.
  WolfGlue los expone con el evento `GlueApp.FilesOpened`.
- Para recibir archivos hace falta un **bundle `.app`** con `Info.plist` (`CFBundleDocumentTypes`).
  Un ejecutable suelto no basta (ver HTAW-INTEGRACION.md).

### DPI, tema y diálogos
- Retina es automático.
- Tema: `effectiveAppearance`, `NSColor.controlAccentColor`, notificación de cambio de apariencia.
- Diálogos: `NSOpenPanel`, `NSSavePanel` (como hoja sobre la ventana dueña) y `NSAlert`.

## Diferencias que la API debe ocultar

| Asunto | Windows | Linux | macOS |
|---|---|---|---|
| Unidades de tamaño | píxeles (convertir con DPI) | lógicas | puntos (lógicas) |
| Cierre de la última ventana | proceso sigue si no se sale | igual | por convención la app sigue viva; WolfGlue aplica `QuitMode` igual en todos |
| Archivos al abrir | `argv` | `argv` | evento del delegado |
| Orden de botones en diálogos | Aceptar / Cancelar | Cancelar / Aceptar | Cancelar / Aceptar |
