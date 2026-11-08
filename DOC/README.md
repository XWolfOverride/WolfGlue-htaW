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
| [BARRA-DE-TITULO.md](BARRA-DE-TITULO.md) | Barra de título con elementos propios: modelo común, implementación en Windows, previsto en macOS/Linux |
| [MENUS.md](MENUS.md) | Menús nativos: modelo común, contextual (hecho en Windows) y menú de ventana (diseño abierto) |
| [CICLO-DE-VIDA.md](CICLO-DE-VIDA.md) | Ventana principal, modos de cierre de la app, ventana «Acerca de» y papel `about` |
| [MENUS-LINUX.md](MENUS-LINUX.md) | Menú de ventana en Linux: estudio y opciones (punto abierto) |
| [FONDO-DE-VENTANA.md](FONDO-DE-VENTANA.md) | Diseño: fondo del sistema (`backdrop`: Mica/Acrylic, NSVisualEffectView) |
| [BANDEJA-Y-NOTIFICACIONES.md](BANDEJA-Y-NOTIFICACIONES.md) | Diseño: icono en la bandeja y notificaciones con modelo común |
| [MENSAJES.md](MENSAJES.md) | Evaluación y diseño: mensajes propios en todos los SO (versión 1.2) |
| [VENTANAS-SIN-MARCO.md](VENTANAS-SIN-MARCO.md) | Diseño: ventanas sin marco y transparentes al escritorio (WolfGlue y htaW con permiso `overlay`) |
| [BRIDGE.md](BRIDGE.md) | Comunicación web ↔ C#: protocolo, shim JS, host propio, seguridad |
| [CONSUMO.md](CONSUMO.md) | Cómo usar WolfGlue como `ProjectReference` desde otro proyecto |
| [manual/](manual/README.md) | **Manual de htaW**: uso, y guía completa para desarrollar apps (manifiesto, API, permisos, CSS, ventanas, distribución, recetas) |
| [manual-en/](manual-en/README.md) | El mismo manual en inglés |
| [HTAW-FORMATO.md](HTAW-FORMATO.md) | Formatos `.htaw`, manifiesto, carga de recursos |
| [HTAW-API.md](HTAW-API.md) | API JavaScript `htaw.*` |
| [HTAW-SEGURIDAD.md](HTAW-SEGURIDAD.md) | Permisos, confirmación, reglas de `shell.exec` |
| [HTAW-ESTILO.md](HTAW-ESTILO.md) | CSS con aspecto nativo por sistema |
| [HTAW-INTEGRACION.md](HTAW-INTEGRACION.md) | Asociación de archivos, procesos, herramientas de desarrollo |
| [HTAW-OTRAS-PLATAFORMAS.md](HTAW-OTRAS-PLATAFORMAS.md) | Evaluación: htaW en Android e iOS y portables para otros sistemas |
| [ICONOS.md](ICONOS.md) | Estilo de los iconos de app y cómo se generan (`tools/iconos`) |
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
