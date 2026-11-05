# Usar WolfGlue desde otro proyecto

WolfGlue se consume **solo como `ProjectReference`**. No hay paquete NuGet.

## Incorporarlo al repositorio

Se recomienda un submódulo git, porque fija la versión exacta de WolfGlue en cada commit del proyecto:

```
MiApp/
├─ lib/WolfGlue/          ← submódulo
├─ src/MiApp/MiApp.csproj
└─ ...
```

## Referencia e importación

En el `.csproj` de la aplicación (el que se publica):

```xml
<ItemGroup>
  <ProjectReference Include="..\..\lib\WolfGlue\src\WolfGlue\WolfGlue.csproj" />
</ItemGroup>
<Import Project="..\..\lib\WolfGlue\src\WolfGlue\WolfGlue.targets" />
```

### Por qué hace falta el `Import`
Con NuGet, `build/*.targets` se importa solo, pero con `ProjectReference` no. Además, las entradas de
Native AOT que enlazan bibliotecas estáticas (`<NativeLibrary>`, `<DirectPInvoke>`) tienen que estar en
el **proyecto que se publica**, no en la biblioteca. `WolfGlue.targets` se encarga de:

- Windows con AOT (`win-*`): enlazar `WebView2LoaderStatic.lib` y declarar `DirectPInvoke` para
  `WebView2Loader` (un solo `.exe`, sin DLL al lado).
- Windows con JIT (`dotnet run`): copiar `WebView2Loader.dll` a la carpeta de salida (no a la de publicación).
- Manifiesto de aplicación por defecto (`app.manifest`: PerMonitorV2, Common Controls v6), si el proyecto no tiene uno.
- La lib y la DLL salen del paquete `Microsoft.Web.WebView2`, que WolfGlue solo **descarga**
  (`PackageDownload`, versión en `WolfGlue.props`); no se referencia ningún ensamblado suyo.
- *(Previsto)* macOS (`osx-*`): montaje del bundle `.app`.

Propiedades recomendadas en la aplicación: `OutputType=WinExe`, `PublishAot=true` y, si no hace falta
cultura, `InvariantGlobalization=true`. El proyecto de ejemplo `samples/Hola/Hola.csproj` sirve de plantilla.

## Scripts de autocompilación del proyecto consumidor

Las plantillas `build.ps1` / `build.sh` habituales necesitan dos cambios para traer el submódulo:

- Al clonar: `git clone --depth 1 --recurse-submodules --shallow-submodules <url>`.
- Al actualizar: `git pull --ff-only` seguido de `git submodule update --init --recursive`.
- Comprobación previa: si existe `.gitmodules` y `lib/WolfGlue` está vacío, ejecutar
  `git submodule update --init` antes de compilar.

## Requisitos para ejecutar

Se añaden a la sección de requisitos del README del proyecto consumidor:
- Windows: runtime de WebView2 (incluido en Windows 11).
- Linux: `libwebkit2gtk-4.1` y GTK3.
- macOS: ninguno.

## Ejemplo mínimo

```csharp
using WolfGlue;
using WolfGlue.Content;

internal static class Program
{
    [STAThread] // obligatorio: WebView2 necesita un hilo STA
    private static int Main()
    {
        var app = new GlueApp(new GlueAppOptions { AppId = "com.ejemplo.miapp" });
        app.Content = new EmbeddedContent(typeof(Program).Assembly);   // recursos "web/…"
        app.Bridge.Register<string, string>("saludo", nombre => $"Hola, {nombre}");
        app.CreateWindow(new GlueWindowOptions { Width = 800, Height = 600 }).Show();
        return app.Run();
    }
}
```

```xml
<!-- la web, incrustada con nombres en forma de ruta -->
<EmbeddedResource Include="web\**" LogicalName="web/%(RecursiveDir)%(Filename)%(Extension)" />
```

```js
// web/index.html
const texto = await glue.call("saludo", "mundo");
```

- `GlueAppOptions`: `AppId` (obligatorio; carpeta de datos del webview en `%LOCALAPPDATA%\<AppId>`),
  `Host` (`app.local`), `QuitMode`, `DevTools`, `DevServerOrigin`, `DataFolder`.
- `GlueWindowOptions`: `Title` (null = el del documento), `Width`/`Height` (unidades lógicas, área de
  contenido), mínimos/máximos, `X`/`Y` (null = centrada), `Resizable`, `Maximized`, `AlwaysOnTop`, `Owner`,
  `Modal`, `Content`, `StartPage`.
- Proveedores de contenido: `EmbeddedContent`, `FolderContent`, `CompositeContent`, o uno propio con `IContentProvider`.
- Desde otros hilos: `app.Invoke`, `app.InvokeAsync`, `app.Emit`.
- `app.Scripts`: JS propio que se inyecta en cada documento tras el shim (`glue` ya existe), antes que la
  página. Añadirlo antes de mostrar las ventanas.
- `window.Reload()`: recarga la página actual.
- `GlueApp.ShowError(título, texto)` (estático): mensaje de error nativo sin necesidad de un `GlueApp`, para
  errores de arranque.
- `MessageOptions.DefaultButton`: botón por defecto de un mensaje (0 = el primero).
- Servicios nativos (`using WolfGlue.Native;`), desde cualquier hilo:

```csharp
string[]? archivos = await app.Native.Dialogs.OpenFileAsync(new() { Multiple = true,
    Filters = [new FileFilter("Texto", "txt", "md")] }, ventana);
string? destino = await app.Native.Dialogs.SaveFileAsync(new() { FileName = "nota.txt" }, ventana);
string? carpeta = await app.Native.Dialogs.ChooseFolderAsync(owner: ventana);
MessageResult r = await app.Native.Dialogs.MessageAsync(new MessageOptions
    { Text = "¿Guardar?", Kind = MessageKind.Question, Buttons = MessageButtons.YesNoCancel }, ventana);
await app.Native.Clipboard.SetTextAsync("hola");
IReadOnlyList<ScreenInfo> pantallas = app.Native.Screens.GetAll();
ThemeInfo tema = app.Native.Theme.Current;
app.Native.Theme.Changed += t => …;
app.Native.Shell.Reveal(ruta);
```

  Con `GlueAppOptions.NativeServicesInWeb = false` no se exponen a la web (`glue.dialog`…); la app puede
  registrar en el bridge su propia versión con las comprobaciones que quiera.

Ejemplo completo en `samples/Hola`.
