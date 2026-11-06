# htaW en Android, iOS y portables para otros sistemas (evaluación)

Evaluación, sin implementar. Dos preguntas:

1. ¿Se puede llevar htaW a Android e iOS, al menos para **ejecutar** apps (sin modo desarrollo ni integraciones)?
2. Con los binarios de htaW de otros sistemas a mano, ¿se pueden generar desde cualquier sistema los **portables**
   de los demás (Linux desde Windows, APK de Android…)?

## Qué cambia en un móvil

| Asunto | Escritorio | Móvil |
|---|---|---|
| Ventanas | Varias, con tamaño, modales | Una pantalla completa por app; las ventanas pasan a ser pantallas apiladas y las modales, hojas o diálogos |
| Archivos | Rutas del sistema, acceso a todo lo del usuario | Almacenamiento propio de la app; lo demás solo con el selector del sistema, que da **URIs** (`content://` en Android, URL con permiso temporal en iOS), no rutas |
| Procesos (`shell.exec`) | Cualquier programa permitido | **No existe**: no hay programas externos que lanzar |
| `shell.open` | App por defecto | Intents (Android) / `UIApplication.open` y hoja de compartir (iOS) |
| Cómo llega el `.htaw` | Doble clic, asociación | «Abrir con» / «Compartir» desde el gestor de archivos, descargas o correo |
| Origen externo (MOTW) | Marca del sistema | No hay marca: todo lo que llega de fuera se trata como externo (se pregunta la primera vez) |
| Webview | WebView2 / WebKitGTK / WKWebView | Android System WebView (Chromium) / WKWebView |

Consecuencia para la API: `htaw.app`, `window` (adaptada), `dialog` (mensajes y selectores), `settings`, `http`,
`clipboard`, `theme`, `permissions` y `shell.open` se pueden ofrecer. `fs` necesita un modelo nuevo y `shell.exec` no
existe. Propuesta: `htaw.app.platform` / `htaw.has()` para que la app sepa qué hay, `MethodNotFound` en lo que no
existe, y en `fs` dos espacios: la carpeta propia de la app (rutas normales) y los archivos elegidos por el usuario
(identificadores opacos que `readText`/`writeText` aceptan como si fueran rutas). Los manifiestos podrían declarar
`"platforms"` para avisar al abrir una app que necesita `shell.exec` en un móvil.

## Android

**Tecnología.** .NET para Android (`net10.0-android`). La vía estable es Mono con compilación AOT; Native AOT en Android
aún no es la vía principal, así que este backend sería la excepción a la regla de Native AOT (hay que confirmarlo
al empezar). WolfGlue ganaría un backend `Platform/Android`:

- Webview: `android.webkit.WebView`. Contenido del origen propio con `shouldInterceptRequest` (igual que el host
  propio de Windows). Mensajes: `addWebMessageListener` de AndroidX WebKit (canal tipo `postMessage`, seguro por
  origen) y `evaluateJavascript` para responder.
- Una `Activity` por ventana o, más simple, una sola con una pila de vistas; las modales como diálogo a pantalla
  completa.
- Tema: modo oscuro del sistema y, desde Android 12, el color de acento dinámico (Material You). `htaw.css` tendría
  una variante `data-os="android"`.
- Recepción de apps: filtros de intent para `ACTION_VIEW`/`ACTION_SEND` con el tipo MIME del `.htaw` y, como Android
  no asocia bien por extensión, también `application/octet-stream`/`application/zip` comprobando el contenido. El
  archivo se copia a la caché de la app (el zip necesita poder buscar dentro).
- Lista de apps abiertas antes (el equivalente del panel): pantalla inicial con las recientes.

**Distribución del htaW de Android.** APK instalable directamente («orígenes desconocidos») o F-Droid. Google Play
prohíbe descargar código ejecutable de fuera de la tienda, pero exceptúa expresamente el código que corre en un
intérprete o máquina virtual con acceso indirecto a las API de Android, como JavaScript en un webview; una app como
htaW entra en esa excepción, aunque habría que revisar la política vigente al publicar.

**Coste:** alto (un backend completo y el modelo de archivos), pero sin bloqueos técnicos.

## iOS

**Tecnología.** .NET para iOS (`net10.0-ios`), que admite Native AOT. El webview es WKWebView, **el mismo que en
macOS**: el backend de macOS (esquema propio con `WKURLSchemeHandler`, mensajes con `WKScriptMessageHandler`) se
reutilizaría casi entero. Hacerlo después de macOS.

- Recepción de apps: tipo de documento exportado para `.htaw` (UTI), «Abrir en htaW» desde Archivos o la hoja de
  compartir. Las apps se guardan en el almacenamiento de htaW.
- Archivos de usuario: `UIDocumentPickerViewController` con URLs de acceso temporal.

**Bloqueos (no técnicos):**

- **Compilar exige macOS con Xcode**, y **firmar** exige una cuenta de desarrollador de Apple (con la gratuita, las
  apps instaladas caducan a los 7 días).
- **App Store**: las directrices prohíben descargar código que cambie la funcionalidad de la app, con excepciones
  para mini apps HTML5 con condiciones (directriz 4.7 en su versión actual). Un lanzador de apps arbitrarias es una
  zona gris: habría que revisarlo con las directrices del momento. Fuera de la tienda: TestFlight, instalación de
  desarrollo o tiendas alternativas en la UE.

**Coste:** medio después de macOS; el riesgo está en la distribución.

## Portables para otros sistemas desde cualquiera

Native AOT no compila de un sistema operativo a otro, pero `bundle` **no compila**: copia un htaW ya compilado y le
añade la app. Así que, con los binarios de htaW de cada sistema a mano, se pueden generar todos los portables desde
cualquiera.

**Dónde están los binarios («runtimes»).** Propuesta: carpeta `runtimes/` en los datos de htaW (y junto a `htaw.exe`),
con un archivo por destino; `htaw bundle --target linux-x64 …` lo busca ahí (`--base` sigue sirviendo para una ruta
concreta). htaW reconoce el sistema y la arquitectura de un binario por su cabecera (PE: campo *Machine*; ELF:
`e_machine`; Mach-O: `cputype`, también universales). En el panel, una lista de runtimes disponibles, «Añadir
runtime…» y un selector de destino al crear el portable. Los binarios saldrían de compilar en cada sistema o, si el
proyecto llega a publicarlos, de sus *Releases*.

| Destino | Qué se genera | Desde otro sistema |
|---|---|---|
| Windows (`.exe`) | Base + app al final, icono en los recursos | Hoy el icono usa `UpdateResourceW` (solo Windows). Para hacerlo desde Linux/macOS: reescribir la sección `.rsrc` en C# puro (medio). |
| Linux | Base ELF + app al final, en un `.tar.gz` con el bit de ejecución, más un `.desktop` y el icono en PNG (y la opción `--install` del portable para registrarlos, ver HTAW-INTEGRACION.md) | Sí, sin limitaciones: el `.tar.gz` guarda los permisos aunque se cree en NTFS. AppImage, más adelante (necesita escribir SquashFS). |
| macOS (`.app`) | Bundle: `Contents/MacOS/htaw` (sin tocar), `Info.plist` (nombre, id, tipos de documento), `Resources/icon.icns` (PNG dentro; se genera sin herramientas de Apple) y `Resources/app.htaw`, en un `.zip` con los permisos Unix | Sí. El binario conserva su firma ad hoc porque no se modifica (la app va en `Resources`, no añadida al binario). Gatekeeper avisará al abrirlo si viene de Internet sin firma de desarrollador. |
| Android (`.apk`) | APK base de htaW + `assets/app.htaw`, nombre e icono propios, **id de paquete propio**, alineado y firmado | Sí, todo en C#: cambiar el id y el nombre exige editar el `AndroidManifest.xml` binario y `resources.arsc` (medio); los iconos son PNG en rutas fijas; `zipalign` es trivial; la firma (APK Signature Scheme v2/v3) se puede hacer con `System.Security.Cryptography`. Hace falta una **clave de firma estable** por usuario (si cambia, las actualizaciones no se instalan encima): htaW la generaría la primera vez y la guardaría en sus datos, con la opción de usar un almacén propio. |
| iOS (`.ipa`) | `Payload/htaw.app` con la app, `Info.plist` e iconos, firmado | No en la práctica: la firma exige certificados y perfiles de Apple, y el binario base se compila en macOS. Herramientas externas pueden firmar fuera de macOS, pero sería una dependencia ajena. Descartado salvo necesidad. |

## Recomendación y orden

1. **Portables de escritorio cruzados** (`--target` y carpeta `runtimes/`): Linux y macOS desde Windows son baratos; el
   icono de Windows desde otros sistemas, medio. Tiene sentido hacerlo junto con los backends de Linux y macOS, que
   son los que darán esos binarios.
2. **Android**: backend de ejecución y, después, generación de APK por app desde cualquier sistema. Es el móvil con
   más valor y menos restricciones.
3. **iOS**: después de macOS, solo ejecución y con distribución de desarrollo; la generación de `.ipa` por app queda
   descartada mientras dependa de la firma de Apple.
