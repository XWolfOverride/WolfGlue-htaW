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

- Windows (`win-*`): enlazar `WebView2LoaderStatic.lib` y declarar `DirectPInvoke` para `WebView2Loader`.
- Manifiesto de aplicación por defecto (PerMonitorV2, Common Controls v6), si el proyecto no tiene uno.
- macOS (`osx-*`): tarea de montaje del bundle `.app` (`Info.plist` a partir de propiedades del proyecto).
- Propiedades recomendadas: `OutputType=WinExe`, `PublishAot=true`, `InvariantGlobalization` según el proyecto.

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

## Ejemplo mínimo previsto

```csharp
var app = new GlueApp("com.ejemplo.miapp");
app.Services.Register<string, string>("saludo", (nombre, ctx) => Task.FromResult($"Hola, {nombre}"));
var w = app.CreateWindow(new() { Title = "Mi App", Width = 800, Height = 600,
                                 Content = Content.FromResources("web/") });
w.Show();
app.Run();
```

(La API exacta se fijará al implementar; este documento se actualizará entonces.)
