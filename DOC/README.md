# Documentación técnica de WolfGlue

Punto de entrada para desarrolladores y agentes de IA. Para el uso como usuario, ver el
[README principal](../README.md). El resumen de decisiones y estado que leen los agentes de IA está
en [AGENTS.md](../AGENTS.md).

## Qué es

- **WolfGlue**: biblioteca C# (.NET 10, Native AOT) que crea ventanas nativas sin frameworks de UI y
  las llena con el webview del sistema. Una app puede tener varias ventanas web gestionadas como una sola.
- **htaW**: ejecutable sobre WolfGlue que lanza aplicaciones `.htaw` hechas solo con web, con una API
  JS propia, permisos declarados y un CSS con aspecto nativo.

## Índice

| Documento | Contenido |
|---|---|
| [ARQUITECTURA.md](ARQUITECTURA.md) | Estructura del repo, capas, modelo de app y ventanas, hilos |
| [PLATAFORMAS.md](PLATAFORMAS.md) | Detalle de Windows, Linux y macOS: APIs, DPI, problemas conocidos |
| [BRIDGE.md](BRIDGE.md) | Comunicación web ↔ C#: protocolo, shim JS, host propio, seguridad |
| [CONSUMO.md](CONSUMO.md) | Cómo usar WolfGlue como `ProjectReference` desde otro proyecto |
| [HTAW-FORMATO.md](HTAW-FORMATO.md) | Formatos `.htaw`, manifiesto, carga de recursos |
| [HTAW-API.md](HTAW-API.md) | API JavaScript `htaw.*` |
| [HTAW-SEGURIDAD.md](HTAW-SEGURIDAD.md) | Permisos, confirmación, reglas de `shell.exec` |
| [HTAW-ESTILO.md](HTAW-ESTILO.md) | CSS con aspecto nativo por sistema |
| [HTAW-INTEGRACION.md](HTAW-INTEGRACION.md) | Asociación de archivos, procesos, herramientas de desarrollo |
| [COMPILACION-Y-PRUEBAS.md](COMPILACION-Y-PRUEBAS.md) | Build, publicación AOT, pruebas, estado |
| [HOJA-DE-RUTA.md](HOJA-DE-RUTA.md) | Orden de implementación y decisiones pendientes |

## Hechos clave (leer antes de tocar nada)

- **Estado:** WolfGlue funciona en Windows (ver HOJA-DE-RUTA.md); htaW, Linux y macOS siguen en diseño.
  Lo no implementado se marca como previsto y se actualiza cuando la implementación se aparte de ello.
- Native AOT en todo: sin reflexión, sin `ComImport` clásico, `System.Text.Json` con
  `JsonSerializerContext`, P/Invoke con `LibraryImport`, callbacks con `[UnmanagedCallersOnly]`.
- Sin WinForms, WPF, Avalonia ni similares. Las ventanas se crean con las APIs del sistema.
- Siempre el webview del sistema; nunca se distribuye un navegador.
- El bucle de UI va en el hilo principal. Todo acceso a ventanas o webviews desde otro hilo pasa por
  `GlueApp.Invoke`.
- WolfGlue se consume como `ProjectReference`, no hay paquete NuGet.
- Textos de usuario en español e inglés (locale, forzable por variable de entorno).
- Saltos de línea CRLF salvo los `.sh` (LF).
